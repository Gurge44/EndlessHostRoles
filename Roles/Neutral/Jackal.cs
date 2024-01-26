using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Jackal
{
    private static readonly int Id = 12100;
    public static List<byte> playerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem CanUseSabotage;
    public static OptionItem CanWinBySabotageWhenNoImpAlive;
    public static OptionItem HasImpostorVision;
    private static OptionItem OptionResetKillCooldownWhenSbGetKilled;
    public static OptionItem ResetKillCooldownWhenSbGetKilled;
    private static OptionItem ResetKillCooldownOn;
    public static OptionItem CanRecruitSidekick;
    public static OptionItem SidekickRecruitLimitOpt;
    public static OptionItem SidekickCountMode;
    public static OptionItem SidekickAssignMode;
    public static OptionItem SidekickCanWinWithOriginalTeam;
    public static OptionItem KillCooldownSK;
    public static OptionItem CanVentSK;
    public static OptionItem CanUseSabotageSK;
    public static Dictionary<byte, int> RecruitLimit = [];

    public static readonly string[] sidekickAssignMode =
    [
        "SidekickAssignMode.SidekickAndRecruit",
        "SidekickAssignMode.Sidekick",
        "SidekickAssignMode.Recruit",
    ];


    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Jackal, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanUseSabotage = BooleanOptionItem.Create(Id + 12, "CanUseSabotage", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanWinBySabotageWhenNoImpAlive = BooleanOptionItem.Create(Id + 14, "JackalCanWinBySabotageWhenNoImpAlive", true, TabGroup.NeutralRoles, false).SetParent(CanUseSabotage);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        OptionResetKillCooldownWhenSbGetKilled = BooleanOptionItem.Create(Id + 16, "ResetKillCooldownWhenPlayerGetKilled", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownOn = FloatOptionItem.Create(Id + 28, "ResetKillCooldownOn", new(0f, 180f, 2.5f), 15f, TabGroup.NeutralRoles, false)
            .SetParent(OptionResetKillCooldownWhenSbGetKilled)
            .SetValueFormat(OptionFormat.Seconds);
        JackalCanKillSidekick = BooleanOptionItem.Create(Id + 15, "JackalCanKillSidekick", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanRecruitSidekick = BooleanOptionItem.Create(Id + 17, "JackalCanRecruitSidekick", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        SidekickAssignMode = StringOptionItem.Create(Id + 29, "SidekickAssignMode", sidekickAssignMode, 0, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
        SidekickRecruitLimitOpt = IntegerOptionItem.Create(Id + 18, "JackalSidekickRecruitLimit", new(0, 15, 1), 0, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick)
                .SetValueFormat(OptionFormat.Times);
        KillCooldownSK = FloatOptionItem.Create(Id + 20, "KillCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick)
            .SetValueFormat(OptionFormat.Seconds);
        CanVentSK = BooleanOptionItem.Create(Id + 21, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
        CanUseSabotageSK = BooleanOptionItem.Create(Id + 22, "CanUseSabotage", true, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
        SidekickCanKillJackal = BooleanOptionItem.Create(Id + 23, "SidekickCanKillJackal", false, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
        //  SidekickKnowOtherSidekick = BooleanOptionItem.Create(6050585, "SidekickKnowOtherSidekick", false, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
        //  SidekickKnowOtherSidekickRole = BooleanOptionItem.Create(6050590, "SidekickKnowOtherSidekickRole", false, TabGroup.NeutralRoles, false).SetParent(SidekickKnowOtherSidekick);
        SidekickCanKillSidekick = BooleanOptionItem.Create(Id + 24, "SidekickCanKillSidekick", false, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
        SidekickCountMode = StringOptionItem.Create(Id + 25, "SidekickCountMode", sidekickCountMode, 0, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
        //   SidekickCanWinWithOriginalTeam = BooleanOptionItem.Create(6050795, "SidekickCanWinWithOriginalTeam", false, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
    }
    public static void Init()
    {
        playerIdList = [];
        RecruitLimit = [];
        ResetKillCooldownWhenSbGetKilled = OptionResetKillCooldownWhenSbGetKilled;

    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        RecruitLimit.TryAdd(playerId, SidekickRecruitLimitOpt.GetInt());

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;

    private static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJackalRecruitLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(RecruitLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        if (RecruitLimit.ContainsKey(PlayerId))
            RecruitLimit[PlayerId] = Limit;
        else
            RecruitLimit.Add(PlayerId, SidekickRecruitLimitOpt.GetInt());
    }

    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static bool CanRecruit(byte id) => RecruitLimit.TryGetValue(id, out var x) && x > 0;
    public static void SetKillButtonText(byte plaeryId)
    {
        if (CanRecruit(plaeryId))
            HudManager.Instance.KillButton.OverrideText($"{GetString("GangsterButtonText")}");
        else
            HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
    }
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void SetHudActive(HudManager __instance, bool isActive)
    {
        __instance.SabotageButton.ToggleVisible(isActive && CanUseSabotage.GetBool());
    }
    public static void AfterPlayerDiedTask(PlayerControl target)
    {
        Main.AllAlivePlayerControls
            .Where(x => !target.Is(CustomRoles.Jackal) && x.Is(CustomRoles.Jackal))
            .Do(x => x.SetKillCooldown(ResetKillCooldownOn.GetFloat()));
    }

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!CanRecruitSidekick.GetBool() || RecruitLimit[killer.PlayerId] < 1) return false;
        if (SidekickAssignMode.GetValue() != 2)
        {
            if (CanBeSidekick(target))
            {
                RecruitLimit[killer.PlayerId]--;
                SendRPC(killer.PlayerId);
                target.RpcSetCustomRole(CustomRoles.Sidekick);

                if (!Main.ResetCamPlayerList.Contains(target.PlayerId))
                    Main.ResetCamPlayerList.Add(target.PlayerId);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("BeRecruitedByJackal")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Sidekick.ToString(), "Assign " + CustomRoles.Sidekick.ToString());
                if (RecruitLimit[killer.PlayerId] < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RecruitLimit[killer.PlayerId]}次招募机会", "Jackal");
                return true;
            }
        }
        if (SidekickAssignMode.GetValue() != 1)
        {
            if (!CanBeSidekick(target) && !target.Is(CustomRoles.Sidekick) && !target.Is(CustomRoles.Recruit) && !target.Is(CustomRoles.Loyal) && !target.Is(CustomRoles.Admired))
            {
                RecruitLimit[killer.PlayerId]--;
                SendRPC(killer.PlayerId);
                target.RpcSetCustomRole(CustomRoles.Recruit);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("BeRecruitedByJackal")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Sidekick.ToString(), "Assign " + CustomRoles.Sidekick.ToString());
                if (RecruitLimit[killer.PlayerId] < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RecruitLimit[killer.PlayerId]}次招募机会", "Jackal");
                return true;
            }
        }
        if (RecruitLimit[killer.PlayerId] < 0)
            HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
        //killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterRecruitmentFailure")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RecruitLimit[killer.PlayerId]}次招募机会", "Jackal");
        return false;
    }
    public static string GetRecruitLimit(byte playerId) => Utils.ColorString(CanRecruit(playerId) ? Utils.GetRoleColor(CustomRoles.Jackal).ShadeColor(0.25f) : Color.gray, RecruitLimit.TryGetValue(playerId, out var recruitLimit) ? $"({recruitLimit})" : "Invalid");

    public static bool CanBeSidekick(this PlayerControl pc)
    {
        return pc != null && !pc.Is(CustomRoles.Sidekick) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Loyal) && !pc.Is(CustomRoles.Admired) && !pc.Is(CustomRoles.Rascal) && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.DualPersonality) && !pc.Is(CustomRoles.Contagious) && pc.GetCustomRole().IsAbleToBeSidekicked();
    }
}