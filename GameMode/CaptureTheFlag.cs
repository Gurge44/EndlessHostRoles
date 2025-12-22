using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR;

public static class CaptureTheFlag
{
    private static OptionItem AlertTeamMembersOfFlagTaken;
    private static OptionItem ArrowToEnemyFlagCarrier;
    private static OptionItem AlertTeamMembersOfEnemyFlagTaken;
    private static OptionItem ArrowToOwnFlagCarrier;
    private static OptionItem TaggedPlayersGet;
    private static OptionItem BackTime;
    private static OptionItem WhenFlagCarrierGetsTagged;
    private static OptionItem SpeedReductionForFlagCarrier;
    private static OptionItem TagCooldown;
    private static OptionItem GameEndCriteria;
    private static OptionItem RoundsToPlay;
    private static OptionItem PointsToWin;
    private static OptionItem TimeLimit;
    private static OptionItem FlagPickupRange;

    private static readonly string[] TaggedPlayersGetOptions =
    [
        "CTF_TaggedPlayersGet.SentBackToBase",
        "CTF_TaggedPlayersGet.Killed",
        "CTF_TaggedPlayersGet.TemporarilyOut"
    ];

    private static readonly string[] WhenFlagCarrierGetsTaggedOptions =
    [
        "CTF_WhenFlagCarrierGetsTagged.FlagIsDropped",
        "CTF_WhenFlagCarrierGetsTagged.FlagIsReturned"
    ];

    private static readonly string[] GameEndCriteriaOptions =
    [
        "CTF_GameEndCriteria.PlayXRounds",
        "CTF_GameEndCriteria.FirstToXPoints",
        "CTF_GameEndCriteria.TimeLimit"
    ];

    public static (UnityEngine.Color Color, string Team) WinnerData = (Color.white, "No one wins");

    private static Dictionary<byte, CTFTeam> PlayerTeams = [];
    private static Dictionary<CTFTeam, CTFTeamData> TeamData = [];
    private static Dictionary<byte, CTFPlayerData> PlayerData = [];
    private static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> DefaultOutfits = [];
    private static Dictionary<byte, long> TemporarilyOutPlayers = [];
    private static bool ValidTag;
    private static long GameStartTS;
    private static int RoundsPlayed => TeamData.Values.Sum(x => x.RoundsWon);
    public static bool IsDeathPossible => TaggedPlayersGet.GetValue() == 1;
    public static float KCD => TagCooldown.GetFloat();
    public static int GameEndCriteriaType => GameEndCriteria.GetValue();
    public static int MaxGameLength => TimeLimit.GetInt();
    public static int TotalRoundsToPlay => GameEndCriteriaType == 0 ? RoundsToPlay.GetInt() : (int)Math.Round(PointsToWin.GetInt() * 1.5f);

    public static bool IsCarrier(byte id)
    {
        if (!ValidTag || !PlayerTeams.TryGetValue(id, out CTFTeam team)) return false;
        return TeamData[team.GetOppositeTeam()].FlagCarrier == id;
    }

    private static (Vector2 Position, string RoomName) BlueFlagBase => Main.CurrentMap switch
    {
        MapNames.Skeld => (new(16.5f, -4.8f), Translator.GetString(nameof(SystemTypes.Nav))),
        MapNames.MiraHQ => (new(-4.5f, 2.0f), Translator.GetString(nameof(SystemTypes.Launchpad))),
        MapNames.Dleks => (new(-16.5f, -4.8f), Translator.GetString(nameof(SystemTypes.Nav))),
        MapNames.Polus => (new(9.5f, -12.5f), Translator.GetString(nameof(SystemTypes.Electrical))),
        MapNames.Airship => (new(-23.5f, -1.6f), Translator.GetString(nameof(SystemTypes.Cockpit))),
        MapNames.Fungle => (new(-15.5f, -7.5f), Translator.GetString(nameof(SystemTypes.Kitchen))),
        (MapNames)6 => (new(-13.31f, -34.56f), Translator.GetString(nameof(SystemTypes.Engine))),
        _ => (Vector2.zero, string.Empty)
    };

