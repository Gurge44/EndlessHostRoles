using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR;

public static class RoomRush
{
    private static OptionItem GlobalTimeMultiplier;
    private static OptionItem TimeWhenFirstTwoPlayersEnterRoom;
    private static OptionItem VentTimes;
    private static OptionItem DisplayRoomName;
    private static OptionItem DisplayArrowToRoom;
    private static OptionItem DontKillLastPlayer;
    private static OptionItem DontLowerTimeLimitWhenTwoPlayersEnterCorrectRoom;
    private static OptionItem DontKillPlayersOutsideRoomWhenTimeRunsOut;
    private static OptionItem WinByPointsInsteadOfDeaths;
    private static OptionItem PointsToWin;

    private static Dictionary<byte, int> Points = [];
    private static int PointsToWinValue;

    public static readonly HashSet<string> HasPlayedFriendCodes = [];
    public static Dictionary<byte, int> VentLimit = [];

    private static HashSet<SystemTypes> AllRooms = [];
    private static SystemTypes RoomGoal;
    private static long TimeLimitEndTS;
    private static HashSet<byte> DonePlayers = [];

    private static bool GameGoing;
    private static DateTime GameStartDateTime;

    private static RandomSpawn.SpawnMap Map;

    public static readonly Dictionary<MapNames, Dictionary<(SystemTypes, SystemTypes), int>> Multipliers = new()
    {
        [MapNames.Skeld] = new()
        {
            [(SystemTypes.Admin, SystemTypes.Weapons)] = 2,
            [(SystemTypes.Admin, SystemTypes.Nav)] = 2,
            [(SystemTypes.Admin, SystemTypes.Comms)] = 2,
            [(SystemTypes.Admin, SystemTypes.Shields)] = 2,
            [(SystemTypes.Admin, SystemTypes.LifeSupp)] = 4,
            [(SystemTypes.Electrical, SystemTypes.MedBay)] = 3,
            [(SystemTypes.Electrical, SystemTypes.Security)] = 3,
            [(SystemTypes.Electrical, SystemTypes.LifeSupp)] = 2,
            [(SystemTypes.MedBay, SystemTypes.Security)] = 3,
            [(SystemTypes.Admin, SystemTypes.Security)] = 2,
            [(SystemTypes.Security, SystemTypes.LifeSupp)] = 2,
            [(SystemTypes.Cafeteria, SystemTypes.LifeSupp)] = 2,
            [(SystemTypes.Storage, SystemTypes.Security)] = 2,
            [(SystemTypes.Storage, SystemTypes.MedBay)] = 2
        },
        [MapNames.MiraHQ] = new()
        {
            [(SystemTypes.Launchpad, SystemTypes.Reactor)] = 2,
            [(SystemTypes.Greenhouse, SystemTypes.Laboratory)] = 2,
            [(SystemTypes.Office, SystemTypes.Laboratory)] = 2,
            [(SystemTypes.Storage, SystemTypes.Comms)] = 4,
            [(SystemTypes.Cafeteria, SystemTypes.Comms)] = 2,
            [(SystemTypes.Balcony, SystemTypes.Comms)] = 2,
            [(SystemTypes.Storage, SystemTypes.MedBay)] = 3,
            [(SystemTypes.Cafeteria, SystemTypes.MedBay)] = 2,
            [(SystemTypes.Balcony, SystemTypes.MedBay)] = 2,
            [(SystemTypes.Storage, SystemTypes.LockerRoom)] = 2
        },
        [MapNames.Polus] = new()
        {
            [(SystemTypes.Storage, SystemTypes.Comms)] = 2,
            [(SystemTypes.Storage, SystemTypes.Office)] = 2,
            [(SystemTypes.Storage, SystemTypes.Admin)] = 2,
            [(SystemTypes.Security, SystemTypes.LifeSupp)] = 2,
            [(SystemTypes.Security, SystemTypes.Comms)] = 2,
            [(SystemTypes.Office, SystemTypes.Specimens)] = 2,
            [(SystemTypes.Comms, SystemTypes.Electrical)] = 2
        },
        [MapNames.Airship] = new()
        {
            [(SystemTypes.Showers, SystemTypes.CargoBay)] = 2,
            [(SystemTypes.Showers, SystemTypes.Medical)] = 2,
            [(SystemTypes.Comms, SystemTypes.VaultRoom)] = 2,
            [(SystemTypes.MeetingRoom, SystemTypes.Records)] = 5,
            [(SystemTypes.MeetingRoom, SystemTypes.Lounge)] = 3,
            [(SystemTypes.MeetingRoom, SystemTypes.MainHall)] = 2,
            [(SystemTypes.MeetingRoom, SystemTypes.CargoBay)] = 2,
            [(SystemTypes.MeetingRoom, SystemTypes.Showers)] = 2,
            [(SystemTypes.Engine, SystemTypes.Security)] = 2,
            [(SystemTypes.Engine, SystemTypes.HallOfPortraits)] = 2,
            [(SystemTypes.MainHall, SystemTypes.Security)] = 2
        },
        [MapNames.Fungle] = new()
        {
            [(SystemTypes.Lookout, SystemTypes.SleepingQuarters)] = 3,
            [(SystemTypes.Lookout, SystemTypes.MeetingRoom)] = 2,
            [(SystemTypes.Lookout, SystemTypes.Storage)] = 3,
            [(SystemTypes.MiningPit, SystemTypes.SleepingQuarters)] = 2,
            [(SystemTypes.MiningPit, SystemTypes.MeetingRoom)] = 2,
            [(SystemTypes.MiningPit, SystemTypes.Storage)] = 2,
            [(SystemTypes.MiningPit, SystemTypes.Dropship)] = 2,
            [(SystemTypes.MiningPit, SystemTypes.Comms)] = 2
        },
        [(MapNames)6] = new()
        {
            [(SystemTypes.Admin, SystemTypes.MeetingRoom)] = 2,
            [(SystemTypes.Admin, SystemTypes.Lounge)] = 2,
            [(SystemTypes.Comms, SystemTypes.Medical)] = 2,
            [((SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Research, SystemTypes.Medical)] = 3,
            [((SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast, SystemTypes.Security)] = 2
        }
    };

    public static bool PointsSystem => WinByPointsInsteadOfDeaths.GetBool();
    public static int RawPointsToWin => PointsToWin.GetInt();

    public static void SetupCustomOption()
    {
        var id = 69_217_001;
        Color color = Utils.GetRoleColor(CustomRoles.RRPlayer);
        const CustomGameMode gameMode = CustomGameMode.RoomRush;

        GlobalTimeMultiplier = new FloatOptionItem(id++, "RR_GlobalTimeMultiplier", new(0.05f, 3f, 0.05f), 1f, TabGroup.GameSettings)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode);

        TimeWhenFirstTwoPlayersEnterRoom = new IntegerOptionItem(id++, "RR_TimeWhenTwoPlayersEntersRoom", new(1, 30, 1), 5, TabGroup.GameSettings)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);

        VentTimes = new IntegerOptionItem(id++, "RR_VentTimes", new(0, 90, 1), 1, TabGroup.GameSettings)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Times);

