using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Impostor
{
    internal class Swiftclaw : RoleBase
    {
        public static OptionItem DashCD;
        public static OptionItem DashDuration;
        public static OptionItem DashSpeed;
        private static readonly Dictionary<byte, (long StartTimeStamp, float NormalSpeed)> DashStart = [];

        public static bool On;
        private static int Id => 643340;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swiftclaw);
            DashCD = new FloatOptionItem(Id + 2, "SwiftclawDashCD", new(0f, 180f, 2.5f), 15f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
                .SetValueFormat(OptionFormat.Seconds);
            DashDuration = new IntegerOptionItem(Id + 3, "SwiftclawDashDur", new(0, 60, 1), 4, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
                .SetValueFormat(OptionFormat.Seconds);
            DashSpeed = new FloatOptionItem(Id + 4, "SwiftclawDashSpeed", new(0.05f, 3f, 0.05f), 2f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Init()
        {
            DashStart.Clear();
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePhantomBasis.GetBool())
                AURoleOptions.PhantomCooldown = DashCD.GetFloat();
            if (Options.UseUnshiftTrigger.GetBool())
                AURoleOptions.ShapeshifterCooldown = DashCD.GetFloat();
        }

        public override void OnPet(PlayerControl pc)
        {
            Dash(pc);
        }

        public override bool OnVanish(PlayerControl pc)
        {
            Dash(pc);
            return false;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !Options.UseUnshiftTrigger.GetBool()) return true;
            Dash(shapeshifter);
            return false;
        }

        private static void Dash(PlayerControl pc)
        {
            if (pc == null || DashStart.ContainsKey(pc.PlayerId)) return;

            DashStart[pc.PlayerId] = (Utils.TimeStamp, Main.AllPlayerSpeed[pc.PlayerId]);
            Main.AllPlayerSpeed[pc.PlayerId] = DashSpeed.GetFloat();
            pc.MarkDirtySettings();
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || pc == null || !DashStart.TryGetValue(pc.PlayerId, out var dashInfo) || dashInfo.StartTimeStamp + DashDuration.GetInt() > Utils.TimeStamp) return;

            Main.AllPlayerSpeed[pc.PlayerId] = dashInfo.NormalSpeed;
            pc.MarkDirtySettings();
            DashStart.Remove(pc.PlayerId);
        }

        public override void OnReportDeadBody()
        {
            foreach (var item in DashStart)
            {
                Main.AllPlayerSpeed[item.Key] = item.Value.NormalSpeed;
            }

            DashStart.Clear();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.PetButton?.OverrideText(Translator.GetString("SwiftclawKillButtonText"));
        }
    }
}