    private static (Vector2 Position, string RoomName) YellowFlagBase => Main.CurrentMap switch
    {
        MapNames.Skeld => (new(-20.5f, -5.5f), Translator.GetString(nameof(SystemTypes.Reactor))),
        MapNames.MiraHQ => (new(17.8f, 23.0f), Translator.GetString(nameof(SystemTypes.Greenhouse))),
        MapNames.Dleks => (new(20.5f, -5.5f), Translator.GetString(nameof(SystemTypes.Reactor))),
        MapNames.Polus => (new(36.5f, -7.5f), Translator.GetString(nameof(SystemTypes.Laboratory))),
        MapNames.Airship => (new(33.5f, -1.5f), Translator.GetString(nameof(SystemTypes.CargoBay))),
        MapNames.Fungle => (new(22.2f, 13.7f), Translator.GetString(nameof(SystemTypes.Comms))),
        (MapNames)6 => (new(12.98f, -25.68f), Translator.GetString(nameof(SystemTypes.Comms))),
        _ => (Vector2.zero, string.Empty)
    };

    public static void SetupCustomOption()
    {
        const int id = 69_215_001;
        Color color = new Color32(0, 165, 255, 255);

        AlertTeamMembersOfFlagTaken = new BooleanOptionItem(id, "CTF_AlertTeamMembersOfFlagTaken", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetColor(color);

        ArrowToEnemyFlagCarrier = new BooleanOptionItem(id + 1, "CTF_ArrowToEnemyFlagCarrier", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetParent(AlertTeamMembersOfFlagTaken)
            .SetColor(color);

        AlertTeamMembersOfEnemyFlagTaken = new BooleanOptionItem(id + 2, "CTF_AlertTeamMembersOfEnemyFlagTaken", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetColor(color);

        ArrowToOwnFlagCarrier = new BooleanOptionItem(id + 3, "CTF_ArrowToOwnFlagCarrier", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetParent(AlertTeamMembersOfEnemyFlagTaken)
            .SetColor(color);

        TaggedPlayersGet = new StringOptionItem(id + 4, "CTF_TaggedPlayersGet", TaggedPlayersGetOptions, 2, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetColor(color);

        BackTime = new IntegerOptionItem(id + 20, "CTF_BackTime", new(1, 30, 1), 5, TabGroup.GameSettings)
            .SetParent(TaggedPlayersGet)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color);

        WhenFlagCarrierGetsTagged = new StringOptionItem(id + 5, "CTF_WhenFlagCarrierGetsTagged", WhenFlagCarrierGetsTaggedOptions, 0, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetColor(color);

        SpeedReductionForFlagCarrier = new FloatOptionItem(id + 6, "CTF_SpeedReductionForFlagCarrier", new(0f, 1f, 0.05f), 0.25f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color);

        TagCooldown = new FloatOptionItem(id + 7, "CTF_TagCooldown", new(0f, 30f, 0.5f), 4.5f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color);

        GameEndCriteria = new StringOptionItem(id + 8, "CTF_GameEndCriteria", GameEndCriteriaOptions, 0, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetColor(color)
            .RegisterUpdateValueEvent((_, _, currentValue) =>
            {
                RoundsToPlay.SetHidden(currentValue != 0);
                PointsToWin.SetHidden(currentValue != 1);
                TimeLimit.SetHidden(currentValue != 2);
            })
            .SetRunEventOnLoad(true);

        RoundsToPlay = new IntegerOptionItem(id + 9, "CTF_RoundsToPlay", new(1, 10, 1), 3, TabGroup.GameSettings)
            .SetParent(GameEndCriteria)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetColor(color);

        PointsToWin = new IntegerOptionItem(id + 10, "CTF_PointsToWin", new(1, 10, 1), 3, TabGroup.GameSettings)
            .SetParent(GameEndCriteria)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetColor(color);

        TimeLimit = new IntegerOptionItem(id + 11, "CTF_TimeLimit", new(10, 900, 10), 300, TabGroup.GameSettings)
            .SetParent(GameEndCriteria)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color);

        FlagPickupRange = new FloatOptionItem(id + 12, "CTF_FlagPickupRange", new(0.25f, 5f, 0.25f), 1.5f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.CaptureTheFlag)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color);
    }

    public static bool KnowTargetRoleColor(PlayerControl target, ref string color)
    {
        if (!ValidTag || !PlayerTeams.TryGetValue(target.PlayerId, out CTFTeam team)) return false;

        color = team switch
        {
            CTFTeam.Blue => "#0000FF",
            CTFTeam.Yellow => "#FFFF00",
            _ => color
        };

        return true;
    }

    public static string GetSuffixText(PlayerControl seer, PlayerControl target)
    {
        if (!ValidTag || seer.PlayerId != target.PlayerId) return string.Empty;

        string arrows = TargetArrow.GetAllArrows(seer.PlayerId);
        arrows = arrows.Length > 0 ? $"{arrows}\n" : string.Empty;

        var str = $"{arrows}<size=1.4>{GetStatistics(target.PlayerId).Replace(" | ", "\n")}</size>\n";

        if (GameEndCriteria.GetValue() == 2)
        {
            long timeLeft = TimeLimit.GetInt() - (Utils.TimeStamp - GameStartTS) + 1;

            if (timeLeft >= 0) str += $"<size=1.8><#ffffff>{timeLeft / 60:00}:{timeLeft % 60:00}</color></size>\n";
            else str += $"<size=1.6><#ffffff>{Translator.GetString("CTF_TimeIsUp")}</color></size>\n";
        }

        if (TemporarilyOutPlayers.TryGetValue(seer.PlayerId, out long backTS))
        {
            long timeLeft = backTS - Utils.TimeStamp;
            str += $"{string.Format(Translator.GetString("CTF_BackIn"), timeLeft)}\n";
        }

        return str + string.Join("<#ffffff> | </color>", TeamData.Select(x => Utils.ColorString(x.Key.GetTeamColor(), x.Value.RoundsWon.ToString())));
    }

    public static string GetStatistics(byte id)
    {
        if (!PlayerData.TryGetValue(id, out CTFPlayerData stats)) return string.Empty;
        return string.Format(Translator.GetString("CTF_PlayerStats"), Math.Round(stats.FlagTime, 1), stats.TagCount);
    }

    public static int GetFlagTime(byte id)
    {
        if (!PlayerData.TryGetValue(id, out CTFPlayerData data)) return 0;
        return (int)Math.Round(data.FlagTime);
    }

    public static int GetTagCount(byte id)
    {
        if (!PlayerData.TryGetValue(id, out CTFPlayerData data)) return 0;
        return data.TagCount;
    }

    public static bool CheckForGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;

        if (!ValidTag) return false;

        PlayerControl[] aapc = Main.AllAlivePlayerControls.Concat(ExtendedPlayerControl.TempExiled.ToValidPlayers()).ToArray();

        switch (aapc.Length)
        {
            case 0:
                ResetSkins();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                CustomWinnerHolder.WinnerIds = Main.PlayerStates.Keys.ToHashSet();
                reason = GameOverReason.CrewmateDisconnect;
                return true;
            case 1:
                ResetSkins();
                TeamData[PlayerTeams[aapc[0].PlayerId]].SetAsWinner();
                return true;
            default:
                // If WinnerData is already set, end the game
                if (WinnerData.Team != "No one wins")
                {
                    ResetSkins();
                    return true;
                }

                // If all players are on the same team, end the game
                if (aapc.All(x => PlayerTeams.TryGetValue(x.PlayerId, out CTFTeam team) && team == CTFTeam.Blue) || aapc.All(x => PlayerTeams.TryGetValue(x.PlayerId, out CTFTeam team) && team == CTFTeam.Yellow))
                {
                    ResetSkins();
                    TeamData[PlayerTeams[aapc[0].PlayerId]].SetAsWinner();
                    return true;
                }

                return false;
        }

        void ResetSkins()
        {
            foreach ((byte key, NetworkedPlayerInfo.PlayerOutfit outfit) in DefaultOutfits)
            {
                PlayerControl pc = key.GetPlayer();

                if (pc != null && outfit != null)
                    Utils.RpcChangeSkin(pc, outfit);
            }
        }
    }

    public static void Init()
    {
        // Reset all data
        PlayerTeams = [];
        TeamData = [];
        WinnerData = (Color.white, "No one wins");
        PlayerData = Main.PlayerStates.Keys.ToDictionary(x => x, _ => new CTFPlayerData());
        DefaultOutfits = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, x => x.Data.DefaultOutfit);
        TemporarilyOutPlayers = [];
        ValidTag = false;
        SendRPC();
    }

    public static IEnumerator OnGameStart()
    {
        yield return new WaitForSeconds(0.2f);
        
        Main.AllPlayerKillCooldown.SetAllValues(TagCooldown.GetFloat());

        yield return new WaitForSecondsRealtime(3f);

        // Assign players to teams
        List<PlayerControl> players = Main.AllAlivePlayerControls.Shuffle().ToList();
        if (Main.GM.Value) players.RemoveAll(x => x.IsHost());
        if (ChatCommands.Spectators.Count > 0) players.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));

        int blueCount = players.Count / 2;
        HashSet<byte> bluePlayers = [];
        HashSet<byte> yellowPlayers = [];

        for (var i = 0; i < blueCount; i++)
        {
            PlayerControl player = players.FirstOrDefault(p => p.Data.DefaultOutfit.ColorId == 1) ?? players.FirstOrDefault(x => x.Data.DefaultOutfit.ColorId != 5) ?? players.RandomElement();
            players.Remove(player);
            PlayerTeams[player.PlayerId] = CTFTeam.Blue;
            bluePlayers.Add(player.PlayerId);
            player.RpcSetColor(1);
            yield return null;
        }

        foreach (PlayerControl player in players)
        {
            PlayerTeams[player.PlayerId] = CTFTeam.Yellow;
            yellowPlayers.Add(player.PlayerId);
            player.RpcSetColor(5);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        // Create flags
        (Vector2 Position, string RoomName) blueFlagBase = BlueFlagBase;
        (Vector2 Position, string RoomName) yellowFlagBase = YellowFlagBase;

        CustomNetObject blueFlag = new BlueFlag(blueFlagBase.Position);
        CustomNetObject yellowFlag = new YellowFlag(yellowFlagBase.Position);

        // Create team data
        TeamData[CTFTeam.Blue] = new(CTFTeam.Blue, blueFlag, bluePlayers, byte.MaxValue);
        TeamData[CTFTeam.Yellow] = new(CTFTeam.Yellow, yellowFlag, yellowPlayers, byte.MaxValue);

        // Teleport players to their respective bases
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (PlayerTeams.TryGetValue(pc.PlayerId, out CTFTeam team))
            {
                switch (team)
                {
                    case CTFTeam.Blue:
                        pc.TP(blueFlagBase.Position);
                        pc.Notify(string.Format(Translator.GetString("CTF_Notify_EnemyTeamRoom"), yellowFlagBase.RoomName));
                        break;
                    case CTFTeam.Yellow:
                        pc.TP(yellowFlagBase.Position);
                        pc.Notify(string.Format(Translator.GetString("CTF_Notify_EnemyTeamRoom"), blueFlagBase.RoomName));
                        break;
                }
            }

            pc.RpcChangeRoleBasis(CustomRoles.CTFPlayer);
            pc.RpcResetAbilityCooldown();

            yield return null;
        }

        yield return new WaitForSeconds(0.2f);
        
        try
        {
            foreach (CTFTeamData data in TeamData.Values)
            {
                try
                {
                    foreach (byte id1 in data.Players)
                    {
                        try
                        {
                            var pc1 = id1.GetPlayer();
                            if (pc1 == null || pc1.AmOwner) continue;

                            var sender = CustomRpcSender.Create("CTF Set Teams");
                            sender.StartMessage(pc1.OwnerId);

                            foreach (byte id2 in data.Players)
                            {
                                try
                                {
                                    if (id1 == id2) continue;

                                    var pc2 = id2.GetPlayer();
                                    if (pc2 == null) continue;

                                    sender.StartRpc(pc2.NetId, RpcCalls.SetRole)
                                        .Write((ushort)RoleTypes.Phantom)
                                        .Write(true)
                                        .EndRpc();
                                }
                                catch (Exception e) { Utils.ThrowException(e); }
                            }
                            
                            sender.SendMessage();
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        yield return new WaitForSeconds(0.2f);

        ValidTag = true;
        GameStartTS = Utils.TimeStamp;
    }

    private static void Restart()
    {
        Logger.Info("Restarting Capture The Flag game", "CTF");
        
        foreach ((CTFTeam team, CTFTeamData data) in TeamData)
        {
            Vector2 flagBase = team.GetFlagBase().Position;
            data.DropFlag();
            data.Flag.TP(flagBase);
            data.Players.ToValidPlayers().MassTP(flagBase);
        }
        
        Main.AllPlayerControls.Do(x => TargetArrow.RemoveAllTarget(x.PlayerId));
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!ValidTag || TemporarilyOutPlayers.ContainsKey(killer.PlayerId) || !PlayerTeams.TryGetValue(target.PlayerId, out CTFTeam targetTeam) || !PlayerTeams.TryGetValue(killer.PlayerId, out CTFTeam killerTeam) || killerTeam == targetTeam || TeamData.Values.Any(x => x.FlagCarrier == killer.PlayerId)) return;

        new[] { killer, target }.Do(x => x.SetKillCooldown(TagCooldown.GetFloat()));

        if (TeamData.FindFirst(x => x.Value.FlagCarrier == target.PlayerId, out KeyValuePair<CTFTeam, CTFTeamData> kvp))
        {
            kvp.Value.DropFlag();
            if (WhenFlagCarrierGetsTagged.GetValue() == 1) kvp.Value.Flag.TP(kvp.Key.GetFlagBase().Position);
        }

        switch (TaggedPlayersGet.GetValue())
        {
            case 0:
                target.TP(targetTeam.GetFlagBase().Position);
                Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                target.MarkDirtySettings();
                break;
            case 1:
                target.Suicide(PlayerState.DeathReason.Kill);
                string notify = string.Format(Translator.GetString("CTF_TeamMemberFallen"), target.PlayerId.ColoredPlayerName());
                TeamData[targetTeam].Players.ToValidPlayers().NotifyPlayers(notify);
                if (Main.GM.Value && AmongUsClient.Instance.AmHost) PlayerControl.LocalPlayer.KillFlash();
                ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());
                break;
            case 2:
                TemporarilyOutPlayers[target.PlayerId] = Utils.TimeStamp + BackTime.GetInt();
                target.ExileTemporarily();
                break;
        }

        if (PlayerData.TryGetValue(killer.PlayerId, out CTFPlayerData data))
        {
            data.TagCount++;
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer, SendOption: SendOption.None);
        }
    }

    public static void TryPickUpFlag(PlayerControl pc)
    {
        if (!ValidTag) return;

        Logger.Info($"Received flag pickup request from {pc.GetRealName()}", "CTF");
        // If the player is near the enemy's flag, pick it up
        Vector2 pos = pc.Pos();
        CTFTeamData enemy = TeamData[PlayerTeams[pc.PlayerId].GetOppositeTeam()];
        if (enemy.IsNearFlag(pos)) enemy.PickUpFlag(pc.PlayerId);
    }

    public static void ApplyGameOptions()
    {
        AURoleOptions.PhantomCooldown = 5f;

        try { AURoleOptions.GuardianAngelCooldown = 900f; }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static bool IsNotInLocalPlayersTeam(PlayerControl pc)
    {
        return !PlayerTeams.TryGetValue(pc.PlayerId, out CTFTeam team) || !PlayerTeams.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out CTFTeam lpTeam) || team != lpTeam;
    }

    private static void SendRPC()
    {
        var w = Utils.CreateRPC(CustomRPC.CTFSync);

        w.Write(WinnerData.Color);
        w.Write(WinnerData.Team);

        w.Write(PlayerData.Count);

        foreach ((byte id, CTFPlayerData data) in PlayerData)
        {
            w.Write(id);
            w.Write(data.FlagTime);
            w.Write(data.TagCount);
        }

        Utils.EndRPC(w);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        WinnerData.Color = reader.ReadColor();
        WinnerData.Team = reader.ReadString();

        int count = reader.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            byte id = reader.ReadByte();

            if (!PlayerData.TryGetValue(id, out CTFPlayerData data))
                PlayerData[id] = data = new CTFPlayerData();

            data.FlagTime = reader.ReadSingle();
            data.TagCount = reader.ReadInt32();
        }
    }

    private static Color GetTeamColor(this CTFTeam team)
    {
        return team switch
        {
            CTFTeam.Blue => Color.blue,
            CTFTeam.Yellow => Color.yellow,
            _ => Color.white
        };
    }

    private static string GetTeamName(this CTFTeam team)
    {
        return team switch
        {
            CTFTeam.Blue => Translator.GetString("CTF_BlueTeamWins"),
            CTFTeam.Yellow => Translator.GetString("CTF_YellowTeamWins"),
            _ => string.Empty
        };
    }

    private static CTFTeam GetOppositeTeam(this CTFTeam team)
    {
        return team switch
        {
            CTFTeam.Blue => CTFTeam.Yellow,
            CTFTeam.Yellow => CTFTeam.Blue,
            _ => CTFTeam.Blue
        };
    }

    private static (Vector2 Position, string RoomName) GetFlagBase(this CTFTeam team)
    {
        return team switch
        {
            CTFTeam.Blue => BlueFlagBase,
            CTFTeam.Yellow => YellowFlagBase,
            _ => (Vector2.zero, string.Empty)
        };
    }

    private enum CTFTeam
    {
        Blue,
        Yellow
    }

    private class CTFTeamData(CTFTeam team, CustomNetObject flag, HashSet<byte> players, byte flagCarrier)
    {
        public CustomNetObject Flag { get; } = flag;
        public HashSet<byte> Players { get; } = players;
        public byte FlagCarrier { get; private set; } = flagCarrier;
        public int RoundsWon { get; private set; }

        public void SetAsWinner()
        {
            WinnerData = (team.GetTeamColor(), team.GetTeamName());
            CustomWinnerHolder.WinnerIds = Players;
            Logger.Info($"{team} team wins", "CTF");
            SendRPC();
        }

        public void Update()
        {
            try
            {
                if (FlagCarrier == byte.MaxValue) return;

                PlayerControl flagCarrierPc = FlagCarrier.GetPlayer();

                if (flagCarrierPc == null || !flagCarrierPc.IsAlive())
                {
                    DropFlag();
                    return;
                }

                Flag.TP(flagCarrierPc.Pos());
                if (PlayerData.TryGetValue(FlagCarrier, out CTFPlayerData data)) data.FlagTime += Time.fixedDeltaTime;

                CTFTeam enemy = team.GetOppositeTeam();

                if (Vector2.Distance(Flag.Position, enemy.GetFlagBase().Position) <= 2f)
                {
                    Main.AllPlayerControls.NotifyPlayers(Translator.GetString($"CTF_{enemy}TeamWonThisRound"));
                    CTFTeamData enemyTeam = TeamData[enemy];

                    if (++enemyTeam.RoundsWon >= PointsToWin.GetInt() && GameEndCriteria.GetValue() == 1)
                    {
                        enemyTeam.SetAsWinner();
                        return;
                    }

                    if (GameEndCriteria.GetValue() == 0 && RoundsPlayed >= RoundsToPlay.GetInt() && RoundsWon != enemyTeam.RoundsWon)
                    {
                        CTFTeamData winner = TeamData.Values.MaxBy(x => x.RoundsWon);
                        winner.SetAsWinner();
                        return;
                    }

                    
                    Restart();
                }
            }
            catch { }
        }

        public void PickUpFlag(byte id)
        {
            if (FlagCarrier == id) return;

            FlagCarrier = id;
            Update();

            Logger.Info($"{id.ColoredPlayerName().RemoveHtmlTags()} picked up the {team} flag", "CTF");

            Main.AllPlayerSpeed[id] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod) - SpeedReductionForFlagCarrier.GetFloat();
            PlayerGameOptionsSender.SetDirty(id);

            if (AlertTeamMembersOfFlagTaken.GetBool())
            {
                bool arrow = ArrowToEnemyFlagCarrier.GetBool();

                TeamData[team].Players
                    .ToValidPlayers()
                    .Do(x =>
                    {
                        if (arrow) TargetArrow.Add(x.PlayerId, id);
                        x.Notify(Utils.ColorString(Color.yellow, Translator.GetString("CTF_FlagTaken")));
                    });
            }

            if (AlertTeamMembersOfEnemyFlagTaken.GetBool())
            {
                bool arrow = ArrowToOwnFlagCarrier.GetBool();

                TeamData[team.GetOppositeTeam()].Players
                    .ToValidPlayers()
                    .Do(x =>
                    {
                        if (arrow) TargetArrow.Add(x.PlayerId, id);
                        x.Notify(Translator.GetString("CTF_EnemyFlagTaken"));
                    });
            }
        }

        public void DropFlag()
        {
            Logger.Info($"{FlagCarrier.ColoredPlayerName().RemoveHtmlTags()} dropped the {team} flag", "CTF");

            if (FlagCarrier != byte.MaxValue)
            {
                if (ArrowToEnemyFlagCarrier.GetBool() || ArrowToOwnFlagCarrier.GetBool())
                    TeamData.Values.SelectMany(x => x.Players).Do(x => TargetArrow.Remove(x, FlagCarrier));

                Main.AllPlayerSpeed[FlagCarrier] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                PlayerGameOptionsSender.SetDirty(FlagCarrier);

                FlagCarrier = byte.MaxValue;
                Utils.NotifyRoles(SendOption: SendOption.None);
            }
        }

        public bool IsNearFlag(Vector2 pos)
        {
            return Vector2.Distance(Flag.Position, pos) <= FlagPickupRange.GetFloat();
        }
    }

