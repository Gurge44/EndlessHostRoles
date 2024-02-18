using Hazel;
using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    public static class DonutDelivery
    {
        private static readonly int Id = 642700;
        private static List<byte> playerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;
        public static OptionItem UsePet;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.DonutDelivery, 1);
            CD = FloatOptionItem.Create(Id + 10, "DonutDeliverCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
                .SetValueFormat(OptionFormat.Times);
            UsePet = CreatePetUseSetting(Id + 13, CustomRoles.DonutDelivery);
        }

        public static void Init()
        {
            playerIdList = [];
        }
        public static void Add(byte playerId)
        {
            if (CurrentGameMode == CustomGameMode.MoveAndStop) return;

            playerIdList.Add(playerId);

            playerId.SetAbilityUseLimit(UseLimit.GetInt());

            if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SetKillCooldown(byte playerId)
        {
            if (CurrentGameMode == CustomGameMode.MoveAndStop)
            {
                Main.AllPlayerKillCooldown[playerId] = MoveAndStopManager.RoundTime + 10;
                return;
            }

            Main.AllPlayerKillCooldown[playerId] = playerId.GetAbilityUseLimit() > 0 ? CD.GetFloat() : 300f;
        }
        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (CurrentGameMode == CustomGameMode.MoveAndStop || !IsEnable || killer == null || target == null || killer.GetAbilityUseLimit() <= 0 || !killer.Is(CustomRoles.DonutDelivery)) return;

            killer.RpcRemoveAbilityUse();

            var num1 = IRandom.Instance.Next(0, 19);
            killer.Notify(GetString($"DonutDelivered-{num1}"));
            RandomNotifyTarget(target);

            killer.SetKillCooldown();
        }
        public static void RandomNotifyTarget(PlayerControl target)
        {
            var num2 = IRandom.Instance.Next(0, 15);
            target.Notify(GetString($"DonutGot-{num2}"));
        }
        public static string GetProgressText(byte id) => $"<color=#777777>-</color> <color=#ffffff>{id.GetAbilityUseLimit()}</color>";
    }
}
