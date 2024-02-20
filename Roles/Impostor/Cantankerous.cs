using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    public class Cantankerous : RoleBase
    {
        private const int Id = 642860;
        private static List<byte> playerIdList = [];

        private static OptionItem PointsGainedPerEjection;
        private static OptionItem StartingPoints;
        private static OptionItem KCD;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Cantankerous);
            KCD = FloatOptionItem.Create(Id + 5, "KillCooldown", new(0f, 60f, 2.5f), 22.5f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Seconds);
            PointsGainedPerEjection = IntegerOptionItem.Create(Id + 6, "CantankerousPointsGainedPerEjection", new(1, 5, 1), 2, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Times);
            StartingPoints = IntegerOptionItem.Create(Id + 7, "CantankerousStartingPoints", new(0, 5, 1), 1, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(StartingPoints.GetInt());
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KCD.GetFloat();

        public override bool CanUseKillButton(PlayerControl pc) => pc.GetAbilityUseLimit() > 0;

        public static void OnCrewmateEjected()
        {
            var value = PointsGainedPerEjection.GetInt();
            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is Cantankerous)
                {
                    Utils.GetPlayerById(state.Key).RpcIncreaseAbilityUseLimitBy(value);
                }
            }
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            if (killer.GetAbilityUseLimit() <= 0) return false;

            killer.RpcRemoveAbilityUse();

            return true;
        }
    }
}
