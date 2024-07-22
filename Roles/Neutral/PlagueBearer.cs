using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

public class PlagueBearer : RoleBase
{
    private const int Id = 26000;
    public static List<byte> playerIdList = [];
    public static Dictionary<byte, List<byte>> PlaguedList = [];
    public static Dictionary<byte, float> PlagueBearerCD = [];
    public static List<byte> PestilenceList = [];

    public static OptionItem PlagueBearerCDOpt;
    public static OptionItem PestilenceCDOpt;
    public static OptionItem PestilenceCanVent;
    public static OptionItem PestilenceHasImpostorVision;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.PlagueBearer);
        PlagueBearerCDOpt = new FloatOptionItem(Id + 10, "PlagueBearerCD", new(0f, 180f, 0.5f), 17.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer])
            .SetValueFormat(OptionFormat.Seconds);
        PestilenceCDOpt = new FloatOptionItem(Id + 11, "PestilenceCD", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer])
            .SetValueFormat(OptionFormat.Seconds);
        PestilenceCanVent = new BooleanOptionItem(Id + 12, "PestilenceCanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer]);
        PestilenceHasImpostorVision = new BooleanOptionItem(Id + 13, "PestilenceHasImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer]);
    }

    public override void Init()
    {
        playerIdList = [];
        PlaguedList = [];
        PlagueBearerCD = [];
        PestilenceList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        PlagueBearerCD.Add(playerId, PlagueBearerCDOpt.GetFloat());
        PlaguedList[playerId] = [];
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = PlagueBearerCD[id];
    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

    public static bool IsPlagued(byte pc, byte target) => PlaguedList.TryGetValue(pc, out var x) && x.Contains(target);

    public static void SendRPC(PlayerControl player, PlayerControl target)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetPlaguedPlayer, SendOption.Reliable); //RPCによる同期
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

    public static (int Plagued, int All) PlaguedPlayerCount(byte playerId)
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
        (int plagued, int all) = PlaguedPlayerCount(player.PlayerId);
        return plagued >= all;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
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

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(GetString("InfectiousKillButtonText"));
    }
}

public class Pestilence : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = PlagueBearer.PestilenceCDOpt.GetFloat();
    public override bool CanUseImpostorVentButton(PlayerControl pc) => PlagueBearer.PestilenceCanVent.GetBool();

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(PlagueBearer.PestilenceHasImpostorVision.GetBool());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (base.OnCheckMurder(killer, target))
            killer.Kill(target);
        return false;
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;
        killer.SetRealKiller(target);
        target.Kill(killer);
        return false;
    }
}