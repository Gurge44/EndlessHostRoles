using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Crewmate
{
    using static Options;

    public class Doormaster : RoleBase
    {
        private const int Id = 640000;
        private static List<byte> PlayerIdList = [];

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem DoormasterAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Doormaster);

            VentCooldown = new FloatOptionItem(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Seconds);

            UseLimitOpt = new IntegerOptionItem(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Times);

            DoormasterAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Times);

            AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
        }

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

        private static void OpenDoors(PlayerControl pc)
        {
            if (pc == null) return;

            if (!pc.Is(CustomRoles.Doormaster)) return;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                DoorsReset.OpenAllDoors();
            }
            else
                pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
        }

        public override bool CanUseVent(PlayerControl pc, int ventId)
        {
            return !IsThisRole(pc) || pc.GetClosestVent()?.Id == ventId;
        }
    }
}