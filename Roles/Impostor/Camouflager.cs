using System.Collections.Generic;

namespace TOHE.Roles.Impostor
{
    public static class Camouflager
    {
        private static readonly int Id = 2500;

        public static OptionItem CamouflageCooldown;
        private static OptionItem CamouflageDuration;
        private static OptionItem CamoLimitOpt;
        public static OptionItem CamoAbilityUseGainWithEachKill;

        public static bool IsActive;
        public static bool IsEnable;
        public static Dictionary<byte, float> CamoLimit = [];

        public static void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Camouflager, 1);
            CamouflageCooldown = FloatOptionItem.Create(Id + 2, "CamouflageCooldown", new(1f, 60f, 1f), 25f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Seconds);
            CamouflageDuration = FloatOptionItem.Create(Id + 3, "CamouflageDuration", new(1f, 30f, 1f), 12f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Seconds);
            CamoLimitOpt = IntegerOptionItem.Create(Id + 4, "AbilityUseLimit", new(0, 5, 1), 1, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Times);
            CamoAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 5, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Times);
        }
        public static void ApplyGameOptions()
        {
            AURoleOptions.ShapeshifterCooldown = CamouflageCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = CamouflageDuration.GetFloat();
        }
        public static void Init()
        {
            IsActive = false;
            CamoLimit = [];
            IsEnable = false;
        }
        public static void Add(byte playerId)
        {
            CamoLimit.Add(playerId, CamoLimitOpt.GetInt());
            IsEnable = true;
        }
        public static void OnShapeshift(PlayerControl pc, bool shapeshifting)
        {
            if (shapeshifting && CamoLimit[pc.PlayerId] < 1)
            {
                pc.SetKillCooldown(CamouflageDuration.GetFloat() + 1f);
            };
            if (shapeshifting) CamoLimit[pc.PlayerId] -= 1;
            IsActive = true;
            Camouflage.CheckCamouflage();
        }
        public static void OnReportDeadBody()
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
