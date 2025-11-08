using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Wyrd : Coven
{
    public enum Action
    {
        Task,
        Report,
        Button
    }

    public static bool On;
    private static List<Wyrd> Instances = [];

    private static OptionItem ShapeshiftCooldown;
    private static OptionItem MaxMarkedPlayersAtOnce;
    private static OptionItem CountdownTime;
    private static OptionItem TimeAdditionOnMoreMark;
    private static OptionItem MarkedDiesOnTask;
    private static OptionItem MarkedDiesOnReport;
    private static OptionItem MarkedDiesOnButton;
    private static OptionItem BlockMeetingOnDeath;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private static Dictionary<Action, bool> ActionSuicideSettings = [];

    private int Countdown;
    private long LastUpdate;

    public HashSet<byte> MarkedPlayers;
    private byte WyrdID;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650150)
            .AutoSetupOption(ref ShapeshiftCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref MaxMarkedPlayersAtOnce, 3, new IntegerValueRule(1, 10, 1), OptionFormat.Players)
            .AutoSetupOption(ref CountdownTime, 45, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref TimeAdditionOnMoreMark, 5, new IntegerValueRule(0, 30, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref MarkedDiesOnTask, true)
            .AutoSetupOption(ref MarkedDiesOnReport, true)
            .AutoSetupOption(ref MarkedDiesOnButton, true)
            .AutoSetupOption(ref BlockMeetingOnDeath, true)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];

        ActionSuicideSettings = new()
        {
            { Action.Task, MarkedDiesOnTask.GetBool() },
            { Action.Report, MarkedDiesOnReport.GetBool() },
            { Action.Button, MarkedDiesOnButton.GetBool() }
        };
    }

    public override void Add(byte playerId)
    {
        On = true;
        WyrdID = playerId;
        Instances.Add(this);
        MarkedPlayers = [];
        Countdown = CountdownTime.GetInt();
        LastUpdate = Utils.TimeStamp;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return HasNecronomicon;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        if (MarkedPlayers.Count >= MaxMarkedPlayersAtOnce.GetInt()) return false;

        if (MarkedPlayers.Count != 0) Countdown += TimeAdditionOnMoreMark.GetInt();

        MarkedPlayers.Add(target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: target);
        Utils.SendRPC(CustomRPC.SyncRoleData, WyrdID, 1, target.PlayerId);
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!Main.IntroDestroyed || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks || TimeMaster.Rewinding || Stasis.IsTimeFrozen) return;

        if (!pc.IsAlive())
        {
            MarkedPlayers.Clear();
            Countdown = CountdownTime.GetInt();
        }

        if (MarkedPlayers.Count == 0 || Countdown <= 0) return;

        long now = Utils.TimeStamp;
        if (LastUpdate == now) return;
        LastUpdate = now;

        Countdown--;
        Utils.SendRPC(CustomRPC.SyncRoleData, WyrdID, 3, Countdown);

        MarkedPlayers.ToValidPlayers().Do(x => Utils.NotifyRoles(SpecifySeer: x, SpecifyTarget: x));
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public static bool CheckPlayerAction(PlayerControl pc, Action action)
    {
        if (!ActionSuicideSettings[action]) return true;

        foreach (Wyrd instance in Instances)
        {
            if (instance.Countdown > 0 || !instance.MarkedPlayers.Contains(pc.PlayerId)) continue;

            pc.Suicide(realKiller: instance.WyrdID.GetPlayer());
            if (pc.AmOwner) Achievements.Type.DestinysChoice.Complete();
            return !BlockMeetingOnDeath.GetBool();
        }

        return true;
    }

    public static void OnAnyoneDeath(PlayerControl target)
    {
        foreach (Wyrd instance in Instances)
        {
            if (instance.MarkedPlayers.Remove(target.PlayerId))
            {
                var wyrd = instance.WyrdID.GetPlayer();
                if (wyrd != null) Utils.NotifyRoles(SpecifySeer: wyrd, SpecifyTarget: target);
                Utils.SendRPC(CustomRPC.SyncRoleData, instance.WyrdID, 2, target.PlayerId);
            }
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                MarkedPlayers.Add(reader.ReadByte());
                break;
            case 2:
                MarkedPlayers.Remove(reader.ReadByte());
                break;
            case 3:
                Countdown = reader.ReadPackedInt32();
                break;
        }
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return base.GetProgressText(playerId, comms) + $" <#ffffff>{MarkedPlayers.Count}/{MaxMarkedPlayersAtOnce.GetInt()}</color>";
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (MarkedPlayers.Count == 0) return string.Empty;

        bool self = seer.PlayerId == target.PlayerId;
        bool seerIsWyrd = seer.PlayerId == WyrdID;

        switch (seerIsWyrd)
        {
            case false when MarkedPlayers.Count == 1 || !MarkedPlayers.Contains(seer.PlayerId) || !self:
            case true when !MarkedPlayers.Contains(target.PlayerId) && !self:
                return string.Empty;
        }

        var sb = new StringBuilder();
        if (Countdown <= 0) sb.Append("<#ff0000>");
        sb.AppendLine(string.Format(Translator.GetString("Wyrd.Suffix.FateCountdown"), Countdown));

        if (Countdown <= 0)
        {
            sb.Append("</color>");

            if (!seerIsWyrd)
            {
                var suicideActions = Enum.GetValues<Action>().Where(x => ActionSuicideSettings[x]).ToList();
                var join = string.Join(", ", suicideActions.ConvertAll(x => Translator.GetString($"Wyrd.Suffix.SuicideWarningOnAction.{x}")));
                sb.AppendLine(string.Format(Translator.GetString("Wyrd.Suffix.SuicideWarnings"), join));
            }
        }

        return sb.ToString().Trim();
    }
}