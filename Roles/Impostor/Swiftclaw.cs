using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Impostor
{
    internal class Swiftclaw
    {
        private static int Id => 643340;
        public static OptionItem DashCD;
        private static OptionItem DashDuration;
        public static OptionItem DashSpeed;
        private static readonly Dictionary<byte, long> DashStart;
        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swiftclaw);
            DashCD = FloatOptionItem.Create(Id + 2, "SwiftclawDashCD", new(0f, 180f, 2.5f), 15f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
                .SetValueFormat(OptionFormat.Seconds);
            DashDuration = FloatOptionItem.Create(Id + 3, "SwiftclawDashDur", new(0f, 60f, 0.5f), 4f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
                .SetValueFormat(OptionFormat.Seconds);
            DashSpeed = FloatOptionItem.Create(Id + 4, "SwiftclawDashSpeed", new(0.05f, 3f, 0.05f), 2f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
                .SetValueFormat(OptionFormat.Multiplier);
        }
        public static void Init() => DashStart.Clear();
        public static void OnPet(PlayerControl pc)
        {
            if (pc == null || DashStart.ContainsKey(pc.PlayerId)) return;
            DashStart[pc.PlayerId] = Utils.GetTimeStamp();
        }
        public static void OnFixedUpdate()
        {

        }
    }
}
