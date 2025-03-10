using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace EHR;

public static class KingOfTheZones
{
    private static OptionItem NumTeams;
    private static OptionItem NumZones;
    private static OptionItem ZonesMove;
    private static OptionItem ZoneMoveTime;
    private static OptionItem AllZonesMoveAtOnce;
    private static OptionItem DowntimeAfterZoneMove;
    private static OptionItem RespawnTime;
    private static OptionItem TagCooldown;
    private static OptionItem GameEndsByPoints;
    private static OptionItem PointsToWin;
    private static OptionItem GameEndsByTimeLimit;
    private static OptionItem MaxGameLength;

    public static (Color Color, string Team) WinnerData = (Color.white, "No one wins");

    private static readonly Dictionary<MapNames, List<List<SystemTypes>>> DefaultZones = new()
    {
        [MapNames.Skeld] =
        [
            [SystemTypes.Cafeteria],
            [SystemTypes.LowerEngine, SystemTypes.Weapons],
            [SystemTypes.LowerEngine, SystemTypes.Admin, SystemTypes.Weapons],
            [SystemTypes.LowerEngine, SystemTypes.UpperEngine, SystemTypes.Weapons, SystemTypes.Shields]
        ],
        [MapNames.Mira] =
        [
            [SystemTypes.LockerRoom],
            [SystemTypes.Launchpad, SystemTypes.Balcony],
            [SystemTypes.Launchpad, SystemTypes.Greenhouse, SystemTypes.Balcony],
            [SystemTypes.Launchpad, SystemTypes.Reactor, SystemTypes.Greenhouse, SystemTypes.Balcony]
        ],
        [MapNames.Polus] =
        [
            [SystemTypes.Office],
            [SystemTypes.LifeSupp, SystemTypes.Laboratory],
            [SystemTypes.LifeSupp, SystemTypes.Office, SystemTypes.Laboratory],
            [SystemTypes.LifeSupp, SystemTypes.Electrical, SystemTypes.Laboratory, SystemTypes.Specimens]
        ],
        [MapNames.Airship] =
        [
            [SystemTypes.MainHall],
            [SystemTypes.CargoBay, SystemTypes.Cockpit],
            [SystemTypes.CargoBay, SystemTypes.MainHall, SystemTypes.Cockpit],
            [SystemTypes.Records, SystemTypes.Electrical, SystemTypes.Kitchen, SystemTypes.VaultRoom]
        ],
        [MapNames.Fungle] =
        [
            [SystemTypes.Greenhouse],
            [SystemTypes.Cafeteria, SystemTypes.Reactor],
            [SystemTypes.FishingDock, SystemTypes.Comms, SystemTypes.Reactor],
            [SystemTypes.FishingDock, SystemTypes.Dropship, SystemTypes.Comms, SystemTypes.Reactor]
        ]
    };

    private static Dictionary<byte, KOTZTeam> PlayerTeams = [];
    private static Dictionary<KOTZTeam, int> Points = [];
    private static Dictionary<SystemTypes, KOTZTeam> ZoneDomination = [];
    private static Dictionary<SystemTypes, long> ZoneMoveSchedules = [];
    private static List<SystemTypes> Zones = [];
    private static HashSet<SystemTypes> AllRooms = [];
    private static bool GameGoing;

    public static readonly HashSet<string> PlayedFCs = [];

    static KingOfTheZones()
    {
        DefaultZones[MapNames.Dleks] = DefaultZones[MapNames.Skeld];
    }

    public static void SetupCustomOption()
    {
        var id = 69_219_001;
        var color = ColorUtility.TryParseHtmlString("#ff0000", out Color c) ? c : Color.red;
        const CustomGameMode gameMode = CustomGameMode.KingOfTheZones;

        NumTeams = new IntegerOptionItem(id++, "KingOfTheZones.NumTeams", new(2, 4, 1), 2, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color);

        NumZones = new IntegerOptionItem(id++, "KingOfTheZones.NumZones", new(1, 4, 1), 1, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color);

        ZonesMove = new BooleanOptionItem(id++, "KingOfTheZones.ZonesMove", true, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color);

        ZoneMoveTime = new IntegerOptionItem(id++, "KingOfTheZones.ZoneMoveTime", new(10, 120, 5), 30, TabGroup.GameSettings)
            .SetParent(ZonesMove)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);

