using Hazel;
using System.Collections.Generic;

namespace TOHE;

public static class CustomWinnerHolder
{
    // The winning team will be stored.
    // Used to determine background color of results, etc.
    // Note: When changing this variable, if you do not change WinnerRoles and WinnerIds at the same time, unexpected winners may appear.
    public static CustomWinner WinnerTeam;
    // Stores the team of additional winning players.
    // Used to display results.
    public static HashSet<AdditionalWinners> AdditionalWinnerTeams;
    // The winning role is stored, and all players whose roles are stored in this variable win.
    // Ideal for handling team neutrals.
    public static HashSet<CustomRoles> WinnerRoles;
    // Stores the winner's PlayerID, all players with this ID win.
    // Ideal for handling neutrals that win alone.
    public static HashSet<byte> WinnerIds;

    public static void Reset()
    {
        WinnerTeam = CustomWinner.Default;
        AdditionalWinnerTeams = [];
        WinnerRoles = [];
        WinnerIds = [];
    }
    public static void ClearWinners()
    {
        WinnerRoles.Clear();
        WinnerIds.Clear();
    }
    /// <summary><para>Assign a value to WinnerTeam. </para><para>Add to AdditionalWinnerTeams if already assigned.</para></summary>
    public static void SetWinnerOrAdditonalWinner(CustomWinner winner)
    {
        if (WinnerTeam == CustomWinner.Default) WinnerTeam = winner;
        else AdditionalWinnerTeams.Add((AdditionalWinners)winner);
    }
    /// <summary><para>Assign a value to WinnerTeam. </para><para>If it is already assigned, add the existing value to AdditionalWinnerTeams and then assign it.</para></summary>
    public static void ShiftWinnerAndSetWinner(CustomWinner winner)
    {
        if (WinnerTeam != CustomWinner.Default)
            AdditionalWinnerTeams.Add((AdditionalWinners)WinnerTeam);
        WinnerTeam = winner;
    }
    /// <summary><para>Delete any existing values and then assign the values to WinnerTeam.</para></summary>
    public static void ResetAndSetWinner(CustomWinner winner)
    {
        Reset();
        WinnerTeam = winner;
    }

    public static MessageWriter WriteTo(MessageWriter writer)
    {
        writer.WritePacked((int)WinnerTeam);

        writer.WritePacked(AdditionalWinnerTeams.Count);
        foreach (var wt in AdditionalWinnerTeams)
            writer.WritePacked((int)wt);

        writer.WritePacked(WinnerRoles.Count);
        foreach (var wr in WinnerRoles)
            writer.WritePacked((int)wr);

        writer.WritePacked(WinnerIds.Count);
        foreach (var id in WinnerIds)
            writer.Write(id);

        return writer;
    }
    public static void ReadFrom(MessageReader reader)
    {
        WinnerTeam = (CustomWinner)reader.ReadPackedInt32();

        AdditionalWinnerTeams = [];
        int AdditionalWinnerTeamsCount = reader.ReadPackedInt32();
        for (int i = 0; i < AdditionalWinnerTeamsCount; i++)
            AdditionalWinnerTeams.Add((AdditionalWinners)reader.ReadPackedInt32());

        WinnerRoles = [];
        int WinnerRolesCount = reader.ReadPackedInt32();
        for (int i = 0; i < WinnerRolesCount; i++)
            WinnerRoles.Add((CustomRoles)reader.ReadPackedInt32());

        WinnerIds = [];
        int WinnerIdsCount = reader.ReadPackedInt32();
        for (int i = 0; i < WinnerIdsCount; i++)
            WinnerIds.Add(reader.ReadByte());
    }
}