using System;
using System.Collections.Generic;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using EHR.Patches;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

public class Witch : RoleBase
{
    private const int Id = 2000;

    public static readonly string[] SwitchTriggerText =
    [
        "TriggerKill",
        "TriggerVent",
        "TriggerDouble"
    ];

    public static List<byte> PlayerIdList = [];

    private static OptionItem ModeSwitchAction;
    private static SwitchTrigger NowSwitchTrigger;

    private bool IsHM;
    private List<byte> SpelledPlayer = [];

    private bool SpellMode;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Witch);

        ModeSwitchAction = new StringOptionItem(Id + 10, "WitchModeSwitchAction", SwitchTriggerText, 2, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Witch]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        SpellMode = false;
        SpelledPlayer = [];
        IsHM = false;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        SpellMode = false;
        SpelledPlayer = [];

        IsHM = Main.PlayerStates[playerId].MainRole == CustomRoles.HexMaster;
        NowSwitchTrigger = IsHM ? (SwitchTrigger)HexMaster.ModeSwitchAction.GetValue() : (SwitchTrigger)ModeSwitchAction.GetValue();
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return true;
    }

    private static void SendRPC(bool doSpell, byte witchId, byte target = 255, bool spellMode = false)
    {
        if (!Utils.DoRPC) return;

        if (doSpell)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.DoSpell, SendOption.Reliable);
            writer.Write(witchId);
            writer.Write(target);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKillOrSpell, SendOption.Reliable);
            writer.Write(witchId);
            writer.Write(spellMode);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void ReceiveRPC(MessageReader reader, bool doSpell)
    {
        byte witch = reader.ReadByte();
        if (Main.PlayerStates[witch].Role is not Witch wc) return;

        if (doSpell)
        {
            byte spelledId = reader.ReadByte();

            if (spelledId != 255)
                wc.SpelledPlayer.Add(spelledId);
            else
                wc.SpelledPlayer.Clear();
        }
        else
            wc.SpellMode = reader.ReadBoolean();
    }

    public override void OnPet(PlayerControl pc)
    {
        if (NowSwitchTrigger == SwitchTrigger.DoubleTrigger) return;

        SwitchMode(pc.PlayerId);
    }

    private void SwitchSpellMode(byte playerId, bool kill)
    {
        bool needSwitch = NowSwitchTrigger switch
        {
            SwitchTrigger.Kill => kill,
            SwitchTrigger.Vent => !kill,
            _ => false
        };

        if (needSwitch) SwitchMode(playerId);
    }

    private void SwitchMode(byte playerId)
    {
        SpellMode = !SpellMode;
        SendRPC(false, playerId);
        PlayerControl pc = Utils.GetPlayerById(playerId);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    private bool IsSpelled(byte target)
    {
        return SpelledPlayer.Contains(target);
    }

    private void SetSpelled(PlayerControl killer, PlayerControl target)
    {
        if (!IsSpelled(target.PlayerId))
        {
            SpelledPlayer.Add(target.PlayerId);
            SendRPC(true, killer.PlayerId, target.PlayerId);
            killer.SetKillCooldown();
            killer.RPCPlayCustomSound("Curse");
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
        }
    }

    public static void RemoveSpelledPlayer()
    {
        foreach (byte witch in PlayerIdList)
        {
            if (Main.PlayerStates[witch].Role is not Witch wc) continue;

            wc.SpelledPlayer.Clear();
            SendRPC(true, witch);
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;

        if (NowSwitchTrigger == SwitchTrigger.DoubleTrigger) return killer.CheckDoubleTrigger(target, () => SetSpelled(killer, target));

        if (!SpellMode)
        {
            SwitchSpellMode(killer.PlayerId, true);
            return true;
        }

        SetSpelled(killer, target);

        SwitchSpellMode(killer.PlayerId, true);

        return false;
    }

    public static void OnCheckForEndVoting(PlayerState.DeathReason deathReason, params byte[] exileIds)
    {
        try
        {
            if (deathReason != PlayerState.DeathReason.Vote) return;

            foreach (byte id in exileIds)
            {
                if (PlayerIdList.Contains(id))
                {
                    if (Main.PlayerStates[id].Role is not Witch wc) continue;

                    wc.SpelledPlayer.Clear();
                }
            }

            var spelledIdList = new List<byte>();

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                foreach (byte witchId in PlayerIdList)
                {
                    if (Main.AfterMeetingDeathPlayers.ContainsKey(pc.PlayerId)) continue;

                    if (Main.PlayerStates[witchId].Role is not Witch wc) continue;

                    PlayerControl witch = Utils.GetPlayerById(witchId);

                    if (wc.SpelledPlayer.Contains(pc.PlayerId) && witch != null && witch.IsAlive())
                    {
                        pc.SetRealKiller(witch);
                        spelledIdList.Add(pc.PlayerId);
                    }
                    else
                        Main.AfterMeetingDeathPlayers.Remove(pc.PlayerId);
                }
            }

            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Spell, [.. spelledIdList]);
            RemoveSpelledPlayer();
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static string GetSpelledMark(byte target, bool isMeeting)
    {
        if (!isMeeting) return string.Empty;

        foreach (byte id in PlayerIdList)
            if (Main.PlayerStates[id].Role is Witch { IsEnable: true } wc && wc.IsSpelled(target))
                return Utils.ColorString(wc.IsHM ? Utils.GetRoleColor(CustomRoles.HexMaster) : Palette.ImpostorRed, "†");

        return string.Empty;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer == null || meeting || seer.PlayerId != target.PlayerId || !seer.Is(CustomRoles.Witch) || (seer.IsModClient() && !hud)) return string.Empty;

        var str = new StringBuilder();

        if (hud)
            str.Append($"<size=90%><color=#00ffa5>{GetString("WitchCurrentMode")}:</color> <b>");
        else
            str.Append($"{GetString("Mode")}: ");

        if (NowSwitchTrigger == SwitchTrigger.DoubleTrigger)
            str.Append(GetString("WitchModeDouble"));
        else
            str.Append(SpellMode ? GetString("WitchModeSpell") : GetString("WitchModeKill"));

        return str.ToString();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (SpellMode && NowSwitchTrigger != SwitchTrigger.DoubleTrigger)
            hud.KillButton.OverrideText(GetString("WitchSpellButtonText"));
        else
            hud.KillButton.OverrideText(GetString("KillButtonText"));
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (PlayerIdList.Contains(pc.PlayerId))
            if (NowSwitchTrigger is SwitchTrigger.Vent)
                SwitchSpellMode(pc.PlayerId, false);
    }

    private enum SwitchTrigger
    {
        Kill,
        Vent,
        DoubleTrigger
    }
}