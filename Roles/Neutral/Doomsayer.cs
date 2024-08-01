using System.Collections.Generic;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral;

public class Doomsayer : RoleBase
{
    private const int Id = 27000;
    public static List<byte> playerIdList = [];
    public static List<CustomRoles> GuessedRoles = [];
    public static Dictionary<byte, int> GuessingToWin = [];

    public static int GuessesCount;
    public static int GuessesCountPerMeeting;
    public static bool CantGuess;

    public static OptionItem DoomsayerAmountOfGuessesToWin;
    public static OptionItem DCanGuessImpostors;
    public static OptionItem DCanGuessCrewmates;
    public static OptionItem DCanGuessNeutrals;
    public static OptionItem DCanGuessAdt;
    public static OptionItem AdvancedSettings;
    public static OptionItem MaxNumberOfGuessesPerMeeting;
    public static OptionItem KillCorrectlyGuessedPlayers;
    public static OptionItem DoesNotSuicideWhenMisguessing;
    public static OptionItem MisguessRolePrevGuessRoleUntilNextMeeting;
    public static OptionItem DoomsayerTryHideMsg;

    public override bool IsEnable => playerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Doomsayer);
        DoomsayerAmountOfGuessesToWin = new IntegerOptionItem(Id + 10, "DoomsayerAmountOfGuessesToWin", new(1, 10, 1), 3, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doomsayer])
            .SetValueFormat(OptionFormat.Times);
        DCanGuessImpostors = new BooleanOptionItem(Id + 12, "DCanGuessImpostors", true, TabGroup.NeutralRoles, true)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doomsayer]);
        DCanGuessCrewmates = new BooleanOptionItem(Id + 13, "DCanGuessCrewmates", true, TabGroup.NeutralRoles, true)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doomsayer]);
        DCanGuessNeutrals = new BooleanOptionItem(Id + 14, "DCanGuessNeutrals", true, TabGroup.NeutralRoles, true)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doomsayer]);
        DCanGuessAdt = new BooleanOptionItem(Id + 15, "DCanGuessAdt", false, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doomsayer]);

        AdvancedSettings = new BooleanOptionItem(Id + 16, "DoomsayerAdvancedSettings", true, TabGroup.NeutralRoles, true)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doomsayer]);
        MaxNumberOfGuessesPerMeeting = new IntegerOptionItem(Id + 17, "DoomsayerMaxNumberOfGuessesPerMeeting", new(1, 10, 1), 2, TabGroup.NeutralRoles)
            .SetParent(AdvancedSettings);
        KillCorrectlyGuessedPlayers = new BooleanOptionItem(Id + 18, "DoomsayerKillCorrectlyGuessedPlayers", true, TabGroup.NeutralRoles, true)
            .SetParent(AdvancedSettings);
        DoesNotSuicideWhenMisguessing = new BooleanOptionItem(Id + 19, "DoomsayerDoesNotSuicideWhenMisguessing", true, TabGroup.NeutralRoles)
            .SetParent(AdvancedSettings);
        MisguessRolePrevGuessRoleUntilNextMeeting = new BooleanOptionItem(Id + 20, "DoomsayerMisguessRolePrevGuessRoleUntilNextMeeting", true, TabGroup.NeutralRoles, true)
            .SetParent(DoesNotSuicideWhenMisguessing);

        DoomsayerTryHideMsg = new BooleanOptionItem(Id + 21, "DoomsayerTryHideMsg", true, TabGroup.NeutralRoles, true)
            .SetColor(Color.green)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doomsayer]);
    }

    public override void Init()
    {
        playerIdList = [];
        GuessedRoles = [];
        GuessingToWin = [];
        GuessesCount = 0;
        GuessesCountPerMeeting = 0;
        CantGuess = false;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        GuessingToWin.TryAdd(playerId, GuessesCount);

        GuessesCount = 0;
        GuessesCountPerMeeting = 0;
        CantGuess = false;
    }

    public static void SendRPC(PlayerControl player)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDoomsayerProgress, SendOption.Reliable);
        writer.Write(player.PlayerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte DoomsayerId = reader.ReadByte();
        GuessingToWin[DoomsayerId]++;
    }

    public static (int, int) GuessedPlayerCount(byte doomsayerId)
    {
        int doomsayerguess = GuessingToWin[doomsayerId], GuessesToWin = DoomsayerAmountOfGuessesToWin.GetInt();

        return (doomsayerguess, GuessesToWin);
    }

    public static void CheckCountGuess(PlayerControl doomsayer)
    {
        if (!(GuessingToWin[doomsayer.PlayerId] >= DoomsayerAmountOfGuessesToWin.GetInt())) return;

        GuessingToWin[doomsayer.PlayerId] = DoomsayerAmountOfGuessesToWin.GetInt();
        GuessesCount = DoomsayerAmountOfGuessesToWin.GetInt();
        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Doomsayer);
        CustomWinnerHolder.WinnerIds.Add(doomsayer.PlayerId);
    }

    public override void OnReportDeadBody()
    {
        if (!(IsEnable && AdvancedSettings.GetBool())) return;

        CantGuess = false;
        GuessesCountPerMeeting = 0;
    }
}