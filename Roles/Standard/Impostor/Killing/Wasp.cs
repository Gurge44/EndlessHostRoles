using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using EHR.Patches;
using Hazel;

namespace EHR.Roles;

public class Wasp : RoleBase
{
    public static bool On;
    private static List<Wasp> Instances = [];

    private static OptionItem StingCooldown;
    private static OptionItem KillDelay;
    private static OptionItem EvadeKills;
    private static OptionItem SwarmModeDuration;
    private static OptionItem WaspDiesAfterSwarmEnd;
    private static OptionItem PestControlDuration;
    private static OptionItem PestControlSpeed;
    private static OptionItem PestControlVision;

    public Dictionary<byte, CountdownTimer> DelayedKills;
    private bool EvadedKillThisRound;
    public HashSet<byte> MeetingKills;
    private CountdownTimer PestControlEnd;
    private CountdownTimer SwarmModeEnd;
    private PlayerControl WaspPC;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645350)
            .AutoSetupOption(ref StingCooldown, 30, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref KillDelay, 5, new IntegerValueRule(1, 30, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref EvadeKills, true)
            .AutoSetupOption(ref SwarmModeDuration, 15, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds, overrideParent: EvadeKills)
            .AutoSetupOption(ref WaspDiesAfterSwarmEnd, true, overrideParent: EvadeKills)
            .AutoSetupOption(ref PestControlDuration, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds, overrideParent: EvadeKills)
            .AutoSetupOption(ref PestControlSpeed, 0.5f, new FloatValueRule(0.05f, 2f, 0.05f), OptionFormat.Multiplier, overrideParent: EvadeKills)
            .AutoSetupOption(ref PestControlVision, 0.2f, new FloatValueRule(0f, 1f, 0.05f), OptionFormat.Multiplier, overrideParent: EvadeKills);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        WaspPC = playerId.GetPlayer();
        DelayedKills = [];
        MeetingKills = [];
        SwarmModeEnd = null;
        PestControlEnd = null;
        EvadedKillThisRound = false;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = SwarmModeEnd == null ? StingCooldown.GetInt() : 0.01f;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (PestControlEnd == null) return;

        opt.SetVision(false);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, PestControlVision.GetFloat());
        opt.SetFloat(FloatOptionNames.CrewLightMod, PestControlVision.GetFloat());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (DelayedKills.ContainsKey(target.PlayerId)) return false;

        if (target.HasKillButton())
            DelayedKills[target.PlayerId] = new CountdownTimer(KillDelay.GetInt(), () =>
            {
                target.Suicide(PlayerState.DeathReason.Stung, killer);
                DelayedKills.Remove(target.PlayerId);
            }, cancelOnMeeting: false, onCanceled: DelayedKills.Clear);
        else
            MeetingKills.Add(target.PlayerId);

        killer.SetKillCooldown(StingCooldown.GetInt());
        return false;
    }

    public override void OnReportDeadBody()
    {
        foreach ((byte id, CountdownTimer timer) in DelayedKills)
        {
            timer.Dispose();
            
            PlayerControl player = id.GetPlayer();
            if (player == null || !player.IsAlive()) continue;

            player.Suicide(PlayerState.DeathReason.Stung, WaspPC);
        }

        DelayedKills.Clear();

        if (WaspPC == null || !WaspPC.IsAlive())
            MeetingKills.Clear();

        if (MeetingKills.Count > 0)
        {
            LateTask.New(() =>
            {
                string stung = string.Join(", ", MeetingKills.Select(x => x.ColoredPlayerName()));
                string role = CustomRoles.Wasp.ToColoredString();
                string text = string.Format(Translator.GetString("WaspStungPlayersMessage"), stung, role);
                Utils.SendMessage(text, title: Translator.GetString("MessageTitle.Attention"), importance: MessageImportance.High);
            }, 10f, "Wasp Stung Players Notify");
        }

        if (SwarmModeEnd != null)
        {
            SwarmModeEnd.Dispose();
            SwarmModeEnd = null;

            if (WaspDiesAfterSwarmEnd.GetBool()) WaspPC.Suicide();
            else WaspPC.ResetKillCooldown();
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (!EvadeKills.GetBool() || EvadedKillThisRound) return true;

        if (IRandom.Instance.Next(2) == 0)
        {
            SwarmModeEnd = new CountdownTimer(SwarmModeDuration.GetInt(), () =>
            {
                SwarmModeEnd = null;

                if (WaspDiesAfterSwarmEnd.GetBool()) target.Suicide();
                else
                {
                    target.ResetKillCooldown();
                    target.SyncSettings();
                    target.SetKillCooldown(StingCooldown.GetInt());
                }
            }, onTick: () => Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target), cancelOnMeeting: false, onCanceled: () => SwarmModeEnd = null);
            Utils.SendRPC(CustomRPC.SyncRoleData, WaspPC.PlayerId);
            target.SyncSettings();
            target.SetKillCooldown(0.01f);
        }
        else
        {
            PestControlEnd = new CountdownTimer(PestControlDuration.GetInt(), () =>
            {
                PestControlEnd = null;
                Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                target.MarkDirtySettings();
            }, onCanceled: () =>
            {
                PestControlEnd = null;
                Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            });
            Main.AllPlayerSpeed[target.PlayerId] = PestControlSpeed.GetFloat();
            target.MarkDirtySettings();
        }

        EvadedKillThisRound = true;
        return false;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return !EvadedKillThisRound || SwarmModeEnd != null;
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return !EvadedKillThisRound;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return !EvadedKillThisRound;
    }

    public override void AfterMeetingTasks()
    {
        EvadedKillThisRound = false;
        MeetingKills.Clear();
    }

    public static void OnExile(byte[] exileIds)
    {
        try
        {
            HashSet<byte> waspDeathList = [];

            foreach (Wasp instance in Instances) waspDeathList.UnionWith(instance.GetStungPlayers(exileIds));
            waspDeathList.ExceptWith(Main.AfterMeetingDeathPlayers.Keys);

            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Stung, [.. waspDeathList]);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static string GetStungMark(byte target)
    {
        return Instances.Any(x => x.MeetingKills.Contains(target)) ? Utils.ColorString(Palette.ImpostorRed, "\u25c0") : string.Empty;
    }

    private HashSet<byte> GetStungPlayers(byte[] exileIds)
    {
        return WaspPC == null || !WaspPC.IsAlive() || Main.AfterMeetingDeathPlayers.ContainsKey(WaspPC.PlayerId) || exileIds.Any(x => x == WaspPC.PlayerId) ? [] : MeetingKills;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        SwarmModeEnd = new CountdownTimer(SwarmModeDuration.GetInt(), () => SwarmModeEnd = null, onCanceled: () => SwarmModeEnd = null);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != WaspPC.PlayerId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || SwarmModeEnd == null) return string.Empty;
        return string.Format(Translator.GetString("Wasp.SwarmModeSuffix"), (int)Math.Ceiling(SwarmModeEnd.Remaining.TotalSeconds));
    }
}