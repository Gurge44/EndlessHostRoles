using AmongUs.GameOptions;

namespace EHR.Impostor;

public class Camouflager : RoleBase
{
    private const int Id = 2500;

    public static OptionItem CamouflageCooldown;
    private static OptionItem CamouflageDuration;
    private static OptionItem CamoLimitOpt;
    public static OptionItem AbilityUseGainWithEachKill;
    public static OptionItem DoesntSpawnOnFungle;

    public static bool IsActive;
    public static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Camouflager);

        CamouflageCooldown = new FloatOptionItem(Id + 2, "CamouflageCooldown", new(1f, 60f, 1f), 25f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
            .SetValueFormat(OptionFormat.Seconds);

        CamouflageDuration = new FloatOptionItem(Id + 3, "CamouflageDuration", new(1f, 30f, 1f), 12f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
            .SetValueFormat(OptionFormat.Seconds);

        CamoLimitOpt = new IntegerOptionItem(Id + 4, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
            .SetValueFormat(OptionFormat.Times);

        AbilityUseGainWithEachKill = new FloatOptionItem(Id + 5, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
            .SetValueFormat(OptionFormat.Times);

        DoesntSpawnOnFungle = new BooleanOptionItem(Id + 6, "DoesntSpawnOnFungle", false, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager]);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (Options.UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = CamouflageCooldown.GetFloat();
        else
        {
            AURoleOptions.ShapeshifterCooldown = CamouflageCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = CamouflageDuration.GetFloat();
        }
    }

    public override void Init()
    {
        IsActive = false;
        On = false;
    }

    public override void Add(byte playerId)
    {
        playerId.SetAbilityUseLimit(CamoLimitOpt.GetInt());
        On = true;
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting)
        {
            IsActive = false;
            Camouflage.CheckCamouflage();
            return true;
        }

        if (pc.GetAbilityUseLimit() < 1 && !Options.DisableShapeshiftAnimations.GetBool())
        {
            pc.SetKillCooldown(CamouflageDuration.GetFloat() + 1f);
            return true;
        }

        pc.RpcRemoveAbilityUse();
        IsActive = true;
        Camouflage.CheckCamouflage();

        return true;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return false;
        pc.RpcRemoveAbilityUse();

        IsActive = true;
        Camouflage.CheckCamouflage();

        LateTask.New(() =>
        {
            if (GameStates.IsInTask && !ExileController.Instance)
            {
                IsActive = false;
                Camouflage.CheckCamouflage();
            }
        }, CamouflageDuration.GetFloat(), "Revert Camouflage");

        return false;
    }

    public override void OnReportDeadBody()
    {
        IsActive = false;
        Camouflage.CheckCamouflage();
    }

    public static void IsDead()
    {
        IsActive = false;
        Camouflage.CheckCamouflage();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(Translator.GetString("CamouflagerShapeshiftText"));
        hud.AbilityButton?.SetUsesRemaining((int)id.GetAbilityUseLimit());
    }
}