        AllZonesMoveAtOnce = new BooleanOptionItem(id++, "KingOfTheZones.AllZonesMoveAtOnce", true, TabGroup.GameSettings)
            .SetParent(ZonesMove)
            .SetGameMode(gameMode)
            .SetColor(color);

        DowntimeAfterZoneMove = new IntegerOptionItem(id++, "KingOfTheZones.DowntimeAfterZoneMove", new(0, 30, 1), 5, TabGroup.GameSettings)
            .SetParent(ZonesMove)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);

        RespawnTime = new IntegerOptionItem(id++, "KingOfTheZones.RespawnTime", new(1, 30, 1), 5, TabGroup.GameSettings)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);

        TagCooldown = new IntegerOptionItem(id++, "KingOfTheZones.TagCooldown", new(1, 60, 1), 10, TabGroup.GameSettings)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);

        GameEndsByPoints = new BooleanOptionItem(id++, "KingOfTheZones.GameEndsByPoints", true, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color)
            .RegisterUpdateValueEvent((_, _) =>
            {
                if (!GameEndsByTimeLimit.GetBool() && !GameEndsByPoints.GetBool())
                    GameEndsByTimeLimit.SetValue(1);
            });

        PointsToWin = new IntegerOptionItem(id++, "KingOfTheZones.PointsToWin", new(30, 300, 10), 120, TabGroup.GameSettings)
            .SetParent(GameEndsByPoints)
            .SetGameMode(gameMode)
            .SetColor(color);

        GameEndsByTimeLimit = new BooleanOptionItem(id++, "KingOfTheZones.GameEndsByTimeLimit", true, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color)
            .RegisterUpdateValueEvent((_, _) =>
            {
                if (!GameEndsByTimeLimit.GetBool() && !GameEndsByPoints.GetBool())
                    GameEndsByPoints.SetValue(1);
            });

        MaxGameLength = new IntegerOptionItem(id, "KingOfTheZones.MaxGameLength", new(10, 900, 10), 300, TabGroup.GameSettings)
            .SetParent(GameEndsByTimeLimit)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);
    }

    static Color GetColor(this KOTZTeam team)
    {
        return team switch
        {
            KOTZTeam.Red => Color.red,
            KOTZTeam.Yellow => Color.yellow,
            KOTZTeam.Blue => Color.cyan,
            KOTZTeam.Green => Color.green,
            _ => Color.white
        };
    }

    static byte GetColorId(this KOTZTeam team)
    {
        return team switch
        {
            KOTZTeam.Red => 0,
            KOTZTeam.Yellow => 5,
            KOTZTeam.Blue => 1,
            KOTZTeam.Green => 11,
            _ => 7
        };
    }

    public static void Init()
    {
        AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
        AllRooms.Remove(SystemTypes.Hallway);
        AllRooms.Remove(SystemTypes.Outside);
        AllRooms.RemoveWhere(x => x.ToString().Contains("Decontamination"));

        Zones = DefaultZones[Main.CurrentMap][NumZones.GetInt() - 1];

        KOTZTeam[] teams = Enum.GetValues<KOTZTeam>();
        Points = teams.ToDictionary(x => x, _ => 0);
        ZoneDomination = Zones.ToDictionary(x => x, _ => KOTZTeam.None);
        ZoneMoveSchedules = [];

        var parts = Main.PlayerStates.Keys.Partition(NumTeams.GetInt());
        PlayerTeams = parts.Zip(teams, (players, team) => players.ToDictionary(x => x, _ => team)).SelectMany(x => x).ToDictionary(x => x.Key, x => x.Value);

        Main.AllPlayerSpeed.SetAllValues(Main.MinSpeed);
        GameGoing = false;
    }

    public static IEnumerator GameStart()
    {
        if (!CustomGameMode.KingOfTheZones.IsActiveOrIntegrated()) yield break;

        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        bool showTutorial = aapc.ExceptBy(PlayedFCs, x => x.FriendCode).Count() >= aapc.Length / 3;
        NameNotifyManager.Reset();

        int teams = NumTeams.GetInt();
        int zones = NumZones.GetInt();

        foreach ((byte id, KOTZTeam team) in PlayerTeams)
        {
            string name = Main.AllPlayerNames[id];
            var skin = new NetworkedPlayerInfo.PlayerOutfit().Set(name, team.GetColorId(), "", "", "", "", "");
            PlayerControl player = id.GetPlayer();
            Utils.RpcChangeSkin(player, skin);

            var notify = Utils.ColorString(team.GetColor(), Translator.GetString($"KOTZ.Notify.AssignedToTeam.{team}"));

            if (showTutorial)
            {
                string tutorial = string.Format(Translator.GetString("KOTZ.Notify.Tutorial.Basics"), teams, zones);
                notify = notify.Insert(0, tutorial + "\n\n");
            }

            player.Notify(notify, 100f);
            Logger.Info($"{name} assigned to {team} team", "KOTZ");
        }

        yield return new WaitForSeconds(showTutorial ? 17f : 4f);
        NameNotifyManager.Reset();

        int tagCooldown = TagCooldown.GetInt();
        int respawnTime = RespawnTime.GetInt();

        if (showTutorial)
        {
            int points = PointsToWin.GetInt();
            int timeLimit = MaxGameLength.GetInt();
            string mins = $"{(timeLimit / 60):N0}";
            string secs = $"{(timeLimit % 60):N0}";

            bool endByPoints = GameEndsByPoints.GetBool();
            bool endByTime = GameEndsByTimeLimit.GetBool();

            string gameEnd = (endByPoints, endByTime) switch
            {
                (true, true) => string.Format(Translator.GetString("KOTZ.Notify.Tutorial.GameEnd.PointsOrTime"), points, mins, secs),
                (true, false) => string.Format(Translator.GetString("KOTZ.Notify.Tutorial.GameEnd.PointsOnly"), points),
                (false, true) => string.Format(Translator.GetString("KOTZ.Notify.Tutorial.GameEnd.TimeOnly"), mins, secs),
                _ => string.Empty
            };

            aapc.Do(x => x.Notify(gameEnd, 100f));
            yield return new WaitForSeconds(endByPoints && endByTime ? 10f : 7f);
            NameNotifyManager.Reset();

            string tags = string.Format(Translator.GetString("KOTZ.Notify.Tutorial.Tagging"), tagCooldown, respawnTime);

            aapc.Do(x => x.Notify(tags, 100f));
            yield return new WaitForSeconds(8f);
            NameNotifyManager.Reset();

            if (ZonesMove.GetBool())
            {
                string zoneMovement = string.Format(Translator.GetString(AllZonesMoveAtOnce.GetBool() ? "KOTZ.Notify.Tutorial.ZonesMoving.AllAtOnce" : "KOTZ.Notify.Tutorial.ZonesMoving.Separately"), ZoneMoveTime.GetInt());

                aapc.Do(x => x.Notify(zoneMovement, 100f));
                yield return new WaitForSeconds(6f);
                NameNotifyManager.Reset();

                int downTime = DowntimeAfterZoneMove.GetInt();

                if (downTime > 0)
                {
                    string downTimeInfo = string.Format(Translator.GetString("KOTZ.Notify.Tutorial.ZonesMoving.Downtime"), downTime);

                    aapc.Do(x => x.Notify(downTimeInfo, 100f));
                    yield return new WaitForSeconds(8f);
                    NameNotifyManager.Reset();
                }
            }
        }
        else
        {
            string info = string.Format(Translator.GetString("KOTZ.Notify.ShortIntro"), teams, zones, tagCooldown, respawnTime);

            aapc.Do(x => x.Notify(info, 100f));
            yield return new WaitForSeconds(7f);
            NameNotifyManager.Reset();
        }

        Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
        Utils.MarkEveryoneDirtySettings();

        GameGoing = true;
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        // Tagging
    }

    public static string GetSuffix(PlayerControl seer)
    {
        // Show all necessary info
    }

    public static string GetStatistics(byte id)
    {
        // For the end screen summary
    }

    public static int GetZoneTime(byte id)
    {
        // Data used to sort the teams' order on the end screen summary
    }

    enum KOTZTeam
    {
        None,
        Red,
        Yellow,
        Blue,
        Green
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    private static class FixedUpdatePatch
    {
        private static long LastUpdate;

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix(PlayerControl __instance)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || !CustomGameMode.KingOfTheZones.IsActiveOrIntegrated() || !Main.IntroDestroyed || !GameGoing || __instance.PlayerId == 255) return;

            long now = Utils.TimeStamp;
            if (LastUpdate == now) return;
            LastUpdate = now;

            if (ZonesMove.GetBool())
            {
                int moveTime = ZoneMoveTime.GetInt();
                double timePart = moveTime / NumZones.GetFloat();
                bool atOnce = AllZonesMoveAtOnce.GetBool();

                if (ZoneMoveSchedules.Count == 0)
                {
                    for (var index = 0; index < Zones.Count; index++)
                    {
                        SystemTypes zone = Zones[index];
                        int timeFromNow = atOnce ? moveTime : (int)Math.Ceiling(timePart * (index + 1));
                        ZoneMoveSchedules[zone] = now + timeFromNow;
                    }
                }
                else
                {
                    List<SystemTypes> redoKeys = [];

                    foreach ((SystemTypes zone, long moveTS) in ZoneMoveSchedules)
                    {
                        if (moveTS >= now)
                        {
                            Zones.Add(AllRooms.Except(Zones).RandomElement());
                            Zones.Remove(zone);
                            redoKeys.Add(zone);
                        }
                    }

                    for (var index = 0; index < redoKeys.Count; index++)
                    {
                        SystemTypes zone = redoKeys[index];
                        int timeFromNow = atOnce ? moveTime : (int)Math.Ceiling(timePart * (index + 1));
                        ZoneMoveSchedules[zone] = now + timeFromNow + DowntimeAfterZoneMove.GetInt(); // WARNING FOR FUTURE SELF: CAREFUL WHEN MAKING THE SUFFIX!!!! IF THE TIMER IS HIGHER THAN THE MOVING PERIOD, IT MEANS THE ROOM IS IN DOWNTIME
                    }
                }
            }

            Dictionary<byte, PlainShipRoom> playerRooms = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, x => x.GetPlainShipRoom());
            Dictionary<SystemTypes, List<(KOTZTeam Team, int Length)>> roomPlayersAndTeams = Zones.ToDictionary(x => x, x => playerRooms.Where(k => k.Value != null && k.Value.RoomId == x).GroupBy(k => PlayerTeams[k.Key]).Select(k => (Team: k.Key, Length: k.Count())).OrderByDescending(k => k.Length).ToList());

            foreach ((SystemTypes zone, List<(KOTZTeam Team, int Length)> teamPlayers) in roomPlayersAndTeams)
            {
                ZoneDomination[zone] = teamPlayers.Count switch
                {
                    0 => KOTZTeam.None,
                    1 => teamPlayers[0].Team,
                    _ when teamPlayers[0].Length == teamPlayers[1].Length => KOTZTeam.None,
                    _ => teamPlayers[0].Team
                };
            }

            ZoneDomination.Values.DoIf(x => x != KOTZTeam.None, x => Points[x]++);

            Utils.NotifyRoles();
        }
    }
}