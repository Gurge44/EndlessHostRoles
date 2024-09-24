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

        private static RandomSpawn.SpawnMap Map;

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
            DateTime start = DateTime.UtcNow;
            DateTime died = state.RealKiller.TimeStamp;
            TimeSpan time = start - died;
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

            yield return new WaitForSeconds(8f);
            
            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            bool showTutorial = aapc.ExceptBy(HasPlayedFriendCodes, x => x.FriendCode).Count() >= aapc.Length / 3;
            if (showTutorial)
            {
                string[] notifies = [Translator.GetString("RR_Tutorial_1"), Translator.GetString("RR_Tutorial_2"), Translator.GetString("RR_Tutorial_3"), Translator.GetString("RR_Tutorial_4")];
                foreach (string notify in notifies)
                {
                    aapc.Do(x => x.Notify(notify, 6f));
                    yield return new WaitForSeconds(3f);
                }

                yield return new WaitForSeconds(4f);
            }
            
            aapc.Do(x => x.Notify(Translator.GetString("RR_ReadyQM"), 2f));

            yield return new WaitForSeconds(2f);

            for (int i = 3; i > 0; i--)
            {
                int time = i;
                aapc.Do(x => x.Notify(time.ToString()));
                yield return new WaitForSeconds(1f);
            }
            
            StartNewRound();

            GameGoing = true;
        }

        private static void StartNewRound()
        {
            PlayerControl[] aapc = Main.AllAlivePlayerControls;
            RoomGoal = AllRooms.RandomElement();
            float distance = Vector2.Distance(Map.Positions.GetValueOrDefault(RoomGoal, RoomGoal.GetRoomClass().transform.position), aapc.RandomElement().transform.position);
            float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            TimeLeft = (int)Math.Ceiling(distance * 3f / speed);
        }

        private static PlainShipRoom GetRoomClass(this SystemTypes systemTypes) => ShipStatus.Instance.AllRooms.First(x => x.RoomId == systemTypes);

        public static string GetSuffix(PlayerControl seer)
        {
            if (!GameGoing || Main.HasJustStarted) return string.Empty;

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
                
                if (__instance.IsAlive() && __instance.GetPlainShipRoom().RoomId == RoomGoal && DonePlayers.Add(__instance.PlayerId))
                {
                    if (DonePlayers.Count == Main.AllAlivePlayerControls.Length)
                    {
                        __instance.Suicide();
                        __instance.Notify(Translator.GetString("RR_YouWereLast"));
                    }
                    else
                    {
                        __instance.Notify(Translator.GetString("RR_GoalReached"));
                        if (DonePlayers.Count == 2)
                        {
                            TimeLeft = TimeWhenFirstPlayerEntersRoom.GetInt();
                            LastUpdate = now;
                        }
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
    }
}