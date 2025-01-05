using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Impostor;

public class Abyssbringer : RoleBase
{
    public static bool On;

    public static OptionItem BlackHolePlaceCooldown;
    private static OptionItem BlackHoleDespawnMode;
    private static OptionItem BlackHoleDespawnTime;
    private static OptionItem BlackHoleMovesTowardsNearestPlayer;
    private static OptionItem BlackHoleMoveSpeed;
    private static OptionItem BlackHoleRadius;
    private byte AbyssbringerId;

    private List<BlackHoleData> BlackHoles = [];

    private int Count;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 649800;
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

        BlackHoleRadius = new FloatOptionItem(++id, "BlackHoleRadius", new(0.1f, 5f, 0.1f), 1.2f, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Multiplier);
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
        if (Options.UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = BlackHolePlaceCooldown.GetInt();
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
        Vector2 pos = shapeshifter.Pos();
        PlainShipRoom room = shapeshifter.GetPlainShipRoom();
        string roomName = room == null ? string.Empty : Translator.GetString($"{room.RoomId}");
        BlackHoles.Add(new(new(pos), Utils.TimeStamp, pos, roomName, 0));
        Utils.SendRPC(CustomRPC.SyncRoleData, AbyssbringerId, 1, pos, roomName);
    }

    public override void OnReportDeadBody()
    {
        if ((DespawnMode)BlackHoleDespawnMode.GetValue() == DespawnMode.AfterMeeting)
        {
            for (var i = 0; i < BlackHoles.Count; i++)
            {
                BlackHoles[i].NetObject.Despawn();
                BlackHoles.RemoveAt(i);
                Utils.SendRPC(CustomRPC.SyncRoleData, AbyssbringerId, 3, i);
            }
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (Count++ < 3) return;

        Count = 0;

        PlayerControl abyssbringer = AbyssbringerId.GetPlayer();
        int count = BlackHoles.Count;

        for (var i = 0; i < count; i++)
        {
            BlackHoleData blackHole = BlackHoles[i];

            var despawnMode = (DespawnMode)BlackHoleDespawnMode.GetValue();

            switch (despawnMode)
            {
                case DespawnMode.AfterTime when Utils.TimeStamp - blackHole.PlaceTimeStamp > BlackHoleDespawnTime.GetInt():
                case DespawnMode.AfterMeeting when GameStates.IsMeeting:
                    RemoveBlackHole();
                    continue;
            }

            PlayerControl nearestPlayer = Main.AllAlivePlayerControls.Without(pc).MinBy(x => Vector2.Distance(x.Pos(), blackHole.Position));

            if (nearestPlayer != null)
            {
                Vector2 pos = nearestPlayer.Pos();

                if (BlackHoleMovesTowardsNearestPlayer.GetBool() && GameStates.IsInTask && !ExileController.Instance)
                {
                    Vector2 direction = (pos - blackHole.Position).normalized;
                    Vector2 newPosition = blackHole.Position + (direction * BlackHoleMoveSpeed.GetFloat() * Time.fixedDeltaTime);
                    blackHole.NetObject.TP(newPosition);
                    blackHole.Position = newPosition;
                }

                if (Vector2.Distance(pos, blackHole.Position) <= BlackHoleRadius.GetFloat())
                {
                    nearestPlayer.RpcExileV2();
                    blackHole.PlayersConsumed++;
                    Utils.SendRPC(CustomRPC.SyncRoleData, AbyssbringerId, 2, i);
                    Notify();

                    PlayerState state = Main.PlayerStates[nearestPlayer.PlayerId];
                    state.deathReason = PlayerState.DeathReason.Consumed;
                    state.RealKiller = (DateTime.Now, AbyssbringerId);
                    state.SetDead();

                    FixedUpdatePatch.LoversSuicide(nearestPlayer.PlayerId);
                    Utils.AfterPlayerDeathTasks(nearestPlayer);

                    if (despawnMode == DespawnMode.After1PlayerEaten) RemoveBlackHole();
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

            void Notify()
            {
                Utils.NotifyRoles(SpecifySeer: abyssbringer, SpecifyTarget: abyssbringer);
            }
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Vector2 pos = reader.ReadVector2();
                string roomName = reader.ReadString();
                BlackHoles.Add(new(new(pos), Utils.TimeStamp, pos, roomName, 0));
                break;
            case 2:
                BlackHoleData blackHole = BlackHoles[reader.ReadPackedInt32()];
                blackHole.PlayersConsumed++;
                break;
            case 3:
                BlackHoles.RemoveAt(reader.ReadPackedInt32());
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != AbyssbringerId || meeting || (seer.IsModClient() && !hud) || BlackHoles.Count == 0) return string.Empty;

        return string.Format(Translator.GetString("Abyssbringer.Suffix"), BlackHoles.Count, string.Join('\n', BlackHoles.Select(x => GetBlackHoleFormatText(x.RoomName, x.PlayersConsumed))));

        string GetBlackHoleFormatText(string roomName, int playersConsumed)
        {
            string rn = roomName == string.Empty ? Translator.GetString("Outside") : roomName;
            return string.Format(Translator.GetString("Abyssbringer.Suffix.BlackHole"), rn, playersConsumed);
        }
    }

    private enum DespawnMode
    {
        None,
        AfterTime,
        After1PlayerEaten,
        AfterMeeting
    }

    private class BlackHoleData(BlackHole NetObject, long PlaceTimeStamp, Vector2 Position, string RoomName, int PlayersConsumed)
    {
        public BlackHole NetObject { get; } = NetObject;
        public long PlaceTimeStamp { get; } = PlaceTimeStamp;
        public Vector2 Position { get; set; } = Position;
        public string RoomName { get; } = RoomName;
        public int PlayersConsumed { get; set; } = PlayersConsumed;
    }
}