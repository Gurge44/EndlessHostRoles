using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Impostor
{
    public class Cantankerous : RoleBase
    {
        private const int Id = 642860;
        private static List<byte> PlayerIdList = [];

        private static OptionItem PointsGainedPerEjection;
        private static OptionItem StartingPoints;
        private static OptionItem KCD;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Cantankerous);
            KCD = new FloatOptionItem(Id + 5, "KillCooldown", new(0f, 60f, 2.5f), 22.5f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Seconds);
            PointsGainedPerEjection = new IntegerOptionItem(Id + 6, "CantankerousPointsGainedPerEjection", new(1, 5, 1), 2, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Times);
            StartingPoints = new IntegerOptionItem(Id + 7, "CantankerousStartingPoints", new(0, 5, 1), 1, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cantankerous])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(StartingPoints.GetInt());
        }

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