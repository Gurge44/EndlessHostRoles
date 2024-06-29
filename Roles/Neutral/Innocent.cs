using AmongUs.GameOptions;

namespace EHR.Neutral
{
    internal class Innocent : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(10800, TabGroup.NeutralRoles, CustomRoles.Innocent);
            Options.InnocentCanWinByImp = new BooleanOptionItem(10810, "InnocentCanWinByImp", false, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Innocent]);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return false;
        }

        public override bool CanUseSabotage(PlayerControl pc)
        {
            return false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("InnocentButtonText"));
            hud.SabotageButton?.ToggleVisible(false);
            hud.AbilityButton?.ToggleVisible(false);
            hud.ImpostorVentButton?.ToggleVisible(false);
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            target.Kill(killer);
            return false;
        }
    }
}