using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Roles;
using Hazel;
using UnityEngine;

namespace EHR.Gamemodes;

public static class RoomRush
{
    private static OptionItem GlobalTimeAddition;
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
    private static readonly StringBuilder Suffix = new();
    private static HashSet<SystemTypes> AllRooms = [];
    private static SystemTypes RoomGoal;
    private static long TimeLimitEndTS;
    private static HashSet<byte> DonePlayers = [];

    private static bool GameGoing;
    private static DateTime GameStartDateTime;

    private static RandomSpawn.SpawnMap Map;

    // Minimum time needed to make the closest path between the two rooms with the worst possible conditions at 1.25x player speed
    public static readonly Dictionary<MapNames, Dictionary<(SystemTypes, SystemTypes), int>> RawTimeNeeded = new()
    {
        [MapNames.Skeld] = new()
        {
            [(SystemTypes.Admin, SystemTypes.Cafeteria)] = 2,
            [(SystemTypes.Admin, SystemTypes.Nav)] = 10,
            [(SystemTypes.Admin, SystemTypes.Weapons)] = 6,
            [(SystemTypes.Admin, SystemTypes.LifeSupp)] = 8,
            [(SystemTypes.Admin, SystemTypes.Shields)] = 5,
            [(SystemTypes.Admin, SystemTypes.Electrical)] = 6,
            [(SystemTypes.Admin, SystemTypes.Reactor)] = 11,
            [(SystemTypes.Admin, SystemTypes.Storage)] = 2,
            [(SystemTypes.Admin, SystemTypes.LowerEngine)] = 8,
            [(SystemTypes.Admin, SystemTypes.UpperEngine)] = 8,
            [(SystemTypes.Admin, SystemTypes.Comms)] = 5,
            [(SystemTypes.Admin, SystemTypes.Security)] = 11,
            [(SystemTypes.Admin, SystemTypes.MedBay)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.Nav)] = 5,
            [(SystemTypes.Cafeteria, SystemTypes.Weapons)] = 1,
            [(SystemTypes.Cafeteria, SystemTypes.LifeSupp)] = 4,
            [(SystemTypes.Cafeteria, SystemTypes.Shields)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.Electrical)] = 6,
            [(SystemTypes.Cafeteria, SystemTypes.Reactor)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.Storage)] = 2,
            [(SystemTypes.Cafeteria, SystemTypes.LowerEngine)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.UpperEngine)] = 4,
            [(SystemTypes.Cafeteria, SystemTypes.Comms)] = 5,
            [(SystemTypes.Cafeteria, SystemTypes.Security)] = 6,
            [(SystemTypes.Cafeteria, SystemTypes.MedBay)] = 2,
            [(SystemTypes.Nav, SystemTypes.Weapons)] = 3,
            [(SystemTypes.Nav, SystemTypes.LifeSupp)] = 3,
            [(SystemTypes.Nav, SystemTypes.Shields)] = 4,
            [(SystemTypes.Nav, SystemTypes.Electrical)] = 11,
            [(SystemTypes.Nav, SystemTypes.Reactor)] = 15,
            [(SystemTypes.Nav, SystemTypes.Storage)] = 7,
            [(SystemTypes.Nav, SystemTypes.LowerEngine)] = 13,
            [(SystemTypes.Nav, SystemTypes.UpperEngine)] = 12,
            [(SystemTypes.Nav, SystemTypes.Comms)] = 6,
            [(SystemTypes.Nav, SystemTypes.Security)] = 15,
            [(SystemTypes.Nav, SystemTypes.MedBay)] = 11,
            [(SystemTypes.Weapons, SystemTypes.LifeSupp)] = 2,
            [(SystemTypes.Weapons, SystemTypes.Shields)] = 5,
            [(SystemTypes.Weapons, SystemTypes.Electrical)] = 10,
            [(SystemTypes.Weapons, SystemTypes.Reactor)] = 11,
            [(SystemTypes.Weapons, SystemTypes.Storage)] = 6,
            [(SystemTypes.Weapons, SystemTypes.LowerEngine)] = 12,
            [(SystemTypes.Weapons, SystemTypes.UpperEngine)] = 8,
            [(SystemTypes.Weapons, SystemTypes.Comms)] = 7,
            [(SystemTypes.Weapons, SystemTypes.Security)] = 11,
            [(SystemTypes.Weapons, SystemTypes.MedBay)] = 7,
            [(SystemTypes.LifeSupp, SystemTypes.Shields)] = 5,
            [(SystemTypes.LifeSupp, SystemTypes.Electrical)] = 12,
            [(SystemTypes.LifeSupp, SystemTypes.Reactor)] = 14,
            [(SystemTypes.LifeSupp, SystemTypes.Storage)] = 7,
            [(SystemTypes.LifeSupp, SystemTypes.LowerEngine)] = 14,
            [(SystemTypes.LifeSupp, SystemTypes.UpperEngine)] = 10,
            [(SystemTypes.LifeSupp, SystemTypes.Comms)] = 7,
            [(SystemTypes.LifeSupp, SystemTypes.Security)] = 13,
            [(SystemTypes.LifeSupp, SystemTypes.MedBay)] = 9,
            [(SystemTypes.Shields, SystemTypes.Electrical)] = 7,
            [(SystemTypes.Shields, SystemTypes.Reactor)] = 12,
            [(SystemTypes.Shields, SystemTypes.Storage)] = 3,
            [(SystemTypes.Shields, SystemTypes.LowerEngine)] = 9,
            [(SystemTypes.Shields, SystemTypes.UpperEngine)] = 11,
            [(SystemTypes.Shields, SystemTypes.Comms)] = 2,
            [(SystemTypes.Shields, SystemTypes.Security)] = 12,
            [(SystemTypes.Shields, SystemTypes.MedBay)] = 10,
            [(SystemTypes.Electrical, SystemTypes.Reactor)] = 6,
            [(SystemTypes.Electrical, SystemTypes.Storage)] = 2,
            [(SystemTypes.Electrical, SystemTypes.LowerEngine)] = 3,
            [(SystemTypes.Electrical, SystemTypes.UpperEngine)] = 7,
            [(SystemTypes.Electrical, SystemTypes.Comms)] = 7,
            [(SystemTypes.Electrical, SystemTypes.Security)] = 6,
            [(SystemTypes.Electrical, SystemTypes.MedBay)] = 11,
            [(SystemTypes.Reactor, SystemTypes.Storage)] = 7,
            [(SystemTypes.Reactor, SystemTypes.LowerEngine)] = 3,
            [(SystemTypes.Reactor, SystemTypes.UpperEngine)] = 3,
            [(SystemTypes.Reactor, SystemTypes.Comms)] = 12,
            [(SystemTypes.Reactor, SystemTypes.Security)] = 2,
            [(SystemTypes.Reactor, SystemTypes.MedBay)] = 6,
            [(SystemTypes.Storage, SystemTypes.LowerEngine)] = 5,
            [(SystemTypes.Storage, SystemTypes.UpperEngine)] = 8,
            [(SystemTypes.Storage, SystemTypes.Comms)] = 3,
            [(SystemTypes.Storage, SystemTypes.Security)] = 8,
            [(SystemTypes.Storage, SystemTypes.MedBay)] = 7,
            [(SystemTypes.LowerEngine, SystemTypes.UpperEngine)] = 3,
            [(SystemTypes.LowerEngine, SystemTypes.Comms)] = 9,
            [(SystemTypes.LowerEngine, SystemTypes.Security)] = 3,
            [(SystemTypes.LowerEngine, SystemTypes.MedBay)] = 7,
            [(SystemTypes.UpperEngine, SystemTypes.Comms)] = 11,
            [(SystemTypes.UpperEngine, SystemTypes.Security)] = 3,
            [(SystemTypes.UpperEngine, SystemTypes.MedBay)] = 3,
            [(SystemTypes.Comms, SystemTypes.Security)] = 12,
            [(SystemTypes.Comms, SystemTypes.MedBay)] = 10,
            [(SystemTypes.Security, SystemTypes.MedBay)] = 6
        },
        [MapNames.MiraHQ] = new()
        {
            [(SystemTypes.Decontamination, SystemTypes.Launchpad)] = 9,
            [(SystemTypes.Decontamination, SystemTypes.Cafeteria)] = 8,
            [(SystemTypes.Decontamination, SystemTypes.Storage)] = 11,
            [(SystemTypes.Decontamination, SystemTypes.Reactor)] = 1,
            [(SystemTypes.Decontamination, SystemTypes.Laboratory)] = 3,
            [(SystemTypes.Decontamination, SystemTypes.LockerRoom)] = 1,
            [(SystemTypes.Decontamination, SystemTypes.Admin)] = 9,
            [(SystemTypes.Decontamination, SystemTypes.Office)] = 8,
            [(SystemTypes.Decontamination, SystemTypes.Greenhouse)] = 9,
            [(SystemTypes.Decontamination, SystemTypes.Comms)] = 4,
            [(SystemTypes.Decontamination, SystemTypes.Balcony)] = 11,
            [(SystemTypes.Decontamination, SystemTypes.MedBay)] = 4,
            [(SystemTypes.Launchpad, SystemTypes.Cafeteria)] = 13,
            [(SystemTypes.Launchpad, SystemTypes.Storage)] = 15,
            [(SystemTypes.Launchpad, SystemTypes.Reactor)] = 13,
            [(SystemTypes.Launchpad, SystemTypes.Laboratory)] = 13,
            [(SystemTypes.Launchpad, SystemTypes.LockerRoom)] = 8,
            [(SystemTypes.Launchpad, SystemTypes.Admin)] = 13,
            [(SystemTypes.Launchpad, SystemTypes.Office)] = 13,
            [(SystemTypes.Launchpad, SystemTypes.Greenhouse)] = 14,
            [(SystemTypes.Launchpad, SystemTypes.Comms)] = 8,
            [(SystemTypes.Launchpad, SystemTypes.Balcony)] = 15,
            [(SystemTypes.Launchpad, SystemTypes.MedBay)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.Storage)] = 1,
            [(SystemTypes.Cafeteria, SystemTypes.Reactor)] = 12,
            [(SystemTypes.Cafeteria, SystemTypes.Laboratory)] = 13,
            [(SystemTypes.Cafeteria, SystemTypes.LockerRoom)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.Admin)] = 6,
            [(SystemTypes.Cafeteria, SystemTypes.Office)] = 6,
            [(SystemTypes.Cafeteria, SystemTypes.Greenhouse)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.Comms)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.Balcony)] = 1,
            [(SystemTypes.Cafeteria, SystemTypes.MedBay)] = 8,
            [(SystemTypes.Storage, SystemTypes.Reactor)] = 14,
            [(SystemTypes.Storage, SystemTypes.Laboratory)] = 15,
            [(SystemTypes.Storage, SystemTypes.LockerRoom)] = 9,
            [(SystemTypes.Storage, SystemTypes.Admin)] = 8,
            [(SystemTypes.Storage, SystemTypes.Office)] = 8,
            [(SystemTypes.Storage, SystemTypes.Greenhouse)] = 9,
            [(SystemTypes.Storage, SystemTypes.Comms)] = 9,
            [(SystemTypes.Storage, SystemTypes.Balcony)] = 2,
            [(SystemTypes.Storage, SystemTypes.MedBay)] = 10,
            [(SystemTypes.Reactor, SystemTypes.Laboratory)] = 2,
            [(SystemTypes.Reactor, SystemTypes.LockerRoom)] = 4,
            [(SystemTypes.Reactor, SystemTypes.Admin)] = 12,
            [(SystemTypes.Reactor, SystemTypes.Office)] = 12,
            [(SystemTypes.Reactor, SystemTypes.Greenhouse)] = 13,
            [(SystemTypes.Reactor, SystemTypes.Comms)] = 7,
            [(SystemTypes.Reactor, SystemTypes.Balcony)] = 14,
            [(SystemTypes.Reactor, SystemTypes.MedBay)] = 8,
            [(SystemTypes.Laboratory, SystemTypes.LockerRoom)] = 5,
            [(SystemTypes.Laboratory, SystemTypes.Admin)] = 13,
            [(SystemTypes.Laboratory, SystemTypes.Office)] = 12,
            [(SystemTypes.Laboratory, SystemTypes.Greenhouse)] = 13,
            [(SystemTypes.Laboratory, SystemTypes.Comms)] = 8,
            [(SystemTypes.Laboratory, SystemTypes.Balcony)] = 15,
            [(SystemTypes.Laboratory, SystemTypes.MedBay)] = 8,
            [(SystemTypes.LockerRoom, SystemTypes.Admin)] = 7,
            [(SystemTypes.LockerRoom, SystemTypes.Office)] = 6,
            [(SystemTypes.LockerRoom, SystemTypes.Greenhouse)] = 7,
            [(SystemTypes.LockerRoom, SystemTypes.Comms)] = 2,
            [(SystemTypes.LockerRoom, SystemTypes.Balcony)] = 9,
            [(SystemTypes.LockerRoom, SystemTypes.MedBay)] = 3,
            [(SystemTypes.Admin, SystemTypes.Office)] = 2,
            [(SystemTypes.Admin, SystemTypes.Greenhouse)] = 2,
            [(SystemTypes.Admin, SystemTypes.Comms)] = 7,
            [(SystemTypes.Admin, SystemTypes.Balcony)] = 8,
            [(SystemTypes.Admin, SystemTypes.MedBay)] = 8,
            [(SystemTypes.Office, SystemTypes.Greenhouse)] = 2,
            [(SystemTypes.Office, SystemTypes.Comms)] = 6,
            [(SystemTypes.Office, SystemTypes.Balcony)] = 8,
            [(SystemTypes.Office, SystemTypes.MedBay)] = 7,
            [(SystemTypes.Greenhouse, SystemTypes.Comms)] = 7,
            [(SystemTypes.Greenhouse, SystemTypes.Balcony)] = 9,
            [(SystemTypes.Greenhouse, SystemTypes.MedBay)] = 8,
            [(SystemTypes.Comms, SystemTypes.Balcony)] = 9,
            [(SystemTypes.Comms, SystemTypes.MedBay)] = 2,
            [(SystemTypes.Balcony, SystemTypes.MedBay)] = 10
        },
        [MapNames.Polus] = new()
        {
            [(SystemTypes.Decontamination2, SystemTypes.Electrical)] = 15,
            [(SystemTypes.Decontamination2, SystemTypes.Security)] = 17,
            [(SystemTypes.Decontamination2, SystemTypes.LifeSupp)] = 18,
            [(SystemTypes.Decontamination2, SystemTypes.Weapons)] = 16,
            [(SystemTypes.Decontamination2, SystemTypes.Comms)] = 16,
            [(SystemTypes.Decontamination2, SystemTypes.Office)] = 14,
            [(SystemTypes.Decontamination2, SystemTypes.Admin)] = 17,
            [(SystemTypes.Decontamination2, SystemTypes.Specimens)] = 6,
            [(SystemTypes.Decontamination2, SystemTypes.Laboratory)] = 1,
            [(SystemTypes.Decontamination2, SystemTypes.Storage)] = 14,
            [(SystemTypes.Decontamination2, SystemTypes.Dropship)] = 13,
            [(SystemTypes.Decontamination2, SystemTypes.Decontamination3)] = 6,
            [(SystemTypes.Decontamination2, SystemTypes.BoilerRoom)] = 20,
            [(SystemTypes.Decontamination3, SystemTypes.Electrical)] = 14,
            [(SystemTypes.Decontamination3, SystemTypes.Security)] = 17,
            [(SystemTypes.Decontamination3, SystemTypes.LifeSupp)] = 13,
            [(SystemTypes.Decontamination3, SystemTypes.Weapons)] = 10,
            [(SystemTypes.Decontamination3, SystemTypes.Comms)] = 11,
            [(SystemTypes.Decontamination3, SystemTypes.Office)] = 7,
            [(SystemTypes.Decontamination3, SystemTypes.Admin)] = 5,
            [(SystemTypes.Decontamination3, SystemTypes.Specimens)] = 5,
            [(SystemTypes.Decontamination3, SystemTypes.Laboratory)] = 14,
            [(SystemTypes.Decontamination3, SystemTypes.Storage)] = 13,
            [(SystemTypes.Decontamination3, SystemTypes.Dropship)] = 14,
            [(SystemTypes.Decontamination3, SystemTypes.BoilerRoom)] = 15,
            [(SystemTypes.Electrical, SystemTypes.Security)] = 1,
            [(SystemTypes.Electrical, SystemTypes.LifeSupp)] = 2,
            [(SystemTypes.Electrical, SystemTypes.Weapons)] = 5,
            [(SystemTypes.Electrical, SystemTypes.Comms)] = 5,
            [(SystemTypes.Electrical, SystemTypes.Office)] = 7,
            [(SystemTypes.Electrical, SystemTypes.Admin)] = 8,
            [(SystemTypes.Electrical, SystemTypes.Specimens)] = 14,
            [(SystemTypes.Electrical, SystemTypes.Laboratory)] = 5,
            [(SystemTypes.Electrical, SystemTypes.Storage)] = 3,
            [(SystemTypes.Electrical, SystemTypes.Dropship)] = 3,
            [(SystemTypes.Electrical, SystemTypes.BoilerRoom)] = 4,
            [(SystemTypes.Security, SystemTypes.LifeSupp)] = 5,
            [(SystemTypes.Security, SystemTypes.Weapons)] = 8,
            [(SystemTypes.Security, SystemTypes.Comms)] = 8,
            [(SystemTypes.Security, SystemTypes.Office)] = 10,
            [(SystemTypes.Security, SystemTypes.Admin)] = 11,
            [(SystemTypes.Security, SystemTypes.Specimens)] = 17,
            [(SystemTypes.Security, SystemTypes.Laboratory)] = 8,
            [(SystemTypes.Security, SystemTypes.Storage)] = 6,
            [(SystemTypes.Security, SystemTypes.Dropship)] = 6,
            [(SystemTypes.Security, SystemTypes.BoilerRoom)] = 7,
            [(SystemTypes.LifeSupp, SystemTypes.Weapons)] = 4,
            [(SystemTypes.LifeSupp, SystemTypes.Comms)] = 3,
            [(SystemTypes.LifeSupp, SystemTypes.Office)] = 6,
            [(SystemTypes.LifeSupp, SystemTypes.Admin)] = 7,
            [(SystemTypes.LifeSupp, SystemTypes.Specimens)] = 14,
            [(SystemTypes.LifeSupp, SystemTypes.Laboratory)] = 10,
            [(SystemTypes.LifeSupp, SystemTypes.Storage)] = 8,
            [(SystemTypes.LifeSupp, SystemTypes.Dropship)] = 7,
            [(SystemTypes.LifeSupp, SystemTypes.BoilerRoom)] = 1,
            [(SystemTypes.Weapons, SystemTypes.Comms)] = 2,
            [(SystemTypes.Weapons, SystemTypes.Office)] = 4,
            [(SystemTypes.Weapons, SystemTypes.Admin)] = 5,
            [(SystemTypes.Weapons, SystemTypes.Specimens)] = 11,
            [(SystemTypes.Weapons, SystemTypes.Laboratory)] = 7,
            [(SystemTypes.Weapons, SystemTypes.Storage)] = 5,
            [(SystemTypes.Weapons, SystemTypes.Dropship)] = 6,
            [(SystemTypes.Weapons, SystemTypes.BoilerRoom)] = 5,
            [(SystemTypes.Comms, SystemTypes.Office)] = 5,
            [(SystemTypes.Comms, SystemTypes.Admin)] = 5,
            [(SystemTypes.Comms, SystemTypes.Specimens)] = 12,
            [(SystemTypes.Comms, SystemTypes.Laboratory)] = 8,
            [(SystemTypes.Comms, SystemTypes.Storage)] = 5,
            [(SystemTypes.Comms, SystemTypes.Dropship)] = 6,
            [(SystemTypes.Comms, SystemTypes.BoilerRoom)] = 5,
            [(SystemTypes.Office, SystemTypes.Admin)] = 2,
            [(SystemTypes.Office, SystemTypes.Specimens)] = 9,
            [(SystemTypes.Office, SystemTypes.Laboratory)] = 5,
            [(SystemTypes.Office, SystemTypes.Storage)] = 7,
            [(SystemTypes.Office, SystemTypes.Dropship)] = 7,
            [(SystemTypes.Office, SystemTypes.BoilerRoom)] = 9,
            [(SystemTypes.Admin, SystemTypes.Specimens)] = 6,
            [(SystemTypes.Admin, SystemTypes.Laboratory)] = 8,
            [(SystemTypes.Admin, SystemTypes.Storage)] = 7,
            [(SystemTypes.Admin, SystemTypes.Dropship)] = 8,
            [(SystemTypes.Admin, SystemTypes.BoilerRoom)] = 9,
            [(SystemTypes.Specimens, SystemTypes.Laboratory)] = 5,
            [(SystemTypes.Specimens, SystemTypes.Storage)] = 14,
            [(SystemTypes.Specimens, SystemTypes.Dropship)] = 12,
            [(SystemTypes.Specimens, SystemTypes.BoilerRoom)] = 15,
            [(SystemTypes.Laboratory, SystemTypes.Storage)] = 5,
            [(SystemTypes.Laboratory, SystemTypes.Dropship)] = 4,
            [(SystemTypes.Laboratory, SystemTypes.BoilerRoom)] = 11,
            [(SystemTypes.Storage, SystemTypes.Dropship)] = 3,
            [(SystemTypes.Storage, SystemTypes.BoilerRoom)] = 9,
            [(SystemTypes.Dropship, SystemTypes.BoilerRoom)] = 9
        },
        [MapNames.Airship] = new()
        {
            [(SystemTypes.Ventilation, SystemTypes.Cockpit)] = 24,
            [(SystemTypes.Ventilation, SystemTypes.Armory)] = 23,
            [(SystemTypes.Ventilation, SystemTypes.Comms)] = 23,
            [(SystemTypes.Ventilation, SystemTypes.Engine)] = 17,
            [(SystemTypes.Ventilation, SystemTypes.Brig)] = 22,
            [(SystemTypes.Ventilation, SystemTypes.VaultRoom)] = 22,
            [(SystemTypes.Ventilation, SystemTypes.Kitchen)] = 17,
            [(SystemTypes.Ventilation, SystemTypes.ViewingDeck)] = 20,
            [(SystemTypes.Ventilation, SystemTypes.GapRoom)] = 23,
            [(SystemTypes.Ventilation, SystemTypes.MainHall)] = 13,
            [(SystemTypes.Ventilation, SystemTypes.Showers)] = 10,
            [(SystemTypes.Ventilation, SystemTypes.Records)] = 9,
            [(SystemTypes.Ventilation, SystemTypes.Lounge)] = 7,
            [(SystemTypes.Ventilation, SystemTypes.Security)] = 14,
            [(SystemTypes.Ventilation, SystemTypes.CargoBay)] = 11,
            [(SystemTypes.Ventilation, SystemTypes.Electrical)] = 7,
            [(SystemTypes.Ventilation, SystemTypes.Medical)] = 13,
            [(SystemTypes.Ventilation, SystemTypes.MeetingRoom)] = 25,
            [(SystemTypes.Ventilation, SystemTypes.HallOfPortraits)] = 16,
            [(SystemTypes.Cockpit, SystemTypes.Armory)] = 3,
            [(SystemTypes.Cockpit, SystemTypes.Comms)] = 2,
            [(SystemTypes.Cockpit, SystemTypes.Engine)] = 4,
            [(SystemTypes.Cockpit, SystemTypes.Brig)] = 8,
            [(SystemTypes.Cockpit, SystemTypes.VaultRoom)] = 10,
            [(SystemTypes.Cockpit, SystemTypes.Kitchen)] = 5,
            [(SystemTypes.Cockpit, SystemTypes.ViewingDeck)] = 7,
            [(SystemTypes.Cockpit, SystemTypes.GapRoom)] = 16,
            [(SystemTypes.Cockpit, SystemTypes.MainHall)] = 8,
            [(SystemTypes.Cockpit, SystemTypes.Showers)] = 12,
            [(SystemTypes.Cockpit, SystemTypes.Records)] = 15,
            [(SystemTypes.Cockpit, SystemTypes.Lounge)] = 17,
            [(SystemTypes.Cockpit, SystemTypes.Security)] = 10,
            [(SystemTypes.Cockpit, SystemTypes.CargoBay)] = 21,
            [(SystemTypes.Cockpit, SystemTypes.Electrical)] = 12,
            [(SystemTypes.Cockpit, SystemTypes.Medical)] = 15,
            [(SystemTypes.Cockpit, SystemTypes.MeetingRoom)] = 13,
            [(SystemTypes.Cockpit, SystemTypes.HallOfPortraits)] = 8,
            [(SystemTypes.Armory, SystemTypes.Comms)] = 2,
            [(SystemTypes.Armory, SystemTypes.Engine)] = 2,
            [(SystemTypes.Armory, SystemTypes.Brig)] = 7,
            [(SystemTypes.Armory, SystemTypes.VaultRoom)] = 8,
            [(SystemTypes.Armory, SystemTypes.Kitchen)] = 1,
            [(SystemTypes.Armory, SystemTypes.ViewingDeck)] = 3,
            [(SystemTypes.Armory, SystemTypes.GapRoom)] = 15,
            [(SystemTypes.Armory, SystemTypes.MainHall)] = 7,
            [(SystemTypes.Armory, SystemTypes.Showers)] = 11,
            [(SystemTypes.Armory, SystemTypes.Records)] = 13,
            [(SystemTypes.Armory, SystemTypes.Lounge)] = 15,
            [(SystemTypes.Armory, SystemTypes.Security)] = 6,
            [(SystemTypes.Armory, SystemTypes.CargoBay)] = 21,
            [(SystemTypes.Armory, SystemTypes.Electrical)] = 8,
            [(SystemTypes.Armory, SystemTypes.Medical)] = 18,
            [(SystemTypes.Armory, SystemTypes.MeetingRoom)] = 11,
            [(SystemTypes.Armory, SystemTypes.HallOfPortraits)] = 4,
            [(SystemTypes.Comms, SystemTypes.Engine)] = 3,
            [(SystemTypes.Comms, SystemTypes.Brig)] = 7,
            [(SystemTypes.Comms, SystemTypes.VaultRoom)] = 9,
            [(SystemTypes.Comms, SystemTypes.Kitchen)] = 4,
            [(SystemTypes.Comms, SystemTypes.ViewingDeck)] = 6,
            [(SystemTypes.Comms, SystemTypes.GapRoom)] = 15,
            [(SystemTypes.Comms, SystemTypes.MainHall)] = 7,
            [(SystemTypes.Comms, SystemTypes.Showers)] = 11,
            [(SystemTypes.Comms, SystemTypes.Records)] = 13,
            [(SystemTypes.Comms, SystemTypes.Lounge)] = 16,
            [(SystemTypes.Comms, SystemTypes.Security)] = 9,
            [(SystemTypes.Comms, SystemTypes.CargoBay)] = 20,
            [(SystemTypes.Comms, SystemTypes.Electrical)] = 11,
            [(SystemTypes.Comms, SystemTypes.Medical)] = 18,
            [(SystemTypes.Comms, SystemTypes.MeetingRoom)] = 12,
            [(SystemTypes.Comms, SystemTypes.HallOfPortraits)] = 7,
            [(SystemTypes.Engine, SystemTypes.Brig)] = 1,
            [(SystemTypes.Engine, SystemTypes.VaultRoom)] = 2,
            [(SystemTypes.Engine, SystemTypes.Kitchen)] = 4,
            [(SystemTypes.Engine, SystemTypes.ViewingDeck)] = 6,
            [(SystemTypes.Engine, SystemTypes.GapRoom)] = 10,
            [(SystemTypes.Engine, SystemTypes.MainHall)] = 1,
            [(SystemTypes.Engine, SystemTypes.Showers)] = 5,
            [(SystemTypes.Engine, SystemTypes.Records)] = 8,
            [(SystemTypes.Engine, SystemTypes.Lounge)] = 10,
            [(SystemTypes.Engine, SystemTypes.Security)] = 8,
            [(SystemTypes.Engine, SystemTypes.CargoBay)] = 14,
            [(SystemTypes.Engine, SystemTypes.Electrical)] = 5,
            [(SystemTypes.Engine, SystemTypes.Medical)] = 12,
            [(SystemTypes.Engine, SystemTypes.MeetingRoom)] = 5,
            [(SystemTypes.Engine, SystemTypes.HallOfPortraits)] = 7,
            [(SystemTypes.Brig, SystemTypes.VaultRoom)] = 1,
            [(SystemTypes.Brig, SystemTypes.Kitchen)] = 9,
            [(SystemTypes.Brig, SystemTypes.ViewingDeck)] = 11,
            [(SystemTypes.Brig, SystemTypes.GapRoom)] = 14,
            [(SystemTypes.Brig, SystemTypes.MainHall)] = 5,
            [(SystemTypes.Brig, SystemTypes.Showers)] = 9,
            [(SystemTypes.Brig, SystemTypes.Records)] = 12,
            [(SystemTypes.Brig, SystemTypes.Lounge)] = 14,
            [(SystemTypes.Brig, SystemTypes.Security)] = 12,
            [(SystemTypes.Brig, SystemTypes.CargoBay)] = 18,
            [(SystemTypes.Brig, SystemTypes.Electrical)] = 10,
            [(SystemTypes.Brig, SystemTypes.Medical)] = 16,
            [(SystemTypes.Brig, SystemTypes.MeetingRoom)] = 4,
            [(SystemTypes.Brig, SystemTypes.HallOfPortraits)] = 12,
            [(SystemTypes.VaultRoom, SystemTypes.Kitchen)] = 10,
            [(SystemTypes.VaultRoom, SystemTypes.ViewingDeck)] = 12,
            [(SystemTypes.VaultRoom, SystemTypes.GapRoom)] = 15,
            [(SystemTypes.VaultRoom, SystemTypes.MainHall)] = 7,
            [(SystemTypes.VaultRoom, SystemTypes.Showers)] = 11,
            [(SystemTypes.VaultRoom, SystemTypes.Records)] = 13,
            [(SystemTypes.VaultRoom, SystemTypes.Lounge)] = 15,
            [(SystemTypes.VaultRoom, SystemTypes.Security)] = 13,
            [(SystemTypes.VaultRoom, SystemTypes.CargoBay)] = 19,
            [(SystemTypes.VaultRoom, SystemTypes.Electrical)] = 11,
            [(SystemTypes.VaultRoom, SystemTypes.Medical)] = 18,
            [(SystemTypes.VaultRoom, SystemTypes.MeetingRoom)] = 6,
            [(SystemTypes.VaultRoom, SystemTypes.HallOfPortraits)] = 13,
            [(SystemTypes.Kitchen, SystemTypes.ViewingDeck)] = 1,
            [(SystemTypes.Kitchen, SystemTypes.GapRoom)] = 16,
            [(SystemTypes.Kitchen, SystemTypes.MainHall)] = 9,
            [(SystemTypes.Kitchen, SystemTypes.Showers)] = 12,
            [(SystemTypes.Kitchen, SystemTypes.Records)] = 14,
            [(SystemTypes.Kitchen, SystemTypes.Lounge)] = 16,
            [(SystemTypes.Kitchen, SystemTypes.Security)] = 3,
            [(SystemTypes.Kitchen, SystemTypes.CargoBay)] = 18,
            [(SystemTypes.Kitchen, SystemTypes.Electrical)] = 5,
            [(SystemTypes.Kitchen, SystemTypes.Medical)] = 14,
            [(SystemTypes.Kitchen, SystemTypes.MeetingRoom)] = 14,
            [(SystemTypes.Kitchen, SystemTypes.HallOfPortraits)] = 1,
            [(SystemTypes.ViewingDeck, SystemTypes.GapRoom)] = 18,
            [(SystemTypes.ViewingDeck, SystemTypes.MainHall)] = 11,
            [(SystemTypes.ViewingDeck, SystemTypes.Showers)] = 14,
            [(SystemTypes.ViewingDeck, SystemTypes.Records)] = 17,
            [(SystemTypes.ViewingDeck, SystemTypes.Lounge)] = 18,
            [(SystemTypes.ViewingDeck, SystemTypes.Security)] = 6,
            [(SystemTypes.ViewingDeck, SystemTypes.CargoBay)] = 21,
            [(SystemTypes.ViewingDeck, SystemTypes.Electrical)] = 7,
            [(SystemTypes.ViewingDeck, SystemTypes.Medical)] = 16,
            [(SystemTypes.ViewingDeck, SystemTypes.MeetingRoom)] = 15,
            [(SystemTypes.ViewingDeck, SystemTypes.HallOfPortraits)] = 4,
            [(SystemTypes.GapRoom, SystemTypes.MainHall)] = 7,
            [(SystemTypes.GapRoom, SystemTypes.Showers)] = 11,
            [(SystemTypes.GapRoom, SystemTypes.Records)] = 13,
            [(SystemTypes.GapRoom, SystemTypes.Lounge)] = 15,
            [(SystemTypes.GapRoom, SystemTypes.Security)] = 13,
            [(SystemTypes.GapRoom, SystemTypes.CargoBay)] = 20,
            [(SystemTypes.GapRoom, SystemTypes.Electrical)] = 11,
            [(SystemTypes.GapRoom, SystemTypes.Medical)] = 19,
            [(SystemTypes.GapRoom, SystemTypes.MeetingRoom)] = 18,
            [(SystemTypes.GapRoom, SystemTypes.HallOfPortraits)] = 14,
            [(SystemTypes.MainHall, SystemTypes.Showers)] = 1,
            [(SystemTypes.MainHall, SystemTypes.Records)] = 3,
            [(SystemTypes.MainHall, SystemTypes.Lounge)] = 5,
            [(SystemTypes.MainHall, SystemTypes.Security)] = 5,
            [(SystemTypes.MainHall, SystemTypes.CargoBay)] = 10,
            [(SystemTypes.MainHall, SystemTypes.Electrical)] = 1,
            [(SystemTypes.MainHall, SystemTypes.Medical)] = 9,
            [(SystemTypes.MainHall, SystemTypes.MeetingRoom)] = 10,
            [(SystemTypes.MainHall, SystemTypes.HallOfPortraits)] = 6,
            [(SystemTypes.Showers, SystemTypes.Records)] = 1,
            [(SystemTypes.Showers, SystemTypes.Lounge)] = 3,
            [(SystemTypes.Showers, SystemTypes.Security)] = 8,
            [(SystemTypes.Showers, SystemTypes.CargoBay)] = 8,
            [(SystemTypes.Showers, SystemTypes.Electrical)] = 4,
            [(SystemTypes.Showers, SystemTypes.Medical)] = 12,
            [(SystemTypes.Showers, SystemTypes.MeetingRoom)] = 14,
            [(SystemTypes.Showers, SystemTypes.HallOfPortraits)] = 9,
            [(SystemTypes.Records, SystemTypes.Lounge)] = 1,
            [(SystemTypes.Records, SystemTypes.Security)] = 9,
            [(SystemTypes.Records, SystemTypes.CargoBay)] = 6,
            [(SystemTypes.Records, SystemTypes.Electrical)] = 6,
            [(SystemTypes.Records, SystemTypes.Medical)] = 8,
            [(SystemTypes.Records, SystemTypes.MeetingRoom)] = 16,
            [(SystemTypes.Records, SystemTypes.HallOfPortraits)] = 12,
            [(SystemTypes.Lounge, SystemTypes.Security)] = 15,
            [(SystemTypes.Lounge, SystemTypes.CargoBay)] = 1,
            [(SystemTypes.Lounge, SystemTypes.Electrical)] = 8,
            [(SystemTypes.Lounge, SystemTypes.Medical)] = 4,
            [(SystemTypes.Lounge, SystemTypes.MeetingRoom)] = 18,
            [(SystemTypes.Lounge, SystemTypes.HallOfPortraits)] = 17,
            [(SystemTypes.Security, SystemTypes.CargoBay)] = 14,
            [(SystemTypes.Security, SystemTypes.Electrical)] = 1,
            [(SystemTypes.Security, SystemTypes.Medical)] = 10,
            [(SystemTypes.Security, SystemTypes.MeetingRoom)] = 18,
            [(SystemTypes.Security, SystemTypes.HallOfPortraits)] = 1,
            [(SystemTypes.CargoBay, SystemTypes.Electrical)] = 5,
            [(SystemTypes.CargoBay, SystemTypes.Medical)] = 1,
            [(SystemTypes.CargoBay, SystemTypes.MeetingRoom)] = 22,
            [(SystemTypes.CargoBay, SystemTypes.HallOfPortraits)] = 16,
            [(SystemTypes.Electrical, SystemTypes.Medical)] = 1,
            [(SystemTypes.Electrical, SystemTypes.MeetingRoom)] = 14,
            [(SystemTypes.Electrical, SystemTypes.HallOfPortraits)] = 3,
            [(SystemTypes.Medical, SystemTypes.MeetingRoom)] = 22,
            [(SystemTypes.Medical, SystemTypes.HallOfPortraits)] = 12,
            [(SystemTypes.MeetingRoom, SystemTypes.HallOfPortraits)] = 15
        },
        [MapNames.Fungle] = new()
        {
            [(SystemTypes.Cafeteria, SystemTypes.Comms)] = 23,
            [(SystemTypes.Cafeteria, SystemTypes.FishingDock)] = 7,
            [(SystemTypes.Cafeteria, SystemTypes.Dropship)] = 3,
            [(SystemTypes.Cafeteria, SystemTypes.Greenhouse)] = 10,
            [(SystemTypes.Cafeteria, SystemTypes.Kitchen)] = 5,
            [(SystemTypes.Cafeteria, SystemTypes.Laboratory)] = 13,
            [(SystemTypes.Cafeteria, SystemTypes.Lookout)] = 15,
            [(SystemTypes.Cafeteria, SystemTypes.MeetingRoom)] = 4,
            [(SystemTypes.Cafeteria, SystemTypes.SleepingQuarters)] = 6,
            [(SystemTypes.Cafeteria, SystemTypes.MiningPit)] = 17,
            [(SystemTypes.Cafeteria, SystemTypes.Reactor)] = 15,
            [(SystemTypes.Cafeteria, SystemTypes.RecRoom)] = 4,
            [(SystemTypes.Cafeteria, SystemTypes.Storage)] = 5,
            [(SystemTypes.Cafeteria, SystemTypes.UpperEngine)] = 17,
            [(SystemTypes.Comms, SystemTypes.FishingDock)] = 23,
            [(SystemTypes.Comms, SystemTypes.Dropship)] = 19,
            [(SystemTypes.Comms, SystemTypes.Greenhouse)] = 13,
            [(SystemTypes.Comms, SystemTypes.Kitchen)] = 21,
            [(SystemTypes.Comms, SystemTypes.Laboratory)] = 21,
            [(SystemTypes.Comms, SystemTypes.Lookout)] = 8,
            [(SystemTypes.Comms, SystemTypes.MeetingRoom)] = 15,
            [(SystemTypes.Comms, SystemTypes.SleepingQuarters)] = 17,
            [(SystemTypes.Comms, SystemTypes.MiningPit)] = 8,
            [(SystemTypes.Comms, SystemTypes.Reactor)] = 9,
            [(SystemTypes.Comms, SystemTypes.RecRoom)] = 21,
            [(SystemTypes.Comms, SystemTypes.Storage)] = 19,
            [(SystemTypes.Comms, SystemTypes.UpperEngine)] = 5,
            [(SystemTypes.FishingDock, SystemTypes.Dropship)] = 8,
            [(SystemTypes.FishingDock, SystemTypes.Greenhouse)] = 12,
            [(SystemTypes.FishingDock, SystemTypes.Kitchen)] = 1,
            [(SystemTypes.FishingDock, SystemTypes.Laboratory)] = 14,
            [(SystemTypes.FishingDock, SystemTypes.Lookout)] = 18,
            [(SystemTypes.FishingDock, SystemTypes.MeetingRoom)] = 6,
            [(SystemTypes.FishingDock, SystemTypes.SleepingQuarters)] = 9,
            [(SystemTypes.FishingDock, SystemTypes.MiningPit)] = 19,
            [(SystemTypes.FishingDock, SystemTypes.Reactor)] = 17,
            [(SystemTypes.FishingDock, SystemTypes.RecRoom)] = 4,
            [(SystemTypes.FishingDock, SystemTypes.Storage)] = 8,
            [(SystemTypes.FishingDock, SystemTypes.UpperEngine)] = 21,
            [(SystemTypes.Dropship, SystemTypes.Greenhouse)] = 9,
            [(SystemTypes.Dropship, SystemTypes.Kitchen)] = 6,
            [(SystemTypes.Dropship, SystemTypes.Laboratory)] = 12,
            [(SystemTypes.Dropship, SystemTypes.Lookout)] = 14,
            [(SystemTypes.Dropship, SystemTypes.MeetingRoom)] = 3,
            [(SystemTypes.Dropship, SystemTypes.SleepingQuarters)] = 5,
            [(SystemTypes.Dropship, SystemTypes.MiningPit)] = 15,
            [(SystemTypes.Dropship, SystemTypes.Reactor)] = 13,
            [(SystemTypes.Dropship, SystemTypes.RecRoom)] = 5,
            [(SystemTypes.Dropship, SystemTypes.Storage)] = 2,
            [(SystemTypes.Dropship, SystemTypes.UpperEngine)] = 16,
            [(SystemTypes.Greenhouse, SystemTypes.Kitchen)] = 11,
            [(SystemTypes.Greenhouse, SystemTypes.Laboratory)] = 10,
            [(SystemTypes.Greenhouse, SystemTypes.Lookout)] = 7,
            [(SystemTypes.Greenhouse, SystemTypes.MeetingRoom)] = 6,
            [(SystemTypes.Greenhouse, SystemTypes.SleepingQuarters)] = 7,
            [(SystemTypes.Greenhouse, SystemTypes.MiningPit)] = 9,
            [(SystemTypes.Greenhouse, SystemTypes.Reactor)] = 5,
            [(SystemTypes.Greenhouse, SystemTypes.RecRoom)] = 11,
            [(SystemTypes.Greenhouse, SystemTypes.Storage)] = 9,
            [(SystemTypes.Greenhouse, SystemTypes.UpperEngine)] = 9,
            [(SystemTypes.Kitchen, SystemTypes.Laboratory)] = 12,
            [(SystemTypes.Kitchen, SystemTypes.Lookout)] = 16,
            [(SystemTypes.Kitchen, SystemTypes.MeetingRoom)] = 5,
            [(SystemTypes.Kitchen, SystemTypes.SleepingQuarters)] = 7,
            [(SystemTypes.Kitchen, SystemTypes.MiningPit)] = 17,
            [(SystemTypes.Kitchen, SystemTypes.Reactor)] = 15,
            [(SystemTypes.Kitchen, SystemTypes.RecRoom)] = 3,
            [(SystemTypes.Kitchen, SystemTypes.Storage)] = 7,
            [(SystemTypes.Kitchen, SystemTypes.UpperEngine)] = 19,
            [(SystemTypes.Laboratory, SystemTypes.Lookout)] = 16,
            [(SystemTypes.Laboratory, SystemTypes.MeetingRoom)] = 8,
            [(SystemTypes.Laboratory, SystemTypes.SleepingQuarters)] = 9,
            [(SystemTypes.Laboratory, SystemTypes.MiningPit)] = 17,
            [(SystemTypes.Laboratory, SystemTypes.Reactor)] = 13,
            [(SystemTypes.Laboratory, SystemTypes.RecRoom)] = 13,
            [(SystemTypes.Laboratory, SystemTypes.Storage)] = 12,
            [(SystemTypes.Laboratory, SystemTypes.UpperEngine)] = 17,
            [(SystemTypes.Lookout, SystemTypes.MeetingRoom)] = 9,
            [(SystemTypes.Lookout, SystemTypes.SleepingQuarters)] = 10,
            [(SystemTypes.Lookout, SystemTypes.MiningPit)] = 2,
            [(SystemTypes.Lookout, SystemTypes.Reactor)] = 6,
            [(SystemTypes.Lookout, SystemTypes.RecRoom)] = 14,
            [(SystemTypes.Lookout, SystemTypes.Storage)] = 12,
            [(SystemTypes.Lookout, SystemTypes.UpperEngine)] = 4,
            [(SystemTypes.MeetingRoom, SystemTypes.SleepingQuarters)] = 1,
            [(SystemTypes.MeetingRoom, SystemTypes.MiningPit)] = 11,
            [(SystemTypes.MeetingRoom, SystemTypes.Reactor)] = 10,
            [(SystemTypes.MeetingRoom, SystemTypes.RecRoom)] = 4,
            [(SystemTypes.MeetingRoom, SystemTypes.Storage)] = 3,
            [(SystemTypes.MeetingRoom, SystemTypes.UpperEngine)] = 12,
            [(SystemTypes.SleepingQuarters, SystemTypes.MiningPit)] = 13,
            [(SystemTypes.SleepingQuarters, SystemTypes.Reactor)] = 11,
            [(SystemTypes.SleepingQuarters, SystemTypes.RecRoom)] = 6,
            [(SystemTypes.SleepingQuarters, SystemTypes.Storage)] = 4,
            [(SystemTypes.SleepingQuarters, SystemTypes.UpperEngine)] = 13,
            [(SystemTypes.MiningPit, SystemTypes.Reactor)] = 7,
            [(SystemTypes.MiningPit, SystemTypes.RecRoom)] = 15,
            [(SystemTypes.MiningPit, SystemTypes.Storage)] = 13,
            [(SystemTypes.MiningPit, SystemTypes.UpperEngine)] = 3,
            [(SystemTypes.Reactor, SystemTypes.RecRoom)] = 15,
            [(SystemTypes.Reactor, SystemTypes.Storage)] = 13,
            [(SystemTypes.Reactor, SystemTypes.UpperEngine)] = 5,
            [(SystemTypes.RecRoom, SystemTypes.Storage)] = 6,
            [(SystemTypes.RecRoom, SystemTypes.UpperEngine)] = 17,
            [(SystemTypes.Storage, SystemTypes.UpperEngine)] = 15
        }
    };

    static RoomRush()
    {
        RawTimeNeeded[MapNames.Dleks] = RawTimeNeeded[MapNames.Skeld];
    }

    public static bool PointsSystem => WinByPointsInsteadOfDeaths.GetBool();
    public static int RawPointsToWin => PointsToWin.GetInt();

    public static void SetupCustomOption()
    {
        var id = 69_217_001;
        Color color = Utils.GetRoleColor(CustomRoles.RRPlayer);
        const CustomGameMode gameMode = CustomGameMode.RoomRush;

        GlobalTimeAddition = new IntegerOptionItem(id++, "RR_GlobalTimeAddition", new(0, 15, 1), 4, TabGroup.GameSettings)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);

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
        /*yield return RRTimeTester.DoTest();
        yield break;*/
        
        GameGoing = false;

        int ventLimit = VentTimes.GetInt();
        VentLimit = Main.EnumeratePlayerControls().ToDictionary(x => x.PlayerId, _ => ventLimit);

        AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
        AllRooms.Remove(SystemTypes.Hallway);
        AllRooms.Remove(SystemTypes.Outside);
        if (SubmergedCompatibility.IsSubmerged()) AllRooms.RemoveWhere(x => (byte)x > 135);

        DonePlayers = [];
        Points = [];

        if (WinByPointsInsteadOfDeaths.GetBool())
            Points = Main.EnumeratePlayerControls().ToDictionary(x => x.PlayerId, _ => 0);

        Map = RandomSpawn.SpawnMap.GetSpawnMap();

        yield return new WaitForSecondsRealtime(Main.CurrentMap == MapNames.Airship ? 8f : 3f);

        var aapc = Main.AllAlivePlayerControlsToList;
        var pcCount = aapc.Count;
        aapc.Do(x => x.RpcSetCustomRole(CustomRoles.RRPlayer));

        PointsToWinValue = PointsToWin.GetInt() * pcCount;

        bool showTutorial = aapc.ExceptBy(HasPlayedFriendCodes, x => x.FriendCode).Count() > pcCount / 2;

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
            yield return new WaitForSecondsRealtime(readingTime);
            if (!GameStates.InGame) yield break;
        }

        for (var i = 3; i > 0; i--)
        {
            NameNotifyManager.Reset();
            aapc.NotifyPlayers(string.Format(Translator.GetString("RR_ReadyQM"), i));
            yield return new WaitForSecondsRealtime(1f);
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
                _ => AllRooms.RandomElement()
            };

        DonePlayers.Clear();
        RoomGoal = AllRooms.Without(previous).RandomElement();
        Vector2 goalPos = RoomGoal.GetRoomClass().transform.position;
        float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        int time;
        
        if (RawTimeNeeded.TryGetValue(map, out Dictionary<(SystemTypes, SystemTypes), int> rawTimes))
        {
            time = initial ? rawTimes.Values.Max() : rawTimes.GetValueOrDefault((previous, RoomGoal), rawTimes.GetValueOrDefault((RoomGoal, previous), 25));
            time = (int)Math.Round(time / (speed / 1.25f));

            bool involvesDecontamination = map switch
            {
                MapNames.MiraHQ => previous is SystemTypes.Laboratory or SystemTypes.Reactor ^ RoomGoal is SystemTypes.Laboratory or SystemTypes.Reactor,
                MapNames.Polus => previous == SystemTypes.Specimens || RoomGoal == SystemTypes.Specimens,
                (MapNames)6 => (previous == (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast) ^ (RoomGoal == (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast),
                _ => false
            };

            if (involvesDecontamination)
                time += 2;

            switch (map)
            {
                case MapNames.Airship:
                    time += previous switch
                    {
                        SystemTypes.Engine => 3,
                        SystemTypes.MainHall => 2,
                        _ => 0
                    };
                    break;
            }

            time += GlobalTimeAddition.GetInt();
            if (time < 6) time = 6;
        }
        else
            time = (int)Math.Ceiling(32 / speed);

        TimeLimitEndTS = Utils.TimeStamp + time;
        Logger.Info($"Starting a new round - Goal = from: {Translator.GetString(previous)} ({previous}), to: {Translator.GetString(RoomGoal)} ({RoomGoal}) - Time: {time}  ({map})", "RoomRush");

        Main.EnumeratePlayerControls().Do(x => LocateArrow.RemoveAllTarget(x.PlayerId));
        if (DisplayArrowToRoom.GetBool()) Main.EnumeratePlayerControls().Do(x => LocateArrow.Add(x.PlayerId, goalPos));

        Utils.NotifyRoles();
        LateTask.New(() => Utils.DirtyName.Add(PlayerControl.LocalPlayer.PlayerId), 0.2f);

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
        if (!GameGoing || Main.HasJustStarted) return string.Empty;

        Suffix.Clear();
        bool dead = !seer.IsAlive();
        bool done = dead || DonePlayers.Contains(seer.PlayerId);
        Color color = done ? Color.green : Color.yellow;

        if (DisplayRoomName.GetBool()) Suffix.Append(Utils.ColorString(color, Translator.GetString(RoomGoal))).Append('\n');
        if (DisplayArrowToRoom.GetBool()) Suffix.Append(Utils.ColorString(color, LocateArrow.GetArrows(seer))).Append('\n');

        color = done ? Color.white : Color.yellow;
        Suffix.Append(Utils.ColorString(color, (TimeLimitEndTS - Utils.TimeStamp).ToString())).Append('\n');

        if (WinByPointsInsteadOfDeaths.GetBool() && Points.TryGetValue(seer.PlayerId, out int points))
        {
            Suffix.AppendFormat(Translator.GetString("RR_Points"), points, PointsToWinValue);

            int highestPoints = Points.Values.Max();
            bool tie = Points.Values.Count(x => x == highestPoints) > 1;

            if (tie && highestPoints >= PointsToWinValue)
            {
                byte tieWith = Points.First(x => x.Key != seer.PlayerId && x.Value == highestPoints).Key;
                Suffix.Append('\n').AppendFormat(Translator.GetString("RR_Tie"), tieWith.ColoredPlayerName());
            }
            else
            {
                Suffix.Append("<size=80%>");
                byte first = Points.GetKeyByValue(highestPoints);
                if (first != seer.PlayerId) Suffix.Append('\n').AppendFormat(Translator.GetString("RR_FirstPoints"), first.ColoredPlayerName(), highestPoints);
                else Suffix.Append('\n').Append(Translator.GetString("RR_YouAreFirst"));
                Suffix.Append("</size>");
            }
        }

        if (VentTimes.GetInt() == 0 || dead || seer.IsModdedClient()) return Suffix.ToString().Trim();

        Suffix.Append('\n');

        int vents = VentLimit.GetValueOrDefault(seer.PlayerId);
        Suffix.AppendFormat(Translator.GetString("RR_VentsRemaining"), vents);

        return Suffix.ToString().Trim();
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                PointsToWinValue = PointsToWin.GetInt() * Main.AllAlivePlayerControlsCount;
                int ventLimit = VentTimes.GetInt();
                VentLimit = Main.EnumeratePlayerControls().ToDictionary(x => x.PlayerId, _ => ventLimit);
                if (WinByPointsInsteadOfDeaths.GetBool()) Points = Main.EnumeratePlayerControls().ToDictionary(x => x.PlayerId, _ => 0);
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
            var aapc = Main.CachedAlivePlayerControls();

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

            for (int index = 0; index < aapc.Count; index++)
            {
                PlayerControl pc = aapc[index];
                bool isInRoom = pc.IsInRoom(RoomGoal);

                if (!pc.inMovingPlat && !pc.inVent && isInRoom && RegisterHost(pc) && DonePlayers.Add(pc.PlayerId))
                {
                    Logger.Info($"{pc.GetRealName()} entered the correct room", "RoomRush");
                    pc.Notify($"<size=100%>{DonePlayers.Count}.</size>", 2f);
                    if (pc.AmOwner) Utils.DirtyName.Add(pc.PlayerId);

                    if (WinByPointsInsteadOfDeaths.GetBool())
                        Points[pc.PlayerId] += aapc.Count == 1 ? 1 : aapc.Count - DonePlayers.Count;

                    int setTo = TimeWhenFirstTwoPlayersEnterRoom.GetInt();
                    long remaining = TimeLimitEndTS - now;

                    if (DonePlayers.Count == 2 && setTo < remaining && !DontLowerTimeLimitWhenTwoPlayersEnterCorrectRoom.GetBool())
                    {
                        Logger.Info($"Two players entered the correct room, setting the timer to {setTo}", "RoomRush");
                        TimeLimitEndTS = now + setTo;
                        LastUpdate = now;

                        if (aapc.Count == 2 && pc.AmOwner)
                            Achievements.Type.WheresTheBlueShell.CompleteAfterGameEnd();
                    }

                    if (DonePlayers.Count == aapc.Count - 1 && !DontKillLastPlayer.GetBool())
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
                    Points[pc.PlayerId] -= aapc.Count - DonePlayers.Count;
            }

            if (LastUpdate != now)
            {
                Utils.NotifyRoles();
                LastUpdate = now;
            }

            if (TimeLimitEndTS > now) return;

            Logger.Info("Time is up, killing everyone who didn't enter the correct room", "RoomRush");
            var lateAapc = Main.CachedAlivePlayerControls();
            PlayerControl[] playersOutsideRoom = lateAapc.ExceptBy(DonePlayers, x => x.PlayerId).ToArray();
            bool everyoneDies = playersOutsideRoom.Length == lateAapc.Count;

            if (WinByPointsInsteadOfDeaths.GetBool())
            {
                Vector2 location = everyoneDies && !Main.LIMap ? Map.Positions.GetValueOrDefault(RoomGoal, RoomGoal.GetRoomClass().transform.position) : DonePlayers.RandomElement().GetPlayer().Pos();
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
                bool registerHost = HostRegisterTimer.ElapsedMilliseconds > Math.Max(200, AmongUsClient.Instance.Ping * 2);
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