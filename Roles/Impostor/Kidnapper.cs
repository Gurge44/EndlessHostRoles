using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Impostor
{
    internal class Kidnapper : RoleBase
    {
        public static bool On;

        public static OptionItem SSCD;
        private static int Id => 643300;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Kidnapper);
            SSCD = new FloatOptionItem(Id + 2, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Kidnapper])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ShapeshifterCooldown = SSCD.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public override bool OnShapeshift(PlayerControl kidnapper, PlayerControl target, bool shapeshifting)
        {
            if (kidnapper == null || target == null || !shapeshifting)
            {
                return true;
            }

            if (!target.TP(kidnapper))
            {
                kidnapper.Notify(Utils.ColorString(Color.yellow, Translator.GetString("TargetCannotBeTeleported")));
            }

            return false;
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
        }
    }
}