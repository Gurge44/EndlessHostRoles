using System.Collections.Generic;
using EHR.Modules;
using EHR.Modules.Extensions;

namespace EHR.Roles;

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
    private static OptionItem AbilityUseGainWithEachKill;

    private static readonly string[] RevivedPlayersModes =
    [
        "Occultist.RPM.Renegade",
        "Occultist.RPM.Madmate"
    ];

    private static ActionSwitchModes ActionSwitchMode;
    private static Dictionary<byte, CountdownTimer> Revives = [];

    private bool InRevivingMode;

    private PlayerControl OccultistPC;

    public override bool IsEnable => On;

    public override bool SeesArrowsToDeadBodies => ArrowsToBodies.GetBool();

    public override void SetupCustomOption()
    {
        StartSetup(645250)
            .AutoSetupOption(ref AbilityUseLimit, 0f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref ReviveTime, 5, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ArrowsToBodies, false)
            .AutoSetupOption(ref RevivedPlayersBodiesCanBeReported, false)
            .AutoSetupOption(ref RevivedPlayers, 0, RevivedPlayersModes)
            .AutoSetupOption(ref CanReviveImpostorsAndMadmates, true)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        ActionSwitchMode = Options.UsePhantomBasis.GetBool() ? ActionSwitchModes.Vanish : Options.UsePets.GetBool() ? ActionSwitchModes.Pet : ActionSwitchModes.Vent;
        Revives = [];
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        OccultistPC = playerId.GetPlayer();
        InRevivingMode = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    private void SwitchAction()
    {
        InRevivingMode = Main.AllAlivePlayerControls.Count >= 4 && !InRevivingMode;
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

    public override bool OnVanish(PlayerControl pc)
    {
        if (ActionSwitchMode == ActionSwitchModes.Vanish)
            SwitchAction();

        return false;
    }

    public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
    {
        if (!InRevivingMode || reporter.GetAbilityUseLimit() < 1 || target.Disconnected || target.Object.IsAlive() || target.Object.Is(CustomRoles.Disregarded) || (target.Object.Is(Team.Impostor) && !CanReviveImpostorsAndMadmates.GetBool())) return true;

        InRevivingMode = false;
        Vector2 pos = reporter.Pos();
        Revives[target.PlayerId] = new CountdownTimer(ReviveTime.GetInt(), () =>
        {
            switch (target.Object.GetCustomSubRoles().FindFirst(x => x.IsConverted(), out CustomRoles convertedAddon))
            {
                case false when !target.Object.Is(Team.Impostor):
                    target.Object.RpcSetCustomRole(RevivedPlayers.GetValue() == 0 ? CustomRoles.Renegade : CustomRoles.Madmate);
                    break;
                case true:
                    target.Object.RpcSetCustomRole(convertedAddon);
                    break;
            }

            target.Object.RpcRevive();
            target.Object.TP(pos);
            target.Object.Notify(Translator.GetString("RevivedByOccultist"), 15f);

            Revives.Remove(target.PlayerId);

            OccultistPC.Notify(string.Format(Translator.GetString("OccultistRevived"), target.PlayerId.ColoredPlayerName()));
        }, onCanceled: () => Revives.Remove(target.PlayerId));
        reporter.RpcRemoveAbilityUse();
        reporter.Notify(string.Format(Translator.GetString("OccultistReviving"), target.PlayerId.ColoredPlayerName()), ReviveTime.GetInt());

        return false;
    }

    public static bool OnAnyoneReportDeadBody(NetworkedPlayerInfo target)
    {
        if (RevivedPlayersBodiesCanBeReported.GetBool()) return true;
        return !Revives.ContainsKey(target.PlayerId);
    }

    public static void OnAnyoneDead()
    {
        if (Main.AllAlivePlayerControls.Count < 4) Instances.ForEach(x => x.SwitchAction());
    }

    public override void OnReportDeadBody()
    {
        if (ArrowsToBodies.GetBool()) LocateArrow.RemoveAllTarget(OccultistPC.PlayerId);
        Revives.Values.Do(x => x.Dispose());
        Revives.Clear();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != OccultistPC.PlayerId || seer.PlayerId != target.PlayerId || meeting || (seer.IsModdedClient() && !hud)) return string.Empty;
        string str = string.Format(Translator.GetString("OccultistSuffix"), Translator.GetString(InRevivingMode ? "OccultistMode.Revive" : "OccultistMode.Report"), Translator.GetString($"OccultistActionSwitchMode.{ActionSwitchMode}"));
        if (Main.AllAlivePlayerControls.Count < 4) str = str.Split('(')[0].TrimEnd(' ');
        return str;
    }

    private enum ActionSwitchModes
    {
        Vent,
        Pet,
        Vanish
    }
}