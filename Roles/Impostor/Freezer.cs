using AmongUs.GameOptions;

namespace EHR.Roles.Impostor
{
    internal class Freezer : RoleBase
    {
        private const int Id = 643530;
        public static bool On;

        private static OptionItem FreezeCooldown;
        private static OptionItem FreezeDuration;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Freezer);
            FreezeCooldown = FloatOptionItem.Create(Id + 2, "FreezeCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Freezer])
                .SetValueFormat(OptionFormat.Seconds);
            FreezeDuration = FloatOptionItem.Create(Id + 3, "FreezeDuration", new(0f, 180f, 0.5f), 10f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Freezer])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ShapeshifterCooldown = FreezeCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (shapeshifting)
            {
                var beforeSpeed = Main.AllPlayerSpeed[target.PlayerId];
                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                target.MarkDirtySettings();
                LateTask.New(() =>
                {
                    Main.AllPlayerSpeed[target.PlayerId] = beforeSpeed;
                    target.MarkDirtySettings();
                }, FreezeDuration.GetFloat(), "FreezerFreezeDuration");
            }

            return false;
        }
    }
}