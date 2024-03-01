using AmongUs.GameOptions;
using TOHE.Modules;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor;

public class Disperser : RoleBase
{
    private const int Id = 17000;

    public static OptionItem DisperserShapeshiftCooldown;
    private static OptionItem DisperserShapeshiftDuration;
    private static OptionItem DisperserLimitOpt;
    public static OptionItem DisperserAbilityUseGainWithEachKill;

    public static bool On;
    public override bool IsEnable => On;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.Disperser);
        DisperserShapeshiftCooldown = FloatOptionItem.Create(Id + 5, "ShapeshiftCooldown", new(1f, 60f, 1f), 20f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Seconds);
        DisperserShapeshiftDuration = FloatOptionItem.Create(Id + 6, "ShapeshiftDuration", new(1f, 30f, 1f), 1f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Seconds);
        DisperserLimitOpt = IntegerOptionItem.Create(Id + 7, "AbilityUseLimit", new(0, 5, 1), 1, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Times);
        DisperserAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 8, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        playerId.SetAbilityUseLimit(DisperserLimitOpt.GetInt());
        On = true;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (UsePets.GetBool()) return;
        AURoleOptions.ShapeshifterCooldown = DisperserShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = DisperserShapeshiftDuration.GetFloat();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifter == null || !shapeshifting) return false;
        if (shapeshifter.GetAbilityUseLimit() < 1)
        {
            shapeshifter.SetKillCooldown(DisperserShapeshiftDuration.GetFloat() + 1f);
            return false;
        }

        shapeshifter.RpcRemoveAbilityUse();

        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (shapeshifter.PlayerId == pc.PlayerId || pc.Data.IsDead || pc.onLadder || pc.inVent || GameStates.IsMeeting)
            {
                if (!pc.Is(CustomRoles.Disperser))
                    pc.Notify(ColorString(GetRoleColor(CustomRoles.Disperser), string.Format(GetString("ErrorTeleport"), pc.GetRealName())));

                continue;
            }

            pc.RPCPlayCustomSound("Teleport");
            pc.TPtoRndVent();
            pc.Notify(ColorString(GetRoleColor(CustomRoles.Disperser), string.Format(GetString("TeleportedInRndVentByDisperser"), pc.GetRealName())));
        }

        return false;
    }

    public override void SetButtonTexts(HudManager __instance, byte id)
    {
        __instance.AbilityButton.ToggleVisible(GetPlayerById(id).IsAlive());
        __instance.AbilityButton.OverrideText(GetString("DisperserVentButtonText"));
    }
}