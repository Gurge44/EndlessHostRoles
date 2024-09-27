using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Translator;

namespace EHR.Crewmate
{
    public class Escort : RoleBase
    {
        private const int Id = 642300;
        private static List<byte> PlayerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;
        private static OptionItem Duration;
        public static OptionItem UsePet;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Escort);
            CD = new FloatOptionItem(Id + 10, "RoleBlockCooldown", new(2.5f, 60f, 2.5f), 30f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escort])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = new IntegerOptionItem(Id + 11, "AbilityUseLimit", new(1, 20, 1), 3, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escort])
                .SetValueFormat(OptionFormat.Times);
            UsePet = Options.CreatePetUseSetting(Id + 12, CustomRoles.Escort);
            Duration = new FloatOptionItem(Id + 13, "RoleBlockDuration", new(1f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escort])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimit.GetInt());
        }

        public override void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = playerId.GetAbilityUseLimit() > 0 ? CD.GetFloat() : 300f;
        }

        public override bool CanUseKillButton(PlayerControl pc) => pc.GetAbilityUseLimit() >= 1;
        public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable || killer == null || target == null || killer.GetAbilityUseLimit() <= 0) return false;

            killer.RpcRemoveAbilityUse();
            killer.SetKillCooldown();
            target.BlockRole(Duration.GetFloat());
            killer.Notify(GetString("EscortTargetHacked"));

            return false;
        }
    }
}