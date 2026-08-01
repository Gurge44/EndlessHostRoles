using AmongUs.GameOptions;

namespace EHR.Roles;

public class Disguiser : RoleBase, IHideAndSeekRole
{
    public static bool On;

    public static OptionItem CanOnlyKillShapeshiftTarget;
    public static OptionItem PenaltyForWrongKillAttempt;
    public static OptionItem ToleranceBeforeSuicide;
    public static OptionItem ResetKillCooldownTo;
    public static OptionItem ShapeshiftCooldown;
    public static OptionItem ShapeshiftDuration;
    public static OptionItem ShapeshiftAnimation;
    public static OptionItem Vision;
    public static OptionItem Speed;

    public override bool IsEnable => On;
    public Team Team => Team.Impostor;
    public int Chance => CustomRoles.Disguiser.GetMode();
    public int Count => CustomRoles.Disguiser.GetCount();
    public float RoleSpeed => Speed.GetFloat();
    public float RoleVision => Vision.GetFloat();

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(69_211_1101, TabGroup.ImpostorRoles, CustomRoles.Disguiser, CustomGameMode.HideAndSeek);

        Vision = new FloatOptionItem(69_211_1103, "DisguiserVision", new(0.05f, 5f, 0.05f), 0.25f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Disguiser]);

        Speed = new FloatOptionItem(69_213_1104, "DisguiserSpeed", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Disguiser]);

        CanOnlyKillShapeshiftTarget = new BooleanOptionItem(69_213_1105, "DisguiserCanOnlyKillShapeshiftTarget", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Disguiser]);
        
        PenaltyForWrongKillAttempt = new StringOptionItem(69_213_1106, "DisguiserPenaltyForWrongKillAttempt", ["DisguiserPFWKA.BlockKill", "DisguiserPFWKA.Suicide", "DisguiserPFWKA.ResetKillCooldown"], 1, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(CanOnlyKillShapeshiftTarget)
            .RegisterUpdateValueEvent((_, _, newValue) =>
            {
                ToleranceBeforeSuicide.SetHidden(newValue != 1);
                ResetKillCooldownTo.SetHidden(newValue != 2);
            })
            .SetRunEventOnLoad(true);
        
        ToleranceBeforeSuicide = new IntegerOptionItem(69_213_1107, "DisguiserToleranceBeforeSuicide", new(0, 14, 1), 2, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Times)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(PenaltyForWrongKillAttempt);
        
        ResetKillCooldownTo = new FloatOptionItem(69_213_1108, "DisguiserResetKillCooldownTo", new(0.5f, 120f, 0.5f), 10f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(PenaltyForWrongKillAttempt);
        
        ShapeshiftCooldown = new IntegerOptionItem(69_213_1109, "DisguiserShapeshiftCooldown", new(0, 180, 1), 0, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Disguiser]);
        
        ShapeshiftDuration = new IntegerOptionItem(69_213_1110, "DisguiserShapeshiftDuration", new(0, 180, 1), 30, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Disguiser]);

        ShapeshiftAnimation = new BooleanOptionItem(69_213_1111, "DisguiserShapeshiftAnimation", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(179, 70, 70, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Disguiser]);
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
            switch (PenaltyForWrongKillAttempt.GetValue())
            {
                case 1:
                {
                    if (killer.GetAbilityUseLimit() < 1)
                        killer.Suicide();
                    else
                        killer.RpcRemoveAbilityUse();

                    break;
                }
                case 2:
                {
                    killer.SetKillCooldown(ResetKillCooldownTo.GetFloat());
                    break;
                }
            }

            return false;
        }

        return true;
    }
}