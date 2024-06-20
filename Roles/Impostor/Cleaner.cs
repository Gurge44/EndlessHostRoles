using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Roles.Neutral;
using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class Cleaner : RoleBase
    {
        public static List<byte> CleanerBodies = [];

        public static bool On;
        private bool CanVent;

        private bool HasImpostorVision;

        private bool IsMedusa;
        private float KCDAfterClean;
        private float KillCooldown;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(2600, TabGroup.ImpostorRoles, CustomRoles.Cleaner);
            CleanerKillCooldown = new FloatOptionItem(2610, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cleaner])
                .SetValueFormat(OptionFormat.Seconds);
            KillCooldownAfterCleaning = new FloatOptionItem(2611, "KillCooldownAfterCleaning", new(0f, 180f, 2.5f), 60f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Cleaner])
                .SetValueFormat(OptionFormat.Seconds);
        }

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
                KillCooldown = CleanerKillCooldown.GetFloat();
                KCDAfterClean = KillCooldownAfterCleaning.GetFloat();
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
            if (IsMedusa)
            {
                hud.KillButton?.OverrideText(Translator.GetString("KillButtonText"));
                hud.ReportButton?.OverrideText(Translator.GetString("MedusaReportButtonText"));
            }
            else hud.ReportButton?.OverrideText(Translator.GetString("CleanerReportButtonText"));
        }

        public override bool CheckReportDeadBody(PlayerControl cleaner, NetworkedPlayerInfo target, PlayerControl killer)
        {
            if (Main.KillTimers[cleaner.PlayerId] > 0f) return true;

            CleanerBodies.Remove(target.PlayerId);
            CleanerBodies.Add(target.PlayerId);

            cleaner.Notify(Translator.GetString("CleanerCleanBody"));
            cleaner.SetKillCooldown(KCDAfterClean);

            Logger.Info($"{cleaner.GetRealName()} cleans up the corpse of {target.Object.GetRealName()}", "Cleaner/Medusa");

            return false;
        }
    }
}