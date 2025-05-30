using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Coven;
using EHR.Impostor;
using EHR.Modules;
using Hazel;
using static EHR.Options;

namespace EHR.Crewmate;

public class Psychic : RoleBase
{
    private const int Id = 7900;
    private static List<byte> PlayerIdList = [];

    private static OptionItem CanSeeNum;
    private static OptionItem Fresh;
    private static OptionItem CkshowEvil;
    private static OptionItem NBshowEvil;
    private static OptionItem NEshowEvil;
    private byte PsychicId;

    private List<byte> RedPlayer = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Psychic);

        CanSeeNum = new IntegerOptionItem(Id + 2, "PsychicCanSeeNum", new(1, 10, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Psychic])
            .SetValueFormat(OptionFormat.Pieces);

        Fresh = new BooleanOptionItem(Id + 6, "PsychicFresh", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Psychic]);

        CkshowEvil = new BooleanOptionItem(Id + 3, "CrewKillingRed", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Psychic]);

        NBshowEvil = new BooleanOptionItem(Id + 4, "NBareRed", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Psychic]);

        NEshowEvil = new BooleanOptionItem(Id + 5, "NEareRed", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Psychic]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        RedPlayer = [];
        PsychicId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        RedPlayer = [];
        PsychicId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private void SendRPC()
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncPsychicRedList, SendOption.Reliable);
        writer.Write(PsychicId);
        writer.Write(RedPlayer.Count);
        foreach (byte pc in RedPlayer) writer.Write(pc);

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        if (Main.PlayerStates[playerId].Role is not Psychic ph) return;

        int count = reader.ReadInt32();
        ph.RedPlayer = [];
        for (var i = 0; i < count; i++) ph.RedPlayer.Add(reader.ReadByte());
    }

    public static bool IsRedForPsy(PlayerControl target, PlayerControl seer)
    {
        if (target == null || seer == null) return false;

        if (Main.PlayerStates[seer.PlayerId].Role is not Psychic ph) return false;

        if (seer.Is(CustomRoles.Madmate)) return target.GetCustomRole().IsNeutral() || target.GetCustomRole().GetCrewmateRoleCategory() == RoleOptionType.Crewmate_Killing;

        return ph.RedPlayer != null && ph.RedPlayer.Contains(target.PlayerId);
    }

    public override void OnReportDeadBody()
    {
        if (Fresh.GetBool() || RedPlayer == null || RedPlayer.Count == 0) GetRedName();
    }

    private void GetRedName()
    {
        if (!IsEnable || !AmongUsClient.Instance.AmHost) return;

        List<PlayerControl> BadListPc = Main.AllAlivePlayerControls.Where(x =>
            (x.Is(CustomRoleTypes.Impostor) && !x.Is(CustomRoles.Trickster)) || x.Is(CustomRoles.Madmate) || x.Is(CustomRoles.Rascal) || Framer.FramedPlayers.Contains(x.PlayerId) || Enchanter.EnchantedPlayers.Contains(x.PlayerId) || x.IsConverted() ||
            (x.GetCustomRole().GetCrewmateRoleCategory() == RoleOptionType.Crewmate_Killing && CkshowEvil.GetBool()) ||
            (x.GetCustomRole().GetNeutralRoleCategory() is RoleOptionType.Neutral_Evil or RoleOptionType.Neutral_Pariah && NEshowEvil.GetBool()) ||
            (x.GetCustomRole().GetNeutralRoleCategory() == RoleOptionType.Neutral_Benign && NBshowEvil.GetBool())
        ).ToList();

        List<byte> BadList = [];
        BadListPc.Do(x => BadList.Add(x.PlayerId));
        List<byte> AllList = [];
        Main.AllAlivePlayerControls.Where(x => !BadList.Contains(x.PlayerId) && !x.Is(CustomRoles.Psychic)).Do(x => AllList.Add(x.PlayerId));

        var ENum = 1;

        for (var i = 1; i < CanSeeNum.GetInt(); i++)
        {
            if (IRandom.Instance.Next(0, 100) < 18)
                ENum++;
        }

        int BNum = CanSeeNum.GetInt() - ENum;
        ENum = Math.Min(ENum, BadList.Count);
        BNum = Math.Min(BNum, AllList.Count);

        if (ENum < 1) goto EndOfSelect;

        RedPlayer = [];

        for (var i = 0; i < ENum && BadList.Count > 0; i++)
        {
            RedPlayer.Add(BadList.RandomElement());
            BadList.RemoveAll(RedPlayer.Contains);
        }

        AllList.RemoveAll(RedPlayer.Contains);

        for (var i = 0; i < BNum && AllList.Count > 0; i++)
        {
            RedPlayer.Add(AllList.RandomElement());
            AllList.RemoveAll(RedPlayer.Contains);
        }

        EndOfSelect:

        Logger.Info($"Requires {CanSeeNum.GetInt()} red names, of which {ENum} evil names are required. After calculation, {RedPlayer.Count} red names are displayed.", "Psychic");
        RedPlayer.Do(x => Logger.Info($"Red for Psychic: {x}: {Main.AllPlayerNames[x]}", "Psychic"));
        SendRPC();
    }
}