using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR;

public static class Deathrace
{
    public static readonly Dictionary<string, HashSet<MapNames>> PlayedMaps = [];
    public static List<SystemTypes> Track = [];
    public static Dictionary<byte, PlayerData> Data = [];
    public static long LastPowerUpSpawn;
    public static List<DeathracePowerUp> SpawnedPowerUps = [];
    public static bool GameGoing;
    
    // Settings
    public static bool Clockwise;
    public static int LapsToWin;
    public static bool EliminateLastToFinishEachLap;
    public static bool SpawnPowerUps;
    public static int PowerUpSpawnFrequency;
    public static int PowerUpEffectDuration;
    public static float PowerUpEffectRange;
    public static float SmokeSpeedReduction;
    public static float EnergyDrinkSpeedIncreasement;
    public static float PowerUpPickupRange;

    public static OptionItem ClockwiseOption;
    public static OptionItem LapsToWinOption;
    public static OptionItem EliminateLastToFinishEachLapOption;
    public static OptionItem SpawnPowerUpsOption;
    public static OptionItem PowerUpSpawnFrequencyOption;
    public static OptionItem PowerUpEffectDurationOption;
    public static OptionItem PowerUpEffectRangeOption;
    public static OptionItem SmokeSpeedReductionOption;
    public static OptionItem EnergyDrinkSpeedIncreasementOption;
    public static OptionItem PowerUpPickupRangeOption;

