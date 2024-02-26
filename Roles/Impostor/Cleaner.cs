using AmongUs.GameOptions;
using TOHE.Roles.Neutral;

namespace TOHE.Roles.Impostor
{
    internal class Cleaner : RoleBase
    {
        private bool HasImpostorVision;
        private bool CanVent;
        private float KillCooldown;
        private float KCDAfterClean;

        private bool IsMedusa;

        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;

            IsMedusa = Main.PlayerStates[playerId].MainRole == CustomRoles.Medusa;

            if (IsMedusa)
            {
                HasImpostorVision = Medusa.HasImpostorVision.GetBool();
                CanVent = Medusa.CanVent.GetBool();
                KillCooldown = Medusa.KillCooldown.GetFloat();
                KCDAfterClean = Medusa.KillCooldownAfterStoneGazing.GetFloat();
            }
            else
            {
                HasImpostorVision = true;
                CanVent = true;
                KillCooldown = Options.CleanerKillCooldown.GetFloat();
                KCDAfterClean = Options.KillCooldownAfterCleaning.GetFloat();
            }
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(HasImpostorVision);
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.ReportButton?.OverrideText(Translator.GetString("CleanerReportButtonText"));
        }

        public override bool CheckReportDeadBody(PlayerControl cleaner, GameData.PlayerInfo target, PlayerControl killer)
        {
            if (Main.KillTimers[cleaner.PlayerId] > 0f) return true;

            Main.CleanerBodies.Remove(target.PlayerId);
            Main.CleanerBodies.Add(target.PlayerId);

            cleaner.Notify(Translator.GetString("CleanerCleanBody"));
            cleaner.SetKillCooldown(KCDAfterClean);

            Logger.Info($"{cleaner.GetRealName()} cleans up the corpse of {target.Object.GetRealName()}", "Cleaner/Medusa");

            return false;
        }
    }
}
