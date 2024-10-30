using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Impostor
{
    public class Morphling : RoleBase
    {
        private const int Id = 3000;
        public static List<byte> PlayerIdList = [];

        private static OptionItem KillCooldown;
        private static OptionItem ShapeshiftCD;
        private static OptionItem ShapeshiftDur;
        public override bool IsEnable => PlayerIdList.Count > 0;


        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Morphling);
            KillCooldown = new FloatOptionItem(Id + 14, "KillCooldown", new(0f, 60f, 2.5f), 10f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCD = new FloatOptionItem(Id + 15, "ShapeshiftCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDur = new FloatOptionItem(Id + 16, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return !Main.PlayerStates[pc.PlayerId].IsDead && pc.IsShifted();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCD.GetFloat();
            AURoleOptions.ShapeshifterDuration = ShapeshiftDur.GetFloat();
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }
    }
}