    public static readonly Dictionary<MapNames, List<SystemTypes>> Tracks = new()
    {
        [MapNames.Skeld] = [SystemTypes.UpperEngine, SystemTypes.LowerEngine, SystemTypes.Storage, SystemTypes.Shields, SystemTypes.Weapons, SystemTypes.Cafeteria],
        [MapNames.MiraHQ] = [SystemTypes.MedBay, /* Vent */ SystemTypes.Balcony, SystemTypes.Cafeteria, SystemTypes.Office, /* Vent */ SystemTypes.Laboratory, SystemTypes.Decontamination, SystemTypes.LockerRoom],
        [MapNames.Polus] = [SystemTypes.Electrical, SystemTypes.LifeSupp, SystemTypes.Admin, SystemTypes.Specimens, SystemTypes.Laboratory],
        [MapNames.Airship] = [SystemTypes.Engine, SystemTypes.Armory, SystemTypes.Kitchen, SystemTypes.HallOfPortraits, SystemTypes.Security, SystemTypes.Electrical, SystemTypes.Medical, SystemTypes.CargoBay, SystemTypes.Lounge, SystemTypes.Records, SystemTypes.Showers, SystemTypes.MainHall],
        [MapNames.Fungle] = [SystemTypes.Cafeteria, (SystemTypes)100, SystemTypes.RecRoom, SystemTypes.Greenhouse, SystemTypes.UpperEngine, /* Vent */ SystemTypes.Storage],
        [(MapNames)6] = [(SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral, SystemTypes.Cafeteria, (SystemTypes)101, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperLobby, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerLobby, SystemTypes.Storage, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerCentral, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Filtration, SystemTypes.Electrical, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerCentral, SystemTypes.Decontamination2, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast, SystemTypes.Decontamination3, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerLobby, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperLobby, (SystemTypes)102]
    };

    public static readonly Dictionary<int, Vector2> CoordinateChecks = new()
    {
        [100] = new Vector2(-22f, 1.82f), // (Fungle) Left side of Cafeteria <-> Splash Zone passthrough
        [101] = new Vector2(-8.15f, 19.12f), // (Submerged) Upper Deck: Central <-> Cafeteria hallway
        [102] = new Vector2(10.43f, 14.74f) // (Submerged) Upper Deck: Lobby <-> Central hallway, right side
    };

    public static readonly Dictionary<MapNames, Dictionary<SystemTypes, List<int>>> UsableVentIDs = new()
    {
        [MapNames.MiraHQ] = new()
        {
            [SystemTypes.MedBay] = [1, 8],
            [SystemTypes.Balcony] = [1, 8],
            [SystemTypes.Office] = [4, 5],
            [SystemTypes.Laboratory] = [4, 5]
        }
    };

    public static readonly Dictionary<MapNames, SystemTypes> InitialSpawnRoom = new()
    {
        [MapNames.Skeld] = SystemTypes.Cafeteria,
        [MapNames.MiraHQ] = SystemTypes.Launchpad,
        [MapNames.Polus] = SystemTypes.Dropship,
        [MapNames.Airship] = SystemTypes.MainHall,
        [MapNames.Fungle] = SystemTypes.Outside
    };

    public class PlayerData
    {
        public PlayerControl Player;
        public SystemTypes NextRoom;
        public SystemTypes LastRoom;
        public int Lap;
        public string LastSuffix;
        public readonly List<PowerUp> PowerUps = [];
    }

    public enum PowerUp
    {
        Smoke,
        Taser,
        EnergyDrink,
        Grenade,
        Ice
    }

    public static readonly Dictionary<MapNames, Vector2[]> PowerUpSpawnPositions = new()
    {
        [MapNames.Skeld] = [new(8.82f, 3.69f), new(14.5f, -4.9f), new(7.73f, -14.31f), new(-0.64f, -6.89f), new(-18.08f, -13.29f), new(-18.2f, 2.48f), new(-0.87f, 5.82f)],
        [MapNames.MiraHQ] = [new(12.41f, -1.41f), new(12.46f, 6.87f), new(4.29f, 0.89f), new(10.81f, 12.09f), new(10.81f, 10.32f), new(7.89f, 10.32f), new(17.8f, 20.41f), new(16.08f, 9.96f), new(25.5f, 2.32f), new(28.24f, 4.59f), new(28.24f, 2.51f), new(28.24f, 0f), new(28.24f, -1.93f), new(19.48f, -1.93f), new(16.75f, 0.76f)],
        [MapNames.Polus] = [new(9.64f, -7.36f), new(7.5f, -12.88f), new(1.93f, -9.19f), new(3.51f, -16.22f), new(3.2f, -21.54f), new(8.6f, -17.71f), new(18.93f, -24.49f), new(20.09f, -25.15f), new(34f, -10.24f), new(40.28f, -7.46f), new(26.69f, -6.88f), new(22.33f, -6.55f), new(14.45f, -12.48f), new(14.57f, -16.95f)],
        [MapNames.Airship] = [new(12.87f, -3.46f), new(14.82f, 3.29f), new(12.32f, 2.13f), new(9.81f, 3.57f), new(5.79f, 3.39f), new(7.02f, -3.37f), new(-0.73f, 3.21f), new(-4.11f, 1.4f), new(-14.84f, -1.15f), new(-14.06f, -5.13f), new(-14.45f, -8.35f), new(-12.17f, -9.37f), new(-2.6f, -8.97f), new(10.3f, -16f), new(10.3f, -15.1f), new(5.41f, -14.6f), new(19.29f, -3.97f), new(18.14f, -3.97f), new(25.25f, -8.55f), new(29f, -1.5f), new(37.14f, -3.34f), new(38f, -3.34f), new(38.36f, 0f), new(30.51f, 1.81f), new(33.69f, 7.34f), new(32.26f, 7.34f), new(30.78f, 7.34f), new(29.21f, 7.34f), new(19.91f, 12.09f), new(17.46f, 9.12f), new(23.99f, -1.02f), new(26f, 0.41f), new(20.82f, 2.53f), new(22f, 2.53f)],
        [MapNames.Fungle] = [new(-16.9f, -2.21f), new(-17f, -4.67f), new(-10f, 0f), new(-0.9f, -8.95f), new(-5.32f, -11.89f), new(7f, -14.64f), new(15.15f, -16.06f), new(19.34f, -11.67f), new(11.34f, -7.08f), new(14.7f, -6f), new(14.34f, 0.41f), new(13.5f, 4f), new(22.61f, 3.35f), new(25.22f, 11.33f), new(2.34f, 4.23f), new(0f, 1.19f), new(-8.16f, 1.42f), new(-12.89f, 2.14f), new(-17.62f, 2.45f), new(-21.34f, -3.22f)],
        [(MapNames)6] = [new(5.08f, 24.88f), new(0.59f, 28f), new(-4.82f, 28.59f), new(-11.12f, 25.48f), new(-8.4f, 28.51f), new(-9.5f, 17.58f), new(1.32f, 16f), new(4.72f, 7.56f), new(5f, -33.61f), new(-0.77f, -35.73f), new(7.69f, -25.07f), new(12.29f, -29.15f), new(-9.93f, -29.11f), new(-9.45f, -41.63f)]
    };

    static Deathrace()
    {
        Tracks[MapNames.Dleks] = Tracks[MapNames.Skeld].ToList();
        InitialSpawnRoom[MapNames.Dleks] = InitialSpawnRoom[MapNames.Skeld];
        PowerUpSpawnPositions[MapNames.Dleks] = PowerUpSpawnPositions[MapNames.Skeld].Select(x => new Vector2(-x.x, x.y)).ToArray();
    }
    
    public static void SetupCustomOption()
    {
        var id = 69_223_001;
        Color color = Utils.GetRoleColor(CustomRoles.Racer);
        const CustomGameMode gameMode = CustomGameMode.Deathrace;
        const TabGroup tab = TabGroup.GameSettings;
        
        ClockwiseOption = new BooleanOptionItem(id++, "Deathrace.ClockwiseOption", true, tab)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode);
        
        LapsToWinOption = new IntegerOptionItem(id++, "Deathrace.LapsToWinOption", new(1, 100, 1), 10, tab)
            .SetColor(color)
            .SetGameMode(gameMode);
        
        EliminateLastToFinishEachLapOption = new BooleanOptionItem(id++, "Deathrace.EliminateLastToFinishEachLapOption", false, tab)
            .SetColor(color)
            .SetGameMode(gameMode);
        
        SpawnPowerUpsOption = new BooleanOptionItem(id++, "Deathrace.SpawnPowerUpsOption", true, tab)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode);