        DisplayRoomName = new BooleanOptionItem(id++, "RR_DisplayRoomName", true, TabGroup.GameSettings)
            .SetColor(color)
            .SetGameMode(gameMode);

        DisplayArrowToRoom = new BooleanOptionItem(id++, "RR_DisplayArrowToRoom", false, TabGroup.GameSettings)
            .SetColor(color)
            .SetGameMode(gameMode);

        DontKillLastPlayer = new BooleanOptionItem(id++, "RR_DontKillLastPlayer", false, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color);

        DontLowerTimeLimitWhenTwoPlayersEnterCorrectRoom = new BooleanOptionItem(id++, "RR_DontLowerTimeLimitWhenTwoPlayersEnterCorrectRoom", true, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color);

        DontKillPlayersOutsideRoomWhenTimeRunsOut = new BooleanOptionItem(id++, "RR_DontKillPlayersOutsideRoomWhenTimeRunsOut", false, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color);

        WinByPointsInsteadOfDeaths = new BooleanOptionItem(id++, "RR_WinByPointsInsteadOfDeaths", false, TabGroup.GameSettings)
            .SetGameMode(gameMode)
            .SetColor(color);

        PointsToWin = new IntegerOptionItem(id, "RR_PointsToWin", new(1, 100, 1), 10, TabGroup.GameSettings)
            .SetParent(WinByPointsInsteadOfDeaths)
            .SetGameMode(gameMode)
            .SetColor(color);
    }

    public static int GetSurvivalTime(byte id)
    {
        if (!Main.PlayerStates.TryGetValue(id, out PlayerState state) || ChatCommands.Spectators.Contains(id) || (id == 0 && Main.GM.Value) || state.deathReason == PlayerState.DeathReason.Disconnected) return -1;

        if (!state.IsDead) return 0;

        DateTime died = state.RealKiller.TimeStamp;
        TimeSpan time = died - GameStartDateTime;
        return (int)time.TotalSeconds;
    }

    public static string GetPoints(byte id)
    {
        if (!WinByPointsInsteadOfDeaths.GetBool()) return string.Empty;
        return Points.TryGetValue(id, out int points) ? $"{points}/{PointsToWinValue}" : string.Empty;
    }

    public static IEnumerator GameStartTasks()
    {
        GameGoing = false;

        int ventLimit = VentTimes.GetInt();
        VentLimit = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, _ => ventLimit);

        AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
        AllRooms.Remove(SystemTypes.Hallway);
        AllRooms.Remove(SystemTypes.Outside);
        AllRooms.Remove(SystemTypes.Ventilation);
        AllRooms.RemoveWhere(x => x.ToString().Contains("Decontamination"));
        if (SubmergedCompatibility.IsSubmerged()) AllRooms.RemoveWhere(x => (byte)x > 135);

        DonePlayers = [];
        Points = [];

        if (WinByPointsInsteadOfDeaths.GetBool())
            Points = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, _ => 0);

        Map = RandomSpawn.SpawnMap.GetSpawnMap();

        yield return new WaitForSeconds(Main.CurrentMap == MapNames.Airship ? 8f : 3f);

        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        aapc.Do(x => x.RpcSetCustomRole(CustomRoles.RRPlayer));

        PointsToWinValue = PointsToWin.GetInt() * aapc.Length;

        bool showTutorial = aapc.ExceptBy(HasPlayedFriendCodes, x => x.FriendCode).Count() > aapc.Length / 2;

        if (showTutorial)
        {
            var readingTime = 0;

            StringBuilder sb = new(Translator.GetString("RR_Tutorial_Basics"));
            sb.AppendLine();

            bool points = WinByPointsInsteadOfDeaths.GetBool();

            if (points)
            {
                sb.AppendLine(Translator.GetString("RR_Tutorial_PointsSystem"));
                sb.AppendLine(Translator.GetString("RR_Tutorial_TimeLimitLastPoints"));
                sb.AppendLine(string.Format(Translator.GetString("RR_Tutorial_PointsToWin"), PointsToWinValue));
                readingTime += 12;
            }
            else
            {
                sb.AppendLine(Translator.GetString("RR_Tutorial_TimeLimitDeath"));
                readingTime += 3;
            }

            bool arrow = DisplayArrowToRoom.GetBool();
            bool name = DisplayRoomName.GetBool();

            switch (arrow, name)
            {
                case (true, true):
                    sb.AppendLine(Translator.GetString("RR_Tutorial_RoomIndication_ArrowAndName"));
                    readingTime += 4;
                    break;
                case (true, false):
                    sb.AppendLine(Translator.GetString("RR_Tutorial_RoomIndication_ArrowOnly"));
                    readingTime += 3;
                    break;
                case (false, true):
                    sb.AppendLine(Translator.GetString("RR_Tutorial_RoomIndication_NameOnly"));
                    readingTime += 3;
                    break;
            }

            if (!points)
            {
                if (!DontLowerTimeLimitWhenTwoPlayersEnterCorrectRoom.GetBool())
                {
                    sb.AppendLine(string.Format(Translator.GetString("RR_Tutorial_LowerTimeWhenTwoPlayersEnterRoom"), TimeWhenFirstTwoPlayersEnterRoom.GetInt()));
                    readingTime += 4;
                }

                if (!DontKillLastPlayer.GetBool())
                {
                    sb.AppendLine(Translator.GetString("RR_Tutorial_LastDeath"));
                    readingTime += 3;
                }

                if (!DontKillPlayersOutsideRoomWhenTimeRunsOut.GetBool())
                {
                    sb.AppendLine(Translator.GetString("RR_Tutorial_DontMoveOutOfRoom"));
                    readingTime += 2;
                }
            }

            if (ventLimit > 0)
            {
                sb.AppendLine(string.Format(Translator.GetString("RR_Tutorial_Venting"), ventLimit));
                readingTime += 3;
            }

            aapc.NotifyPlayers(sb.Insert(0, "<#ffffff>").Append("</color>").ToString().Trim(), 100f);
            yield return new WaitForSeconds(readingTime);
            if (!GameStates.InGame) yield break;
        }

        for (var i = 3; i > 0; i--)
        {
            NameNotifyManager.Reset();
            aapc.NotifyPlayers(string.Format(Translator.GetString("RR_ReadyQM"), i));
            yield return new WaitForSeconds(1f);
        }

        if (ventLimit > 0)
            aapc.Do(x => x.RpcSetRoleGlobal(RoleTypes.Engineer));

        Utils.SendRPC(CustomRPC.RoomRushDataSync, 1);

        NameNotifyManager.Reset();
        StartNewRound(true);
        GameGoing = true;
        GameStartDateTime = DateTime.Now;
    }

    private static void StartNewRound(bool initial = false)
    {
        if (GameStates.IsEnded) return;

        MapNames map = Main.CurrentMap;

        SystemTypes previous = !initial
            ? RoomGoal
            : map switch
            {
                MapNames.Skeld => SystemTypes.Cafeteria,
                MapNames.MiraHQ => SystemTypes.Launchpad,
                MapNames.Dleks => SystemTypes.Cafeteria,
                MapNames.Polus => SystemTypes.Dropship,
                MapNames.Airship => SystemTypes.MainHall,
                MapNames.Fungle => SystemTypes.Dropship,
                (MapNames)6 => (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral,
                _ => throw new ArgumentOutOfRangeException(map.ToString(), "Invalid map")
            };

        DonePlayers.Clear();
        RoomGoal = AllRooms.Without(previous).RandomElement();
        Vector2 goalPos = Map.Positions.GetValueOrDefault(RoomGoal, RoomGoal.GetRoomClass().transform.position);
        Vector2 previousPos = Map.Positions.GetValueOrDefault(previous, initial ? Main.AllAlivePlayerControls.RandomElement().Pos() : previous.GetRoomClass().transform.position);
        float distance = initial ? 50 : Vector2.Distance(goalPos, previousPos);
        float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        var time = (int)Math.Ceiling(distance / speed);
        Dictionary<(SystemTypes, SystemTypes), int> multipliers = Multipliers[map == MapNames.Dleks ? MapNames.Skeld : map];
        time *= multipliers.GetValueOrDefault((previous, RoomGoal), multipliers.GetValueOrDefault((RoomGoal, previous), 1));

        bool involvesDecontamination = map switch
        {
            MapNames.MiraHQ => previous is SystemTypes.Laboratory or SystemTypes.Reactor ^ RoomGoal is SystemTypes.Laboratory or SystemTypes.Reactor,
            MapNames.Polus => previous == SystemTypes.Specimens || RoomGoal == SystemTypes.Specimens,
            (MapNames)6 => (previous == (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast) ^ (RoomGoal == (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast),
            _ => false
        };

        if (involvesDecontamination)
        {
            int decontaminationTime = Options.ChangeDecontaminationTime.GetBool()
                ? map == MapNames.Polus
                    ? Options.DecontaminationTimeOnPolus.GetInt() + Options.DecontaminationDoorOpenTimeOnPolus.GetInt()
                    : Options.DecontaminationTimeOnMiraHQ.GetInt() + Options.DecontaminationDoorOpenTimeOnMiraHQ.GetInt()
                : 6;

            if (SubmergedCompatibility.IsSubmerged()) decontaminationTime = 3;
            time += decontaminationTime;
        }

        switch (map)
        {
            case MapNames.Fungle when RoomGoal == SystemTypes.Laboratory || previous == SystemTypes.Laboratory:
                time += (int)(8 / speed);
                break;
            case MapNames.Polus when (RoomGoal == SystemTypes.Laboratory && previous is not SystemTypes.Storage and not SystemTypes.Specimens and not SystemTypes.Office) || (previous == SystemTypes.Laboratory && RoomGoal is not SystemTypes.Office and not SystemTypes.Storage and not SystemTypes.Electrical and not SystemTypes.Specimens):
                time -= (int)(5 * speed);
                break;
            case MapNames.Airship when previous == SystemTypes.GapRoom:
                time *= RoomGoal switch
                {
                    SystemTypes.MeetingRoom => 6,
                    SystemTypes.Brig or SystemTypes.VaultRoom or SystemTypes.Records or SystemTypes.Showers or SystemTypes.Lounge => 3,
                    SystemTypes.Engine or SystemTypes.CargoBay or SystemTypes.Medical => 2,
                    _ => 1
                };
                break;
        }

        var maxTime = (int)Math.Ceiling((map is MapNames.Skeld or MapNames.Dleks ? 25 : 32) / speed);
        time = Math.Clamp((int)Math.Round(time * GlobalTimeMultiplier.GetFloat()), 6, maxTime);
        TimeLimitEndTS = Utils.TimeStamp + time;
        Logger.Info($"Starting a new round - Goal = from: {Translator.GetString(previous.ToString())} ({previous}), to: {Translator.GetString(RoomGoal.ToString())} ({RoomGoal}) - Time: {time}  ({map})", "RoomRush");
        Main.AllPlayerControls.Do(x => LocateArrow.RemoveAllTarget(x.PlayerId));
        if (DisplayArrowToRoom.GetBool()) Main.AllPlayerControls.Do(x => LocateArrow.Add(x.PlayerId, goalPos));

        Utils.NotifyRoles();
        Utils.DirtyName.Add(PlayerControl.LocalPlayer.PlayerId);

        if (WinByPointsInsteadOfDeaths.GetBool())
        {
            Logger.Info($"Points: {string.Join(", ", Points.Select(x => $"{Main.AllPlayerNames[x.Key]}: {x.Value}"))}", "RoomRush");

            if (Utils.DoRPC)
            {
                MessageWriter w = Utils.CreateRPC(CustomRPC.RoomRushDataSync);
                w.WritePacked(3);
                w.WritePacked(Points.Count);

                foreach ((byte key, int value) in Points)
                {
                    w.Write(key);
                    w.WritePacked(value);
                }

                Utils.EndRPC(w);
            }
        }
    }

    public static string GetSuffix(PlayerControl seer)
    {
        if (!GameGoing || Main.HasJustStarted || seer == null) return string.Empty;

        StringBuilder sb = new();
        bool dead = !seer.IsAlive();
        bool done = dead || DonePlayers.Contains(seer.PlayerId);
        Color color = done ? Color.green : Color.yellow;

        if (DisplayRoomName.GetBool()) sb.Append(Utils.ColorString(color, Translator.GetString(RoomGoal.ToString())) + "\n");
        if (DisplayArrowToRoom.GetBool()) sb.Append(Utils.ColorString(color, LocateArrow.GetArrows(seer)) + "\n");

        color = done ? Color.white : Color.yellow;
        sb.Append(Utils.ColorString(color, (TimeLimitEndTS - Utils.TimeStamp).ToString()) + "\n");

        if (WinByPointsInsteadOfDeaths.GetBool() && Points.TryGetValue(seer.PlayerId, out int points))
        {
            sb.Append(string.Format(Translator.GetString("RR_Points"), points, PointsToWinValue));

            int highestPoints = Points.Values.Max();
            bool tie = Points.Values.Count(x => x == highestPoints) > 1;

            if (tie && highestPoints >= PointsToWinValue)
            {
                byte tieWith = Points.First(x => x.Key != seer.PlayerId && x.Value == highestPoints).Key;
                sb.Append("\n" + string.Format(Translator.GetString("RR_Tie"), tieWith.ColoredPlayerName()));
            }
            else
            {
                sb.Append("<size=80%>");
                byte first = Points.GetKeyByValue(highestPoints);
                if (first != seer.PlayerId) sb.Append("\n" + string.Format(Translator.GetString("RR_FirstPoints"), first.ColoredPlayerName(), highestPoints));
                else sb.Append("\n" + Translator.GetString("RR_YouAreFirst"));
                sb.Append("</size>");
            }
        }

        if (VentTimes.GetInt() == 0 || dead || seer.IsModdedClient()) return sb.ToString().Trim();

        sb.Append('\n');

        int vents = VentLimit.GetValueOrDefault(seer.PlayerId);
        sb.Append(string.Format(Translator.GetString("RR_VentsRemaining"), vents));

        return sb.ToString().Trim();
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                PointsToWinValue = PointsToWin.GetInt() * Main.AllAlivePlayerControls.Length;
                int ventLimit = VentTimes.GetInt();
                VentLimit = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, _ => ventLimit);
                if (WinByPointsInsteadOfDeaths.GetBool()) Points = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, _ => 0);
                break;
            case 2:
                int limit = reader.ReadPackedInt32();
                byte id = reader.ReadByte();
                VentLimit[id] = limit;
                break;
            case 3:
                int count = reader.ReadPackedInt32();

                for (var i = 0; i < count; i++)
                {
                    byte key = reader.ReadByte();
                    int value = reader.ReadPackedInt32();
                    Points[key] = value;
                }

                break;
        }
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        private static long LastUpdate = Utils.TimeStamp;

        public static void Postfix( /*PlayerControl __instance*/)
        {
            if (!GameGoing || Main.HasJustStarted || Options.CurrentGameMode != CustomGameMode.RoomRush || !AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || GameStates.IsEnded || !Main.IntroDestroyed) return;

            long now = Utils.TimeStamp;
            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            if (WinByPointsInsteadOfDeaths.GetBool())
            {
                int highestPoints = Points.Values.Max();
                bool tie = Points.Values.Count(x => x == highestPoints) > 1;

                if (!tie && highestPoints >= PointsToWinValue)
                {
                    byte winner = Points.GetKeyByValue(highestPoints);
                    Logger.Info($"{Main.AllPlayerNames[winner]} has reached the points goal, ending the game", "RoomRush");
                    CustomWinnerHolder.WinnerIds = [winner];
                    return;
                }
            }

            foreach (PlayerControl pc in aapc)
            {
                bool isInRoom = pc.IsInRoom(RoomGoal);

                if (!pc.inMovingPlat && !pc.inVent && isInRoom && RegisterHost(pc) && DonePlayers.Add(pc.PlayerId))
                {
                    Logger.Info($"{pc.GetRealName()} entered the correct room", "RoomRush");
                    pc.Notify($"<size=100%>{DonePlayers.Count}.</size>", 2f);
                    if (pc.AmOwner) Utils.DirtyName.Add(pc.PlayerId);

                    if (WinByPointsInsteadOfDeaths.GetBool())
                        Points[pc.PlayerId] += aapc.Length == 1 ? 1 : aapc.Length - DonePlayers.Count;

                    int setTo = TimeWhenFirstTwoPlayersEnterRoom.GetInt();
                    long remaining = TimeLimitEndTS - now;

                    if (DonePlayers.Count == 2 && setTo < remaining && !DontLowerTimeLimitWhenTwoPlayersEnterCorrectRoom.GetBool())
                    {
                        Logger.Info($"Two players entered the correct room, setting the timer to {setTo}", "RoomRush");
                        TimeLimitEndTS = now + setTo;
                        LastUpdate = now;

                        if (aapc.Length == 2 && pc.AmOwner)
                            Achievements.Type.WheresTheBlueShell.CompleteAfterGameEnd();
                    }

                    if (DonePlayers.Count == aapc.Length - 1 && !DontKillLastPlayer.GetBool())
                    {
                        PlayerControl last = aapc.First(x => !DonePlayers.Contains(x.PlayerId));
                        Logger.Info($"All players entered the correct room except one, killing the last player ({last.GetRealName()})", "RoomRush");
                        last.Notify(Translator.GetString("RR_YouWereLast"));

                        if (WinByPointsInsteadOfDeaths.GetBool()) last.TP(DonePlayers.RandomElement().GetPlayer());
                        else last.Suicide();

                        StartNewRound();
                        return;
                    }
                }
                else if (!isInRoom && !DontKillPlayersOutsideRoomWhenTimeRunsOut.GetBool() && DonePlayers.Remove(pc.PlayerId) && WinByPointsInsteadOfDeaths.GetBool())
                    Points[pc.PlayerId] -= aapc.Length - DonePlayers.Count;
            }

            if (LastUpdate != now)
            {
                Utils.NotifyRoles();
                LastUpdate = now;
            }

            if (TimeLimitEndTS > now) return;

            Logger.Info("Time is up, killing everyone who didn't enter the correct room", "RoomRush");
            PlayerControl[] lateAapc = Main.AllAlivePlayerControls;
            PlayerControl[] playersOutsideRoom = lateAapc.ExceptBy(DonePlayers, x => x.PlayerId).ToArray();
            bool everyoneDies = playersOutsideRoom.Length == lateAapc.Length;

            if (WinByPointsInsteadOfDeaths.GetBool())
            {
                Vector2 location = everyoneDies ? Map.Positions.GetValueOrDefault(RoomGoal, RoomGoal.GetRoomClass().transform.position) : DonePlayers.RandomElement().GetPlayer().Pos();
                playersOutsideRoom.MassTP(location);
            }
            else
            {
                playersOutsideRoom.Do(x => x.Suicide());
                if (everyoneDies) CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
            }

            StartNewRound();

            if (playersOutsideRoom.Any(x => x.AmOwner))
                Achievements.Type.OutOfTime.Complete();
        }

        private static readonly Stopwatch HostRegisterTimer = new();

        private static bool RegisterHost(PlayerControl pc)
        {
            if (!pc.AmOwner || DonePlayers.Contains(pc.PlayerId)) return true;

            if (HostRegisterTimer.IsRunning)
            {
                bool registerHost = HostRegisterTimer.ElapsedMilliseconds > AmongUsClient.Instance.Ping * 2;
                if (registerHost) HostRegisterTimer.Reset();
                return registerHost;
            }

            HostRegisterTimer.Restart();
            return false;
        }
    }
}

public class RRPlayer : RoleBase
{
    public override bool IsEnable => Options.CurrentGameMode == CustomGameMode.RoomRush;

    public override void Init() { }

    public override void Add(byte playerId) { }

    public override void SetupCustomOption() { }

    public override void OnExitVent(PlayerControl pc, Vent vent)
    {
        RoomRush.VentLimit[pc.PlayerId]--;
        int newLimit = RoomRush.VentLimit[pc.PlayerId];
        Utils.SendRPC(CustomRPC.RoomRushDataSync, 2, newLimit, pc.PlayerId);
        if (newLimit <= 0) pc.RpcSetRoleGlobal(RoleTypes.Crewmate);
    }
}