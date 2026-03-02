using System;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;
using UnityEngine;

namespace EHR.Roles;

public class Astral : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    private byte AstralId;
    public CountdownTimer Timer;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651400)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10, new IntegerValueRule(1, 30, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.3f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Timer = null;
        AstralId = playerId;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;

        try { AURoleOptions.GuardianAngelCooldown = 900f; }
        catch { }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Options.UsePets.GetBool()) return;
        LateTask.New(() => BecomeGhostTemporarily(pc), 2f, log: false);
    }

    public override void OnPet(PlayerControl pc)
    {
        BecomeGhostTemporarily(pc);
    }

    void BecomeGhostTemporarily(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1f || ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting) return;
        pc.RpcRemoveAbilityUse();

        Timer = new CountdownTimer(AbilityDuration.GetInt(), () => BecomeAliveAgain(pc), onTick: () => Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc), onCanceled: () => Timer = null);
        Utils.SendRPC(CustomRPC.SyncRoleData, AstralId);

        pc.RpcSetRoleGlobal(RoleTypes.GuardianAngel);
        pc.MarkDirtySettings();
        LateTask.New(pc.RpcResetAbilityCooldown, 0.2f, log: false);
    }

    void BecomeAliveAgain(PlayerControl pc, bool onMeeting = false)
    {
        Timer = null;
        if (!pc.IsAlive()) return;

        GhostRolesManager.RemoveGhostRole(pc.PlayerId);
        pc.RpcSetRoleGlobal(Options.UsePets.GetBool() ? RoleTypes.Crewmate : RoleTypes.Engineer);
        Camouflage.RpcSetSkin(pc);

        if (onMeeting) return;

        pc.SyncSettings();

        if (!Options.UsePets.GetBool())
            pc.RpcResetAbilityCooldown();

        Utils.NotifyRoles(SpecifySeer: pc);
        Utils.NotifyRoles(SpecifyTarget: pc);

        if (!pc.IsInsideMap())
        {
            Vector2 playerPosition = pc.Pos();
            Vector2 closestSpawnPosition = FastVector2.TryGetClosest(playerPosition, RandomSpawn.SpawnMap.GetSpawnMap().Positions.Values, out Vector2 closest1) ? closest1 : new Vector2(50f, 50f);
            Vector3 closestVentPosition = pc.GetClosestVent()?.transform.position ?? closestSpawnPosition;
            pc.TP(Vector2.Distance(playerPosition, closestVentPosition) < Vector2.Distance(playerPosition, closestSpawnPosition) ? closestVentPosition : closestSpawnPosition);
        }
    }

    public override void OnReportDeadBody()
    {
        PlayerControl astralPc = AstralId.GetPlayer();
        if (astralPc == null) return;

        if (Timer != null)
        {
            Timer.Dispose();
            Timer = null;
            BecomeAliveAgain(astralPc, true);
        }

        if (!astralPc.IsAlive()) return;
        ChatManager.ClearChat(astralPc);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = new CountdownTimer(AbilityDuration.GetInt(), () => Timer = null, onCanceled: () => Timer = null);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != AstralId || seer.PlayerId != target.PlayerId || hud || meeting || Timer == null) return string.Empty;
        return string.Format(Translator.GetString("AstralSuffix"), (int)Math.Ceiling(Timer.Remaining.TotalSeconds));
    }
}
