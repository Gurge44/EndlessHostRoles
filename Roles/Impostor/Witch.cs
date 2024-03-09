using Hazel;
using System.Collections.Generic;
using System.Text;
using TOHE.Modules;
using TOHE.Patches;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public class Witch : RoleBase
{
    public enum SwitchTrigger
    {
        Kill,
        Vent,
        DoubleTrigger,
    }

    public static readonly string[] SwitchTriggerText =
    [
        "TriggerKill",
        "TriggerVent",
        "TriggerDouble"
    ];

    private const int Id = 2000;
    public static List<byte> playerIdList = [];

    public bool SpellMode;
    public List<byte> SpelledPlayer = [];

    public static OptionItem ModeSwitchAction;
    public static SwitchTrigger NowSwitchTrigger;

    private bool IsHM;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Witch);
        ModeSwitchAction = StringOptionItem.Create(Id + 10, "WitchModeSwitchAction", SwitchTriggerText, 2, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Witch]);
    }

    public override void Init()
    {
        playerIdList = [];
        SpellMode = false;
        SpelledPlayer = [];
        IsHM = false;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        SpellMode = false;
        SpelledPlayer = [];

        IsHM = Main.PlayerStates[playerId].MainRole == CustomRoles.HexMaster;
        NowSwitchTrigger = IsHM ? (SwitchTrigger)HexMaster.ModeSwitchAction.GetValue() : (SwitchTrigger)ModeSwitchAction.GetValue();

        if (!AmongUsClient.Instance.AmHost || !IsHM) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;

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
        var witch = reader.ReadByte();
        if (Main.PlayerStates[witch].Role is not Witch wc) return;

        if (doSpell)
        {
            var spelledId = reader.ReadByte();
            if (spelledId != 255)
            {
                wc.SpelledPlayer.Add(spelledId);
            }
            else
            {
                wc.SpelledPlayer.Clear();
            }
        }
        else
        {
            wc.SpellMode = reader.ReadBoolean();
        }
    }

    void SwitchSpellMode(byte playerId, bool kill)
    {
        bool needSwitch = NowSwitchTrigger switch
        {
            SwitchTrigger.Kill => kill,
            SwitchTrigger.Vent => !kill,
            _ => false
        };
        if (needSwitch)
        {
            SpellMode = !SpellMode;
            SendRPC(false, playerId);
            var pc = Utils.GetPlayerById(playerId);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    bool IsSpelled(byte target) => SpelledPlayer.Contains(target);

    void SetSpelled(PlayerControl killer, PlayerControl target)
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
        foreach (byte witch in playerIdList)
        {
            if (Main.PlayerStates[witch].Role is not Witch wc) continue;
            wc.SpelledPlayer.Clear();
            SendRPC(true, witch);
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;

        if (NowSwitchTrigger == SwitchTrigger.DoubleTrigger)
        {
            return killer.CheckDoubleTrigger(target, () => { SetSpelled(killer, target); });
        }

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
        if (deathReason != PlayerState.DeathReason.Vote) return;
        foreach (byte id in exileIds)
        {
            if (playerIdList.Contains(id))
            {
                if (Main.PlayerStates[id].Role is not Witch wc) continue;
                wc.SpelledPlayer.Clear();
            }
        }

        var spelledIdList = new List<byte>();
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            foreach (var witchId in playerIdList)
            {
                if (Main.AfterMeetingDeathPlayers.ContainsKey(pc.PlayerId)) continue;
                if (Main.PlayerStates[witchId].Role is not Witch wc) continue;

                var witch = Utils.GetPlayerById(witchId);
                if (wc.SpelledPlayer.Contains(pc.PlayerId) && witch != null && witch.IsAlive())
                {
                    pc.SetRealKiller(witch);
                    spelledIdList.Add(pc.PlayerId);
                }
                else
                {
                    Main.AfterMeetingDeathPlayers.Remove(pc.PlayerId);
                }
            }
        }

        CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Spell, [.. spelledIdList]);
        RemoveSpelledPlayer();
    }

    public static string GetSpelledMark(byte target, bool isMeeting)
    {
        if (!isMeeting) return string.Empty;
        foreach (var id in playerIdList)
        {
            if (Main.PlayerStates[id].Role is Witch { IsEnable: true } wc && wc.IsSpelled(target))
            {
                return Utils.ColorString(Palette.ImpostorRed, "†");
            }
        }

        return string.Empty;
    }

    public static string GetSpellModeText(PlayerControl witch, bool hud, bool isMeeting = false)
    {
        if (witch == null || isMeeting) return string.Empty;

        var str = new StringBuilder();
        if (hud)
        {
            str.Append($"<color=#00ffa5>{GetString("WitchCurrentMode")}:</color> <b>");
        }
        else
        {
            str.Append($"{GetString("Mode")}: ");
        }

        if (NowSwitchTrigger == SwitchTrigger.DoubleTrigger)
        {
            str.Append(GetString("WitchModeDouble"));
        }
        else
        {
            str.Append(IsSpellMode(witch.PlayerId) ? GetString("WitchModeSpell") : GetString("WitchModeKill"));
        }

        return str.ToString();
    }

    public static void GetAbilityButtonText(HudManager hud)
    {
        if (IsSpellMode(PlayerControl.LocalPlayer.PlayerId) && NowSwitchTrigger != SwitchTrigger.DoubleTrigger)
        {
            hud.KillButton.OverrideText(GetString("WitchSpellButtonText"));
        }
        else
        {
            hud.KillButton.OverrideText(GetString("KillButtonText"));
        }
    }

    static bool IsSpellMode(byte id) => Main.PlayerStates[id].Role is Witch { IsEnable: true, SpellMode: true };

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (playerIdList.Contains(pc.PlayerId))
        {
            if (NowSwitchTrigger is SwitchTrigger.Vent)
            {
                SwitchSpellMode(pc.PlayerId, false);
            }
        }
    }
}