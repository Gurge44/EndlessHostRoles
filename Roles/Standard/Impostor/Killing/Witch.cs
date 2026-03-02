using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Patches;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class Witch : RoleBase
{
    private const int Id = 2000;

    public static readonly string[] SwitchTriggerText =
    [
        "TriggerKill",
        "TriggerVent",
        "TriggerDouble",
        "TriggerVanish"
    ];

    public static List<byte> PlayerIdList = [];

    public static OptionItem ModeSwitchAction;
    private static OptionItem SpellCooldown;
    private static OptionItem MaxSpellsPerRound;
    private static SwitchTrigger NowSwitchTrigger;

    private bool IsHM;
    private List<byte> SpelledPlayer = [];
    private byte WitchId;
    private bool SpellMode;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Witch);

        ModeSwitchAction = new StringOptionItem(Id + 10, "WitchModeSwitchAction", SwitchTriggerText, 2, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Witch]);
        
        SpellCooldown = new FloatOptionItem(Id + 11, "WitchSpellCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Witch])
            .SetValueFormat(OptionFormat.Seconds);
        
        MaxSpellsPerRound = new IntegerOptionItem(Id + 12, "WitchMaxSpellsPerRound", new(1, 14, 1), 3, TabGroup.ImpostorRoles)
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
        WitchId = playerId;

        IsHM = Main.PlayerStates[playerId].MainRole == CustomRoles.HexMaster;
        if (!IsHM) playerId.SetAbilityUseLimit(MaxSpellsPerRound.GetInt());
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

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (NowSwitchTrigger == SwitchTrigger.Vanish)
        {
            AURoleOptions.PhantomCooldown = SpellCooldown.GetFloat();
            AURoleOptions.PhantomDuration = 1f;
        }
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
        if (NowSwitchTrigger is SwitchTrigger.DoubleTrigger or SwitchTrigger.Vanish) return;

        SwitchMode(pc.PlayerId);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (NowSwitchTrigger == SwitchTrigger.Vanish)
        {
            var killRange = GameManager.Instance.LogicOptions.GetKillDistance();
            if (!FastVector2.TryGetClosestPlayerInRangeTo(pc, killRange, out PlayerControl target, x => !x.IsImpostor())) return false;
            SetSpelled(pc, target);
        }

        return false;
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
        if (!IsSpelled(target.PlayerId) && killer.GetAbilityUseLimit() > 0)
        {
            SpelledPlayer.Add(target.PlayerId);
            SendRPC(true, killer.PlayerId, target.PlayerId);
            killer.SetKillCooldown(SpellCooldown.GetFloat());
            killer.RPCPlayCustomSound("Curse");
            killer.RpcRemoveAbilityUse();
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

    public override void OnReportDeadBody()
    {
        HashSet<byte> spelledPlayers = [];

        foreach (byte id in PlayerIdList)
        {
            if (Main.PlayerStates[id].Role is not Witch wc) continue;

            PlayerControl pc = id.GetPlayer();

            if (pc == null || !pc.IsAlive())
            {
                wc.SpelledPlayer.Clear();
                SendRPC(true, id);
            }

            spelledPlayers.UnionWith(wc.SpelledPlayer);
        }

        if (spelledPlayers.Count > 0)
        {
            LateTask.New(() =>
            {
                string cursed = string.Join(", ", spelledPlayers.Select(x => x.ColoredPlayerName()));
                string role = IsHM ? CustomRoles.HexMaster.ToColoredString() : CustomRoles.Witch.ToColoredString();
                string text = string.Format(GetString("WitchCursedPlayersMessage"), cursed, role);
                Utils.SendMessage(text, title: GetString("MessageTitle.Attention"), importance: MessageImportance.High);
            }, 10f, "Witch Cursed Players Notify");
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;

        if (NowSwitchTrigger == SwitchTrigger.DoubleTrigger)
            return killer.CheckDoubleTrigger(target, () => SetSpelled(killer, target));

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

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
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
                    
                    witchId.SetAbilityUseLimit(MaxSpellsPerRound.GetInt());
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
        {
            if (Main.PlayerStates[id].Role is Witch { IsEnable: true } wc && wc.IsSpelled(target))
                return Utils.ColorString(wc.IsHM ? Utils.GetRoleColor(CustomRoles.HexMaster) : Palette.ImpostorRed, "†");
        }

        return string.Empty;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != WitchId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;

        var str = new StringBuilder();

        if (hud)
            str.Append($"<size=90%><color=#00ffa5>{GetString("WitchCurrentMode")}:</color> <b>");
        else
            str.Append($"{GetString("Mode")}: ");

        switch (NowSwitchTrigger)
        {
            case SwitchTrigger.DoubleTrigger:
                str.Append(GetString("WitchModeDouble"));
                break;
            case SwitchTrigger.Vanish:
                str.Append(GetString("WitchModeVanish"));
                break;
            default:
                str.Append(SpellMode ? GetString("WitchModeSpell") : GetString("WitchModeKill"));
                break;
        }

        return str.ToString();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (SpellMode && NowSwitchTrigger != SwitchTrigger.DoubleTrigger)
        {
            ActionButton button = (NowSwitchTrigger == SwitchTrigger.Vanish ? hud.AbilityButton : hud.KillButton);
            button.OverrideText(IsHM ? GetString("HexButtonText") : GetString("WitchSpellButtonText"));
        }
        else
            hud.KillButton.OverrideText(GetString("KillButtonText"));
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (PlayerIdList.Contains(pc.PlayerId))
        {
            if (NowSwitchTrigger is SwitchTrigger.Vent)
                SwitchSpellMode(pc.PlayerId, false);
        }
    }

    public enum SwitchTrigger
    {
        Kill,
        Vent,
        DoubleTrigger,
        Vanish
    }
}