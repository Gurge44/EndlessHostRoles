using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Neutral
{
    public class Pickpocket : RoleBase
    {
        private const int Id = 12600;
        public static List<byte> PlayerIdList = [];

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        public static OptionItem VotesPerKill;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pickpocket);

            KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Pickpocket])
                .SetValueFormat(OptionFormat.Seconds);

            CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Pickpocket]);

            HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Pickpocket]);

            VotesPerKill = new FloatOptionItem(Id + 12, "VotesPerKill", new(0.1f, 10f, 0.1f), 0.3f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Pickpocket]);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
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
    }
}