using System.Collections.Generic;

namespace TOHE.Roles.Impostor
{
    internal class Swiftclaw
    {
        private static int Id => 643340;
        public static OptionItem DashCD;
        public static OptionItem DashDuration;
        public static OptionItem DashSpeed;
        private static readonly Dictionary<byte, (long StartTimeStamp, float NormalSpeed)> DashStart = [];
        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swiftclaw);
            DashCD = FloatOptionItem.Create(Id + 2, "SwiftclawDashCD", new(0f, 180f, 2.5f), 15f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
                .SetValueFormat(OptionFormat.Seconds);
            DashDuration = IntegerOptionItem.Create(Id + 3, "SwiftclawDashDur", new(0, 60, 1), 4, TabGroup.ImpostorRoles, false)
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

            DashStart[pc.PlayerId] = (Utils.TimeStamp, Main.AllPlayerSpeed[pc.PlayerId]);
            Main.AllPlayerSpeed[pc.PlayerId] = DashSpeed.GetFloat();
            pc.MarkDirtySettings();
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || pc == null || !DashStart.TryGetValue(pc.PlayerId, out var dashInfo) || dashInfo.StartTimeStamp + DashDuration.GetInt() > Utils.TimeStamp) return;

            Main.AllPlayerSpeed[pc.PlayerId] = dashInfo.NormalSpeed;
            pc.MarkDirtySettings();
            DashStart.Remove(pc.PlayerId);
        }
        public static void OnReportDeadBody()
        {
            foreach (var item in DashStart)
            {
                Main.AllPlayerSpeed[item.Key] = item.Value.NormalSpeed;
            }
            DashStart.Clear();
        }
    }
}
