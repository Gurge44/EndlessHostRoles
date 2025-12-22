using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Patches;

namespace EHR.Neutral;

public class Shifter : RoleBase
{
    public static bool On;

    public static HashSet<byte> WasShifter = [];
    private static int ShifterInteractionsCount;

    public static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;
    public static OptionItem CanGuess;
    private static OptionItem CanBeKilled;
    public static OptionItem CanBeVoted;
    public static OptionItem CanVote;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(644400)
            .AutoSetupOption(ref KillCooldown, 15f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds, "AbilityCooldown")
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true)
            .AutoSetupOption(ref CanGuess, false)
            .AutoSetupOption(ref CanBeKilled, true)
            .AutoSetupOption(ref CanBeVoted, true)
            .AutoSetupOption(ref CanVote, true);
    }

    public override void Init()
    {
        if (GameStates.InGame && !Main.HasJustStarted) return;

        On = false;

        WasShifter = [];

        ShifterInteractionsCount = 0;
    }

    public override void Add(byte playerId)
    {
        On = true;

        PlayerControl pc = playerId.GetPlayer();
        if (pc == null) return;

        pc.ResetKillCooldown();
        pc.SyncSettings();
        pc.SetKillCooldown();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return true;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurder(killer, target)) return false;

        CustomRoles targetRole = target.GetCustomRole();

        killer.RpcSetCustomRole(targetRole);
        killer.RpcChangeRoleBasis(targetRole);

        killer.SetAbilityUseLimit(target.GetAbilityUseLimit());

        TaskState taskState = target.GetTaskState();
        if (taskState.HasTasks) Main.PlayerStates[killer.PlayerId].TaskState = taskState;

        killer.SyncSettings();

        // ------------------------------------------------------------------------------------------

        target.RpcSetCustomRole(CustomRoles.Shifter);
        target.RpcChangeRoleBasis(CustomRoles.Shifter);
        Main.AbilityUseLimit.Remove(target.PlayerId);
        Utils.SendRPC(CustomRPC.RemoveAbilityUseLimit, target.PlayerId);
        target.SyncSettings();
        LateTask.New(() => target.SetKillCooldown(), 0.2f, log: false);

        // ------------------------------------------------------------------------------------------

        Utils.NotifyRoles(SpecifyTarget: killer);
        Utils.NotifyRoles(SpecifyTarget: target);

        WasShifter.Add(killer.PlayerId);

        if (killer.AmOwner || target.AmOwner)
        {
            ShifterInteractionsCount++;
            if (ShifterInteractionsCount >= 3) Achievements.Type.TheresThisGameMyDadTaughtMeItsCalledSwitch.Complete();
        }

        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        OnCheckMurder(pc, ExternalRpcPetPatch.SelectKillButtonTarget(pc));
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("ShifterKillButtonText"));
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return CanBeKilled.GetBool();
    }
}