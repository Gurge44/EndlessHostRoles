using System.Collections.Generic;
using System.Linq;
using Hazel;
using TOHE.Modules;
using TOHE.Patches;

namespace TOHE.Roles.Neutral;

public class Collector : RoleBase
{
    private const int Id = 11100;
    public static OptionItem CollectorCollectAmount;
    private static List<byte> playerIdList = [];
    public static Dictionary<byte, byte> CollectorVoteFor = [];
    public static Dictionary<byte, int> CollectVote = [];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Collector);
        CollectorCollectAmount = IntegerOptionItem.Create(Id + 13, "CollectorCollectAmount", new(1, 60, 1), 30, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Collector])
            .SetValueFormat(OptionFormat.Votes);
    }

    public override void Init()
    {
        playerIdList = [];
        CollectorVoteFor = [];
        CollectVote = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        CollectVote.TryAdd(playerId, 0);
    }

    public override bool IsEnable => playerIdList.Count > 0;

    private static void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCollectorVotes, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(CollectVote[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Num = reader.ReadInt32();
        CollectVote.TryAdd(PlayerId, 0);
        CollectVote[PlayerId] = Num;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        if (!CollectVote.TryGetValue(playerId, out var VoteAmount)) return string.Empty;
        int CollectNum = CollectorCollectAmount.GetInt();
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Collector).ShadeColor(0.25f), $"({VoteAmount}/{CollectNum})");
    }

    public static bool CollectorWin(bool check = true)
    {
        var pc = Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Collector) && x.IsAlive() && CollectDone(x)).ToArray();
        if (pc.Length > 0)
        {
            if (check) return true;
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Collector);
            foreach (var winner in pc) CustomWinnerHolder.WinnerIds.Add(winner.PlayerId);
            return true;
        }

        return false;
    }

    public static bool CollectDone(PlayerControl player)
    {
        if (player.Is(CustomRoles.Collector))
        {
            var pcid = player.PlayerId;
            int VoteAmount = CollectVote[pcid];
            int CollectNum = CollectorCollectAmount.GetInt();
            if (VoteAmount >= CollectNum) return true;
        }

        return false;
    }

    public static void CollectorVotes(PlayerControl target, PlayerVoteArea ps) //集票者投票给谁
    {
        if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Collector))
            CollectorVoteFor.TryAdd(target.PlayerId, ps.TargetPlayerId);
    }

    public static void CollectAmount(Dictionary<byte, int> VotingData, MeetingHud __instance) //得到集票者收集到的票
    {
        foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
        {
            if (pva == null) continue;
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null) continue;
            foreach ((byte key, int value) in VotingData)
            {
                if (CollectorVoteFor.ContainsKey(key) && pc.PlayerId == CollectorVoteFor[key] && pc.Is(CustomRoles.Collector))
                {
                    CollectVote.TryAdd(pc.PlayerId, 0);
                    CollectVote[pc.PlayerId] += value;
                    SendRPC(pc.PlayerId);
                }
            }
        }
    }
}