namespace TOHE.Roles.Crewmate
{
    using System.Collections.Generic;
    using System.Linq;
    using static TOHE.Options;

    public static class Ricochet
    {
        private static readonly int Id = 6400;
        private static List<byte> playerIdList = new();
        public static Dictionary<byte, float> UseLimit = new();
        public static byte ProtectAgainst = new();

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem RicochetAbilityUseGainWithEachTaskCompleted;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ricochet, 1);
            VentCooldown = FloatOptionItem.Create(Id + 11, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
            .SetValueFormat(OptionFormat.Times);
            RicochetAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
            .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = new();
            UseLimit = new();
            ProtectAgainst = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Any();
        public static bool OnKillAttempt(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;
            if (target.Is(CustomRoles.Ricochet)) return true;

            if (ProtectAgainst == killer.PlayerId)
            {
                killer.SetKillCooldown(time: 5f);
                return false;
            }

            return true;
        }
        public static void OnVote(PlayerControl pc, PlayerControl target)
        {
            if (target == null) return;
            if (pc == null) return;

            ProtectAgainst = target.PlayerId;
            UseLimit[pc.PlayerId] -= 1;
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            return string.Empty;
        }
    }
}