    private class CTFPlayerData
    {
        public int TagCount { get; set; }
        public float FlagTime { get; set; }
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || Options.CurrentGameMode != CustomGameMode.CaptureTheFlag || !Main.IntroDestroyed || __instance.PlayerId >= 254 || WinnerData.Team != "No one wins" || IntroCutsceneDestroyPatch.IntroDestroyTS + 5 > Utils.TimeStamp) return;

            if (__instance.IsHost())
            {
                TeamData.Values.Do(x => x.Update());

                if (GameEndCriteria.GetValue() == 2)
                {
                    long timeLeft = TimeLimit.GetInt() - (Utils.TimeStamp - GameStartTS) + 1;

                    switch (timeLeft)
                    {
                        case <= 1 when TeamData.Count == 2 && TeamData[CTFTeam.Blue].RoundsWon != TeamData[CTFTeam.Yellow].RoundsWon:
                        {
                            CTFTeamData winner = TeamData.Values.MaxBy(x => x.RoundsWon);
                            winner.SetAsWinner();
                            return;
                        }
                        case >= -1:
                        {
                            Utils.NotifyRoles(SendOption: SendOption.None);
                            break;
                        }
                    }
                }
            }

            if (!PlayerTeams.TryGetValue(__instance.PlayerId, out CTFTeam team)) return;
            bool blue = team == CTFTeam.Blue;
            int colorId = blue ? 1 : 5;

