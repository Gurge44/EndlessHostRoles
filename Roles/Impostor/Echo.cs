namespace EHR.Roles.Impostor
{
    public class Echo : RoleBase
    {
        public static bool On;

        private static OptionItem ShapeshiftCooldown;
        private static OptionItem ShapeshiftDuration;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            int id = 647600;
            const TabGroup tab = TabGroup.ImpostorRoles;
            const CustomRoles role = CustomRoles.Echo;
            Options.SetupRoleOptions(id++, tab, role);
            var parent = Options.CustomRoleSpawnChances[role];
            ShapeshiftCooldown = new FloatOptionItem(++id, "ShapeshiftCooldown", new(0f, 180f, 0.5f), 30f, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDuration = new FloatOptionItem(++id, "ShapeshiftDuration", new(0f, 180f, 0.5f), 10f, tab)
                .SetParent(parent)
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

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            bool shifted = id.IsPlayerShifted();
            string text = !shifted ? "EchoShapeshiftButtonText" : "KillButtonText";
            hud.AbilityButton.OverrideText(Translator.GetString(text));
        }

        public override bool CanUseKillButton(PlayerControl pc) => false;

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (shapeshifting)
            {
                var pos = target.Pos();
                if (!shapeshifter.RpcCheckAndMurder(target, check: true) || !target.TP(shapeshifter)) return false;
                target.RpcShapeshift(shapeshifter, false);
                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                target.MarkDirtySettings();
                shapeshifter.TP(pos);
            }
            else
            {
                target = Utils.GetPlayerById(shapeshifter.shapeshiftTargetPlayerId);
                if (target == null) return true;
                target.RpcShapeshift(target, false);
                var pos = shapeshifter.Pos();
                shapeshifter.TP(target);
                target.TP(pos);
                LateTask.New(() => target.Suicide(PlayerState.DeathReason.Kill, shapeshifter), 0.2f, "Echo Unshift Kill");
            }

            return true;
        }
    }
}