using AmongUs.GameOptions;

namespace EHR.Roles;

public class SNSSeeker : RoleBase, IHideAndSeekRole
{
    public static bool On;

    public static OptionItem CanOnlyKillShapeshiftTarget;
    public static OptionItem PenaltyForWrongKillAttempt;
    public static OptionItem ToleranceBeforeSuicide;
    public static OptionItem ShapeshiftCooldown;
    public static OptionItem ShapeshiftDuration;
    public static OptionItem ShapeshiftAnimation;
    public static OptionItem Vision;
    public static OptionItem Speed;

    public override bool IsEnable => On;
    public Team Team => Team.Impostor;
    public int Chance => CustomRoles.SNSSeeker.GetMode();
    public int Count => CustomRoles.SNSSeeker.GetCount();
    public float RoleSpeed => Speed.GetFloat();
    public float RoleVision => Vision.GetFloat();

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(69_211_1101, TabGroup.ImpostorRoles, CustomRoles.SNSSeeker, CustomGameMode.HideAndSeek);

        Vision = new FloatOptionItem(69_211_1103, "SNSSeekerVision", new(0.05f, 5f, 0.05f), 0.25f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SNSSeeker]);

        Speed = new FloatOptionItem(69_213_1104, "SNSSeekerSpeed", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SNSSeeker]);

        CanOnlyKillShapeshiftTarget = new BooleanOptionItem(69_213_1105, "SNSSeekerCanOnlyKillShapeshiftTarget", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SNSSeeker]);
        
        PenaltyForWrongKillAttempt = new StringOptionItem(69_213_1106, "SNSSeekerPenaltyForWrongKillAttempt", ["SNSSeekerPFWKA.BlockKill", "SNSSeekerPFWKA.Suicide"], 1, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(CanOnlyKillShapeshiftTarget);
        
        ToleranceBeforeSuicide = new IntegerOptionItem(69_213_1107, "SNSSeekerToleranceBeforeSuicide", new(0, 14, 1), 2, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Times)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(PenaltyForWrongKillAttempt);
        
        ShapeshiftCooldown = new IntegerOptionItem(69_213_1108, "SNSSeekerShapeshiftCooldown", new(0, 180, 1), 0, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SNSSeeker]);
        
        ShapeshiftDuration = new IntegerOptionItem(69_213_1109, "SNSSeekerShapeshiftDuration", new(0, 180, 1), 30, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SNSSeeker]);

        ShapeshiftAnimation = new BooleanOptionItem(69_213_1110, "SNSSeekerShapeshiftAnimation", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SNSSeeker]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        
        if (CanOnlyKillShapeshiftTarget.GetBool() && PenaltyForWrongKillAttempt.GetValue() == 1)
            playerId.SetAbilityUseLimit(ToleranceBeforeSuicide.GetInt());
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetInt();
        AURoleOptions.ShapeshifterDuration = ShapeshiftDuration.GetInt();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        return ShapeshiftAnimation.GetBool();
    }

    public static bool CheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (CanOnlyKillShapeshiftTarget.GetBool() && killer.shapeshiftTargetPlayerId != target.PlayerId)
        {
            if (PenaltyForWrongKillAttempt.GetValue() == 1)
            {
                if (killer.GetAbilityUseLimit() < 1)
                    killer.Suicide();
                else
                    killer.RpcRemoveAbilityUse();
            }

            return false;
        }

        return true;
    }
}