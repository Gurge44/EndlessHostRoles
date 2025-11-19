using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR;

public static class Mingle
{
    public static readonly HashSet<string> HasPlayedFCs = [];

    public static bool GameGoing;
    public static DateTime GameStartDateTime;
    public static Dictionary<SystemTypes, int> RequiredPlayerCount = [];
    public static HashSet<SystemTypes> AllRooms = [];
    public static long TimeEndTS;
    public static long LastUpdateTS;
    public static int Time;

    public static int TimeLimit;
    public static int TimeDecreaseOnNoDeath;
    public static int ExtraTimeOnAirship;
    public static int ExtraTimeOnFungle;
    public static bool DisplayCurrentPlayerCountInEachRoom;
    public static int MinTime;
    public static int MaxRequiredPlayersPerRoom;
    public static int MaxWinningPlayers;
    
    public static OptionItem TimeLimitOption;
    public static OptionItem TimeDecreaseOnNoDeathOption;
    public static OptionItem ExtraTimeOnAirshipOption;
    public static OptionItem ExtraTimeOnFungleOption;
    public static OptionItem DisplayCurrentPlayerCountInEachRoomOption;
    public static OptionItem MinTimeOption;
    public static OptionItem MaxRequiredPlayersPerRoomOption;
    public static OptionItem MaxWinningPlayersOption;
    
    public static void SetupCustomOption()
    {
        var id = 69_224_001;
        Color color = Utils.GetRoleColor(CustomRoles.MinglePlayer);
        const CustomGameMode gameMode = CustomGameMode.Mingle;
        const TabGroup tab = TabGroup.GameSettings;
        
        TimeLimitOption = new IntegerOptionItem(id++, "Mingle.TimeLimitOption", new(1, 300, 1), 60, tab)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);
        
        TimeDecreaseOnNoDeathOption = new IntegerOptionItem(id++, "Mingle.TimeDecreaseOnNoDeathOption", new(0, 60, 1), 10, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);
        
        ExtraTimeOnAirshipOption = new IntegerOptionItem(id++, "Mingle.ExtraTimeOnAirshipOption", new(0, 300, 1), 10, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);
        
        ExtraTimeOnFungleOption = new IntegerOptionItem(id++, "Mingle.ExtraTimeOnFungleOption", new(0, 300, 1), 5, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);
        
        DisplayCurrentPlayerCountInEachRoomOption = new BooleanOptionItem(id++, "Mingle.DisplayCurrentPlayerCountInEachRoomOption", true, tab)
            .SetColor(color)
            .SetGameMode(gameMode);
        
        MinTimeOption = new IntegerOptionItem(id++, "Mingle.MinTimeOption", new(1, 300, 1), 10, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);
        
        MaxRequiredPlayersPerRoomOption = new IntegerOptionItem(id++, "Mingle.MaxRequiredPlayersPerRoomOption", new(1, 30, 1), 10, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Players);
        
