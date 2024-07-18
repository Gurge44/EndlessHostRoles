using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Impostor
{
    internal class Miner : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(3800, TabGroup.ImpostorRoles, CustomRoles.Miner);
            Options.MinerSSCD = new FloatOptionItem(3811, "ShapeshiftCooldown", new(1f, 180f, 1f), 15f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Miner])
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
            if (Options.UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = Options.MinerSSCD.GetFloat();
            else
            {
                if (Options.UsePets.GetBool()) return;
                AURoleOptions.ShapeshifterCooldown = Options.MinerSSCD.GetFloat();
                AURoleOptions.ShapeshifterDuration = 1f;
            }
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool()) hud.PetButton?.OverrideText(Translator.GetString("MinerTeleButtonText"));
            else hud.AbilityButton?.OverrideText(Translator.GetString("MinerTeleButtonText"));
        }

        public override void OnPet(PlayerControl pc)
        {
            TeleportToVent(pc);
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !Options.UseUnshiftTrigger.GetBool()) return true;
            TeleportToVent(shapeshifter);

            return false;
        }

        public override bool OnVanish(PlayerControl pc)
        {
            TeleportToVent(pc);
            return false;
        }

        private static void TeleportToVent(PlayerControl pc)
        {
            if (Main.LastEnteredVent.ContainsKey(pc.PlayerId))
            {
                var position = Main.LastEnteredVentLocation[pc.PlayerId];
                pc.TP(new Vector2(position.x, position.y));
            }
        }
    }
}