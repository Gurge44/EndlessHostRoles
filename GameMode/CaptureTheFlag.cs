using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
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

    private static NetworkedPlayerInfo.PlayerOutfit YellowOutfit => new NetworkedPlayerInfo.PlayerOutfit().Set("", 5, "", "", "", "pet_coaltonpet", "");
    private static NetworkedPlayerInfo.PlayerOutfit BlueOutfit => new NetworkedPlayerInfo.PlayerOutfit().Set("", 1, "", "", "", "pet_coaltonpet", "");

    private static (Vector2 Position, string RoomName) BlueFlagBase => Main.CurrentMap switch
    {
        MapNames.Skeld => (new(16.5f, -4.8f), Translator.GetString(nameof(SystemTypes.Nav))),
        MapNames.MiraHQ => (new(-4.5f, 2.0f), Translator.GetString(nameof(SystemTypes.Launchpad))),
        MapNames.Dleks => (new(-16.5f, -4.8f), Translator.GetString(nameof(SystemTypes.Nav))),
        MapNames.Polus => (new(9.5f, -12.5f), Translator.GetString(nameof(SystemTypes.Electrical))),
        MapNames.Airship => (new(-23.5f, -1.6f), Translator.GetString(nameof(SystemTypes.Cockpit))),
        MapNames.Fungle => (new(-15.5f, -7.5f), Translator.GetString(nameof(SystemTypes.Kitchen))),
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
            .RegisterUpdateValueEvent((_, args) =>
            {
                RoundsToPlay.SetHidden(args.CurrentValue != 0);
                PointsToWin.SetHidden(args.CurrentValue != 1);
                TimeLimit.SetHidden(args.CurrentValue != 2);
            });

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
        if (!ValidTag) return string.Empty;

        if (seer.PlayerId != target.PlayerId) return string.Empty;

        string arrows = TargetArrow.GetAllArrows(seer);
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
        PlayerControl[] aapc = Main.AllAlivePlayerControls;

        if (!ValidTag) return false;

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
            var sender = CustomRpcSender.Create("CTF - ResetSkins", SendOption.Reliable);
            var hasValue = false;

            foreach ((byte key, NetworkedPlayerInfo.PlayerOutfit outfit) in DefaultOutfits)
            {
                PlayerControl pc = key.GetPlayer();

                if (pc != null && outfit != null)
                {
                    Utils.RpcChangeSkin(pc, outfit, sender);
                    hasValue = true;

                    if (sender.stream.Length > 400)
                    {
                        sender.SendMessage();
                        sender = CustomRpcSender.Create("CTF - ResetSkins", SendOption.Reliable);
                        hasValue = false;
                    }
                }
            }

            sender.SendMessage(dispose: !hasValue);
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

    public static void OnGameStart()
    {
        Main.AllPlayerKillCooldown.SetAllValues(TagCooldown.GetFloat());

        // Assign players to teams
        List<PlayerControl> players = Main.AllAlivePlayerControls.Shuffle().ToList();
        if (Main.GM.Value) players.RemoveAll(x => x.IsHost());
        if (ChatCommands.Spectators.Count > 0) players.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));

        int blueCount = players.Count / 2;
        HashSet<byte> bluePlayers = [];
        HashSet<byte> yellowPlayers = [];
        NetworkedPlayerInfo.PlayerOutfit blueOutfit = BlueOutfit;
        NetworkedPlayerInfo.PlayerOutfit yellowOutfit = YellowOutfit;

        Dictionary<byte, CustomRpcSender> senders = [];
        Dictionary<byte, bool> hasValue = [];
        players.ForEach(x => senders[x.PlayerId] = CustomRpcSender.Create("CTF - OnGameStart", SendOption.Reliable));
        players.ForEach(x => hasValue[x.PlayerId] = false);

        for (var i = 0; i < blueCount; i++)
        {
            PlayerControl player = players.FirstOrDefault(p => p.Data.DefaultOutfit.ColorId == 1) ?? players.FirstOrDefault(x => x.Data.DefaultOutfit.ColorId != 5) ?? players.RandomElement();
            players.Remove(player);
            PlayerTeams[player.PlayerId] = CTFTeam.Blue;
            bluePlayers.Add(player.PlayerId);
            blueOutfit.PlayerName = player.GetRealName();
            blueOutfit.PetId = player.Data.DefaultOutfit.PetId;

            if (senders.TryGetValue(player.PlayerId, out CustomRpcSender sender) && hasValue.ContainsKey(player.PlayerId))
            {
                hasValue[player.PlayerId] |= Utils.RpcChangeSkin(player, blueOutfit, sender);

                if (sender.stream.Length > 400)
                {
                    sender.SendMessage();
                    senders[player.PlayerId] = CustomRpcSender.Create("CTF - OnGameStart", SendOption.Reliable);
                    hasValue[player.PlayerId] = false;
                }
            }
        }

        foreach (PlayerControl player in players)
        {
            PlayerTeams[player.PlayerId] = CTFTeam.Yellow;
            yellowPlayers.Add(player.PlayerId);
            yellowOutfit.PlayerName = player.GetRealName();
            yellowOutfit.PetId = player.Data.DefaultOutfit.PetId;

            if (senders.TryGetValue(player.PlayerId, out CustomRpcSender sender) && hasValue.ContainsKey(player.PlayerId))
            {
                hasValue[player.PlayerId] |= Utils.RpcChangeSkin(player, yellowOutfit, sender);

                if (sender.stream.Length > 400)
                {
                    sender.SendMessage();
                    senders[player.PlayerId] = CustomRpcSender.Create("CTF - OnGameStart", SendOption.Reliable);
                    hasValue[player.PlayerId] = false;
                }
            }
        }

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
            if (!senders.TryGetValue(pc.PlayerId, out CustomRpcSender sender) || !hasValue.ContainsKey(pc.PlayerId)) continue;

            if (PlayerTeams.TryGetValue(pc.PlayerId, out CTFTeam team))
            {
                switch (team)
                {
                    case CTFTeam.Blue:
                        hasValue[pc.PlayerId] |= sender.TP(pc, blueFlagBase.Position);
                        hasValue[pc.PlayerId] |= sender.Notify(pc, string.Format(Translator.GetString("CTF_Notify_EnemyTeamRoom"), yellowFlagBase.RoomName));
                        break;
                    case CTFTeam.Yellow:
                        hasValue[pc.PlayerId] |= sender.TP(pc, yellowFlagBase.Position);
                        hasValue[pc.PlayerId] |= sender.Notify(pc, string.Format(Translator.GetString("CTF_Notify_EnemyTeamRoom"), blueFlagBase.RoomName));
                        break;
                }
            }

            hasValue[pc.PlayerId] |= pc.RpcChangeRoleBasis(CustomRoles.CTFPlayer, sender: sender);
            hasValue[pc.PlayerId] |= sender.RpcResetAbilityCooldown(pc);

            if (sender.stream.Length > 400)
            {
                sender.SendMessage();
                senders[pc.PlayerId] = CustomRpcSender.Create("CTF - OnGameStart", SendOption.Reliable);
                hasValue[pc.PlayerId] = false;
            }
        }

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
                            if (pc1 == null) continue;

                            int targetClientId = pc1.OwnerId;

                            if (!senders.TryGetValue(id1, out CustomRpcSender sender) || !hasValue.ContainsKey(id1)) continue;

                            foreach (byte id2 in data.Players)
                            {
                                try
                                {
                                    if (id1 == id2) continue;

                                    var pc2 = id2.GetPlayer();
                                    if (pc2 == null) continue;

                                    hasValue[id1] |= sender.RpcSetRole(pc2, RoleTypes.Phantom, targetClientId, changeRoleMap: true);

                                    if (sender.stream.Length > 400)
                                    {
                                        sender.SendMessage();
                                        senders[id1] = CustomRpcSender.Create("CTF - OnGameStart", SendOption.Reliable);
                                        hasValue[id1] = false;
                                    }
                                }
                                catch (Exception e) { Utils.ThrowException(e); }
                            }
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        senders.IntersectBy(hasValue.Keys, x => x.Key).Do(x => x.Value.SendMessage(!hasValue[x.Key]));

        ValidTag = true;
        GameStartTS = Utils.TimeStamp;
        LateTask.New(() => Main.ProcessShapeshifts = true, 3f, log: false);
    }

    private static void Restart()
    {
        Logger.Info("Restarting Capture The Flag game", "CTF");

        var sender = CustomRpcSender.Create("CTF - Restart", SendOption.Reliable);
        var hasValue = false;

        foreach ((CTFTeam team, CTFTeamData data) in TeamData)
        {
            Vector2 flagBase = team.GetFlagBase().Position;
            data.DropFlag();
            data.Flag.TP(flagBase);

            hasValue |= data.Players.ToValidPlayers().Aggregate(hasValue, (current, pc) => current || sender.TP(pc, flagBase));
        }

        sender.SendMessage(!hasValue);
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!ValidTag || TemporarilyOutPlayers.ContainsKey(killer.PlayerId) || !PlayerTeams.TryGetValue(target.PlayerId, out CTFTeam targetTeam) || !PlayerTeams.TryGetValue(killer.PlayerId, out CTFTeam killerTeam) || killerTeam == targetTeam || TeamData.Values.Any(x => x.FlagCarrier == killer.PlayerId)) return;

        var sender = CustomRpcSender.Create("CTF - OnCheckMurder", SendOption.Reliable);

        new[] { killer, target }.Do(x => sender.SetKillCooldown(x, TagCooldown.GetFloat()));

        if (TeamData.FindFirst(x => x.Value.FlagCarrier == target.PlayerId, out KeyValuePair<CTFTeam, CTFTeamData> kvp))
        {
            kvp.Value.DropFlag();
            if (WhenFlagCarrierGetsTagged.GetValue() == 1) kvp.Value.Flag.TP(kvp.Key.GetFlagBase().Position);
        }

        switch (TaggedPlayersGet.GetValue())
        {
            case 0:
                sender.TP(target, targetTeam.GetFlagBase().Position);
                Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                target.MarkDirtySettings();
                break;
            case 1:
                target.Suicide();
                string notify = string.Format(Translator.GetString("CTF_TeamMemberFallen"), target.PlayerId.ColoredPlayerName());
                TeamData[targetTeam].Players.ToValidPlayers().NotifyPlayers(notify);
                if (Main.GM.Value && AmongUsClient.Instance.AmHost) PlayerControl.LocalPlayer.KillFlash();
                ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());
                break;
            case 2:
                TemporarilyOutPlayers[target.PlayerId] = Utils.TimeStamp + BackTime.GetInt();
                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                sender.TP(target, Pelican.GetBlackRoomPS());
                target.MarkDirtySettings();
                break;
        }

        if (PlayerData.TryGetValue(killer.PlayerId, out CTFPlayerData data)) data.TagCount++;
        sender.NotifyRolesSpecific(killer, killer, out sender, out _);

        sender.SendMessage();
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

            var sender = CustomRpcSender.Create("CTF - PickUpFlag", SendOption.Reliable);
            var hasValue = false;

            if (AlertTeamMembersOfFlagTaken.GetBool())
            {
                bool arrow = ArrowToEnemyFlagCarrier.GetBool();

                TeamData[team].Players
                    .Select(x => x.GetPlayer())
                    .DoIf(x => x != null, x =>
                    {
                        if (arrow) TargetArrow.Add(x.PlayerId, id);
                        hasValue |= sender.Notify(x, Utils.ColorString(Color.yellow, Translator.GetString("CTF_FlagTaken")));

                        if (sender.stream.Length > 400)
                        {
                            sender.SendMessage();
                            sender = CustomRpcSender.Create("CTF - PickUpFlag", SendOption.Reliable);
                            hasValue = false;
                        }
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
                        hasValue |= sender.Notify(x, Translator.GetString("CTF_EnemyFlagTaken"));

                        if (sender.stream.Length > 400)
                        {
                            sender.SendMessage();
                            sender = CustomRpcSender.Create("CTF - PickUpFlag", SendOption.Reliable);
                            hasValue = false;
                        }
                    });
            }

            sender.SendMessage(dispose: !hasValue);
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
                Utils.NotifyRoles();
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

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    private static class FixedUpdatePatch
    {
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix(PlayerControl __instance)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || !CustomGameMode.CaptureTheFlag.IsActiveOrIntegrated() || !Main.IntroDestroyed || __instance.PlayerId >= 254 || WinnerData.Team != "No one wins") return;

            if (__instance.IsHost())
            {
                TeamData.Values.Do(x => x.Update());

                if (GameEndCriteria.GetValue() == 2)
                {
                    long timeLeft = TimeLimit.GetInt() - (Utils.TimeStamp - GameStartTS) + 1;

                    switch (timeLeft)
                    {
                        case <= 1 when TeamData[CTFTeam.Blue].RoundsWon != TeamData[CTFTeam.Yellow].RoundsWon:
                        {
                            CTFTeamData winner = TeamData.Values.MaxBy(x => x.RoundsWon);
                            winner.SetAsWinner();
                            return;
                        }
                        case >= -1:
                        {
                            Utils.NotifyRoles();
                            break;
                        }
                    }
                }
            }

            if (!PlayerTeams.TryGetValue(__instance.PlayerId, out CTFTeam team)) return;
            bool blue = team == CTFTeam.Blue;
            int colorId = blue ? 1 : 5;

            var sender = CustomRpcSender.Create("CTF - FixedUpdate", SendOption.Reliable, log: false);
            var hasValue = false;

            if (__instance.CurrentOutfit.ColorId != colorId)
            {
                Utils.RpcChangeSkin(__instance, blue ? BlueOutfit : YellowOutfit, sender);
                hasValue = true;
            }

            Vector2 pos = __instance.Pos();
            Vector2 blackRoomPS = Pelican.GetBlackRoomPS();

            if (TemporarilyOutPlayers.TryGetValue(__instance.PlayerId, out long endTS))
            {
                if (Utils.TimeStamp >= endTS)
                {
                    TemporarilyOutPlayers.Remove(__instance.PlayerId);
                    Main.AllPlayerSpeed[__instance.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                    __instance.MarkDirtySettings();
                    hasValue |= sender.TP(__instance, team.GetFlagBase().Position);
                    hasValue |= sender.SetKillCooldown(__instance);
                    RPC.PlaySoundRPC(__instance.PlayerId, Sounds.TaskComplete);
                }
                else if (GameEndCriteria.GetValue() != 2)
                {
                    hasValue |= sender.NotifyRolesSpecific(__instance, __instance, out sender, out bool cleared);
                    if (cleared) hasValue = false;
                    if (Vector2.Distance(pos, blackRoomPS) > 2f) hasValue |= sender.TP(__instance, blackRoomPS);
                }
            }
            else if (Vector2.Distance(pos, blackRoomPS) <= 2f) { hasValue |= sender.TP(__instance, team.GetFlagBase().Position); }

            sender.SendMessage(dispose: !hasValue);
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