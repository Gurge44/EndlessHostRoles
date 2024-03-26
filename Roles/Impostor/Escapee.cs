using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR.Roles.Impostor
{
    internal class Escapee : RoleBase
    {
        public Vector2? EscapeeLocation;
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(3600, TabGroup.ImpostorRoles, CustomRoles.Escapee);
            Options.EscapeeSSCD = FloatOptionItem.Create(3611, "ShapeshiftCooldown", new(1f, 180f, 1f), 5f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escapee])
                .SetValueFormat(OptionFormat.Seconds);
        }

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