using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public static class KingOfTheZones
{
    private static OptionItem AutoSetNumTeams;
    private static OptionItem PreferNumTeams;
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
    private static OptionItem SpawnProtectionTime;

    private static readonly string[] PreferNumTeamsOptions = ["KOTZ.PNTO.Less", "KOTZ.PNTO.More"];

    public static (UnityEngine.Color Color, string Team) WinnerData = (Color.white, "No one wins");

    private static readonly Dictionary<MapNames, List<List<SystemTypes>>> DefaultZones = new()
    {
        [MapNames.Skeld] =
        [
            [SystemTypes.Cafeteria],
            [SystemTypes.LowerEngine, SystemTypes.Weapons],
            [SystemTypes.LowerEngine, SystemTypes.Admin, SystemTypes.Weapons],
            [SystemTypes.LowerEngine, SystemTypes.UpperEngine, SystemTypes.Weapons, SystemTypes.Shields]
        ],
        [MapNames.MiraHQ] =
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
        ],
        [(MapNames)6] =
        [
            [(SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral],
            [(SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerCentral],
            [(SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Research, SystemTypes.Admin, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperLobby],
            [(SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerCentral, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast, SystemTypes.Cafeteria]
        ]
    };

    private static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> DefaultOutfits = [];
    private static Dictionary<byte, KOTZTeam> PlayerTeams = [];
    private static Dictionary<byte, long> RespawnTimes = [];
    private static Dictionary<byte, long> SpawnProtectionTimes = [];
    private static Dictionary<KOTZTeam, int> Points = [];
    private static Dictionary<SystemTypes, KOTZTeam> ZoneDomination = [];
    private static Dictionary<SystemTypes, long> ZoneMoveSchedules = [];
    private static Dictionary<SystemTypes, long> ZoneDowntimeExpire = [];
    private static List<SystemTypes> Zones = [];
    private static HashSet<SystemTypes> AllRooms = [];
    private static int TimeLeft;
    private static long GameStartTS;
    private static string LastShortInfo = string.Empty;

    public static bool GameGoing;
    public static readonly HashSet<string> PlayedFCs = [];

    static KingOfTheZones()
    {
        DefaultZones[MapNames.Dleks] = DefaultZones[MapNames.Skeld];
    }

    public static int KCD => TagCooldown.GetInt();
    public static int MaxGameTime => GameEndsByTimeLimit.GetBool() ? MaxGameLength.GetInt() : int.MaxValue;
    public static int MaxGameTimeByPoints => GameEndsByPoints.GetBool() ? PointsToWin.GetInt() * 2 : MaxGameTime;

    public static void SetupCustomOption()
    {
        var id = 69_219_001;
        Color color = ColorUtility.TryParseHtmlString("#ff0000", out Color c) ? c : Color.red;
        const CustomGameMode gameMode = CustomGameMode.KingOfTheZones;

        AutoSetNumTeams = new BooleanOptionItem(id++, "KingOfTheZones.AutoSetNumTeams", true, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color);

        PreferNumTeams = new StringOptionItem(id++, "KingOfTheZones.PreferNumTeams", PreferNumTeamsOptions, 1, TabGroup.GameSettings)
            .SetParent(AutoSetNumTeams)
            .SetGameMode(gameMode)
            .SetColor(color);

        NumTeams = new IntegerOptionItem(id++, "KingOfTheZones.NumTeams", new(2, 7, 1), 2, TabGroup.GameSettings)
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
            .RegisterUpdateValueEvent((_, _, _) =>
            {
                if (!GameEndsByTimeLimit.GetBool() && !GameEndsByPoints.GetBool())
                    GameEndsByTimeLimit.SetValue(1);
            })
            .SetRunEventOnLoad(true);

        PointsToWin = new IntegerOptionItem(id++, "KingOfTheZones.PointsToWin", new(30, 300, 10), 120, TabGroup.GameSettings)
            .SetParent(GameEndsByPoints)
            .SetGameMode(gameMode)
            .SetColor(color);

        GameEndsByTimeLimit = new BooleanOptionItem(id++, "KingOfTheZones.GameEndsByTimeLimit", true, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color)
            .RegisterUpdateValueEvent((_, _, _) =>
            {
                if (!GameEndsByTimeLimit.GetBool() && !GameEndsByPoints.GetBool())
                    GameEndsByPoints.SetValue(1);
            })
            .SetRunEventOnLoad(true);

        MaxGameLength = new IntegerOptionItem(id++, "KingOfTheZones.MaxGameLength", new(10, 900, 10), 300, TabGroup.GameSettings)
            .SetParent(GameEndsByTimeLimit)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);

        SpawnProtectionTime = new IntegerOptionItem(id, "KingOfTheZones.SpawnProtectionTime", new(0, 30, 1), 5, TabGroup.GameSettings)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);
    }

    private static Color GetColor(this KOTZTeam team)
    {
        return team switch
        {
            KOTZTeam.Red => Color.red,
            KOTZTeam.Yellow => Color.yellow,
            KOTZTeam.Blue => Color.cyan,
            KOTZTeam.Green => Color.green,
            KOTZTeam.Tan => Palette.Brown,
            KOTZTeam.Rose => Color.magenta,
            KOTZTeam.Orange => Palette.Orange,
            _ => Color.white
        };
    }

    private static byte GetColorId(this KOTZTeam team)
    {
        return team switch
        {
            KOTZTeam.Red => 0,
            KOTZTeam.Yellow => 5,
            KOTZTeam.Blue => 10,
            KOTZTeam.Green => 11,
            KOTZTeam.Tan => 16,
            KOTZTeam.Rose => 13,
            KOTZTeam.Orange => 4,
            _ => 7
        };
    }

    public static void Init()
    {
        WinnerData = (Color.white, "No one wins");

        AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
        AllRooms.Remove(SystemTypes.Hallway);
        AllRooms.Remove(SystemTypes.Outside);
        AllRooms.Remove(SystemTypes.Ventilation);
        AllRooms.RemoveWhere(x => x.ToString().Contains("Decontamination"));
        if (SubmergedCompatibility.IsSubmerged()) AllRooms.RemoveWhere(x => (byte)x > 135);

        Zones = DefaultZones[Main.CurrentMap][NumZones.GetInt() - 1];

        KOTZTeam[] teams = Enum.GetValues<KOTZTeam>();
        Points = teams.ToDictionary(x => x, _ => 0);
        ZoneDomination = Zones.ToDictionary(x => x, _ => KOTZTeam.None);
        ZoneMoveSchedules = [];
        ZoneDowntimeExpire = [];
        RespawnTimes = [];
        SpawnProtectionTimes = [];
        TimeLeft = 0;

        DefaultOutfits = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, x => x.Data.DefaultOutfit);

        List<byte> ids = Main.PlayerStates.Keys.Shuffle();
        if (Main.GM.Value) ids.Remove(0);
        ids.RemoveAll(ChatCommands.Spectators.Contains);

        int numPlayers = ids.Count;

        if (AutoSetNumTeams.GetBool() && numPlayers % NumTeams.GetInt() != 0)
        {
            if (numPlayers is 2 or 3 or 5 or 7) NumTeams.SetValue(numPlayers - 2);
            else if (numPlayers <= 1 || Enumerable.Range(2, (int)Math.Sqrt(numPlayers) - 1).Any(x => numPlayers % x == 0))
            {
                List<int> divisors = Enumerable.Range(2, 6).Where(x => numPlayers % x == 0).ToList();

                if (divisors.Count > 0)
                {
                    int selectedTeamCount = PreferNumTeams.GetValue() == 0 ? divisors.Min() : divisors.Max();
                    NumTeams.SetValue(selectedTeamCount - 2);
                    Logger.Msg($"Auto set teams to {selectedTeamCount}", "KOTZ");
                }
            }
        }

        PlayerTeams = ids
            .Partition(NumTeams.GetInt())
            .Zip(teams[1..], (players, team) => players.ToDictionary(x => x, _ => team))
            .SelectMany(x => x)
            .ToDictionary(x => x.Key, x => x.Value);

        float normalSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        if (Main.GM.Value) Main.AllPlayerSpeed[0] = normalSpeed;
        ChatCommands.Spectators.Do(x => Main.AllPlayerSpeed[x] = normalSpeed);

        GameGoing = false;
        SendRPC();
    }

    public static IEnumerator GameStart()
    {
        if (Options.CurrentGameMode != CustomGameMode.KingOfTheZones) yield break;

        yield return new WaitForSecondsRealtime(3f);

        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        bool showTutorial = aapc.ExceptBy(PlayedFCs, x => x.FriendCode).Count() > aapc.Length / 2;
        NameNotifyManager.Reset();

        int teams = NumTeams.GetInt();
        int zones = NumZones.GetInt();

        foreach ((byte id, KOTZTeam team) in PlayerTeams)
        {
            try
            {
                PlayerControl player = id.GetPlayer();
                if (player == null) continue;
                string name = Main.AllPlayerNames[id];

                var writer = CustomRpcSender.Create("KOTZ.GameStart.TeamAssignmentNotifies", SendOption.Reliable);
                var hasData = false;

                try
                {
                    player.RpcSetColor(team.GetColorId());
                }
                catch (Exception e) { Utils.ThrowException(e); }

                string notify = Utils.ColorString(team.GetColor(), GetString($"KOTZ.Notify.AssignedToTeam.{team}"));

                if (showTutorial)
                {
                    string tutorial = string.Format(GetString("KOTZ.Notify.Tutorial.Basics"), teams, zones);
                    notify = notify.Insert(0, tutorial + "\n\n");
                }

                hasData |= writer.Notify(player, $"<#ffffff>{notify}</color>", 100f);
                Logger.Info($"{name} assigned to {team} team", "KOTZ");

                if (!player.AmOwner)
                {
                    try
                    {
                        int targetClientId = player.OwnerId;
                        PlayerTeams.DoIf(
                            x => x.Key != id && x.Value == team,
                            x => writer.RpcSetRole(x.Key.GetPlayer(), RoleTypes.Impostor, targetClientId, changeRoleMap: true));

                        hasData = true;
                    }
                    catch (Exception e) { Utils.ThrowException(e); }
                }

                writer.SendMessage(!hasData);
            }
            catch (Exception e) { Utils.ThrowException(e); }

            yield return null;
        }

        yield return new WaitForSeconds(showTutorial ? 8f : 2f);
        NameNotifyManager.Reset();
        if (!GameStates.InGame || !Main.IntroDestroyed) goto End;

        int tagCooldown = TagCooldown.GetInt();
        int respawnTime = RespawnTime.GetInt();

        if (showTutorial)
        {
            int points = PointsToWin.GetInt();
            int timeLimit = MaxGameLength.GetInt();
            var mins = $"{timeLimit / 60:N0}";
            var secs = $"{timeLimit % 60:N0}";

            bool endByPoints = GameEndsByPoints.GetBool();
            bool endByTime = GameEndsByTimeLimit.GetBool();

            string gameEnd = (endByPoints, endByTime) switch
            {
                (true, true) => string.Format(GetString("KOTZ.Notify.Tutorial.GameEnd.PointsOrTime"), points, mins, secs),
                (true, false) => string.Format(GetString("KOTZ.Notify.Tutorial.GameEnd.PointsOnly"), points),
                (false, true) => string.Format(GetString("KOTZ.Notify.Tutorial.GameEnd.TimeOnly"), mins, secs),
                _ => string.Empty
            };

            aapc.NotifyPlayers($"<#ffffff>{gameEnd}</color>", 100f);
            yield return new WaitForSeconds(4f);
            NameNotifyManager.Reset();
            if (!GameStates.InGame || !Main.IntroDestroyed) goto End;

            string tags = string.Format(GetString("KOTZ.Notify.Tutorial.Tagging"), tagCooldown, respawnTime);

            aapc.NotifyPlayers($"<#ffffff>{tags}</color>", 100f);
            yield return new WaitForSeconds(4f);
            NameNotifyManager.Reset();
            if (!GameStates.InGame || !Main.IntroDestroyed) goto End;

            if (ZonesMove.GetBool())
            {
                string zoneMovement = string.Format(GetString(AllZonesMoveAtOnce.GetBool() ? "KOTZ.Notify.Tutorial.ZonesMoving.AllAtOnce" : "KOTZ.Notify.Tutorial.ZonesMoving.Separately"), ZoneMoveTime.GetInt());

                aapc.NotifyPlayers($"<#ffffff>{zoneMovement}</color>", 100f);
                yield return new WaitForSeconds(4f);
                NameNotifyManager.Reset();
                if (!GameStates.InGame || !Main.IntroDestroyed) goto End;

                int downTime = DowntimeAfterZoneMove.GetInt();

                if (downTime > 0)
                {
                    string downTimeInfo = string.Format(GetString("KOTZ.Notify.Tutorial.ZonesMoving.Downtime"), downTime);

                    aapc.NotifyPlayers($"<#ffffff>{downTimeInfo}</color>", 100f);
                    yield return new WaitForSeconds(4f);
                    NameNotifyManager.Reset();
                    if (!GameStates.InGame || !Main.IntroDestroyed) goto End;
                }
            }

            yield return StartingCountdown();
        }
        else
        {
            string info = string.Format(GetString("KOTZ.Notify.ShortIntro"), teams, zones, tagCooldown, respawnTime);

            if (LastShortInfo != info)
            {
                LastShortInfo = info;
                aapc.NotifyPlayers($"<#ffffff>{info}</color>", 100f);
                yield return new WaitForSeconds(4f);
                NameNotifyManager.Reset();
                if (!GameStates.InGame || !Main.IntroDestroyed) goto End;
            }
            else
                yield return StartingCountdown();
        }

        KeyValuePair<SystemTypes, Vector2>[] spawnsConst = RandomSpawn.SpawnMap.GetSpawnMap().Positions.ExceptBy(Zones, x => x.Key).ToArray();
        List<KeyValuePair<SystemTypes, Vector2>> spawns = spawnsConst.ToList();

        foreach (PlayerControl player in aapc)
        {
            try
            {
                player.SetKillCooldown(GetKillCooldown(player));

                KeyValuePair<SystemTypes, Vector2> spawn = spawns.RandomElement();
                player.TP(spawn.Value);
                spawns.RemoveAll(x => x.Key == spawn.Key);

                if (spawns.Count == 0) spawns = spawnsConst.ToList();
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        TimeLeft = GameEndsByTimeLimit.GetBool() ? MaxGameLength.GetInt() : 0;
        GameStartTS = Utils.TimeStamp;

        GameGoing = true;

        End:

        Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
        Utils.SyncAllSettings();

        yield return Utils.NotifyEveryoneAsync(1, false);
        yield break;

        IEnumerator StartingCountdown()
        {
            for (var i = 3; i > 0; i--)
            {
                NameNotifyManager.Reset();
                aapc.NotifyPlayers($"<#ffffff>{string.Format(GetString("RR_ReadyQM"), i)}</color>", 100f);
                yield return new WaitForSeconds(1f);
            }

            NameNotifyManager.Reset();
        }
    }

    public static bool GetNameColor(PlayerControl pc, ref string color)
    {
        if (!GameGoing || !Main.IntroDestroyed) return false;

        if (PlayerTeams.TryGetValue(pc.PlayerId, out KOTZTeam team))
        {
            color = team switch
            {
                KOTZTeam.Red => "#ff0000",
                KOTZTeam.Yellow => "#ffff00",
                KOTZTeam.Blue => "#00ffff",
                KOTZTeam.Green => "#00ff00",
                KOTZTeam.Tan => "#A88E8E",
                KOTZTeam.Rose => "#FAB8EB",
                KOTZTeam.Orange => "#ff8800",
                _ => "#ffffff"
            };

            return true;
        }

        return false;
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!Main.IntroDestroyed || !GameGoing || PlayerTeams[killer.PlayerId] == PlayerTeams[target.PlayerId] || SpawnProtectionTimes.ContainsKey(target.PlayerId) || new[] { killer, target }.Any(x => RespawnTimes.ContainsKey(x.PlayerId))) return;

        killer.SetKillCooldown(GetKillCooldown(killer));
        target.SetKillCooldown(GetKillCooldown(target));

        RespawnTimes[target.PlayerId] = Utils.TimeStamp + RespawnTime.GetInt() + 1;
        target.ExileTemporarily();
    }

    private static float GetKillCooldown(PlayerControl player)
    {
        float cd = TagCooldown.GetInt();
        if (!PlayerTeams.TryGetValue(player.PlayerId, out KOTZTeam playerTeam)) return cd;

        if (ZoneDomination.ContainsValue(playerTeam))
            cd *= 1.5f;

        Dictionary<KOTZTeam, byte[]> teamPlayers = PlayerTeams.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.Select(g => g.Key).ToArray());
        int maxTeamSize = teamPlayers.Values.Max(x => x.Length);
        int teamSize = teamPlayers[playerTeam].Length;

        if (maxTeamSize > teamSize)
            cd /= 2f;

        return cd;
    }

    public static string GetSuffix(PlayerControl seer)
    {
        if (!Main.IntroDestroyed || !GameGoing) return string.Empty;

        StringBuilder sb = new();
        long now = Utils.TimeStamp;
        bool justStarted = GameStartTS + 15 > now;

        if (RespawnTimes.TryGetValue(seer.PlayerId, out long respawnTS))
            sb.AppendLine(string.Format(GetString("KOTZ.Suffix.RespawnTime"), respawnTS - now));
        else if (SpawnProtectionTimes.TryGetValue(seer.PlayerId, out long protectionEndTS))
            sb.AppendLine(string.Format(GetString("KOTZ.Suffix.SpawnProtectionTime"), protectionEndTS - now));

        PlainShipRoom room = seer.GetPlainShipRoom();
        SystemTypes zone = room == null ? SystemTypes.Hallway : room.RoomId;

        if (justStarted) sb.Append(GetString(Zones.Count == 1 ? "KOTZ.SuffixHelp.Zones.Single" : "KOTZ.SuffixHelp.Zones.Plural"));
        sb.AppendLine(string.Join(" | ", Zones.Select(x => Utils.ColorString(ZoneDomination[x].GetColor(), (zone == x ? "<u>" : string.Empty) + GetString($"{x}") + (zone == x ? "</u>" : string.Empty) + (ZoneMoveSchedules.TryGetValue(x, out long moveTS) ? $"<size=80%> {(ZoneDowntimeExpire.TryGetValue(x, out long downtimeEndTS) ? Utils.ColorString(Color.gray, $"{downtimeEndTS - now}") : $"<#ffff44>{moveTS - now}</color>")}</size>" : string.Empty)))));

        if (justStarted) sb.Append(GetString("KOTZ.SuffixHelp.Points"));
        sb.AppendLine(string.Join(" | ", Points.IntersectBy(PlayerTeams.Values, x => x.Key).Select(x => Utils.ColorString(x.Key.GetColor(), $"{x.Value}"))));

        int highestPoints = Points.Values.Max();
        bool tie = Points.Values.Count(x => x == highestPoints) > 1;
        bool end = (GameEndsByTimeLimit.GetBool() && TimeLeft <= 0) || (GameEndsByPoints.GetBool() && highestPoints >= PointsToWin.GetInt());

        if (tie && end)
        {
            string tieTeams = string.Join(' ', Points.Where(x => x.Value == highestPoints).Select(x => Utils.ColorString(x.Key.GetColor(), "\u25a0")));
            sb.AppendLine($"<size=80%>{string.Format(GetString("KOTZ.Suffix.Tie"), tieTeams)}</size>");
        }
        else
        {
            if (GameEndsByTimeLimit.GetBool())
            {
                if (justStarted) sb.Append(GetString("KOTZ.SuffixHelp.GameEndTimer"));
                sb.AppendLine($"<size=80%>{TimeLeft / 60:N0}:{TimeLeft % 60:00}</size>");
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
        reason = GameOverReason.ImpostorsByKill;

        if (!Main.IntroDestroyed) return false;

        PlayerControl[] aapc = Main.AllAlivePlayerControls.Concat(ExtendedPlayerControl.TempExiled.ToValidPlayers()).ToArray();

        switch (aapc.Length)
        {
            case 0:
            {
                ResetSkins();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                CustomWinnerHolder.WinnerIds = Main.PlayerStates.Keys.ToHashSet();
                reason = GameOverReason.CrewmateDisconnect;
                return true;
            }
            case 1:
            {
                ResetSkins();
                KOTZTeam winner = PlayerTeams[aapc[0].PlayerId];
                Color color = winner.GetColor();
                CustomWinnerHolder.WinnerIds = PlayerTeams.Where(x => x.Value == winner).Select(x => x.Key).ToHashSet();
                WinnerData = (color, Utils.ColorString(color, GetString($"KOTZ.EndScreen.Winner.{winner}")));
                SendRPC();
                return true;
            }
            default:
            {
                if (Options.IntegrateNaturalDisasters.GetBool() && Enum.GetValues<KOTZTeam>().FindFirst(x => aapc.All(p => PlayerTeams[p.PlayerId] == x), out KOTZTeam team))
                {
                    ResetSkins();
                    Color color = team.GetColor();
                    CustomWinnerHolder.WinnerIds = PlayerTeams.Where(x => x.Value == team).Select(x => x.Key).ToHashSet();
                    WinnerData = (color, Utils.ColorString(color, GetString($"KOTZ.EndScreen.Winner.{team}")));
                    SendRPC();
                    return true;
                }
                
                return WinnerData.Team != "No one wins";
            }
        }

        void ResetSkins() => DefaultOutfits.Select(x => (pc: x.Key.GetPlayer(), outfit: x.Value)).DoIf(x => x.pc != null && x.outfit != null, x => Utils.RpcChangeSkin(x.pc, x.outfit));
    }

    public static bool IsNotInLocalPlayersTeam(PlayerControl pc)
    {
        return !PlayerTeams.TryGetValue(pc.PlayerId, out KOTZTeam team) || !PlayerTeams.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out KOTZTeam lpTeam) || team != lpTeam;
    }

    private static void SendRPC()
    {
        var w = Utils.CreateRPC(CustomRPC.KOTZSync);

        w.Write(WinnerData.Color);
        w.Write(WinnerData.Team);

        w.Write(Points.Count);

        foreach ((KOTZTeam team, int points) in Points)
        {
            w.Write((int)team);
            w.Write(points);
        }

        Utils.EndRPC(w);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        WinnerData.Color = reader.ReadColor();
        WinnerData.Team = reader.ReadString();

        Loop.Times(reader.ReadInt32(), _ => Points[(KOTZTeam)reader.ReadInt32()] = reader.ReadInt32());
    }

    private enum KOTZTeam
    {
        None,
        Red,
        Yellow,
        Blue,
        Green,
        Tan,
        Rose,
        Orange
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        private static long LastUpdate;

        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || Options.CurrentGameMode != CustomGameMode.KingOfTheZones || !Main.IntroDestroyed || !GameGoing) return;

            long now = Utils.TimeStamp;
            if (LastUpdate == now) return;
            LastUpdate = now;

            try
            {
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

                            try
                            {
                                SystemTypes newZone = AllRooms.Except(Zones).RandomElement();

                                int index = Zones.IndexOf(zone);
                                Zones.RemoveAt(index);
                                Zones.Insert(index, newZone);

                                added.Add(newZone);

                                removed.Add(zone);
                                ZoneDomination.Remove(zone);

                                Logger.Info($"Zone moved: {zone} => {newZone}", "KOTZ");
                            }
                            catch (Exception e) { Utils.ThrowException(e); }
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
            }
            catch (Exception e) { Utils.ThrowException(e); }

            try
            {
                if (SpawnProtectionTimes.Count > 0)
                {
                    List<byte> toRemove = [];

                    foreach ((byte id, long protectionTS) in SpawnProtectionTimes)
                    {
                        if (protectionTS > now) continue;
                        toRemove.Add(id);
                    }

                    toRemove.ForEach(x => SpawnProtectionTimes.Remove(x));
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }

            try
            {
                if (RespawnTimes.Count > 0)
                {
                    List<byte> toRemove = [];

                    foreach ((byte id, long respawnTS) in RespawnTimes)
                    {
                        if (respawnTS > now) continue;

                        try
                        {
                            PlayerControl player = id.GetPlayer();
                            if (player == null) continue;

                            player.ReviveFromTemporaryExile();
                            player.TP(RandomSpawn.SpawnMap.GetSpawnMap().Positions.ExceptBy(Zones, x => x.Key).RandomElement().Value);
                            LateTask.New(() => player.SetKillCooldown(GetKillCooldown(player)), 1.5f, log: false);
                            RPC.PlaySoundRPC(player.PlayerId, Sounds.SpawnSound);
                            Utils.NotifyRoles(SpecifyTarget: player, SendOption: SendOption.None);

                            int spawnProtectionTime = SpawnProtectionTime.GetInt();
                            if (spawnProtectionTime > 0) SpawnProtectionTimes[id] = now + spawnProtectionTime;
                        }
                        catch (Exception e) { Utils.ThrowException(e); }

                        toRemove.Add(id);

                        Logger.Info($"{Main.AllPlayerNames[id]} respawned", "KOTZ");
                    }

                    toRemove.ForEach(x => RespawnTimes.Remove(x));
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }

            try
            {
                Dictionary<byte, PlainShipRoom> playerRooms = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, x => x.GetPlainShipRoom());
                Dictionary<SystemTypes, List<(KOTZTeam Team, int Length)>> roomPlayersAndTeams = Zones.ToDictionary(x => x, x => playerRooms.ExceptBy(RespawnTimes.Keys, k => k.Key).Where(k => k.Value != null && k.Value.RoomId == x).GroupBy(k => PlayerTeams[k.Key]).Select(k => (Team: k.Key, Length: k.Count())).OrderByDescending(k => k.Length).ToList());

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
            }
            catch (Exception e) { Utils.ThrowException(e); }

            foreach (PlayerControl player in Main.AllAlivePlayerControls)
            {
                try
                {
                    byte colorId = PlayerTeams[player.PlayerId].GetColorId();
                    if (player.CurrentOutfit.ColorId == colorId) continue;

                    player.RpcSetColor(colorId);
                }
                catch (Exception e) { Utils.ThrowException(e); }
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
                KOTZTeam winner = Points.GetKeyByValue(highestPoints);
                CustomWinnerHolder.WinnerIds = PlayerTeams.Where(x => x.Value == winner).Select(x => x.Key).ToHashSet();
                Color color = winner.GetColor();
                WinnerData = (color, Utils.ColorString(color, GetString($"KOTZ.EndScreen.Winner.{winner}")));
                SendRPC();

                Logger.Info($"Game ended. Winner: {winner} team", "KOTZ");
            }
        }
    }
}
