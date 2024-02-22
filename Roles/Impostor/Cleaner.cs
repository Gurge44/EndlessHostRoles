namespace TOHE.Roles.Impostor
{
    internal class Cleaner : RoleBase
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

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.CleanerKillCooldown.GetFloat();
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
            cleaner.SetKillCooldown(Options.KillCooldownAfterCleaning.GetFloat());
            Logger.Info($"{cleaner.GetRealName()} cleans up the corpse of {target.Object.GetRealName()}", "Cleaner/Medusa");

            return false;
        }
    }
}
