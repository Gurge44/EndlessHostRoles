using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    internal class Kidnapper
    {
        private static int Id => 643300;
        public static OptionItem SSCD;
        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Kidnapper);
            SSCD = FloatOptionItem.Create(Id + 2, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Kidnapper])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void OnShapeshift(PlayerControl kidnapper, PlayerControl target)
        {
            if (kidnapper == null || target == null) return;
            target.TP(kidnapper);
        }
    }
}