            if (__instance.CurrentOutfit.ColorId != colorId)
            {
                __instance.SetColor(colorId);

                CustomRpcSender.Create("Color")
                    .AutoStartRpc(__instance.NetId, 8)
                    .Write(__instance.Data.NetId)
                    .Write((byte)colorId)
                    .EndRpc()
                    .SendMessage();
            }

            if (TemporarilyOutPlayers.TryGetValue(__instance.PlayerId, out long endTS))
            {
                if (Utils.TimeStamp >= endTS)
                {
                    TemporarilyOutPlayers.Remove(__instance.PlayerId);
                    __instance.ReviveFromTemporaryExile();
                    __instance.TP(team.GetFlagBase().Position);
                    RPC.PlaySoundRPC(__instance.PlayerId, Sounds.SpawnSound);
                    Utils.NotifyRoles(SpecifySeer: __instance, SpecifyTarget: __instance, SendOption: SendOption.None);
                }
                else if (GameEndCriteria.GetValue() != 2)
                    Utils.NotifyRoles(SpecifySeer: __instance, SpecifyTarget: __instance, SendOption: SendOption.None);
            }
        }
    }
}

public class CTFPlayer : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption() { }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        CaptureTheFlag.TryPickUpFlag(pc);
        return false;
    }
}
