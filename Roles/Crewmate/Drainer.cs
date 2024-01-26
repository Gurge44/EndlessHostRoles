﻿using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    public static class Drainer
    {
        private static readonly int Id = 642500;
        private static List<byte> playerIdList = [];

        private static OptionItem VentCD;
        private static OptionItem UseLimit;
        public static OptionItem DrainerAbilityUseGainWithEachTaskCompleted;

        public static float DrainLimit;

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
        }

        public static void Init()
        {
            playerIdList = [];
            playersInVents = [];
            DrainLimit = 0;
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            DrainLimit = UseLimit.GetInt();
        }

        public static void ApplyGameOptions()
        {
            AURoleOptions.EngineerCooldown = VentCD.GetFloat();
        }

        public static bool IsEnable => playerIdList.Count > 0;

        public static void SendRPC()
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDrainerLimit, SendOption.Reliable, -1);
            writer.Write(DrainLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            DrainLimit = reader.ReadInt32();
        }

        public static void OnAnyoneExitVent(PlayerControl pc)
        {
            if (!IsEnable || !AmongUsClient.Instance.AmHost) return;
            if (pc != null) playersInVents.Remove(pc.PlayerId);
        }

        public static void OnDrainerEnterVent(PlayerControl pc, Vent vent)
        {
            if (!IsEnable) return;
            if (!pc.Is(CustomRoles.Drainer)) return;
            if (DrainLimit <= 0) return;

            DrainLimit--;

            var vents = vent.NearbyVents.Where(vent => vent != null).AddItem(vent).ToArray();
            foreach (var ventToDrain in vents) KillPlayersInVent(pc, ventToDrain);
        }

        public static void OnAnyoneEnterVent(PlayerControl pc, Vent vent)
        {
            if (!IsEnable || !AmongUsClient.Instance.AmHost || pc == null || vent == null) return;

            if (pc.Is(CustomRoles.Drainer))
            {
                OnDrainerEnterVent(pc, vent);
                return;
            }

            playersInVents.Remove(pc.PlayerId);
            playersInVents.Add(pc.PlayerId, vent.Id);
        }

        public static string GetProgressText() => $"<color=#777777>-</color> <color=#ffffff>{DrainLimit}</color>";

        private static void KillPlayersInVent(PlayerControl pc, Vent vent)
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

        public static void OnReportDeadBody()
        {
            if (!IsEnable) return;
            playersInVents.Clear();
        }
    }
}
