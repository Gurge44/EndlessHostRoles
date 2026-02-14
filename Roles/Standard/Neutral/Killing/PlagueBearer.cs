using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class PlagueBearer : RoleBase
{
    private const int Id = 26000;
    public static List<byte> PlayerIdList = [];
    public static Dictionary<byte, List<byte>> PlaguedList = [];
    public static List<byte> PestilenceList = [];

    public static OptionItem PlagueBearerCDOpt;
    public static OptionItem PestilenceCDOpt;
    public static OptionItem PestilenceCanVent;
    public static OptionItem PestilenceHasImpostorVision;
    public static OptionItem InfectionSpreads;
    public static OptionItem AnnounceTransformation;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.PlagueBearer);

        PlagueBearerCDOpt = new FloatOptionItem(Id + 10, "PlagueBearerCD", new(0f, 180f, 0.5f), 17.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer])
            .SetValueFormat(OptionFormat.Seconds);

        PestilenceCDOpt = new FloatOptionItem(Id + 11, "PestilenceCD", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer])
            .SetValueFormat(OptionFormat.Seconds);

        PestilenceCanVent = new BooleanOptionItem(Id + 12, "PestilenceCanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer]);

        PestilenceHasImpostorVision = new BooleanOptionItem(Id + 13, "PestilenceHasImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer]);
        
        InfectionSpreads = new BooleanOptionItem(Id + 14, "InfectionSpreads", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer]);
        
        AnnounceTransformation = new BooleanOptionItem(Id + 15, "AnnounceTransformation", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.PlagueBearer]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        PlaguedList = [];
        PestilenceList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        PlaguedList[playerId] = [];
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = PlagueBearerCDOpt.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public static bool IsPlagued(byte pc, byte target)
    {
        return PlaguedList.TryGetValue(pc, out List<byte> x) && x.Contains(target);
    }

    private static void SendRPC(byte player, byte target)
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetPlaguedPlayer, SendOption.Reliable);
        writer.Write(player);
        writer.Write(target);
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

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (pc.PlayerId == playerId) continue;

            all++;
            if (IsPlagued(playerId, pc.PlayerId)) plagued++;
        }

        return (plagued, all);
    }

    public static bool IsPlaguedAll(PlayerControl player)
    {
        if (!player.Is(CustomRoles.PlagueBearer) || !Main.IntroDestroyed) return false;

        (int plagued, int all) = PlaguedPlayerCount(player.PlayerId);
        return plagued >= all;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (IsPlagued(killer.PlayerId, target.PlayerId))
        {
            killer.ResetKillCooldown();
            killer.SetKillCooldown(PlagueBearerCDOpt.GetFloat());
            killer.Notify(GetString("PlagueBearerAlreadyPlagued"));
            return false;
        }

        PlaguedList[killer.PlayerId].Add(target.PlayerId);
        SendRPC(killer.PlayerId, target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        killer.ResetKillCooldown();
        killer.SetKillCooldown(PlagueBearerCDOpt.GetFloat());
        return false;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(GetString("InfectiousKillButtonText"));
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public static void CheckAndSpreadInfection(PlayerControl pc, PlayerControl target)
    {
        if (PlayerIdList.Count == 0 || !InfectionSpreads.GetBool()) return;
        if (!PlaguedList.FindFirst(x => x.Value.Contains(pc.PlayerId) && !x.Value.Contains(target.PlayerId), out KeyValuePair<byte, List<byte>> kvp)) return;
        kvp.Value.Add(target.PlayerId);
        SendRPC(kvp.Key, target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: kvp.Key.GetPlayer(), SpecifyTarget: target);
        Logger.Info($"{pc.GetNameWithRole()} infects {target.GetNameWithRole()}", "PlagueBearer");
    }
}

public class Pestilence : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    private bool Announced;

    public override void SetupCustomOption() { }

    public override void Add(byte playerId)
    {
        On = true;
        Announced = false;
    }

    public override void Init()
    {
        On = false;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = PlagueBearer.PestilenceCDOpt.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return PlagueBearer.PestilenceCanVent.GetBool();
    }

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
        if (killer == null || target == null || !killer.IsAlive()) return false;

        killer.SetRealKiller(target);
        target.Kill(killer);
        target.SetKillCooldown(1f);

        if (target.AmOwner)
            Achievements.Type.YoureTooLate.Complete();

        return false;
    }

    public override void OnReportDeadBody()
    {
        if (Announced || !PlagueBearer.AnnounceTransformation.GetBool()) return;
        LateTask.New(() => Utils.SendMessage(GetString("TransformationAnnouncementMessage"), title: $"{CustomRoles.PlagueBearer.ToColoredString()} => {CustomRoles.Pestilence.ToColoredString()}", importance: MessageImportance.High), 12f, "Pb to Pesti transform message");
        Announced = true;
    }
}