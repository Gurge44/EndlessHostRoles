using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Neutral
{
    public class Pyromaniac : RoleBase
    {
        private const int Id = 648000;
        public static List<byte> PlayerIdList = [];

        private static OptionItem KillCooldown;
        private static OptionItem DouseCooldown;
        private static OptionItem BurnCooldown;
        public static OptionItem CanVent;
        private static OptionItem HasImpostorVision;

        public List<byte> DousedList = [];

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pyromaniac);

            KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac])
                .SetValueFormat(OptionFormat.Seconds);

            DouseCooldown = new FloatOptionItem(Id + 11, "PyroDouseCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac])
                .SetValueFormat(OptionFormat.Seconds);

            BurnCooldown = new FloatOptionItem(Id + 12, "PyroBurnCooldown", new(0f, 180f, 2.5f), 5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac])
                .SetValueFormat(OptionFormat.Seconds);

            CanVent = new BooleanOptionItem(Id + 13, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac]);
            HasImpostorVision = new BooleanOptionItem(Id + 14, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac]);
        }

        public override void Init()
        {
            PlayerIdList = [];
            DousedList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            DousedList = [];
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(HasImpostorVision.GetBool());
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return true;

            if (target == null) return true;

            if (DousedList.Contains(target.PlayerId))
            {
                LateTask.New(() => { killer.SetKillCooldown(BurnCooldown.GetFloat()); }, 0.1f, log: false);
                return true;
            }

            return killer.CheckDoubleTrigger(target, () =>
            {
                DousedList.Add(target.PlayerId);
                killer.SetKillCooldown(DouseCooldown.GetFloat());
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            });
        }
    }
}