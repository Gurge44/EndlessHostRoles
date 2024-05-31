using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Roles.Crewmate
{
    public class Aid : RoleBase
    {
        private const int Id = 640200;
        private static List<byte> playerIdList = [];
        public static Dictionary<byte, long> ShieldedPlayers = [];

        public static OptionItem AidDur;
        public static OptionItem AidCD;
        public static OptionItem UseLimitOpt;
        public static OptionItem UsePet;
        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Aid);
            AidCD = new FloatOptionItem(Id + 10, "AidCD", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Seconds);
            AidDur = new FloatOptionItem(Id + 11, "AidDur", new(0f, 60f, 1f), 10f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = new IntegerOptionItem(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Times);
            UsePet = Options.CreatePetUseSetting(Id + 13, CustomRoles.Aid);
        }

        public override void Init()
        {
            playerIdList = [];
            ShieldedPlayers = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());

            if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte playerId) => Main.AllPlayerKillCooldown[playerId] = AidCD.GetInt();
        public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);
        public override bool CanUseKillButton(PlayerControl pc) => pc.GetAbilityUseLimit() >= 1;

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            if (killer.GetAbilityUseLimit() >= 1)
            {
                killer.RpcRemoveAbilityUse();
                ShieldedPlayers.TryAdd(target.PlayerId, Utils.TimeStamp);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                return false;
            }

            return false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
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

            if (change && GameStates.IsInTask)
            {
                Utils.NotifyRoles(SpecifySeer: pc);
            }
        }
    }
}