using System.Collections.Generic;
using EHR.Modules;

namespace EHR.Impostor;

public class Occultist : RoleBase
{
    public static bool On;
    private static List<Occultist> Instances = [];

    private static OptionItem AbilityUseLimit;
    private static OptionItem ReviveTime;
    private static OptionItem ArrowsToBodies;
    private static OptionItem RevivedPlayersBodiesCanBeReported;
    private static OptionItem RevivedPlayers;
    private static OptionItem CanReviveImpostorsAndMadmates;
    public static OptionItem AbilityUseGainWithEachKill;

    private static readonly string[] RevivedPlayersModes =
    [
        "Occultist.RPM.Renegade",
        "Occultist.RPM.Madmate"
    ];

    private static ActionSwitchModes ActionSwitchMode;
    private static Dictionary<byte, ReviveData> Revives = [];

    private bool InRevivingMode;

    private long LastUpdate;
    private PlayerControl OccultistPC;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645250)
            .AutoSetupOption(ref AbilityUseLimit, 0, new IntegerValueRule(0, 20, 1), OptionFormat.Times)
            .AutoSetupOption(ref ReviveTime, 5, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ArrowsToBodies, false)
            .AutoSetupOption(ref RevivedPlayersBodiesCanBeReported, false)
            .AutoSetupOption(ref RevivedPlayers, 0, RevivedPlayersModes)
            .AutoSetupOption(ref CanReviveImpostorsAndMadmates, true)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.4f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        ActionSwitchMode = Options.UseUnshiftTrigger.GetBool() ? ActionSwitchModes.Unshift : Options.UsePets.GetBool() ? ActionSwitchModes.Pet : ActionSwitchModes.Vent;
        Instances = [];
        Revives = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        OccultistPC = playerId.GetPlayer();
        InRevivingMode = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
    }

    void SwitchAction()
    {
        InRevivingMode = Main.AllAlivePlayerControls.Length >= 4 && !InRevivingMode;
        Utils.SendRPC(CustomRPC.SyncRoleData, OccultistPC.PlayerId, 1, InRevivingMode);
        Utils.NotifyRoles(SpecifySeer: OccultistPC, SpecifyTarget: OccultistPC);
    }

    public override void OnExitVent(PlayerControl pc, Vent vent)
    {
        if (ActionSwitchMode == ActionSwitchModes.Vent)
            SwitchAction();
    }

    public override void OnPet(PlayerControl pc)
    {
        if (ActionSwitchMode == ActionSwitchModes.Pet)
            SwitchAction();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting && ActionSwitchMode == ActionSwitchModes.Unshift)
            SwitchAction();

        return false;
    }

    public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
    {
        if (!InRevivingMode || reporter.GetAbilityUseLimit() < 1 || target.Disconnected || target.Object.IsAlive() || target.Object.Is(CustomRoles.Unreportable) || (target.Object.Is(Team.Impostor) && !CanReviveImpostorsAndMadmates.GetBool())) return true;

        InRevivingMode = false;
        Revives[target.PlayerId] = new(Utils.TimeStamp, reporter.Pos(), false);
        reporter.RpcRemoveAbilityUse();
        reporter.Notify(string.Format(Translator.GetString("OccultistReviving"), target.PlayerId.ColoredPlayerName()), ReviveTime.GetInt());

        return false;
    }

    public static bool OnAnyoneReportDeadBody(NetworkedPlayerInfo target)
    {
        if (RevivedPlayersBodiesCanBeReported.GetBool()) return true;
        return !Revives.ContainsKey(target.PlayerId);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || !pc.IsAlive())
        {
            Revives.Clear();
            return;
        }

        long now = Utils.TimeStamp;
        if (now == LastUpdate) return;
        LastUpdate = now;

        foreach ((byte id, ReviveData data) in Revives)
        {
            if (data.Done) continue;

            if (now - data.StartTimeStamp >= ReviveTime.GetInt())
            {
                var player = id.GetPlayer();
                if (player == null) continue;

                if (!player.Is(Team.Impostor))
                    player.RpcSetCustomRole(RevivedPlayers.GetValue() == 0 ? CustomRoles.Refugee : CustomRoles.Madmate);

                player.RpcRevive();
                player.TP(data.Position);
                player.Notify(Translator.GetString("RevivedByOccultist"), 15f);

                OccultistPC.Notify(string.Format(Translator.GetString("OccultistRevived"), player.PlayerId.ColoredPlayerName()));

                data.Done = true;
            }
        }

        if (Main.AllAlivePlayerControls.Length < 4) SwitchAction();
    }

    public static void OnAnyoneDied(PlayerControl target)
    {
        if (!ArrowsToBodies.GetBool()) return;
        var pos = target.Pos();
        Instances.Do(x => LocateArrow.Add(x.OccultistPC.PlayerId, pos));
    }

    public override void OnReportDeadBody()
    {
        if (ArrowsToBodies.GetBool()) LocateArrow.RemoveAllTarget(OccultistPC.PlayerId);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != OccultistPC.PlayerId || seer.PlayerId != target.PlayerId || meeting || (seer.IsModClient() && !hud)) return string.Empty;
        var str = string.Format(Translator.GetString("OccultistSuffix"), Translator.GetString(InRevivingMode ? "OccultistMode.Revive" : "OccultistMode.Report"), Translator.GetString($"OccultistActionSwitchMode.{ActionSwitchMode}"));
        if (Main.AllAlivePlayerControls.Length < 4) str = str.Split('(')[0].TrimEnd(' ');
        return str;
    }

    enum ActionSwitchModes
    {
        Vent,
        Pet,
        Unshift
    }

    class ReviveData(long startTimeStamp, Vector2 position, bool done)
    {
        public long StartTimeStamp { get; } = startTimeStamp;
        public Vector2 Position { get; } = position;
        public bool Done { get; set; } = done;
    }
}