        PowerUpSpawnFrequencyOption = new IntegerOptionItem(id++, "Deathrace.PowerUpSpawnFrequencyOption", new(1, 60, 1), 10, tab)
            .SetParent(SpawnPowerUpsOption)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);
        
        PowerUpEffectDurationOption = new IntegerOptionItem(id++, "Deathrace.PowerUpEffectDurationOption", new(1, 60, 1), 5, tab)
            .SetParent(SpawnPowerUpsOption)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);
        
        PowerUpEffectRangeOption = new FloatOptionItem(id++, "Deathrace.PowerUpEffectRangeOption", new(0.25f, 10f, 0.25f), 5f, tab)
            .SetParent(SpawnPowerUpsOption)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Multiplier);
        
        SmokeSpeedReductionOption = new FloatOptionItem(id++, "Deathrace.SmokeSpeedReductionOption", new(0.05f, 1f, 0.05f), 0.25f, tab)
            .SetParent(SpawnPowerUpsOption)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Multiplier);
        
        EnergyDrinkSpeedIncreasementOption = new FloatOptionItem(id++, "Deathrace.EnergyDrinkSpeedIncreasementOption", new(0.05f, 3f, 0.05f), 0.75f, tab)
            .SetParent(SpawnPowerUpsOption)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Multiplier);
        
        PowerUpPickupRangeOption = new FloatOptionItem(id, "Deathrace.PowerUpPickupRangeOption", new(0.1f, 5f, 0.1f), 1f, tab)
            .SetParent(SpawnPowerUpsOption)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public static void Init()
    {
        GameGoing = false;
        LastPowerUpSpawn = 0;
        SpawnedPowerUps = [];
        Data = [];
        
        Clockwise = ClockwiseOption.GetBool();
        LapsToWin = LapsToWinOption.GetInt();
        EliminateLastToFinishEachLap = EliminateLastToFinishEachLapOption.GetBool();
        SpawnPowerUps = SpawnPowerUpsOption.GetBool();
        PowerUpSpawnFrequency = PowerUpSpawnFrequencyOption.GetInt();
        PowerUpEffectDuration = PowerUpEffectDurationOption.GetInt();
        PowerUpEffectRange = PowerUpEffectRangeOption.GetFloat();
        SmokeSpeedReduction = SmokeSpeedReductionOption.GetFloat();
        EnergyDrinkSpeedIncreasement = EnergyDrinkSpeedIncreasementOption.GetFloat();
        PowerUpPickupRange = PowerUpPickupRangeOption.GetFloat();
        
        Main.AllPlayerSpeed.SetAllValues(Main.MinSpeed);
    }

    public static System.Collections.IEnumerator GameStart()
    {
        yield return new WaitForSeconds(2f);

        Track = Tracks[Main.CurrentMap].ToList();
        Logger.Info($"Track: {string.Join(" » ", Track)}", "Deathrace");
        
        List<PlayerControl> players = Main.AllAlivePlayerControls.ToList();
        if (Main.GM.Value) players.RemoveAll(x => x.IsHost());
        if (ChatCommands.Spectators.Count > 0) players.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));
        
        Data = players.ToDictionary(x => x.PlayerId, x => new PlayerData
        {
            Player = x,
            NextRoom = Clockwise ? Track[0] : Track[^1],
            LastRoom = Clockwise ? Track[^1] : Track[0],
            Lap = 0
        });
        
        int playedOnMap = PlayedMaps.Count(x => x.Value.Contains(Main.CurrentMap));
        bool showTutorial = playedOnMap <= players.Count / 2;

        if (showTutorial)
        {
            showTutorial = PlayedMaps.Count <= players.Count / 2;

            if (showTutorial)
            {
                players.NotifyPlayers("<#ffffff>" + Translator.GetString("Deathrace.Tutorial.Basics"), 100f);
                yield return new WaitForSeconds(Main.CurrentMap == MapNames.Airship ? 10f : 5f);
                NameNotifyManager.Reset();
            }

            var track = Track.ToList();
            if (!Clockwise) track.Reverse();
            players.NotifyPlayers("<#ffffff>" + string.Format(Translator.GetString("Deathrace.Tutorial.Track"), string.Join(" » ", track.ConvertAll(x => Translator.GetString(x.ToString())))), 100f);
            yield return new WaitForSeconds(Track.Count);
            NameNotifyManager.Reset();

            if (showTutorial && SpawnPowerUps)
            {
                players.NotifyPlayers("<#ffffff>" + Translator.GetString("Deathrace.Tutorial.PowerUps"), 100f);
                yield return new WaitForSeconds(4f);
                NameNotifyManager.Reset();
            }
            
            if (Main.CurrentMap == MapNames.Airship)
                players.MassTP(new RandomSpawn.AirshipSpawnMap().Positions[Clockwise ? Track[^1] : Track[0]]);
        }
        else if (Main.CurrentMap == MapNames.Airship)
        {
            yield return new WaitForSeconds(3f);
            players.MassTP(new RandomSpawn.AirshipSpawnMap().Positions[Clockwise ? Track[^1] : Track[0]]);
        }

        for (var i = 5; i > 0; i--)
        {
            NameNotifyManager.Reset();
            players.NotifyPlayers("<#ffffff>" + string.Format(Translator.GetString("RR_ReadyQM"), i));
            yield return new WaitForSeconds(1f);
        }
        
        NameNotifyManager.Reset();
        Utils.NotifyRoles();
        
        Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
        Utils.SyncAllSettings();
        
        GameGoing = true;
    }

    public static bool KnowRoleColor(PlayerControl seer, PlayerControl target, out string color)
    {
        color = "#FFFFFF";
        return true;
    }

    public static string GetSuffix(PlayerControl seer, PlayerControl target, bool hud)
    {
        if (!GameGoing || seer.PlayerId != target.PlayerId || (seer.IsHost() && !hud) || !Data.TryGetValue(seer.PlayerId, out var data)) return string.Empty;

        StringBuilder sb = new("<#ffffff>");
        
        sb.AppendLine($"<#888888>{string.Format(Translator.GetString("Deathrace.Lap"), data.Lap + 1, LapsToWin)}</color>");

        if (seer.IsInRoom(data.LastRoom))
            sb.Append($"<u>{Translator.GetString(data.LastRoom.ToString())}</u> ");
        
        int index = Track.IndexOf(data.LastRoom);
        bool coordinateCheck = CoordinateChecks.TryGetValue((int)data.NextRoom, out var coordinates);
        string nextRoom = Translator.GetString(coordinateCheck ? "Deathrace.CoordinateCheck" : data.NextRoom.ToString());
        SystemTypes nextNextRoom = Clockwise ? (index >= Track.Count - 2 ? Track[(index + 2) % Track.Count] : Track[index + 2]) : (index <= 1 ? Track[(index - 2 + Track.Count) % Track.Count] : Track[index - 2]);

        sb.Append($"» {nextRoom} » {Translator.GetString(nextNextRoom.ToString())} » ....");
        
        if (UsableVentIDs.TryGetValue(Main.CurrentMap, out var dict) && dict.ContainsKey(data.NextRoom) && dict.ContainsKey(data.LastRoom))
            sb.Append($"\n<#ffff44>{Translator.GetString("Deathrace.VentUse")}</color>");

        if (coordinateCheck)
            sb.Append($"\n<#ffff44>{string.Format(Translator.GetString($"Deathrace.CoordinateCheckInfo.{(int)data.NextRoom}"), Math.Round(Vector2.Distance(coordinates, seer.Pos()), 1))}</color>");
        
        if (data.PowerUps.Count > 0)
        {
            PowerUp powerUp = data.PowerUps[0];
            
            char icon = powerUp switch
            {
                PowerUp.Smoke => '♨',
                PowerUp.Taser => '〄',
                PowerUp.EnergyDrink => '∂',
                PowerUp.Grenade => '♁',
                PowerUp.Ice => '☃',
                _ => throw new ArgumentOutOfRangeException(nameof(powerUp), powerUp, "Unhandled power-up type")
            };
            
            Color color = powerUp switch
            {
                PowerUp.Smoke => new Color(0.5f, 0.5f, 0.5f),
                PowerUp.Taser => new Color(1f, 1f, 0f),
                PowerUp.EnergyDrink => new Color(1f, 0.5f, 0f),
                PowerUp.Grenade => new Color(1f, 0f, 0f),
                PowerUp.Ice => new Color(0f, 1f, 1f),
                _ => throw new ArgumentOutOfRangeException(nameof(powerUp), powerUp, "Unhandled power-up type")
            };
            
            sb.Append(Utils.ColorString(color, $"\n{string.Format(Translator.GetString($"Deathrace.PowerUpInfo.{powerUp}"), icon)}"));
        }

        if (data.PowerUps.Count > 1)
            sb.Append($"\n<#00a5ff><size=80%>{string.Format(Translator.GetString("Deathrace.PowerUpInfo.More"), data.PowerUps.Count - 1)}</size></color>");
        
        return sb.ToString();
    }

    public static string GetStatistics(byte id)
    {
        if (!Data.TryGetValue(id, out var data)) return string.Empty;
        return string.Format(Translator.GetString("Deathrace.Lap"), data.Lap, LapsToWin);
    }

    public static bool CheckGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;
        if (GameStates.IsEnded || !GameGoing) return false;
        PlayerControl[] aapc = Main.AllAlivePlayerControls;

        switch (aapc.Length)
        {
            case 1:
                PlayerControl winner = aapc[0];
                Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "Deathrace");
                CustomWinnerHolder.WinnerIds = [winner.PlayerId];
                Main.DoBlockNameChange = true;
                return true;
            case 0:
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                Logger.Warn("No players alive. Force ending the game", "Deathrace");
                return true;
            default:
                return false;
        }
    }

    public static string GetTaskBarText()
    {
        return string.Join('\n', Data.Select(x => $"{x.Key.ColoredPlayerName()}: {string.Format(Translator.GetString("Deathrace.Lap"), x.Value.Lap + 1, LapsToWin)}"));
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!SpawnPowerUps || !Data.TryGetValue(killer.PlayerId, out var data) || data.PowerUps.Count == 0) return;
        PowerUp powerUp = data.PowerUps[0];
        if (powerUp != PowerUp.Taser) return;
        killer.RPCPlayCustomSound("Line");
        data.PowerUps.RemoveAt(0);
        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
        target.RPCPlayCustomSound("Bite");
        target.MarkDirtySettings();
        LateTask.New(() =>
        {
            if (target == null) return;
            Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
            target.MarkDirtySettings();
        }, PowerUpEffectDuration, $"Taser Revert ({Main.AllPlayerNames.GetValueOrDefault(killer.PlayerId, $"ID {killer.PlayerId}")} => {Main.AllPlayerNames.GetValueOrDefault(target.PlayerId, $"ID {target.PlayerId}")})");
    }

    public static bool CanUseVent(PlayerControl pc, int ventId)
    {
        if (!AmongUsClient.Instance.AmHost || (pc.inVent && pc.GetClosestVent()?.Id == ventId)) return true;
        return Data.TryGetValue(pc.PlayerId, out var data) && UsableVentIDs.TryGetValue(Main.CurrentMap, out var dict) && dict.ContainsKey(data.NextRoom) && dict.TryGetValue(data.LastRoom, out var vents) && vents.Contains(ventId);
    }

    public static void UsePowerUp(PlayerControl pc)
    {
        if (!SpawnPowerUps || !Data.TryGetValue(pc.PlayerId, out var data) || data.PowerUps.Count == 0) return;
        PowerUp powerUp = data.PowerUps[0];
        pc.RPCPlayCustomSound("Line");
        data.PowerUps.RemoveAt(0);
        PlayerControl[] playersInRange = Utils.GetPlayersInRadius(PowerUpEffectRange, pc.Pos()).Without(pc).ToArray();

        switch (powerUp)
        {
            case PowerUp.Smoke:
            {
                playersInRange = playersInRange.Where(x => !Mathf.Approximately(Main.AllPlayerSpeed[x.PlayerId], Main.MinSpeed)).ToArray();
                
                foreach (PlayerControl player in playersInRange)
                {
                    if (Main.AllPlayerSpeed[player.PlayerId] < 0f) Main.AllPlayerSpeed[player.PlayerId] += SmokeSpeedReduction;
                    else Main.AllPlayerSpeed[player.PlayerId] -= SmokeSpeedReduction;
                    
                    player.MarkDirtySettings();
                }
                
                LateTask.New(() =>
                {
                    foreach (PlayerControl player in playersInRange)
                    {
                        if (player == null) continue;
                        
                        if (Main.AllPlayerSpeed[player.PlayerId] < 0f) Main.AllPlayerSpeed[player.PlayerId] -= SmokeSpeedReduction;
                        else Main.AllPlayerSpeed[player.PlayerId] += SmokeSpeedReduction;
                        
                        player.MarkDirtySettings();
                    }
                }, PowerUpEffectDuration, $"Smoke Revert ({Main.AllPlayerNames.GetValueOrDefault(pc.PlayerId, $"ID {pc.PlayerId}")})");
                break;
            }
            case PowerUp.EnergyDrink:
            {
                if (Main.AllPlayerSpeed[pc.PlayerId] < 0f) Main.AllPlayerSpeed[pc.PlayerId] -= EnergyDrinkSpeedIncreasement;
                else Main.AllPlayerSpeed[pc.PlayerId] += EnergyDrinkSpeedIncreasement;
                pc.MarkDirtySettings();
                
                LateTask.New(() =>
                {
                    if (pc == null || Mathf.Approximately(Main.AllPlayerSpeed[pc.PlayerId], Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod)) || Mathf.Approximately(Main.AllPlayerSpeed[pc.PlayerId], Main.MinSpeed)) return;
                    if (Main.AllPlayerSpeed[pc.PlayerId] < 0f) Main.AllPlayerSpeed[pc.PlayerId] += EnergyDrinkSpeedIncreasement;
                    else Main.AllPlayerSpeed[pc.PlayerId] -= EnergyDrinkSpeedIncreasement;
                    pc.MarkDirtySettings();
                }, PowerUpEffectDuration, $"Energy Drink Revert ({Main.AllPlayerNames.GetValueOrDefault(pc.PlayerId, $"ID {pc.PlayerId}")})");
                break;
            }
            case PowerUp.Grenade:
            {
                foreach (PlayerControl player in playersInRange)
                {
                    if (!Main.PlayerStates.TryGetValue(player.PlayerId, out var state)) return;
                    state.IsBlackOut = true;
                    player.RPCPlayCustomSound("FlashBang");
                    player.MarkDirtySettings();
                }
                
                LateTask.New(() =>
                {
                    foreach (PlayerControl player in playersInRange)
                    {
                        if (player == null || !Main.PlayerStates.TryGetValue(player.PlayerId, out var state) || !state.IsBlackOut) continue;
                        state.IsBlackOut = false;
                        RPC.PlaySoundRPC(player.PlayerId, Sounds.TaskComplete);
                        player.MarkDirtySettings();
                    }
                }, PowerUpEffectDuration, $"Grenade Revert ({Main.AllPlayerNames.GetValueOrDefault(pc.PlayerId, $"ID {pc.PlayerId}")})");
                break;
            }
            case PowerUp.Ice:
            {
                foreach (PlayerControl player in playersInRange)
                {
                    Main.AllPlayerSpeed[player.PlayerId] *= -1;
                    RPC.PlaySoundRPC(player.PlayerId, Sounds.SabotageSound);
                    player.MarkDirtySettings();
                }
                
                LateTask.New(() =>
                {
                    foreach (PlayerControl player in playersInRange)
                    {
                        if (player == null || Main.AllPlayerSpeed[player.PlayerId] >= 0f) continue;
                        Main.AllPlayerSpeed[player.PlayerId] *= -1;
                        RPC.PlaySoundRPC(player.PlayerId, Sounds.TaskComplete);
                        player.MarkDirtySettings();
                    }
                }, PowerUpEffectDuration, $"Ice Revert ({Main.AllPlayerNames.GetValueOrDefault(pc.PlayerId, $"ID {pc.PlayerId}")})");
                break;
            }
        }
    }

    public static class FixedUpdatePatch
    {
        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost || !GameGoing || GameStates.IsEnded) return;

            long now = Utils.TimeStamp;

            if (SpawnPowerUps && now - LastPowerUpSpawn >= PowerUpSpawnFrequency)
            {
                PowerUp powerUp = Enum.GetValues<PowerUp>().RandomElement();
                Vector2[] availableSpawns = PowerUpSpawnPositions[Main.CurrentMap].Except(SpawnedPowerUps.ConvertAll(x => x.Position)).ToArray();
                if (availableSpawns.Length > 0) SpawnedPowerUps.Add(new DeathracePowerUp(availableSpawns.RandomElement(), powerUp));
                LastPowerUpSpawn = now;
            }
            
            byte removeId = byte.MaxValue;
            
            foreach ((byte id, PlayerData data) in Data)
            {
                if (data.Player == null || !data.Player.IsAlive())
                {
                    removeId = id;
                    continue;
                }
                
                if (data.Player.inMovingPlat) continue;

                if (SpawnedPowerUps.FindFirst(x => Vector2.Distance(data.Player.Pos(), x.Position) <= PowerUpPickupRange, out var powerUp))
                {
                    RPC.PlaySoundRPC(id, Sounds.TaskUpdateSound);
                    data.PowerUps.Add(powerUp.PowerUp);
                    SpawnedPowerUps.Remove(powerUp);
                    powerUp.Despawn();
                }

                PlainShipRoom room = data.Player.GetPlainShipRoom();
                bool coordinateCheck = CoordinateChecks.TryGetValue((int)data.NextRoom, out var coordinates);
                if (room != null && room.RoomId is SystemTypes.Hallway or SystemTypes.Outside or SystemTypes.Decontamination2 or SystemTypes.Decontamination3) room = null;

                if ((room == null && !coordinateCheck) || (room != null && room.RoomId == data.LastRoom))
                {
                    CheckAndNotify(data);
                    continue;
                }

                if (coordinateCheck ? Vector2.Distance(coordinates, data.Player.Pos()) < 2f : room.RoomId == data.NextRoom)
                {
                    data.LastRoom = data.NextRoom;
                    int index = Track.IndexOf(data.NextRoom);
                    bool endOfTrack = Clockwise ? index == Track.Count - 1 : index == 0;

                    if (endOfTrack)
                    {
                        data.Lap++;
                        RPC.PlaySoundRPC(id, Sounds.TaskComplete);
                        Utils.SendRPC(CustomRPC.DeathraceSync, id, data.Lap);
                        
                        if (data.Lap >= LapsToWin)
                        {
                            GameGoing = false;
                            CustomWinnerHolder.WinnerIds = [id];
                            break;
                        }

                        if (EliminateLastToFinishEachLap && Data.Count > 1)
                        {
                            var minLap = Data.Min(x => x.Value.Lap);
                            var playersWithMinLap = Data.Where(x => x.Value.Lap == minLap).ToList();

                            if (playersWithMinLap.Count == 1)
                            {
                                playersWithMinLap[0].Value.Player.Suicide();
                                removeId = playersWithMinLap[0].Key;
                                continue;
                            }
                        }

                        data.NextRoom = Clockwise ? Track[0] : Track[^1];
                    }
                    else
                    {
                        data.NextRoom = Clockwise ? Track[index + 1] : Track[index - 1];
                        RPC.PlaySoundRPC(id, Sounds.TaskUpdateSound);
                    }

                    Logger.Info($"{Main.AllPlayerNames.GetValueOrDefault(id, $"ID {id}")} entered {data.LastRoom}, next is {data.NextRoom}", "Deathrace");
                }
                else if (!coordinateCheck && InitialSpawnRoom[Main.CurrentMap] != room.RoomId)
                {
                    Logger.Info($"{Main.AllPlayerNames.GetValueOrDefault(id, $"ID {id}")} was supposed to enter {data.NextRoom}, but they've entered {room.RoomId}", "Deathrace");
                    data.Player.Suicide();
                    removeId = id;
                }
                
                if (data.Player.AmOwner) continue;

                CheckAndNotify(data);
            }

            Data.Remove(removeId);
        }

        private static void CheckAndNotify(PlayerData data)
        {
            string suffix = GetSuffix(data.Player, data.Player, false);

            if (data.LastSuffix != suffix)
            {
                Utils.NotifyRoles(SpecifySeer: data.Player, SpecifyTarget: data.Player);
                data.LastSuffix = suffix;
            }
        }
    }

    public class Racer : RoleBase
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
            UsePowerUp(pc);
            return false;
        }
    }
}
