using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Roles.Impostor
{
    internal class Visionary : RoleBase
    {
        public static bool On;

        private static OptionItem UseLimit;
        private static OptionItem VisionaryAbilityUseGainWithEachKill;
        private static OptionItem ShapeshiftCooldown;

        public List<byte> RevealedPlayerIds = [];
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(16150, TabGroup.ImpostorRoles, CustomRoles.Visionary);
            UseLimit = new IntegerOptionItem(16152, "AbilityUseLimit", new(0, 5, 1), 0, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Visionary])
                .SetValueFormat(OptionFormat.Times);
            VisionaryAbilityUseGainWithEachKill = new FloatOptionItem(16153, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 1f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Visionary])
                .SetValueFormat(OptionFormat.Times);
            ShapeshiftCooldown = new FloatOptionItem(16154, "ShapeshiftCooldown", new(1f, 60f, 1f), 15f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Visionary])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
            RevealedPlayerIds = [];
            playerId.SetAbilityUseLimit(UseLimit.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            killer.RpcIncreaseAbilityUseLimitBy(VisionaryAbilityUseGainWithEachKill.GetFloat());
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting) return true;
            if (RevealedPlayerIds.Contains(target.PlayerId) || shapeshifter.GetAbilityUseLimit() < 1) return false;

            RevealedPlayerIds.Add(target.PlayerId);
            shapeshifter.RpcRemoveAbilityUse();
            Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: target);

            return false;
        }
    }
}