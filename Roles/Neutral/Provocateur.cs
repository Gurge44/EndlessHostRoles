using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Roles.Neutral
{
    internal class Provocateur : RoleBase
    {
        public static Dictionary<byte, byte> Provoked = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption() => Options.SetupRoleOptions(18500, TabGroup.NeutralRoles, CustomRoles.Provocateur);

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive() && !Provoked.ContainsKey(pc.PlayerId);
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return false;
        }

        public override bool CanUseSabotage(PlayerControl pc)
        {
            return false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("ProvocateurButtonText"));
            hud.SabotageButton?.ToggleVisible(false);
            hud.AbilityButton?.ToggleVisible(false);
            hud.ImpostorVentButton?.ToggleVisible(false);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
            killer.Kill(target);
            Provoked.TryAdd(killer.PlayerId, target.PlayerId);
            return false;
        }
    }
}