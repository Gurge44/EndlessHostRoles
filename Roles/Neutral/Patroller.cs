using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Neutral
{
    public class Patroller : RoleBase
    {
        private const int Id = 645000;
        public static bool On;

        private static OptionItem KillCooldown;
        private static OptionItem DecreasedKillCooldown;
        private static OptionItem IncreasedSpeed;
        private PlainShipRoom LastRoom;

        private Dictionary<Boost, PlainShipRoom> RoomBoosts = [];

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Patroller);
            KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Patroller])
                .SetValueFormat(OptionFormat.Seconds);
            DecreasedKillCooldown = FloatOptionItem.Create(Id + 3, "DecreasedKillCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Patroller])
                .SetValueFormat(OptionFormat.Seconds);
            IncreasedSpeed = FloatOptionItem.Create(Id + 4, "GamblerSpeedup", new(0.05f, 5f, 0.05f), 1.75f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Patroller])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;

            LastRoom = null;
            RoomBoosts = ShipStatus.Instance.AllRooms
                .Shuffle(IRandom.Instance)
                .Zip(Enum.GetValues<Boost>())
                .ToDictionary(x => x.Second, x => x.First);

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Utils.GetPlayerById(id).GetPlainShipRoom() == RoomBoosts[Boost.Cooldown] ? DecreasedKillCooldown.GetFloat() : KillCooldown.GetFloat();
        public override bool CanUseImpostorVentButton(PlayerControl pc) => pc.GetPlainShipRoom() == RoomBoosts[Boost.Vent];
        public override bool CanUseSabotage(PlayerControl pc) => pc.GetPlainShipRoom() == RoomBoosts[Boost.Sabotage];

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            var room = Utils.GetPlayerById(id)?.GetPlainShipRoom();
            if (room == null) return;
            opt.SetVision(room == RoomBoosts[Boost.Vision]);
            opt.SetInt(Int32OptionNames.KillDistance, room == RoomBoosts[Boost.Range] ? 2 : 0);
            Main.AllPlayerSpeed[id] = room == RoomBoosts[Boost.Speed] ? IncreasedSpeed.GetFloat() : Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive() || !GameStates.IsInTask) return;

            var room = pc.GetPlainShipRoom();
            if (room == LastRoom) return;
            LastRoom = room;

            pc.Notify(string.Format(Translator.GetString("PatrollerNotify"), Translator.GetString(room.RoomId.ToString()), Translator.GetString($"PatrollerBoost.{RoomBoosts.GetKeyByValue(room)}")));
            pc.SyncSettings();
        }

        public override void OnPet(PlayerControl pc)
        {
            var s = RoomBoosts.Select(x => $"{Translator.GetString(x.Value.RoomId.ToString())} \u21e8 {Translator.GetString($"PatrollerBoost.{x.Key}")}");
            pc.Notify(string.Join('\n', s));
        }

        enum Boost
        {
            Speed,
            Range,
            Cooldown,
            Vision,
            Vent,
            Sabotage
        }
    }
}