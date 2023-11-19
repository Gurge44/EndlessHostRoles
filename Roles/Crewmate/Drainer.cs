using AmongUs.GameOptions;
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
        public static void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.EngineerCooldown = VentCD.GetFloat();
        }
        public static bool IsEnable => playerIdList.Any();
        public static void SendRPC()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDrainerLimit, SendOption.Reliable, -1);
            writer.Write(DrainLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            DrainLimit = reader.ReadInt32();
        }
        public static void OnEnterVent(PlayerControl pc, int ventId)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Drainer)) return;
            if (DrainLimit <= 0) return;

            DrainLimit--;

            KillPlayersInVent(pc, ventId);
        }
        public static void OnOtherPlayerEnterVent(PlayerControl pc, int ventId)
        {
            OnEnterVent(pc, ventId);
            if (pc == null) return;
            if (pc.Is(CustomRoles.Drainer)) return;

            playersInVents.TryAdd(pc.PlayerId, ventId);
        }
        public static void OnExitVent(PlayerControl pc, int ventId)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Drainer)) return;

            KillPlayersInVent(pc, ventId);
        }
        public static void OnOtherPlayerExitVent(PlayerControl pc, int ventId)
        {
            OnExitVent(pc, ventId);
            if (pc == null) return;
            if (pc.Is(CustomRoles.Drainer)) return;

            playersInVents.Remove(pc.PlayerId);
        }
        public static string GetProgressText() => $"<color=#777777>-</color> <color=#ffffff>{DrainLimit}</color>";
        private static void KillPlayersInVent(PlayerControl pc, int ventId)
        {
            if (!playersInVents.ContainsValue(ventId)) return;

            foreach (var venterId in playersInVents.Where(x => x.Value == ventId))
            {
                var venter = Utils.GetPlayerById(venterId.Key);
                if (venter == null) continue;

                if (pc.RpcCheckAndMurder(venter, true))
                {
                    venter.MyPhysics?.RpcBootFromVent(ventId);
                    _ = new LateTask(() =>
                    {
                        venter.SetRealKiller(pc);
                        venter.Kill(venter);
                        Main.PlayerStates[venter.PlayerId].SetDead();
                        Main.PlayerStates[venter.PlayerId].deathReason = PlayerState.DeathReason.Demolished;
                    }, 0.55f, "Drainer-KillPlayerInVent");
                }
            }
        }
        public static void OnReportDeadBody()
        {
            playersInVents.Clear();
        }
    }
}
