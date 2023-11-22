using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TOHE.Options;
using static TOHE.Utils;
using static TOHE.Translator;
using TOHE.Roles.Neutral;
using UnityEngine.Bindings;

namespace TOHE.Roles.Impostor
{
    public static class Duellist
    {
        private static readonly int Id = 642850;

        private static OptionItem SSCD;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Duellist);
            SSCD = FloatOptionItem.Create(Id + 10, "ShapeshiftCooldown", new(0f, 60f, 2.5f), 15f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Duellist])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void OnShapeshift(PlayerControl duellist, PlayerControl target)
        {
            if (duellist == null || target == null) return;
            if (target.inMovingPlat || target.onLadder || target.MyPhysics.Animations.IsPlayingEnterVentAnimation() || target.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || !target.IsAlive())
            {
                duellist.Notify(GetString("TargetCannotBeTeleported"));
                return;
            }

            var pos = Pelican.GetBlackRoomPS();
            duellist.TP(pos);
            target.TP(pos);
            
        }
    }
}
