using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Impostor
{
    public class Abyssbringer : RoleBase
    {
        public static bool On;

        private static OptionItem BlackHolePlaceCooldown;
        private static OptionItem BlackHoleDespawnMode;
        private static OptionItem BlackHoleDespawnTime;
        private static OptionItem BlackHoleMovesTowardsNearestPlayer;
        private static OptionItem BlackHoleMoveSpeed;

        private List<(BlackHole NetObject, long PlaceTimeStamp, Vector2 Position)> BlackHoles = [];
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            int id = 649800;
            const TabGroup tab = TabGroup.ImpostorRoles;
            const CustomRoles role = CustomRoles.Abyssbringer;
            Options.SetupRoleOptions(id++, tab, role);
            BlackHolePlaceCooldown = new IntegerOptionItem(++id, "BlackHolePlaceCooldown", new(1, 180, 1), 30, tab)
                .SetParent(Options.CustomRoleSpawnChances[role])
                .SetValueFormat(OptionFormat.Seconds);
            BlackHoleDespawnMode = new StringOptionItem(++id, "BlackHoleDespawnMode", Enum.GetNames<DespawnMode>(), 0, tab)
                .SetParent(Options.CustomRoleSpawnChances[role]);
            BlackHoleDespawnTime = new IntegerOptionItem(++id, "BlackHoleDespawnTime", new(1, 60, 1), 15, tab)
                .SetParent(BlackHoleDespawnMode)
                .SetValueFormat(OptionFormat.Seconds);
            BlackHoleMovesTowardsNearestPlayer = new BooleanOptionItem(++id, "BlackHoleMovesTowardsNearestPlayer", true, tab)
                .SetParent(Options.CustomRoleSpawnChances[role]);
            BlackHoleMoveSpeed = new FloatOptionItem(++id, "BlackHoleMoveSpeed", new(0.25f, 10f, 0.25f), 1f, tab)
                .SetParent(BlackHoleMovesTowardsNearestPlayer);
        }

        public override void Add(byte playerId)
        {
            On = true;
            BlackHoles = [];
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = BlackHolePlaceCooldown.GetInt();
            else
            {
                AURoleOptions.ShapeshifterCooldown = BlackHolePlaceCooldown.GetInt();
                AURoleOptions.ShapeshifterDuration = 1f;
            }
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !Options.UseUnshiftTrigger.GetBool()) return true;
            CreateBlackHole(shapeshifter);
            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            CreateBlackHole(pc);
        }

        public override bool OnVanish(PlayerControl pc)
        {
            CreateBlackHole(pc);
            return false;
        }

        private void CreateBlackHole(PlayerControl shapeshifter)
        {
            var pos = shapeshifter.Pos();
            BlackHoles.Add((new(pos), Utils.TimeStamp, pos));
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            int count = BlackHoles.Count;
            for (int i = 0; i < count; i++)
            {
                var blackHole = BlackHoles[i];

                var despawnMode = (DespawnMode)BlackHoleDespawnMode.GetValue();
                switch (despawnMode)
                {
                    case DespawnMode.AfterTime when Utils.TimeStamp - blackHole.PlaceTimeStamp > BlackHoleDespawnTime.GetInt():
                        RemoveBlackHole();
                        continue;
                    case DespawnMode.AfterMeeting when GameStates.IsMeeting:
                        RemoveBlackHole();
                        continue;
                }

                var nearestPlayer = Main.AllAlivePlayerControls.Without(pc).MinBy(x => Vector2.Distance(x.Pos(), blackHole.Position));
                if (nearestPlayer != null)
                {
                    var pos = nearestPlayer.Pos();

                    if (BlackHoleMovesTowardsNearestPlayer.GetBool() && GameStates.IsInTask && !ExileController.Instance)
                    {
                        var direction = (pos - blackHole.Position).normalized;
                        var newPosition = blackHole.Position + direction * BlackHoleMoveSpeed.GetFloat() * Time.fixedDeltaTime;
                        blackHole.NetObject.TP(newPosition);
                        blackHole.Position = newPosition;
                    }

                    if (Vector2.Distance(pos, blackHole.Position) < 0.5f)
                    {
                        nearestPlayer.RpcExileV2();

                        var state = Main.PlayerStates[nearestPlayer.PlayerId];
                        state.deathReason = PlayerState.DeathReason.Consumed;
                        state.SetDead();

                        if (despawnMode == DespawnMode.After1PlayerEaten)
                        {
                            RemoveBlackHole();
                        }
                    }
                }

                continue;

                void RemoveBlackHole()
                {
                    BlackHoles.RemoveAt(i);
                    blackHole.NetObject.Despawn();
                }
            }
        }

        enum DespawnMode
        {
            None,
            AfterTime,
            After1PlayerEaten,
            AfterMeeting
        }
    }
}