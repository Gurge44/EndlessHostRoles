using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate
{
    public class DonutDelivery : RoleBase
    {
        private const int Id = 642700;
        private static List<byte> playerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;
        public static OptionItem UsePet;

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.DonutDelivery);
            CD = new FloatOptionItem(Id + 10, "DonutDeliverCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = new IntegerOptionItem(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
                .SetValueFormat(OptionFormat.Times);
            UsePet = CreatePetUseSetting(Id + 13, CustomRoles.DonutDelivery);
        }

        public override void Init()
        {
            playerIdList = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
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

            var num1 = IRandom.Instance.Next(0, 19);
            killer.Notify(GetString($"DonutDelivered-{num1}"));
            RandomNotifyTarget(target);

            killer.SetKillCooldown();

            return false;
        }

        public static void RandomNotifyTarget(PlayerControl target)
        {
            var num2 = IRandom.Instance.Next(0, 6);
            target.Notify(GetString($"DonutGot-{num2}"));
        }
    }
}