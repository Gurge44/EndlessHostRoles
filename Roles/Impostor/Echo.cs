using System.Collections.Generic;

namespace EHR.Impostor
{
    public class Echo : RoleBase
    {
        public static bool On;
        public static List<Echo> Instances = [];

        private static OptionItem ShapeshiftCooldown;
        private static OptionItem ShapeshiftDuration;

        public PlayerControl EchoPC;
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

        public override void Init()
        {
            Instances = [];
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            EchoPC = Utils.GetPlayerById(playerId);
            Instances.Add(this);
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
                RevertSwap(shapeshifter, target);
                LateTask.New(() => target.Suicide(PlayerState.DeathReason.Kill, shapeshifter), 0.2f, "Echo Unshift Kill");
            }

            return true;
        }

        private static void RevertSwap(PlayerControl echo, PlayerControl target)
        {
            target.RpcShapeshift(target, false);
            var pos = echo.Pos();
            echo.TP(target);
            target.TP(pos);
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (!target.IsShifted()) return true;
            var ssTarget = Utils.GetPlayerById(target.shapeshiftTargetPlayerId);
            if (ssTarget == null || !killer.RpcCheckAndMurder(ssTarget, check: true)) return true;

            RevertSwap(target, ssTarget);
            LateTask.New(() => killer.Kill(ssTarget), 0.2f, log: false);
            return false;
        }

        public void OnTargetCheckMurder(PlayerControl killer, PlayerControl target)
        {
            RevertSwap(EchoPC, target);
            LateTask.New(() =>
            {
                killer.RpcCheckAndMurder(EchoPC);
                target.Suicide(PlayerState.DeathReason.Kill, EchoPC);
            }, 0.2f, log: false);
        }
    }
}