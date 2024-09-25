using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace EHR
{
    public static class RoomRush
    {
        private static OptionItem GlobalTimeMultiplier;
        private static OptionItem TimeWhenFirstPlayerEntersRoom;
        private static OptionItem VentTimes;

        public static HashSet<string> HasPlayedFriendCodes = [];

        public static Dictionary<byte, int> VentLimit = [];

        private static HashSet<SystemTypes> AllRooms = [];
        private static SystemTypes RoomGoal;
        private static int TimeLeft;
        private static HashSet<byte> DonePlayers = [];

        public static bool GameGoing;
        private static DateTime GameStartDateTime;

        private static RandomSpawn.SpawnMap Map;

        private static readonly Dictionary<MapNames, Dictionary<(SystemTypes, SystemTypes), int>> Multipliers = new()
        {
            [MapNames.Skeld] = new()
            {
                [(SystemTypes.Admin, SystemTypes.LifeSupp)] = 4,
                [(SystemTypes.Electrical, SystemTypes.MedBay)] = 3,
                [(SystemTypes.Electrical, SystemTypes.Security)] = 3
            },
            [MapNames.Mira] = new()
            {
                [(SystemTypes.Launchpad, SystemTypes.Reactor)] = 2,
                [(SystemTypes.Greenhouse, SystemTypes.Laboratory)] = 2,
                [(SystemTypes.Office, SystemTypes.Laboratory)] = 2,
                [(SystemTypes.Storage, SystemTypes.Comms)] = 5,
                [(SystemTypes.Cafeteria, SystemTypes.Comms)] = 3,
                [(SystemTypes.Balcony, SystemTypes.Comms)] = 2,
                [(SystemTypes.Storage, SystemTypes.MedBay)] = 4,
                [(SystemTypes.Cafeteria, SystemTypes.MedBay)] = 3,
                [(SystemTypes.Balcony, SystemTypes.MedBay)] = 5,
                [(SystemTypes.Storage, SystemTypes.LockerRoom)] = 2,
                [(SystemTypes.Balcony, SystemTypes.LockerRoom)] = 2
            },
            [MapNames.Polus] = new()
            {
                [(SystemTypes.Laboratory, SystemTypes.Admin)] = 2,
                [(SystemTypes.Storage, SystemTypes.Comms)] = 2,
                [(SystemTypes.Storage, SystemTypes.Office)] = 2,
                [(SystemTypes.Security, SystemTypes.LifeSupp)] = 2,
                [(SystemTypes.Security, SystemTypes.Comms)] = 2,
                [(SystemTypes.Security, SystemTypes.Electrical)] = 2
            },
            [MapNames.Airship] = new()
            {
                [(SystemTypes.MainHall, SystemTypes.GapRoom)] = 2,
                [(SystemTypes.MainHall, SystemTypes.Kitchen)] = 2,
                [(SystemTypes.Showers, SystemTypes.CargoBay)] = 2,
                [(SystemTypes.Showers, SystemTypes.Lounge)] = 2,
                [(SystemTypes.Showers, SystemTypes.Electrical)] = 2,
                [(SystemTypes.Showers, SystemTypes.Medical)] = 2,
                [(SystemTypes.Ventilation, SystemTypes.CargoBay)] = 2,
                [(SystemTypes.Ventilation, SystemTypes.Lounge)] = 2,
                [(SystemTypes.Ventilation, SystemTypes.Electrical)] = 2,
                [(SystemTypes.Ventilation, SystemTypes.Medical)] = 2,
                [(SystemTypes.Comms, SystemTypes.VaultRoom)] = 2,
                [(SystemTypes.GapRoom, SystemTypes.Records)] = 3,
                [(SystemTypes.GapRoom, SystemTypes.Lounge)] = 3,
                [(SystemTypes.GapRoom, SystemTypes.Brig)] = 4,
                [(SystemTypes.GapRoom, SystemTypes.VaultRoom)] = 3,
                [(SystemTypes.GapRoom, SystemTypes.Engine)] = 2,
                [(SystemTypes.MeetingRoom, SystemTypes.Records)] = 3,
                [(SystemTypes.MeetingRoom, SystemTypes.Lounge)] = 3,
                [(SystemTypes.MeetingRoom, SystemTypes.MainHall)] = 2,
                [(SystemTypes.Engine, SystemTypes.Security)] = 2,
                [(SystemTypes.MainHall, SystemTypes.Security)] = 2
            },
            [MapNames.Fungle] = new()
            {
                [(SystemTypes.Lookout, SystemTypes.SleepingQuarters)] = 3,
                [(SystemTypes.Lookout, SystemTypes.MeetingRoom)] = 2,
                [(SystemTypes.Lookout, SystemTypes.Storage)] = 2,
                [(SystemTypes.Lookout, SystemTypes.Dropship)] = 2,
                [(SystemTypes.Lookout, SystemTypes.FishingDock)] = 2,
                [(SystemTypes.Lookout, SystemTypes.RecRoom)] = 2,
                [(SystemTypes.Lookout, SystemTypes.Kitchen)] = 2,
                [(SystemTypes.Lookout, SystemTypes.Cafeteria)] = 2,
                [(SystemTypes.MiningPit, SystemTypes.SleepingQuarters)] = 3,
                [(SystemTypes.MiningPit, SystemTypes.MeetingRoom)] = 2,
                [(SystemTypes.MiningPit, SystemTypes.Storage)] = 2,
                [(SystemTypes.MiningPit, SystemTypes.Dropship)] = 2,
                [(SystemTypes.MiningPit, SystemTypes.FishingDock)] = 2,
                [(SystemTypes.MiningPit, SystemTypes.RecRoom)] = 2,
                [(SystemTypes.MiningPit, SystemTypes.Kitchen)] = 2,
                [(SystemTypes.MiningPit, SystemTypes.Cafeteria)] = 2
            }
        };
        
        // TODO: Fix vent button not active for host

        public static void SetupCustomOption()
        {
            int id = 69_217_001;
            Color color = Utils.GetRoleColor(CustomRoles.RRPlayer);
            const CustomGameMode gameMode = CustomGameMode.RoomRush;
            
            GlobalTimeMultiplier = new FloatOptionItem(id++, "RR_GlobalTimeMultiplier", new(0.05f, 2f, 0.05f), 1f, TabGroup.GameSettings)
                .SetHeader(true)
                .SetColor(color)
                .SetGameMode(gameMode);

            TimeWhenFirstPlayerEntersRoom = new IntegerOptionItem(id++, "RR_TimeWhenTwoPlayersEntersRoom", new(1, 30, 1), 5, TabGroup.GameSettings)
                .SetColor(color)
                .SetGameMode(gameMode)
                .SetValueFormat(OptionFormat.Seconds);

            VentTimes = new IntegerOptionItem(id, "RR_VentTimes", new(0, 90, 1), 1, TabGroup.GameSettings)
                .SetColor(color)
                .SetGameMode(gameMode)
                .SetValueFormat(OptionFormat.Times);
        }
        
        public static int GetSurvivalTime(byte id)
        {
            if (!Main.PlayerStates.TryGetValue(id, out var state)) return 0;
            DateTime died = state.RealKiller.TimeStamp;
            TimeSpan time = died - GameStartDateTime;
            return (int)time.TotalSeconds;
        }

        public static void OnGameStart()
        {
            if (Options.CurrentGameMode != CustomGameMode.RoomRush) return;
            Main.Instance.StartCoroutine(GameStartTasks());
        }

        private static System.Collections.IEnumerator GameStartTasks()
        {
            GameGoing = false;

            int ventLimit = VentTimes.GetInt();
            VentLimit = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, _ => ventLimit);

            AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
            AllRooms.Remove(SystemTypes.Hallway);
            AllRooms.Remove(SystemTypes.Outside);
            AllRooms.RemoveWhere(x => x.ToString().Contains("Decontamination"));
            
            DonePlayers = [];

            Map = Main.CurrentMap switch
            {
                MapNames.Skeld => new RandomSpawn.SkeldSpawnMap(),
                MapNames.Mira => new RandomSpawn.MiraHQSpawnMap(),
                MapNames.Polus => new RandomSpawn.PolusSpawnMap(),
                MapNames.Dleks => new RandomSpawn.DleksSpawnMap(),
                MapNames.Airship => new RandomSpawn.AirshipSpawnMap(),
                MapNames.Fungle => new RandomSpawn.FungleSpawnMap(),
                _ => throw new ArgumentOutOfRangeException()
            };

            yield return new WaitForSeconds(Main.CurrentMap == MapNames.Airship ? 20f : 12f);
            
            PlayerControl[] aapc = Main.AllAlivePlayerControls;
            aapc.Do(x => x.RpcSetCustomRole(CustomRoles.RRPlayer));

            bool showTutorial = aapc.ExceptBy(HasPlayedFriendCodes, x => x.FriendCode).Count() >= aapc.Length / 3;
            if (showTutorial)
            {
                string[] notifies = [Translator.GetString("RR_Tutorial_1"), Translator.GetString("RR_Tutorial_2"), Translator.GetString("RR_Tutorial_3"), Translator.GetString("RR_Tutorial_4")];
                foreach (string notify in notifies)
                {
                    aapc.Do(x => x.Notify(notify, 8f));
                    yield return new WaitForSeconds(3f);
                }

                yield return new WaitForSeconds(4f);
            }
            
            NameNotifyManager.Reset();
            aapc.Do(x => x.Notify(Translator.GetString("RR_ReadyQM")));

            yield return new WaitForSeconds(2f);

            for (int i = 3; i > 0; i--)
            {
                int time = i;
                NameNotifyManager.Reset();
                aapc.Do(x => x.Notify(time.ToString()));
                yield return new WaitForSeconds(1f);
            }
            
            if (ventLimit > 0) aapc.Without(PlayerControl.LocalPlayer).Do(x => x.RpcChangeRoleBasis(CustomRoles.EngineerEHR));

            NameNotifyManager.Reset();
            StartNewRound(true);
            GameGoing = true;
            GameStartDateTime = DateTime.Now;
        }

        private static void StartNewRound(bool initial = false)
        {
            if (!initial) KillPlayersOutsideRoom();
            DonePlayers.Clear();
            SystemTypes previous = RoomGoal;
            RoomGoal = AllRooms.Without(previous).RandomElement();
            Vector2 goalPos = Map.Positions.GetValueOrDefault(RoomGoal, RoomGoal.GetRoomClass().transform.position);
            Vector2 previousPos = Map.Positions.GetValueOrDefault(previous, initial ? Main.AllAlivePlayerControls.RandomElement().Pos() : previous.GetRoomClass().transform.position);
            float distance = initial ? 50 : Vector2.Distance(goalPos, previousPos);
            float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            int time = (int)Math.Ceiling(distance / speed);
            MapNames map = Main.CurrentMap;
            Dictionary<(SystemTypes, SystemTypes), int> multipliers = Multipliers[map == MapNames.Dleks ? MapNames.Skeld : map];
            time *= multipliers.GetValueOrDefault((previous, RoomGoal), multipliers.GetValueOrDefault((RoomGoal, previous), 1));
            bool involvesDecontamination = Main.CurrentMap switch
            {
                MapNames.Mira => !(previous is SystemTypes.Laboratory or SystemTypes.Reactor && RoomGoal is SystemTypes.Laboratory or SystemTypes.Reactor) && (previous is SystemTypes.Laboratory or SystemTypes.Reactor || RoomGoal is SystemTypes.Laboratory or SystemTypes.Reactor),
                MapNames.Polus => previous == SystemTypes.Specimens || RoomGoal == SystemTypes.Specimens,
                _ => false
            };
            if (involvesDecontamination) time += 15;
            switch (Main.CurrentMap)
            {
                case MapNames.Airship:
                    if (RoomGoal == SystemTypes.Ventilation)
                        time = (int)(time * 0.4f);
                    break;
                case MapNames.Fungle when RoomGoal == SystemTypes.Laboratory || previous == SystemTypes.Laboratory:
                    time += (int)(8 / speed);
                    break;
                case MapNames.Polus when RoomGoal == SystemTypes.Laboratory || (previous == SystemTypes.Laboratory && RoomGoal is not SystemTypes.Office and not SystemTypes.Storage):
                    time -= (int)(7 * speed);
                    break;
            }

            TimeLeft = (int)Math.Round(time * GlobalTimeMultiplier.GetFloat());
            Utils.NotifyRoles();
        }

        private static void KillPlayersOutsideRoom()
        {
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                var room = pc.GetPlainShipRoom();
                if (room != null && room.RoomId != RoomGoal)
                    pc.Suicide();
            }
        }

        private static PlainShipRoom GetRoomClass(this SystemTypes systemTypes) => ShipStatus.Instance.AllRooms.First(x => x.RoomId == systemTypes);

        public static string GetSuffix(PlayerControl seer)
        {
            if (!GameGoing || Main.HasJustStarted || seer == null || !seer.IsAlive()) return string.Empty;

            var sb = new StringBuilder();
            var room = seer.GetPlainShipRoom();
            var done = room != null && room.RoomId == RoomGoal;
            var color = done ? Color.green : Color.yellow;
            sb.AppendLine(Utils.ColorString(color, Translator.GetString(RoomGoal.ToString())));
            color = done ? Color.white : Color.yellow;
            sb.AppendLine(Utils.ColorString(color, TimeLeft.ToString()));
            
            sb.AppendLine();
            
            int vents = VentLimit.GetValueOrDefault(seer.PlayerId);
            sb.Append(string.Format(Translator.GetString("RR_VentsRemaining"), vents));
            
            return sb.ToString();
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        static class FixedUpdatePatch
        {
            private static long LastUpdate = Utils.TimeStamp;

            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            public static void Postfix(PlayerControl __instance)
            {
                if (!GameGoing || Main.HasJustStarted || Options.CurrentGameMode != CustomGameMode.RoomRush || !AmongUsClient.Instance.AmHost) return;

                long now = Utils.TimeStamp;
                var aapc = Main.AllAlivePlayerControls;

                var room = __instance.GetPlainShipRoom();
                if (__instance.IsAlive() && !__instance.inMovingPlat && room != null && room.RoomId == RoomGoal && DonePlayers.Add(__instance.PlayerId))
                {
                    if (DonePlayers.Count == 2)
                    {
                        TimeLeft = TimeWhenFirstPlayerEntersRoom.GetInt();
                        LastUpdate = now;
                    }

                    if (DonePlayers.Count == aapc.Length - 1)
                    {
                        var last = aapc.First(x => !DonePlayers.Contains(x.PlayerId));
                        last.Suicide();
                        last.Notify(Translator.GetString("RR_YouWereLast"));
                        StartNewRound();
                        return;
                    }
                }
                
                if (LastUpdate == now) return;
                LastUpdate = now;

                TimeLeft--;
                Utils.NotifyRoles();

                if (TimeLeft <= 0)
                {
                    Main.AllAlivePlayerControls.ExceptBy(DonePlayers, x => x.PlayerId).Do(x => x.Suicide());
                    StartNewRound();
                }
            }
        }
    }

    public class RRPlayer : RoleBase
    {
        public override bool IsEnable => Options.CurrentGameMode == CustomGameMode.RoomRush;
        
        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }

        public override void SetupCustomOption()
        {
        }

        public override void OnExitVent(PlayerControl pc, Vent vent)
        {
            RoomRush.VentLimit[pc.PlayerId]--;
        }

        public override bool CanUseVent(PlayerControl pc, int ventId) => RoomRush.GameGoing && (pc.inVent || RoomRush.VentLimit[pc.PlayerId] > 0);
    }
}