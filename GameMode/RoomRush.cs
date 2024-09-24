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
        private static OptionItem TimeWhenFirstPlayerEntersRoom;
        private static OptionItem VentTimes;

        public static HashSet<string> HasPlayedFriendCodes = [];

        public static Dictionary<byte, int> VentLimit = [];

        private static HashSet<SystemTypes> AllRooms = [];
        private static SystemTypes RoomGoal;
        private static int TimeLeft;
        private static HashSet<byte> DonePlayers = [];

        private static bool GameGoing;
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
                [(SystemTypes.Launchpad, SystemTypes.LockerRoom)] = 2,
                [(SystemTypes.Greenhouse, SystemTypes.Laboratory)] = 2,
                [(SystemTypes.Office, SystemTypes.Laboratory)] = 2,
                [(SystemTypes.Storage, SystemTypes.Comms)] = 5,
                [(SystemTypes.Cafeteria, SystemTypes.Comms)] = 3,
                [(SystemTypes.Balcony, SystemTypes.Comms)] = 3,
                [(SystemTypes.Storage, SystemTypes.MedBay)] = 4,
                [(SystemTypes.Cafeteria, SystemTypes.MedBay)] = 3,
                [(SystemTypes.Balcony, SystemTypes.MedBay)] = 5,
                [(SystemTypes.Storage, SystemTypes.LockerRoom)] = 2,
                [(SystemTypes.Balcony, SystemTypes.LockerRoom)] = 2
            },
            [MapNames.Polus] = new()
            {
                [(SystemTypes.Laboratory, SystemTypes.Office)] = 2,
                [(SystemTypes.Laboratory, SystemTypes.Admin)] = 2,
                [(SystemTypes.Storage, SystemTypes.Comms)] = 2,
                [(SystemTypes.Storage, SystemTypes.Office)] = 2,
                [(SystemTypes.Security, SystemTypes.LifeSupp)] = 2
            },
            [MapNames.Airship] = new()
            {
                [(SystemTypes.MainHall, SystemTypes.GapRoom)] = 2,
                [(SystemTypes.MainHall, SystemTypes.Kitchen)] = 2,
                [(SystemTypes.Showers, SystemTypes.CargoBay)] = 2,
                [(SystemTypes.Showers, SystemTypes.Lounge)] = 2,
                [(SystemTypes.Showers, SystemTypes.Electrical)] = 2,
                [(SystemTypes.Showers, SystemTypes.Medical)] = 2,
                [(SystemTypes.Comms, SystemTypes.VaultRoom)] = 2,
                [(SystemTypes.Cockpit, SystemTypes.VaultRoom)] = 2,
            },
            [MapNames.Fungle] = new()
        };
        
        // TODO: Fix vent button not active for host

        public static void SetupCustomOption()
        {
            int id = 69_217_001;
            Color color = Utils.GetRoleColor(CustomRoles.RRPlayer);
            const CustomGameMode gameMode = CustomGameMode.RoomRush;

            TimeWhenFirstPlayerEntersRoom = new IntegerOptionItem(id++, "RR_TimeWhenTwoPlayersEntersRoom", new(1, 30, 1), 5, TabGroup.GameSettings)
                .SetHeader(true)
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
            GameStartDateTime = DateTime.Now;
            
            VentLimit = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, _ => VentTimes.GetInt());

            AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
            AllRooms.Remove(SystemTypes.Hallway);
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

            bool showTutorial = aapc.ExceptBy(HasPlayedFriendCodes, x => x.FriendCode).Count() >= aapc.Length / 3;
            if (showTutorial)
            {
                string[] notifies = [Translator.GetString("RR_Tutorial_1"), Translator.GetString("RR_Tutorial_2"), Translator.GetString("RR_Tutorial_3"), Translator.GetString("RR_Tutorial_4")];
                foreach (string notify in notifies)
                {
                    aapc.Do(x => x.Notify(notify));
                    yield return new WaitForSeconds(3f);
                }

                yield return new WaitForSeconds(4f);
            }
            
            aapc.Do(x => x.Notify(Translator.GetString("RR_ReadyQM"), 2f));

            yield return new WaitForSeconds(2f);

            for (int i = 3; i > 0; i--)
            {
                int time = i;
                NameNotifyManager.Reset();
                aapc.Do(x => x.Notify(time.ToString()));
                yield return new WaitForSeconds(1f);
            }
            
            NameNotifyManager.Reset();
            StartNewRound(true);
            GameGoing = true;
        }

        private static void StartNewRound(bool initial = false)
        {
            if (!initial) KillPlayersOutsideRoom();
            DonePlayers.Clear();
            SystemTypes previous = RoomGoal;
            RoomGoal = AllRooms.RandomElement();
            Vector2 goalPos = Map.Positions.GetValueOrDefault(RoomGoal, RoomGoal.GetRoomClass().transform.position);
            Vector2 previousPos = Map.Positions.GetValueOrDefault(previous, initial ? Main.AllAlivePlayerControls.RandomElement().Pos() : previous.GetRoomClass().transform.position);
            float distance = Vector2.Distance(goalPos, previousPos);
            float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            int time = (int)Math.Ceiling(distance / speed);
            MapNames map = Main.CurrentMap;
            Dictionary<(SystemTypes, SystemTypes), int> multipliers = Multipliers[map == MapNames.Dleks ? MapNames.Skeld : map];
            time *= multipliers.GetValueOrDefault((previous, RoomGoal), multipliers.GetValueOrDefault((RoomGoal, previous), 1));
            bool involvesDecontamination = Main.CurrentMap switch
            {
                MapNames.Mira => previous is SystemTypes.Laboratory or SystemTypes.Reactor || RoomGoal is SystemTypes.Laboratory or SystemTypes.Reactor,
                MapNames.Polus => previous == SystemTypes.Specimens || RoomGoal == SystemTypes.Specimens,
                _ => false
            };
            if (involvesDecontamination) time += 18;
            if (initial && Main.CurrentMap == MapNames.Airship) time = (int)Math.Ceiling(60 / speed);
            TimeLeft = time;
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
            sb.AppendLine(Translator.GetString(RoomGoal.ToString()));
            sb.AppendLine(TimeLeft.ToString());
            
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
                if (__instance.IsAlive() && room != null && room.RoomId == RoomGoal && DonePlayers.Add(__instance.PlayerId))
                {
                    __instance.Notify(Translator.GetString("RR_GoalReached"));
                    
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

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            RoomRush.VentLimit[pc.PlayerId]--;
        }

        public override bool CanUseVent(PlayerControl pc, int ventId) => pc.inVent || RoomRush.VentLimit[pc.PlayerId] > 0;

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton.SetEnabled();
            hud.AbilityButton.SetUsesRemaining(RoomRush.VentLimit[id]);
        }
    }
}