using AmongUs.GameOptions;

namespace TOHE.Roles.Impostor
{
    internal class ImperiusCurse : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

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
            AURoleOptions.ShapeshifterCooldown = Options.ImperiusCurseShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterLeaveSkin = false;
            AURoleOptions.ShapeshifterDuration = Options.ShapeImperiusCurseShapeshiftDuration.GetFloat();
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