        MaxWinningPlayersOption = new IntegerOptionItem(id, "Mingle.MaxWinningPlayersOption", new(1, 10, 1), 1, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Players);
    }

    public static string GetSuffix(PlayerControl seer)
    {
        return seer.IsHost() ? string.Empty : GetRoomsInfo(seer, false);
    }

    public static int GetSurvivalTime(byte id)
    {
        if (!Main.PlayerStates.TryGetValue(id, out PlayerState state) || ChatCommands.Spectators.Contains(id) || (id == 0 && Main.GM.Value) || state.deathReason == PlayerState.DeathReason.Disconnected) return -1;

        if (!state.IsDead) return 0;

        DateTime died = state.RealKiller.TimeStamp;
        TimeSpan time = died - GameStartDateTime;
        return (int)time.TotalSeconds;
    }

    public static bool CheckGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;
        if (GameStates.IsEnded || !GameGoing || TimeEndTS > Utils.TimeStamp) return false;
        PlayerControl[] aapc = Main.AllAlivePlayerControls;

        switch (aapc.Length)
        {
            case 1:
                PlayerControl winner = aapc[0];
                Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "Mingle");
                CustomWinnerHolder.WinnerIds = [winner.PlayerId];
                Main.DoBlockNameChange = true;
                return true;
            case 0:
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                Logger.Warn("No players alive. Force ending the game", "Mingle");
                return true;
            case var p when p <= MaxWinningPlayers:
                CustomWinnerHolder.WinnerIds = aapc.Select(x => x.PlayerId).ToHashSet();
                Logger.Info($"Winners: {string.Join(", ", aapc.Select(x => x.GetRealName().RemoveHtmlTags()))}", "Mingle");
                Main.DoBlockNameChange = true;
                return true;
            default:
                return false;
        }
    }
    
    public static string GetTaskBarText()
    {
        return GetRoomsInfo(PlayerControl.LocalPlayer, true);
    }

    private static string GetRoomsInfo(PlayerControl pc, bool hud)
    {
        StringBuilder sb = new("<#ffffff><size=90%>");
        PlainShipRoom plainShipRoom = pc.GetPlainShipRoom();

        foreach ((SystemTypes room, int required) in RequiredPlayerCount)
        {
            int count = GetNumPlayersInRoom(room);

            if (plainShipRoom != null && plainShipRoom.RoomId == room)
                sb.Append(hud ? "➡ " : "<u>");
            
            if (DisplayCurrentPlayerCountInEachRoom)
            {
                string color = count > required ? "FF4647" : count == required ? "91FF65" : "FFDE59";
                sb.Append($"<#{color}>");
            }

            sb.Append(Translator.GetString(room.ToString()));
            sb.Append(':');
            sb.Append(' ');
            
            if (DisplayCurrentPlayerCountInEachRoom)
            {
                sb.Append(count);
                sb.Append(" / ");
            }
            
            sb.Append(required);
            
            if (DisplayCurrentPlayerCountInEachRoom)
            {
                sb.Append(' ');
                sb.Append(count > required ? "＋ <#ff0000>╳</color>" : count == required ? "＝ <#00ff00>✓</color>" : "－ <#ff0000>╳</color>");
                sb.Append("</color>");
            }

            if (plainShipRoom != null && plainShipRoom.RoomId == room && !hud)
                sb.Append("</u>");

            sb.Append('\n');
        }
        
        sb.Append("</size>");

        long timeLeft = TimeEndTS - Utils.TimeStamp;

        if (timeLeft >= 0)
        {
            sb.Append('\n');
            if (hud) sb.Append("<b><size=200%>");
            sb.Append(timeLeft);
            if (hud) sb.Append("</size></b>");
        }
        
        if (plainShipRoom == null || !RequiredPlayerCount.ContainsKey(plainShipRoom.RoomId))
        {
            sb.Append('\n');
            sb.Append("<#ffff00><size=70%>");
            sb.Append('⚠');
            sb.Append(' ');
            sb.Append(Translator.GetString("Mingle.NotInRequiredRoom"));
            sb.Append(' ');
            sb.Append('⚠');
            sb.Append("</size></color>");
        }

        return sb.ToString();
    }

    public static System.Collections.IEnumerator GameStart()
    {
        GameGoing = false;
        
        TimeLimit = TimeLimitOption.GetInt();
        TimeDecreaseOnNoDeath = TimeDecreaseOnNoDeathOption.GetInt();
        ExtraTimeOnAirship = ExtraTimeOnAirshipOption.GetInt();
        ExtraTimeOnFungle = ExtraTimeOnFungleOption.GetInt();
        DisplayCurrentPlayerCountInEachRoom = DisplayCurrentPlayerCountInEachRoomOption.GetBool();
        MinTime = MinTimeOption.GetInt();
        MaxRequiredPlayersPerRoom = MaxRequiredPlayersPerRoomOption.GetInt();
        MaxWinningPlayers = MaxWinningPlayersOption.GetInt();

        RequiredPlayerCount = [];
        int extraTime = Main.CurrentMap switch
        {
            MapNames.Airship => ExtraTimeOnAirship,
            MapNames.Fungle => ExtraTimeOnFungle,
            _ => 0
        };
        Time = TimeLimit + extraTime;
        MinTime += extraTime;
        TimeEndTS = 0;
        LastUpdateTS = 0;
        NameNotifyManager.Reset();

        AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
        AllRooms.Remove(SystemTypes.Hallway);
        AllRooms.Remove(SystemTypes.Outside);
        AllRooms.Remove(SystemTypes.Ventilation);
        AllRooms.RemoveWhere(x => x.ToString().Contains("Decontamination"));
        if (SubmergedCompatibility.IsSubmerged()) AllRooms.RemoveWhere(x => (byte)x > 135);

        yield return new WaitForSeconds(Main.CurrentMap == MapNames.Airship ? 8f : 3f);

        List<PlayerControl> players = Main.AllAlivePlayerControls.ToList();
        if (Main.GM.Value) players.RemoveAll(x => x.IsHost());
        if (ChatCommands.Spectators.Count > 0) players.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));
        
        bool showTutorial = players.ExceptBy(HasPlayedFCs, x => x.FriendCode).Count() > players.Count / 2;

        if (showTutorial)
        {
            players.NotifyPlayers("<#ffffff>" + Translator.GetString("Mingle.Tutorial"), 100f);
            yield return new WaitForSeconds(12f);
            NameNotifyManager.Reset();
        }

        for (var i = 3; i > 0; i--)
        {
            NameNotifyManager.Reset();
            players.NotifyPlayers(string.Format(Translator.GetString("RR_ReadyQM"), i));
            yield return new WaitForSeconds(1f);
        }
        
        NameNotifyManager.Reset();
        StartNewRound();
        GameGoing = true;
        GameStartDateTime = DateTime.Now;
    }

    private static void StartNewRound()
    {
        if (GameStates.IsEnded) return;

        RequiredPlayerCount = [];
        int playerCount = Main.AllAlivePlayerControls.Length;
        bool last2 = playerCount <= 2;

        while (playerCount > 0)
        {
            var room = AllRooms.Except(RequiredPlayerCount.Keys).RandomElement();
            var count = last2 ? 1 : IRandom.Instance.Next(1, Math.Min(playerCount, MaxRequiredPlayersPerRoom) + 1);
            if (count >= playerCount) count = Math.Max(1, playerCount - 1);
            RequiredPlayerCount[room] = count;
            playerCount -= count;
        }

        TimeEndTS = Utils.TimeStamp + Time;
        Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
        Utils.SyncAllSettings();
    }

    private static void KillPlayers()
    {
        var aapc = Main.AllAlivePlayerControls;
        Dictionary<PlayerControl, SystemTypes> playerRooms = aapc.Select(x => (pc: x, room: x.GetPlainShipRoom())).ToDictionary(x => x.pc, x => x.room == null ? SystemTypes.Outside : x.room.RoomId);
        Dictionary<SystemTypes, int> playerCount = [];
        HashSet<PlayerControl> toKill = [];

        foreach ((PlayerControl pc, SystemTypes room) in playerRooms)
        {
            if (room == SystemTypes.Outside || !RequiredPlayerCount.ContainsKey(room))
                toKill.Add(pc);
            else if (!playerCount.TryAdd(room, 1))
                playerCount[room]++;
        }

        foreach ((SystemTypes room, int required) in RequiredPlayerCount)
        {
            int count = playerCount.GetValueOrDefault(room, 0);
            if (count == 0 || required == count) continue;
            playerRooms.DoIf(x => x.Value == room, x => toKill.Add(x.Key));
        }
        
        switch (toKill.Count)
        {
            case 0:
                Time = Math.Max(Time - TimeDecreaseOnNoDeath, MinTime);
                break;
            case var x when x == aapc.Length:
                Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
                Main.PlayerStates.Values.DoIf(s => !s.IsDead, s => s.RealKiller.TimeStamp = DateTime.Now);
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                GameGoing = false;
                break;
            default:
                toKill.Do(x => x.Suicide());
                break;
        }
    }
    
    private static int GetNumPlayersInRoom(SystemTypes room) => Main.AllAlivePlayerControls.Where(x => !x.inMovingPlat).Count(x => x.IsInRoom(room));

    public static void HandleDisconnect()
    {
        SystemTypes decreaseRoom = AllRooms.First(x => RequiredPlayerCount.TryGetValue(x, out var required) && GetNumPlayersInRoom(x) < required);
        
        if (RequiredPlayerCount[decreaseRoom] <= 1) RequiredPlayerCount.Remove(decreaseRoom);
        else RequiredPlayerCount[decreaseRoom]--;
    }

    public static class FixedUpdatePatch
    {
        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost || !Main.IntroDestroyed || !GameGoing || GameStates.IsEnded) return;
            
            long now = Utils.TimeStamp;
            if (LastUpdateTS == now) return;
            LastUpdateTS = now;

            if (TimeEndTS == now)
            {
                Main.AllPlayerSpeed.SetAllValues(Main.MinSpeed);
                Utils.SyncAllSettings();
            }
            else if (TimeEndTS < now)
            {
                KillPlayers();
                StartNewRound();
            }
            else
            {
                Dictionary<SystemTypes, int> numPlayersInRoom = Main.AllAlivePlayerControls.Select(x => (pc: x, room: x.GetPlainShipRoom())).GroupBy(x => x.room == null ? SystemTypes.Outside : x.room.RoomId).ToDictionary(x => x.Key, x => x.Count());
                
                if (RequiredPlayerCount.All(x => numPlayersInRoom.GetValueOrDefault(x.Key, 0) == x.Value))
                {
                    Main.AllAlivePlayerControls.NotifyPlayers(Utils.ColorString(Color.green, "✓"), 3f);
                    Time = Math.Max(Time - TimeDecreaseOnNoDeath, MinTime);
                    StartNewRound();
                    return;
                }
            }
            
            Utils.NotifyRoles();
        }
    }
}