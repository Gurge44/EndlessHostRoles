using AmongUs.GameOptions;

namespace TOHE.Roles.Impostor
{
    internal class Minimalism : RoleBase
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

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.SabotageButton?.ToggleVisible(false);
            hud.AbilityButton?.ToggleVisible(false);
            hud.ReportButton?.ToggleVisible(false);
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.MNKillCooldown.GetFloat();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(true);
        }
    }
}
