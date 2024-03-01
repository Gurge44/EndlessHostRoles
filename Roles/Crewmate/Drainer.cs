using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    public class Drainer : RoleBase
    {
        private const int Id = 642500;
        private static List<byte> playerIdList = [];

        private static OptionItem VentCD;
        private static OptionItem UseLimit;
        public static OptionItem DrainerAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static Dictionary<byte, int> playersInVents = [];

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Drainer, 1);
            VentCD = IntegerOptionItem.Create(Id + 10, "VentCooldown", new(1, 60, 1), 30, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Drainer])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Drainer])
                .SetValueFormat(OptionFormat.Times);
            DrainerAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Drainer])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Drainer])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
            playersInVents = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimit.GetInt());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.EngineerCooldown = VentCD.GetFloat();
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public static void OnAnyoneExitVent(PlayerControl pc)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (pc != null) playersInVents.Remove(pc.PlayerId);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (pc.GetAbilityUseLimit() <= 0) return;

            pc.RpcRemoveAbilityUse();

            var vents = vent.NearbyVents.Where(v => v != null).AddItem(vent).ToArray();
            foreach (var ventToDrain in vents) KillPlayersInVent(pc, ventToDrain);
        }

        public static void OnAnyoneEnterVent(PlayerControl pc, Vent vent)
        {
            if (!AmongUsClient.Instance.AmHost || pc == null || vent == null || pc.Is(CustomRoles.Drainer)) return;

            playersInVents[pc.PlayerId] = vent.Id;
        }

        void KillPlayersInVent(PlayerControl pc, Vent vent)
        {
            if (!IsEnable) return;

            int ventId = vent.Id;

            if (!playersInVents.ContainsValue(ventId)) return;

            foreach (var venterId in playersInVents.Where(x => x.Value == ventId).ToArray())
            {
                var venter = Utils.GetPlayerById(venterId.Key);
                if (venter == null) continue;

                if (pc != null && pc.RpcCheckAndMurder(venter, true))
                {
                    venter.MyPhysics.RpcBootFromVent(ventId);
                    _ = new LateTask(() =>
                    {
                        venter.Suicide(PlayerState.DeathReason.Demolished, pc);
                        Logger.Info($"Killed venter {venter.GetNameWithRole()} (was inside {vent.name}, ID {ventId})", "Drainer");
                    }, 0.55f, "Drainer-KillPlayerInVent");
                }
            }
        }

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            playersInVents.Clear();
        }
    }
}