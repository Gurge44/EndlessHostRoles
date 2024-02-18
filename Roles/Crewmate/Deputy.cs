using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class Deputy
{
    private static readonly int Id = 6500;
    private static List<byte> playerIdList = [];

    public static OptionItem HandcuffCooldown;
    public static OptionItem HandcuffMax;
    public static OptionItem DeputyHandcuffCDForTarget;
    private static OptionItem DeputyHandcuffDelay;
    public static OptionItem UsePet;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Deputy);
        HandcuffCooldown = FloatOptionItem.Create(Id + 10, "DeputyHandcuffCooldown", new(0f, 60f, 2.5f), 17.5f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Seconds);
        DeputyHandcuffCDForTarget = FloatOptionItem.Create(Id + 14, "DeputyHandcuffCDForTarget", new(0f, 180f, 2.5f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Seconds);
        HandcuffMax = IntegerOptionItem.Create(Id + 12, "DeputyHandcuffMax", new(1, 20, 1), 4, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Times);
        DeputyHandcuffDelay = IntegerOptionItem.Create(Id + 11, "DeputyHandcuffDelay", new(0, 20, 1), 5, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Seconds);
        UsePet = CreatePetUseSetting(Id + 13, CustomRoles.Deputy);
    }

    public static void Init()
    {
        playerIdList = [];
    }

    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(HandcuffMax.GetInt());

        if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public static bool IsEnable => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = HandcuffCooldown.GetFloat();
    public static bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && player.GetAbilityUseLimit() >= 1;

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return false;
        if (CanBeHandcuffed(target))
        {
            killer.RpcRemoveAbilityUse();

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("DeputyHandcuffedPlayer")));

            //  target.ResetKillCooldown();
            _ = new LateTask(() =>
            {
                if (GameStates.IsInTask)
                {
                    target.SetKillCooldown(DeputyHandcuffCDForTarget.GetFloat());
                    target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("HandcuffedByDeputy")));
                    if (target.IsModClient()) target.RpcResetAbilityCooldown();
                    if (!target.IsModClient()) target.RpcGuardAndKill(target);
                }
            }, DeputyHandcuffDelay.GetInt());

            killer.SetKillCooldown();

            if (killer.GetAbilityUseLimit() < 0)
                HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
            return true;
        }

        if (killer.GetAbilityUseLimit() < 0)
            HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("DeputyInvalidTarget")));
        return false;
    }

    public static string GetHandcuffLimit(byte id) => Utils.GetAbilityUseLimitDisplay(id);
    public static bool CanBeHandcuffed(this PlayerControl pc) => pc != null && !pc.Is(CustomRoles.Deputy);
}