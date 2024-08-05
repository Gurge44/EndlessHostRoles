using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR.Impostor
{
    public class Abyssbringer : RoleBase
    {
        public static bool On;

        public static OptionItem BlackHolePlaceCooldown;
        private static OptionItem BlackHoleDespawnMode;
        private static OptionItem BlackHoleDespawnTime;
        private static OptionItem BlackHoleMovesTowardsNearestPlayer;
        private static OptionItem BlackHoleMoveSpeed;
        private byte AbyssbringerId;

        private List<BlackHoleData> BlackHoles = [];
        public override bool IsEnable => On;

        public override void SetupCustomOption()
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
            AbyssbringerId = playerId;
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
            var room = shapeshifter.GetPlainShipRoom();
            var roomName = room == null ? string.Empty : Translator.GetString($"{room.RoomId}");
            BlackHoles.Add(new(new(pos), Utils.TimeStamp, pos, roomName, 0));
            Utils.SendRPC(CustomRPC.SyncRoleData, AbyssbringerId, 1, pos, roomName);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            var abyssbringer = AbyssbringerId.GetPlayer();
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

                    if (Vector2.Distance(pos, blackHole.Position) < 1f)
                    {
                        nearestPlayer.RpcExileV2();
                        blackHole.PlayersConsumed++;
                        Utils.SendRPC(CustomRPC.SyncRoleData, AbyssbringerId, 2, i);
                        Notify();

                        var state = Main.PlayerStates[nearestPlayer.PlayerId];
                        state.deathReason = PlayerState.DeathReason.Consumed;
                        state.RealKiller = (DateTime.Now, AbyssbringerId);
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
                    Utils.SendRPC(CustomRPC.SyncRoleData, AbyssbringerId, 3, i);
                    Notify();
                }

                void Notify() => Utils.NotifyRoles(SpecifySeer: abyssbringer, SpecifyTarget: abyssbringer);
            }
        }

        public void ReceiveRPC(Hazel.MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    var pos = reader.ReadVector2();
                    var roomName = reader.ReadString();
                    BlackHoles.Add(new(new(pos), Utils.TimeStamp, pos, roomName, 0));
                    break;
                case 2:
                    var blackHole = BlackHoles[reader.ReadPackedInt32()];
                    blackHole.PlayersConsumed++;
                    break;
                case 3:
                    BlackHoles.RemoveAt(reader.ReadPackedInt32());
                    break;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != AbyssbringerId || isMeeting || (seer.IsModClient() && !isHUD) || BlackHoles.Count == 0) return string.Empty;
            return string.Format(Translator.GetString("Abyssbringer.Suffix"), BlackHoles.Count, string.Join('\n', BlackHoles.Select(x => GetBlackHoleFormatText(x.RoomName, x.PlayersConsumed))));

            string GetBlackHoleFormatText(string roomName, int playersConsumed)
            {
                var rn = roomName == string.Empty ? Translator.GetString("Outside") : roomName;
                return string.Format(Translator.GetString("Abyssbringer.Suffix.BlackHole"), rn, playersConsumed);
            }
        }

        enum DespawnMode
        {
            None,
            AfterTime,
            After1PlayerEaten,
            AfterMeeting
        }

        class BlackHoleData(BlackHole NetObject, long PlaceTimeStamp, Vector2 Position, string RoomName, int PlayersConsumed)
        {
            public BlackHole NetObject { get; } = NetObject;
            public long PlaceTimeStamp { get; } = PlaceTimeStamp;
            public Vector2 Position { get; set; } = Position;
            public string RoomName { get; } = RoomName;
            public int PlayersConsumed { get; set; } = PlayersConsumed;
        }
    }
}