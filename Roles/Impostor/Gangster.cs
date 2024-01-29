﻿using Hazel;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public static class Gangster
{
    private static readonly int Id = 2900;
    private static List<byte> playerIdList = [];

    private static OptionItem RecruitLimitOpt;
    public static OptionItem KillCooldown;
    public static OptionItem SheriffCanBeMadmate;
    public static OptionItem MayorCanBeMadmate;
    public static OptionItem NGuesserCanBeMadmate;
    public static OptionItem JudgeCanBeMadmate;
    public static OptionItem MarshallCanBeMadmate;
    public static OptionItem FarseerCanBeMadmate;
    //public static OptionItem RetributionistCanBeMadmate;

    public static Dictionary<byte, int> RecruitLimit = [];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Gangster);
        KillCooldown = FloatOptionItem.Create(Id + 10, "GangsterRecruitCooldown", new(0f, 60f, 2.5f), 7.5f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster])
            .SetValueFormat(OptionFormat.Seconds);
        RecruitLimitOpt = IntegerOptionItem.Create(Id + 12, "GangsterRecruitLimit", new(1, 5, 1), 1, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster])
            .SetValueFormat(OptionFormat.Times);

        SheriffCanBeMadmate = BooleanOptionItem.Create(Id + 14, "GanSheriffCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        MayorCanBeMadmate = BooleanOptionItem.Create(Id + 15, "GanMayorCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        NGuesserCanBeMadmate = BooleanOptionItem.Create(Id + 16, "GanNGuesserCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        JudgeCanBeMadmate = BooleanOptionItem.Create(Id + 17, "GanJudgeCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        MarshallCanBeMadmate = BooleanOptionItem.Create(Id + 18, "GanMarshallCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        FarseerCanBeMadmate = BooleanOptionItem.Create(Id + 19, "GanFarseerCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        //RetributionistCanBeMadmate = BooleanOptionItem.Create(Id + 20, "GanRetributionistCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);

    }
    public static void Init()
    {
        playerIdList = [];
        RecruitLimit = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        RecruitLimit.TryAdd(playerId, RecruitLimitOpt.GetInt());
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetGangsterRecruitLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(RecruitLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        RecruitLimit.TryAdd(PlayerId, Limit);
        RecruitLimit[PlayerId] = Limit;
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanRecruit(id) ? KillCooldown.GetFloat() : Options.DefaultKillCooldown;
    public static bool CanRecruit(byte id) => RecruitLimit.TryGetValue(id, out var x) && x > 0;
    public static void SetKillButtonText(byte plaeryId)
    {
        if (CanRecruit(plaeryId))
            HudManager.Instance.KillButton.OverrideText(GetString("GangsterButtonText"));
        else
            HudManager.Instance.KillButton.OverrideText(GetString("KillButtonText"));
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (RecruitLimit[killer.PlayerId] < 1) return false;
        if (CanBeMadmate(target))
        {
            if (!killer.Is(CustomRoles.Recruit) && !killer.Is(CustomRoles.Charmed) && !killer.Is(CustomRoles.Contagious))
            {
                RecruitLimit[killer.PlayerId]--;
                SendRPC(killer.PlayerId);
                target.RpcSetCustomRole(CustomRoles.Madmate);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("BeRecruitedByGangster")));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Madmate.ToString(), "Assign " + CustomRoles.Madmate.ToString());
                if (RecruitLimit[killer.PlayerId] < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RecruitLimit[killer.PlayerId]}次招募机会", "Gangster");
                return true;
            }
            if (killer.Is(CustomRoles.Recruit))
            {
                RecruitLimit[killer.PlayerId]--;
                SendRPC(killer.PlayerId);
                target.RpcSetCustomRole(CustomRoles.Recruit);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Recruit), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Recruit), GetString("BeRecruitedByGangster")));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Recruit.ToString(), "Assign " + CustomRoles.Recruit.ToString());
                if (RecruitLimit[killer.PlayerId] < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RecruitLimit[killer.PlayerId]}次招募机会", "Gangster");
                return true;
            }
            if (killer.Is(CustomRoles.Charmed))
            {
                RecruitLimit[killer.PlayerId]--;
                SendRPC(killer.PlayerId);
                target.RpcSetCustomRole(CustomRoles.Charmed);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Charmed), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Charmed), GetString("BeRecruitedByGangster")));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Charmed.ToString(), "Assign " + CustomRoles.Charmed.ToString());
                if (RecruitLimit[killer.PlayerId] < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RecruitLimit[killer.PlayerId]}次招募机会", "Gangster");
                return true;
            }
            if (killer.Is(CustomRoles.Contagious))
            {
                RecruitLimit[killer.PlayerId]--;
                SendRPC(killer.PlayerId);
                target.RpcSetCustomRole(CustomRoles.Contagious);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Contagious), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Contagious), GetString("BeRecruitedByGangster")));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Contagious.ToString(), "Assign " + CustomRoles.Contagious.ToString());
                if (RecruitLimit[killer.PlayerId] < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RecruitLimit[killer.PlayerId]}次招募机会", "Gangster");
                return true;
            }
        }
        if (RecruitLimit[killer.PlayerId] < 0)
            HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("GangsterRecruitmentFailure")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RecruitLimit[killer.PlayerId]}次招募机会", "Gangster");
        return false;
    }
    public static string GetRecruitLimit(byte playerId) => Utils.ColorString(CanRecruit(playerId) ? Utils.GetRoleColor(CustomRoles.Gangster).ShadeColor(0.25f) : Color.gray, RecruitLimit.TryGetValue(playerId, out var recruitLimit) ? $"({recruitLimit})" : "Invalid");

    public static bool CanBeMadmate(this PlayerControl pc)
    {
        return pc != null && pc.GetCustomRole().IsCrewmate() && !pc.Is(CustomRoles.Madmate)
        && !(
            (pc.Is(CustomRoles.Sheriff) && !SheriffCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Mayor) && !MayorCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.NiceGuesser) && !NGuesserCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Judge) && !JudgeCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Marshall) && !MarshallCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Farseer) && !FarseerCanBeMadmate.GetBool()) ||
            //(pc.Is(CustomRoles.Retributionist) && !RetributionistCanBeMadmate.GetBool()) ||
            pc.Is(CustomRoles.NiceSwapper) ||
            pc.Is(CustomRoles.Snitch) ||
            pc.Is(CustomRoles.Needy) ||
            pc.Is(CustomRoles.Lazy) ||
            pc.Is(CustomRoles.Loyal) ||
            pc.Is(CustomRoles.CyberStar) ||
            pc.Is(CustomRoles.Demolitionist) ||
            pc.Is(CustomRoles.NiceEraser) ||
            pc.Is(CustomRoles.Egoist)
            );
    }
}