using AmongUs.GameOptions;
using TOHE.Modules;
using UnityEngine;

namespace TOHE.Roles.Impostor
{
    internal class Escapee : RoleBase
    {
        public Vector2? EscapeeLocation;
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            EscapeeLocation = null;
        }

        public override void Init()
        {
            On = false;
            EscapeeLocation = null;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool()) hud.PetButton?.OverrideText(Translator.GetString("EscapeeAbilityButtonText"));
            else hud.AbilityButton?.OverrideText(Translator.GetString("EscapeeAbilityButtonText"));
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            if (Options.UsePets.GetBool()) return;
            try
            {
                AURoleOptions.ShapeshifterCooldown = Options.EscapeeSSCD.GetFloat();
                AURoleOptions.ShapeshifterDuration = Options.EscapeeSSDuration.GetFloat();
            }
            catch
            {
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            TeleportOrMark(pc);
        }

        private void TeleportOrMark(PlayerControl pc)
        {
            if (EscapeeLocation != null)
            {
                var position = (Vector2)EscapeeLocation;
                EscapeeLocation = null;
                pc.TP(position);
                pc.RPCPlayCustomSound("Teleport");
            }
            else
            {
                EscapeeLocation = pc.Pos();
            }
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (shapeshifting)
            {
                TeleportOrMark(shapeshifter);
            }

            return false;
        }
    }
}