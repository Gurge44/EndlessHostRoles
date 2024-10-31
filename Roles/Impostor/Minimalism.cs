using AmongUs.GameOptions;

namespace EHR.Impostor
{
    internal class Minimalism : RoleBase
    {
        public static bool On;

        private static OptionItem MnKillCooldown;
        private static OptionItem BypassShields;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(16300, TabGroup.ImpostorRoles, CustomRoles.Minimalism);

            MnKillCooldown = new FloatOptionItem(16310, "KillCooldown", new(2.5f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Minimalism])
                .SetValueFormat(OptionFormat.Seconds);

            BypassShields = new BooleanOptionItem(16311, "BypassShields", true, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Minimalism]);
        }

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
            Main.AllPlayerKillCooldown[id] = MnKillCooldown.GetFloat();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(true);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            if (BypassShields.GetBool())
            {
                killer.Kill(target);
                return false;
            }

            return true;
        }
    }
}