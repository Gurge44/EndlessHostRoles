using AmongUs.GameOptions;

namespace EHR.Roles.Impostor
{
    public class Camouflager : RoleBase
    {
        private const int Id = 2500;

        public static OptionItem CamouflageCooldown;
        private static OptionItem CamouflageDuration;
        private static OptionItem CamoLimitOpt;
        public static OptionItem CamoAbilityUseGainWithEachKill;
        public static OptionItem DoesntSpawnOnFungle;

        public static bool IsActive;
        public static bool On;

        public static void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Camouflager);
            CamouflageCooldown = FloatOptionItem.Create(Id + 2, "CamouflageCooldown", new(1f, 60f, 1f), 25f, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Seconds);
            CamouflageDuration = FloatOptionItem.Create(Id + 3, "CamouflageDuration", new(1f, 30f, 1f), 12f, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Seconds);
            CamoLimitOpt = IntegerOptionItem.Create(Id + 4, "AbilityUseLimit", new(0, 5, 1), 1, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Times);
            CamoAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 5, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Times);
            DoesntSpawnOnFungle = BooleanOptionItem.Create(Id + 6, "DoesntSpawnOnFungle", false, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager]);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ShapeshifterCooldown = CamouflageCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = CamouflageDuration.GetFloat();
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

        public override bool IsEnable => On;

        public override bool OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
        {
            if (shapeshifting && pc.GetAbilityUseLimit() < 1)
            {
                pc.SetKillCooldown(CamouflageDuration.GetFloat() + 1f);
            }

            if (shapeshifting) pc.RpcRemoveAbilityUse();
            IsActive = true;
            Camouflage.CheckCamouflage();

            return true;
        }

        public override void OnReportDeadBody()
        {
            Reset();
        }

        public static void Reset()
        {
            IsActive = false;
            Camouflage.CheckCamouflage();
        }

        public static void IsDead(PlayerControl target)
        {
            if (!target.Data.IsDead || GameStates.IsMeeting) return;

            if (target.Is(CustomRoles.Camouflager) && target.Data.IsDead)
            {
                IsActive = false;
                Camouflage.CheckCamouflage();
                Utils.NotifyRoles(ForceLoop: true);
            }
        }
    }
}