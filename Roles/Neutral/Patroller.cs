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

        private int Count;
        private PlainShipRoom LastRoom;
        private byte PatrollerId;

        private Dictionary<Boost, PlainShipRoom> RoomBoosts = [];

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Patroller);
            KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Patroller])
                .SetValueFormat(OptionFormat.Seconds);
            DecreasedKillCooldown = new FloatOptionItem(Id + 3, "DecreasedKillCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Patroller])
                .SetValueFormat(OptionFormat.Seconds);
            IncreasedSpeed = new FloatOptionItem(Id + 4, "GamblerSpeedup", new(0.05f, 5f, 0.05f), 1.75f, TabGroup.NeutralRoles)
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
            PatrollerId = playerId;

            LastRoom = null;
            RoomBoosts = ShipStatus.Instance.AllRooms
                .Shuffle()
                .Zip(Enum.GetValues<Boost>())
                .ToDictionary(x => x.Second, x => x.First);

            playerId.SetAbilityUseLimit(1);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Utils.GetPlayerById(id).GetPlainShipRoom() == RoomBoosts[Boost.Cooldown] ? DecreasedKillCooldown.GetFloat() : KillCooldown.GetFloat();
        public override bool CanUseImpostorVentButton(PlayerControl pc) => pc.inVent || pc.GetAbilityUseLimit() > 0 || pc.GetPlainShipRoom() == RoomBoosts[Boost.Vent];
        public override bool CanUseSabotage(PlayerControl pc) => pc.GetPlainShipRoom() == RoomBoosts[Boost.Sabotage];

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            if (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool())
                AURoleOptions.PhantomCooldown = 1f;
            if (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool())
                AURoleOptions.ShapeshifterCooldown = 1f;
            var room = Utils.GetPlayerById(id)?.GetPlainShipRoom();
            if (room == null) return;
            opt.SetVision(room == RoomBoosts[Boost.Vision]);
            opt.SetInt(Int32OptionNames.KillDistance, room == RoomBoosts[Boost.Range] ? 2 : 0);
            Main.AllPlayerSpeed[id] = room == RoomBoosts[Boost.Speed] ? IncreasedSpeed.GetFloat() : Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive() || !GameStates.IsInTask) return;
            Count++;
            if (Count < 20) return;
            Count = 0;

            var room = pc.GetPlainShipRoom();
            if ((LastRoom != null && room != null && room == LastRoom) || (LastRoom == null && room == null)) return;
            LastRoom = room;

            if (room != null)
            {
                var roomName = Translator.GetString(room.RoomId.ToString());
                pc.Notify(RoomBoosts.Any(x => x.Value == room)
                    ? string.Format(Translator.GetString("PatrollerNotify"), roomName, Translator.GetString($"PatrollerBoost.{RoomBoosts.First(x => x.Value == room).Key}"))
                    : string.Format(Translator.GetString("PatrollerNotifyNoBoost"), roomName), 300f);
            }
            else
            {
                NameNotifyManager.Notice.Remove(pc.PlayerId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }

            pc.MarkDirtySettings();

            if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                HudManager.Instance.SetHudActive(pc, pc.Data.Role, true);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (pc.GetPlainShipRoom() == RoomBoosts[Boost.Vent]) return;
            pc.RpcRemoveAbilityUse();
        }

        public override void AfterMeetingTasks()
        {
            PatrollerId.SetAbilityUseLimit(1);
        }

        public override void OnPet(PlayerControl pc)
        {
            var s = RoomBoosts.Select(x => $"{Translator.GetString(x.Value.RoomId.ToString())} \u21e8 {Translator.GetString($"PatrollerBoost.{x.Key}")}");
            pc.Notify(string.Join('\n', s));
        }

        public override bool OnVanish(PlayerControl pc)
        {
            OnPet(pc);
            return false;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !Options.UseUnshiftTrigger.GetBool()) return true;
            OnPet(shapeshifter);
            return false;
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