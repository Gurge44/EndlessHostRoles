using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Catcher : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    public static OptionItem TrapPlaceDelay;
    public static OptionItem CatchRange;
    public static OptionItem MinPlayersTrappedToShowInfo;
    public static OptionItem AbilityUseLimit;
    public static OptionItem AbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    
    private byte CatcherId;
    private Dictionary<byte, CustomRoles> CaughtRoles;
    private CountdownTimer DelayTimer;
    private Dictionary<Vector2, CatcherTrap> Traps;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 648650;
        const TabGroup tab = TabGroup.CrewmateRoles;
        const CustomRoles role = CustomRoles.Catcher;

        Options.SetupRoleOptions(id++, tab, role);

        StringOptionItem parent = Options.CustomRoleSpawnChances[role];

        AbilityCooldown = new FloatOptionItem(++id, "AbilityCooldown", new(0f, 180f, 0.5f), 10f, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);

        TrapPlaceDelay = new FloatOptionItem(++id, "Catcher.TrapPlaceDelay", new(0f, 180f, 1f), 5f, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);

        CatchRange = new FloatOptionItem(++id, "Catcher.CatchRange", new(0f, 10f, 0.25f), 1.5f, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Multiplier);

        MinPlayersTrappedToShowInfo = new IntegerOptionItem(++id, "Catcher.MinPlayersTrappedToShowInfo", new(1, 10, 1), 2, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Players);

        AbilityUseLimit = new FloatOptionItem(++id, "AbilityUseLimit", new(0, 20, 0.05f), 3, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Times);

        AbilityUseGainWithEachTaskCompleted = new FloatOptionItem(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        CatcherId = playerId;
        Traps = [];
        DelayTimer = null;
        CaughtRoles = [];
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    private void PlaceTrap(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;

        pc.RpcRemoveAbilityUse();

        Vector2 pos = pc.Pos();
        Traps[pos] = new(pos, pc);

        pc.Notify(Translator.GetString("Catcher.TrapPlaced"));
    }

    public override void OnPet(PlayerControl pc)
    {
        PlaceTrap(pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Options.UsePets.GetBool()) return;
        DelayTimer = new CountdownTimer(TrapPlaceDelay.GetInt(), () =>
        {
            PlaceTrap(pc);
            DelayTimer = null;
        }, onTick: () =>
        {
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            pc.RpcResetAbilityCooldown();
        }, onCanceled: () => DelayTimer = null);
        Utils.SendRPC(CustomRPC.SyncRoleData, CatcherId);
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (CaughtRoles.ContainsKey(pc.PlayerId) || pc.PlayerId == CatcherId) return;

        Vector2 pos = pc.Pos();
        float range = CatchRange.GetFloat();
        if (Traps.Keys.Any(x => FastVector2.DistanceWithinRange(x, pos, range))) CaughtRoles[pc.PlayerId] = pc.GetCustomRole();
    }

    public override void OnReportDeadBody()
    {
        if (Traps.Count == 0) return;

        Traps.Values.Do(x => x.Despawn());
        Traps = [];

        PlayerControl catcher = CatcherId.GetPlayer();
        if (catcher == null || !catcher.IsAlive()) return;

        LateTask.New(() =>
        {
            if (CaughtRoles.Count >= MinPlayersTrappedToShowInfo.GetInt())
            {
                string roles = string.Join(", ", CaughtRoles.Values.Select(x => x.ToColoredString()));
                Utils.SendMessage("\n", CatcherId, Translator.GetString("Catcher.CaughtRoles") + roles, importance: MessageImportance.High);
            }
            else
                Utils.SendMessage("\n", CatcherId, Translator.GetString("Catcher.NotEnoughCaughtRoles"), importance: MessageImportance.Low);

            CaughtRoles = [];
        }, 10f, "Send Catcher Caught Roles");
    }

    public void ReceiveRPC(MessageReader reader)
    {
        DelayTimer = new CountdownTimer(TrapPlaceDelay.GetInt(), () => DelayTimer = null, onCanceled: () => DelayTimer = null);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != CatcherId || meeting || (seer.IsModdedClient() && !hud) || DelayTimer == null || Options.UsePets.GetBool()) return string.Empty;
        return string.Format(Translator.GetString("Catcher.Suffix"), (int)Math.Ceiling(DelayTimer.Remaining.TotalSeconds));
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}