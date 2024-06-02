using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Roles.Impostor
{
    internal static class GeneratorStatic
    {
        private static int GetDefaultCost(this Action action) => action switch
        {
            Action.Kill => 5,
            Action.Shapeshift => 10,
            Action.Vent => 20,
            Action.Sabotage => 15,

            _ => 15
        };

        enum Action
        {
            Kill,
            Shapeshift,
            Vent,
            Sabotage
        }

        internal class Generator : RoleBase
        {
            public static bool On;

            private static OptionItem ChargesGainedPerSecond;
            private static OptionItem StartingCharges;
            private static OptionItem ChargesLostWhileShiftedPerSecond;
            private static OptionItem MaxChargesStored;
            private static readonly Dictionary<Action, OptionItem> ActionCostSettings = [];

            private static int Gain;
            private static int ChargesLostWhileShifted;
            private static Dictionary<Action, int> ActionCosts = [];

            private int Charges;
            private long LastUpdate;
            public override bool IsEnable => On;

            public static void SetupCustomOption()
            {
                const int id = 11380;
                Options.SetupRoleOptions(id, TabGroup.ImpostorRoles, CustomRoles.Generator);
                ChargesGainedPerSecond = new IntegerOptionItem(id + 2, "Generator.ChargesGainedEverySecond", new(1, 30, 1), 1, TabGroup.ImpostorRoles)
                    .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Generator]);
                StartingCharges = new IntegerOptionItem(id + 3, "Generator.StartingCharges", new(0, 100, 1), 0, TabGroup.ImpostorRoles)
                    .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Generator]);
                ChargesLostWhileShiftedPerSecond = new IntegerOptionItem(id + 4, "Generator.ChargesLostWhileShiftedPerSecond", new(1, 30, 1), 1, TabGroup.ImpostorRoles)
                    .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Generator]);
                MaxChargesStored = new IntegerOptionItem(id + 5, "Generator.MaxChargesStored", new(0, 200, 1), 100, TabGroup.ImpostorRoles)
                    .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Generator]);

                foreach (var action in Enum.GetValues<Action>())
                {
                    var option = new IntegerOptionItem(id + 6 + (int)action, $"Generator.{action}.Cost", new(0, 100, 1), action.GetDefaultCost(), TabGroup.ImpostorRoles)
                        .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Generator]);
                    ActionCostSettings.Add(action, option);
                }
            }

            public override void Add(byte playerId)
            {
                On = true;
                Charges = StartingCharges.GetInt();
                playerId.SetAbilityUseLimit(Charges);
                LastUpdate = Utils.TimeStamp + 8;
            }

            public override void Init()
            {
                On = false;
                Gain = ChargesGainedPerSecond.GetInt();
                ChargesLostWhileShifted = ChargesLostWhileShiftedPerSecond.GetInt();
                ActionCosts = ActionCostSettings.ToDictionary(x => x.Key, x => x.Value.GetInt());
            }

            public override void ApplyGameOptions(IGameOptions opt, byte playerId)
            {
                AURoleOptions.ShapeshifterCooldown = 1f;
                AURoleOptions.ShapeshifterDuration = 300f;
            }

            public override void OnFixedUpdate(PlayerControl pc)
            {
                if (!GameStates.IsInTask || pc == null || !pc.IsAlive()) return;

                long now = Utils.TimeStamp;
                if (now <= LastUpdate) return;
                LastUpdate = now;

                int beforeCharges = Charges;
                bool shifted = pc.IsShifted();

                if (shifted) Charges -= ChargesLostWhileShifted;
                else Charges += Gain;

                Charges = Math.Clamp(Charges, 0, MaxChargesStored.GetInt());

                if (Charges <= 0 && shifted)
                {
                    pc.Notify(Translator.GetString("Generator.Notify.RevertShapeshift"));
                    pc.RpcShapeshift(pc, !Options.DisableShapeshiftAnimations.GetBool());
                    pc.RpcResetAbilityCooldown();
                }

                if (beforeCharges != Charges) pc.SetAbilityUseLimit(Charges, log: false);
            }

            public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
            {
                int cost = ActionCosts[Action.Vent];

                if (Charges < cost)
                {
                    LateTask.New(() =>
                    {
                        physics.RpcBootFromVent(ventId);
                        physics.myPlayer.Notify(string.Format(Translator.GetString("Generator.Notify.NotEnoughCharges"), cost));
                    }, 0.5f, "Generator not enough charges to vent");
                    return;
                }

                Charges -= cost;
            }

            public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
            {
                if (!base.OnCheckMurder(killer, target)) return false;

                int cost = ActionCosts[Action.Kill];
                if (Charges < cost)
                {
                    killer.Notify(string.Format(Translator.GetString("Generator.Notify.NotEnoughCharges"), cost));
                    return false;
                }

                return true;
            }

            public override void OnMurder(PlayerControl killer, PlayerControl target)
            {
                Charges -= ActionCosts[Action.Kill];
            }

            public override bool OnSabotage(PlayerControl pc)
            {
                if (!pc.IsAlive()) return true;

                int cost = ActionCosts[Action.Sabotage];

                if (Charges < cost)
                {
                    pc.Notify(string.Format(Translator.GetString("Generator.Notify.NotEnoughCharges"), cost));
                    return false;
                }

                Charges -= cost;
                return true;
            }

            public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
            {
                if (!shapeshifting) return true;

                int cost = ActionCosts[Action.Shapeshift];

                if (Charges < cost)
                {
                    shapeshifter.Notify(string.Format(Translator.GetString("Generator.Notify.NotEnoughCharges"), cost));
                    return false;
                }

                Charges -= cost;
                return true;
            }
        }
    }
}