namespace TOHE.Roles.Crewmate
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;

    public static class Aid
    {
        private static readonly int Id = 640200;
        private static List<byte> playerIdList = [];
        public static Dictionary<byte, long> ShieldedPlayers = [];

        public static OptionItem AidDur;
        public static OptionItem AidCD;
        public static OptionItem UseLimitOpt;
        public static OptionItem UsePet;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Aid);
            AidCD = FloatOptionItem.Create(Id + 10, "AidCD", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Seconds);
            AidDur = FloatOptionItem.Create(Id + 11, "AidDur", new(0f, 60f, 1f), 10f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Times);
            UsePet = Options.CreatePetUseSetting(Id + 13, CustomRoles.Aid);
        }
        public static void Init()
        {
            playerIdList = [];
            ShieldedPlayers = [];
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());

            if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static void SetKillCooldown(byte playerId) => Main.AllPlayerKillCooldown[playerId] = AidCD.GetInt();
        public static bool IsEnable => playerIdList.Count > 0;
        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;
            if (!killer.Is(CustomRoles.Aid)) return true;

            if (killer.GetAbilityUseLimit() >= 1)
            {
                killer.RpcRemoveAbilityUse();
                ShieldedPlayers.TryAdd(target.PlayerId, Utils.TimeStamp);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                return false;
            }
            else
            {
                return false;
            }
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null || !pc.Is(CustomRoles.Aid) || ShieldedPlayers.Count == 0) return;

            bool change = false;

            foreach (var x in ShieldedPlayers)
            {
                if (x.Value + AidDur.GetInt() < Utils.TimeStamp || !GameStates.IsInTask)
                {
                    ShieldedPlayers.Remove(x.Key);
                    change = true;
                }
            }

            if (change && GameStates.IsInTask) { Utils.NotifyRoles(SpecifySeer: pc); }
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            if (Utils.GetPlayerById(playerId) == null) return string.Empty;

            var sb = new StringBuilder();

            sb.Append(Utils.GetTaskCount(playerId, comms));
            sb.Append(Utils.GetAbilityUseLimitDisplay(playerId));

            return sb.ToString();
        }
    }
}