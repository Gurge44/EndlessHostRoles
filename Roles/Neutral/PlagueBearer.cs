using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Impostor;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;
public static class PlagueBearer
{
    private static readonly int Id = 26000;
    public static List<byte> playerIdList = [];
    public static Dictionary<byte, List<byte>> PlaguedList = [];
    public static Dictionary<byte, float> PlagueBearerCD = [];
    public static Dictionary<byte, int> PestilenceCD = [];
    public static List<byte> PestilenceList = [];

    public static OptionItem PlagueBearerCDOpt;
    public static OptionItem PestilenceCDOpt;
    public static OptionItem PestilenceCanVent;
    public static OptionItem PestilenceHasImpostorVision;


    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.PlagueBearer);
        PlagueBearerCDOpt = FloatOptionItem.Create(Id + 10, "PlagueBearerCD", new(0f, 180f, 2.5f), 17.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer])
                .SetValueFormat(OptionFormat.Seconds);
        PestilenceCDOpt = FloatOptionItem.Create(Id + 11, "PestilenceCD", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer])
                .SetValueFormat(OptionFormat.Seconds);
        PestilenceCanVent = BooleanOptionItem.Create(Id + 12, "PestilenceCanVent", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer]);
        PestilenceHasImpostorVision = BooleanOptionItem.Create(Id + 13, "PestilenceHasImpostorVision", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer]);
    }

    public static void Init()
    {
        playerIdList = [];
        PlaguedList = [];
        PlagueBearerCD = [];
        PestilenceList = [];
    }

    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        PlagueBearerCD.Add(playerId, PlagueBearerCDOpt.GetFloat());
        PlaguedList[playerId] = [];
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;

    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = PlagueBearerCD[id];
    public static void SetKillCooldownPestilence(byte id) => Main.AllPlayerKillCooldown[id] = PestilenceCDOpt.GetFloat();

    public static bool IsPlagued(byte pc, byte target) => PlaguedList.TryGetValue(pc, out var x) && x.Contains(target);
    public static void SendRPC(PlayerControl player, PlayerControl target)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer;
        writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.setPlaguedPlayer, SendOption.Reliable, -1);//RPCによる同期
        writer.Write(player.PlayerId);
        writer.Write(target.PlayerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlagueBearerId = reader.ReadByte();
        byte PlaguedId = reader.ReadByte();
        PlaguedList[PlagueBearerId].Add(PlaguedId);
    }
    public static (int, int) PlaguedPlayerCount(byte playerId)
    {
        int plagued = 0, all = 0;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == playerId)
                continue;
            all++;
            if (IsPlagued(playerId, pc.PlayerId))
                plagued++;
        }
        return (plagued, all);
    }

    public static bool IsPlaguedAll(PlayerControl player)
    {

        if (!player.Is(CustomRoles.PlagueBearer)) return false;
        var count = PlaguedPlayerCount(player.PlayerId);
        return count.Item1 >= count.Item2;
    }

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (IsPlagued(killer.PlayerId, target.PlayerId))
        {
            killer.Notify(GetString("PlagueBearerAlreadyPlagued"));
            return false;
        }
        PlaguedList[killer.PlayerId].Add(target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        Logger.Msg($"kill cooldown {PlagueBearerCD[killer.PlayerId]}", "PlagueBearer");
        return false;
    }

    public static bool IsIndirectKill(PlayerControl killer)
    {
        return Main.PuppeteerList.ContainsKey(killer.PlayerId) ||
            Main.TaglockedList.ContainsKey(killer.PlayerId) ||
            Main.CursedPlayers.ContainsValue(killer) ||
            Sniper.snipeTarget.ContainsValue(killer.PlayerId);
    }

    public static bool OnCheckMurderPestilence(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;
        if (!PestilenceList.Contains(target.PlayerId)) return false;
        if (target.Is(CustomRoles.Guardian) && target.AllTasksCompleted()) return true;
        if (target.Is(CustomRoles.Opportunist) && target.AllTasksCompleted()) return true;
        if (target.Is(CustomRoles.Veteran) && Main.VeteranInProtect.ContainsKey(target.PlayerId)) return true;
        if (target.Is(CustomRoles.TimeMaster) && Main.TimeMasterInProtect.ContainsKey(target.PlayerId)) return true;
        if (IsIndirectKill(killer)) return false;
        killer.SetRealKiller(target);
        target.Kill(killer);
        return true;
    }
}
