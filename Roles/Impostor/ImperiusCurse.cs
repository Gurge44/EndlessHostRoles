using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class ImperiusCurse : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(3700, TabGroup.ImpostorRoles, CustomRoles.ImperiusCurse);
            ShapeImperiusCurseShapeshiftDuration = FloatOptionItem.Create(3710, "ShapeshiftDuration", new(2.5f, 300f, 2.5f), 20f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ImperiusCurse])
                .SetValueFormat(OptionFormat.Seconds);
            ImperiusCurseShapeshiftCooldown = FloatOptionItem.Create(3711, "ShapeshiftCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ImperiusCurse])
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
            AURoleOptions.ShapeshifterCooldown = ImperiusCurseShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterLeaveSkin = false;
            AURoleOptions.ShapeshifterDuration = ShapeImperiusCurseShapeshiftDuration.GetFloat();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton?.OverrideText(Translator.GetString("ImperiusCurseButtonText"));
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (shapeshifting)
            {
                _ = new LateTask(() =>
                {
                    if (!(!GameStates.IsInTask || !shapeshifter.IsAlive() || !target.IsAlive() || shapeshifter.inVent || target.inVent))
                    {
                        var originPs = target.Pos();
                        target.TP(shapeshifter.Pos());
                        shapeshifter.TP(originPs);
                    }
                }, 1.5f, "ImperiusCurse TP");
            }

            return true;
        }
    }
}