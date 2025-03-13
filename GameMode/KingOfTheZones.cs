﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;
using static EHR.Translator;

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

    private static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> DefaultOutfits = [];
    private static Dictionary<byte, KOTZTeam> PlayerTeams = [];
    private static Dictionary<byte, long> RespawnTimes = [];
    private static Dictionary<KOTZTeam, int> Points = [];
    private static Dictionary<SystemTypes, KOTZTeam> ZoneDomination = [];
    private static Dictionary<SystemTypes, long> ZoneMoveSchedules = [];
    private static Dictionary<SystemTypes, long> ZoneDowntimeExpire = [];
    private static List<SystemTypes> Zones = [];
    private static HashSet<SystemTypes> AllRooms = [];
    private static int TimeLeft;
    private static long GameStartTS;

    public static bool GameGoing;
    public static readonly HashSet<string> PlayedFCs = [];

    static KingOfTheZones()
    {
        DefaultZones[MapNames.Dleks] = DefaultZones[MapNames.Skeld];
    }

    public static int KCD => TagCooldown.GetInt();

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
        WinnerData = (Color.white, "No one wins");

        AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
        AllRooms.Remove(SystemTypes.Hallway);
        AllRooms.Remove(SystemTypes.Outside);
        AllRooms.RemoveWhere(x => x.ToString().Contains("Decontamination"));

        Zones = DefaultZones[Main.CurrentMap][NumZones.GetInt() - 1];

        KOTZTeam[] teams = Enum.GetValues<KOTZTeam>();
        Points = teams.ToDictionary(x => x, _ => 0);
        ZoneDomination = Zones.ToDictionary(x => x, _ => KOTZTeam.None);
        ZoneMoveSchedules = [];
        ZoneDowntimeExpire = [];
        RespawnTimes = [];
        TimeLeft = 0;

        DefaultOutfits = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, x => x.Data.DefaultOutfit);

        List<byte> ids = Main.PlayerStates.Keys.Shuffle();
        if (Main.GM.Value) ids.Remove(0);
        ids.RemoveAll(ChatCommands.Spectators.Contains);

        PlayerTeams = ids
            .Partition(NumTeams.GetInt())
            .Zip(teams[1..], (players, team) => players.ToDictionary(x => x, _ => team))
            .SelectMany(x => x)
            .ToDictionary(x => x.Key, x => x.Value);

        Main.AllPlayerSpeed.SetAllValues(Main.MinSpeed);
        float normalSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        if (Main.GM.Value) Main.AllPlayerSpeed[0] = normalSpeed;
        ChatCommands.Spectators.Do(x => Main.AllPlayerSpeed[x] = normalSpeed);

        GameGoing = false;
    }

    public static IEnumerator GameStart()
    {
        if (!CustomGameMode.KingOfTheZones.IsActiveOrIntegrated()) yield break;

        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        bool showTutorial = aapc.ExceptBy(PlayedFCs, x => x.FriendCode).Count() > aapc.Length / 2;
        NameNotifyManager.Reset();

        int teams = NumTeams.GetInt();
        int zones = NumZones.GetInt();

        foreach ((byte id, KOTZTeam team) in PlayerTeams)
        {
            PlayerControl player = id.GetPlayer();

            string name = Main.AllPlayerNames[id];
            var skin = new NetworkedPlayerInfo.PlayerOutfit().Set(name, team.GetColorId(), "", "", "", "", "");
            Utils.RpcChangeSkin(player, skin);

            var notify = Utils.ColorString(team.GetColor(), GetString($"KOTZ.Notify.AssignedToTeam.{team}"));

            if (showTutorial)
            {
                string tutorial = string.Format(GetString("KOTZ.Notify.Tutorial.Basics"), teams, zones);
                notify = notify.Insert(0, tutorial + "\n\n");
            }

            player.Notify($"<#ffffff>{notify}</color>", 100f);
            Logger.Info($"{name} assigned to {team} team", "KOTZ");

            yield return null;
        }

        yield return new WaitForSeconds(showTutorial ? 15f : 2f);
        NameNotifyManager.Reset();
        if (!GameStates.InGame || !Main.IntroDestroyed) goto End;

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
                (true, true) => string.Format(GetString("KOTZ.Notify.Tutorial.GameEnd.PointsOrTime"), points, mins, secs),
                (true, false) => string.Format(GetString("KOTZ.Notify.Tutorial.GameEnd.PointsOnly"), points),
                (false, true) => string.Format(GetString("KOTZ.Notify.Tutorial.GameEnd.TimeOnly"), mins, secs),
                _ => string.Empty
            };

            aapc.Do(x => x.Notify($"<#ffffff>{gameEnd}</color>", 100f));
            yield return new WaitForSeconds(endByPoints && endByTime ? 9f : 6f);
            NameNotifyManager.Reset();
            if (!GameStates.InGame || !Main.IntroDestroyed) goto End;

            string tags = string.Format(GetString("KOTZ.Notify.Tutorial.Tagging"), tagCooldown, respawnTime);

            aapc.Do(x => x.Notify($"<#ffffff>{tags}</color>", 100f));
            yield return new WaitForSeconds(7f);
            NameNotifyManager.Reset();
            if (!GameStates.InGame || !Main.IntroDestroyed) goto End;

            if (ZonesMove.GetBool())
            {
                string zoneMovement = string.Format(GetString(AllZonesMoveAtOnce.GetBool() ? "KOTZ.Notify.Tutorial.ZonesMoving.AllAtOnce" : "KOTZ.Notify.Tutorial.ZonesMoving.Separately"), ZoneMoveTime.GetInt());

                aapc.Do(x => x.Notify($"<#ffffff>{zoneMovement}</color>", 100f));
                yield return new WaitForSeconds(5f);
                NameNotifyManager.Reset();
                if (!GameStates.InGame || !Main.IntroDestroyed) goto End;

                int downTime = DowntimeAfterZoneMove.GetInt();

                if (downTime > 0)
                {
                    string downTimeInfo = string.Format(GetString("KOTZ.Notify.Tutorial.ZonesMoving.Downtime"), downTime);

                    aapc.Do(x => x.Notify($"<#ffffff>{downTimeInfo}</color>", 100f));
                    yield return new WaitForSeconds(7f);
                    NameNotifyManager.Reset();
                    if (!GameStates.InGame || !Main.IntroDestroyed) goto End;
                }
            }

            for (var i = 3; i > 0; i--)
            {
                int time = i;
                NameNotifyManager.Reset();
                aapc.Do(x => x.Notify($"<#ffffff>{string.Format(GetString("RR_ReadyQM"), time)}</color>", 100f));
                yield return new WaitForSeconds(1f);
            }
        }
        else
        {
            string info = string.Format(GetString("KOTZ.Notify.ShortIntro"), teams, zones, tagCooldown, respawnTime);

            aapc.Do(x => x.Notify($"<#ffffff>{info}</color>", 100f));
            yield return new WaitForSeconds(7f);
            NameNotifyManager.Reset();
            if (!GameStates.InGame || !Main.IntroDestroyed) goto End;
        }

        var spawnsConst = RandomSpawn.SpawnMap.GetSpawnMap().Positions.ExceptBy(Zones, x => x.Key).ToArray();
        var spawns = spawnsConst.ToList();

        foreach (PlayerControl player in aapc)
        {
            player.SetKillCooldown(TagCooldown.GetInt());

            var spawn = spawns.RandomElement();
            player.TP(spawn.Value);
            spawns.RemoveAll(x => x.Key == spawn.Key);

            if (spawns.Count == 0) spawns = spawnsConst.ToList();
        }

        TimeLeft = GameEndsByTimeLimit.GetBool() ? MaxGameLength.GetInt() : 0;
        GameStartTS = Utils.TimeStamp;

        GameGoing = true;

        End:

        Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
        Utils.SyncAllSettings();

        yield return Utils.NotifyEveryoneAsync(speed: 1, noCache: false);
    }

    public static bool GetNameColor(PlayerControl pc, ref string color)
    {
        if (!GameGoing || !Main.IntroDestroyed) return false;

        if (PlayerTeams.TryGetValue(pc.PlayerId, out var team))
        {
            color = team switch
            {
                KOTZTeam.Red => "#ff0000",
                KOTZTeam.Yellow => "#ffff00",
                KOTZTeam.Blue => "#00ffff",
                KOTZTeam.Green => "#00ff00",
                _ => "#ffffff"
            };

            return true;
        }

        return false;
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        target.KillFlash();
        foreach (var player in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.GM)))
        {
            player.KillFlash();
        }
        
        if (!Main.IntroDestroyed || !GameGoing || PlayerTeams[killer.PlayerId] == PlayerTeams[target.PlayerId]) return;

        PlayerControl[] pcs = [killer, target];
        if (pcs.Any(x => RespawnTimes.ContainsKey(x.PlayerId))) return;

        pcs.Do(x => x.SetKillCooldown(TagCooldown.GetInt()));

        RespawnTimes[target.PlayerId] = Utils.TimeStamp + RespawnTime.GetInt() + 1;
        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
        target.MarkDirtySettings();
        target.TP(Pelican.GetBlackRoomPS());
    }

    public static string GetSuffix(PlayerControl seer)
    {
        if (!Main.IntroDestroyed || !GameGoing) return string.Empty;

        StringBuilder sb = new();
        long now = Utils.TimeStamp;
        bool justStarted = GameStartTS + 15 > now;

        if (RespawnTimes.TryGetValue(seer.PlayerId, out var respawnTS))
            sb.AppendLine(string.Format(GetString("KOTZ.Suffix.RespawnTime"), respawnTS - now));

        var room = seer.GetPlainShipRoom();
        var zone = room == null ? SystemTypes.Hallway : room.RoomId;

        if (justStarted) sb.Append(GetString(Zones.Count == 1 ? "KOTZ.SuffixHelp.Zones.Single" : "KOTZ.SuffixHelp.Zones.Plural"));
        sb.AppendLine(string.Join(" | ", Zones.Select(x => Utils.ColorString(ZoneDomination[x].GetColor(), (zone == x ? "<u>" : string.Empty) + GetString($"{x}") + (zone == x ? "</u>" : string.Empty) + (ZoneMoveSchedules.TryGetValue(x, out var moveTS) ? $"<size=80%> {(ZoneDowntimeExpire.TryGetValue(x, out var downtimeEndTS) ? Utils.ColorString(Color.gray, $"{downtimeEndTS - now}") : $"<#ffff44>{moveTS - now}</color>")}</size>" : string.Empty)))));

        if (justStarted) sb.Append(GetString("KOTZ.SuffixHelp.Points"));
        sb.AppendLine(string.Join(" | ", Points.IntersectBy(PlayerTeams.Values, x => x.Key).Select(x => Utils.ColorString(x.Key.GetColor(), $"{x.Value}"))));

        int highestPoints = Points.Values.Max();
        bool tie = Points.Values.Count(x => x == highestPoints) > 1;
        bool end = (GameEndsByTimeLimit.GetBool() && TimeLeft <= 0) || (GameEndsByPoints.GetBool() && highestPoints >= PointsToWin.GetInt());

        if (tie && end)
        {
            var tieTeams = string.Join(' ', Points.Where(x => x.Value == highestPoints).Select(x => Utils.ColorString(x.Key.GetColor(), "\u25a0")));
            sb.AppendLine($"<size=80%>{string.Format(GetString("KOTZ.Suffix.Tie"), tieTeams)}</size>");
        }
        else
        {
            if (GameEndsByTimeLimit.GetBool())
            {
                if (justStarted) sb.Append(GetString("KOTZ.SuffixHelp.GameEndTimer"));
                sb.AppendLine($"<size=80%>{(TimeLeft / 60):N0}:{(TimeLeft % 60):00}</size>");
            }

            if (GameEndsByPoints.GetBool())
                sb.AppendLine($"<size=80%>{string.Format(GetString("KOTZ.Suffix.PointsToWin"), PointsToWin.GetInt())}</size>");
        }

        return sb.Insert(0, "<#ffffff>").Append("</color>").ToString().Trim();
    }

    public static string GetStatistics(byte id)
    {
        return string.Format(GetString("KOTZ.EndScreen.Statistics"), GetZoneTime(id));
    }

    public static int GetZoneTime(byte id)
    {
        try { return Points[PlayerTeams[id]]; }
        catch { return 0; }
    }

    public static bool CheckForGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorByKill;
        PlayerControl[] aapc = Main.AllAlivePlayerControls;

        if (!Main.IntroDestroyed) return false;

        switch (aapc.Length)
        {
            case 0:
                ResetSkins();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                CustomWinnerHolder.WinnerIds = Main.PlayerStates.Keys.ToHashSet();
                reason = GameOverReason.HumansDisconnect;
                return true;
            case 1:
                ResetSkins();
                var winner = PlayerTeams[aapc[0].PlayerId];
                var color = winner.GetColor();
                CustomWinnerHolder.WinnerIds = PlayerTeams.Where(x => x.Value == winner).Select(x => x.Key).ToHashSet();
                WinnerData = (color, Utils.ColorString(color, GetString($"KOTZ.EndScreen.Winner.{winner}")));
                return true;
            default:
                return WinnerData.Team != "No one wins";
        }

        void ResetSkins() => DefaultOutfits.Select(x => (pc: x.Key.GetPlayer(), outfit: x.Value)).DoIf(x => x.pc != null && x.outfit != null, x => Utils.RpcChangeSkin(x.pc, x.outfit));
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
        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || !CustomGameMode.KingOfTheZones.IsActiveOrIntegrated() || !Main.IntroDestroyed || !GameGoing) return;

            long now = Utils.TimeStamp;
            if (LastUpdate == now) return;
            LastUpdate = now;

            if (ZonesMove.GetBool())
            {
                int moveTime = ZoneMoveTime.GetInt();
                double timePart = moveTime / NumZones.GetFloat();
                bool atOnce = AllZonesMoveAtOnce.GetBool();
                int downtime = DowntimeAfterZoneMove.GetInt();

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
                    List<SystemTypes> added = [];
                    List<SystemTypes> removed = [];

                    foreach ((SystemTypes zone, long moveTS) in ZoneMoveSchedules)
                    {
                        if (moveTS > now) continue;

                        SystemTypes newZone = AllRooms.Except(Zones).RandomElement();

                        int index = Zones.IndexOf(zone);
                        Zones.RemoveAt(index);
                        Zones.Insert(index, newZone);

                        added.Add(newZone);

                        removed.Add(zone);
                        ZoneDomination.Remove(zone);

                        Logger.Info($"Zone moved: {zone} => {newZone}", "KOTZ");
                    }

                    removed.ForEach(x => ZoneMoveSchedules.Remove(x));

                    added.ForEach(x =>
                    {
                        ZoneMoveSchedules[x] = now + moveTime + downtime;
                        if (downtime > 0) ZoneDowntimeExpire[x] = now + downtime;
                    });
                }

                if (downtime > 0 && ZoneDowntimeExpire.Count > 0)
                {
                    List<SystemTypes> toRemove = [];

                    foreach ((SystemTypes zone, long downtimeExpire) in ZoneDowntimeExpire)
                    {
                        if (downtimeExpire > now) continue;
                        toRemove.Add(zone);
                    }

                    toRemove.ForEach(x => ZoneDowntimeExpire.Remove(x));
                }
            }

            if (RespawnTimes.Count > 0)
            {
                List<byte> toRemove = [];

                foreach ((byte id, long respawnTS) in RespawnTimes)
                {
                    if (respawnTS > now) continue;

                    PlayerControl player = id.GetPlayer();
                    Main.AllPlayerSpeed[id] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                    RPC.PlaySoundRPC(player.PlayerId, Sounds.TaskComplete);
                    player.TP(RandomSpawn.SpawnMap.GetSpawnMap().Positions.ExceptBy(Zones, x => x.Key).RandomElement().Value);
                    player.SetKillCooldown(TagCooldown.GetInt());
                    player.MarkDirtySettings();
                    toRemove.Add(id);

                    Logger.Info($"{Main.AllPlayerNames[id]} respawned", "KOTZ");
                }

                toRemove.ForEach(x => RespawnTimes.Remove(x));
            }

            Dictionary<byte, PlainShipRoom> playerRooms = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, x => x.GetPlainShipRoom());
            Dictionary<SystemTypes, List<(KOTZTeam Team, int Length)>> roomPlayersAndTeams = Zones.ToDictionary(x => x, x => playerRooms.Where(k => k.Value != null && k.Value.RoomId == x).GroupBy(k => PlayerTeams[k.Key]).Select(k => (Team: k.Key, Length: k.Count())).OrderByDescending(k => k.Length).ToList());

            foreach ((SystemTypes zone, List<(KOTZTeam Team, int Length)> teamPlayers) in roomPlayersAndTeams)
            {
                ZoneDomination[zone] = ZoneDowntimeExpire.ContainsKey(zone)
                    ? KOTZTeam.None
                    : teamPlayers.Count switch
                    {
                        0 => KOTZTeam.None,
                        1 => teamPlayers[0].Team,
                        _ when teamPlayers[0].Length == teamPlayers[1].Length => KOTZTeam.None,
                        _ => teamPlayers[0].Team
                    };
            }

            ZoneDomination.Values.DoIf(x => x != KOTZTeam.None, x => Points[x]++);

            Logger.Info($"Zone domination: {string.Join(", ", ZoneDomination.Select(x => $"{x.Key} = {x.Value}"))}", "KOTZ");

            foreach (PlayerControl player in Main.AllAlivePlayerControls)
            {
                byte colorId = PlayerTeams[player.PlayerId].GetColorId();
                if (player.Data.DefaultOutfit.ColorId == colorId) continue;

                string name = Main.AllPlayerNames[player.PlayerId];
                Utils.RpcChangeSkin(player, new NetworkedPlayerInfo.PlayerOutfit().Set(name, colorId, "", "", "", "", ""));
            }

            int highestPoints = Points.Values.Max();
            bool tie = Points.Values.Count(x => x == highestPoints) > 1;

            if (GameEndsByTimeLimit.GetBool())
            {
                if (TimeLeft > 0) TimeLeft--;
                if (TimeLeft <= 0) EndGame();
            }

            if (GameEndsByPoints.GetBool() && highestPoints >= PointsToWin.GetInt())
                EndGame();

            Utils.NotifyRoles();

            return;

            void EndGame()
            {
                if (tie) return;
                GameGoing = false;
                DefaultOutfits.Select(x => (pc: x.Key.GetPlayer(), outfit: x.Value)).DoIf(x => x.pc != null && x.outfit != null, x => Utils.RpcChangeSkin(x.pc, x.outfit));
                var winner = Points.GetKeyByValue(highestPoints);
                CustomWinnerHolder.WinnerIds = PlayerTeams.Where(x => x.Value == winner).Select(x => x.Key).ToHashSet();
                Color color = winner.GetColor();
                WinnerData = (color, Utils.ColorString(color, GetString($"KOTZ.EndScreen.Winner.{winner}")));

                Logger.Info($"Game ended. Winner: {winner} team", "KOTZ");
            }
        }
    }
}
