using AmongUs.GameOptions;
using EHR.Modules;
using System.Collections.Generic;

namespace EHR.Roles.Crewmate
{
    using static Options;

    public class Doormaster : RoleBase
    {
        private const int Id = 640000;
        private static List<byte> playerIdList = [];

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem DoormasterAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Doormaster);
            VentCooldown = FloatOptionItem.Create(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Times);
            DoormasterAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnPet(PlayerControl pc)
        {
            OpenDoors(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            OpenDoors(pc);
        }

        static void OpenDoors(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Doormaster)) return;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                DoorsReset.OpenAllDoors();
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
    }
}