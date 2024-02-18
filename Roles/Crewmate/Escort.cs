using System.Collections.Generic;
using TOHE.Roles.Neutral;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    public static class Escort
    {
        private static readonly int Id = 642300;
        private static List<byte> playerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;
        public static OptionItem UsePet;

        public static void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Escort, 1);
            CD = FloatOptionItem.Create(Id + 10, "EscortCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escort])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escort])
                .SetValueFormat(OptionFormat.Times);
            UsePet = Options.CreatePetUseSetting(Id + 12, CustomRoles.Escort);
        }

        public static void Init()
        {
            playerIdList = [];
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);

            playerId.SetAbilityUseLimit(UseLimit.GetInt());

            if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public static bool IsEnable => playerIdList.Count > 0;

        public static void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = playerId.GetAbilityUseLimit() > 0 ? CD.GetFloat() : 300f;
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable || killer == null || target == null || killer.GetAbilityUseLimit() <= 0 || !killer.Is(CustomRoles.Escort)) return;

            killer.RpcRemoveAbilityUse();
            killer.SetKillCooldown();
            Glitch.hackedIdList.TryAdd(target.PlayerId, Utils.TimeStamp);
            killer.Notify(GetString("EscortTargetHacked"));
        }

        public static string GetProgressText(byte id) => $"<color=#777777>-</color> <color=#ffffff>{id.GetAbilityUseLimit()}</color>";
    }
}