﻿using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using UnityEngine;

namespace TOHE.Roles.Crewmate;

public static class Medic
{
    private static readonly int Id = 7100;
    public static List<byte> playerIdList = [];
    public static List<byte> ProtectList = [];
    public static byte TempMarkProtected;
    public static Dictionary<byte, int> ProtectLimit = [];
    public static int SkillLimit;

    public static OptionItem WhoCanSeeProtect;
    private static OptionItem KnowShieldBroken;
    private static OptionItem ShieldDeactivatesWhenMedicDies;
    private static OptionItem ShieldDeactivationIsVisible;
    private static OptionItem ResetCooldown;
    public static OptionItem GuesserIgnoreShield;
    private static OptionItem AmountOfShields;
    public static OptionItem UsePet;

    public static readonly string[] MedicWhoCanSeeProtectName =
    [
        "SeeMedicAndTarget",
        "SeeMedic",
        "SeeTarget",
        "SeeNoone",
    ];

    public static readonly string[] KnowShieldBrokenOption =
    [
        "SeeMedicAndTarget",
        "SeeMedic",
        "SeeTarget",
        "SeeNoone",
    ];

    public static readonly string[] ShieldDeactivationIsVisibleOption =
    [
        "DeactivationImmediately",
        "DeactivationAfterMeeting",
        "DeactivationIsVisibleOFF",
    ];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Medic);
        WhoCanSeeProtect = StringOptionItem.Create(Id + 10, "MedicWhoCanSeeProtect", MedicWhoCanSeeProtectName, 0, TabGroup.CrewmateRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);
        KnowShieldBroken = StringOptionItem.Create(Id + 16, "MedicKnowShieldBroken", KnowShieldBrokenOption, 1, TabGroup.CrewmateRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);
        ShieldDeactivatesWhenMedicDies = BooleanOptionItem.Create(Id + 24, "MedicShieldDeactivatesWhenMedicDies", true, TabGroup.CrewmateRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);
        ShieldDeactivationIsVisible = StringOptionItem.Create(Id + 25, "MedicShielDeactivationIsVisible", ShieldDeactivationIsVisibleOption, 0, TabGroup.CrewmateRoles, false)
            .SetParent(ShieldDeactivatesWhenMedicDies);
        ResetCooldown = FloatOptionItem.Create(Id + 30, "MedicResetCooldown", new(0f, 120f, 1f), 15f, TabGroup.CrewmateRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic])
            .SetValueFormat(OptionFormat.Seconds);
        GuesserIgnoreShield = BooleanOptionItem.Create(Id + 32, "MedicShieldedCanBeGuessed", true, TabGroup.CrewmateRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);
        AmountOfShields = IntegerOptionItem.Create(Id + 34, "MedicAmountOfShields", new(1, 14, 1), 1, TabGroup.CrewmateRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);
        UsePet = Options.CreatePetUseSetting(Id + 36, CustomRoles.Medic);
    }
    public static void Init()
    {
        playerIdList = [];
        ProtectList = [];
        ProtectLimit = [];
        TempMarkProtected = byte.MaxValue;
        SkillLimit = AmountOfShields.GetInt();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        ProtectLimit.TryAdd(playerId, SkillLimit);

        Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()} : {ProtectLimit[playerId]} shields left", "Medicaler");

        if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMedicalerProtectLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(ProtectLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        if (ProtectLimit.ContainsKey(PlayerId))
            ProtectLimit[PlayerId] = Limit;
        else
            ProtectLimit.Add(PlayerId, SkillLimit);
    }
    private static void SendRPCForProtectList()
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMedicalerProtectList, SendOption.Reliable, -1);
        writer.Write(ProtectList.Count);
        foreach (byte x in ProtectList.ToArray())
            writer.Write(x);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPCForProtectList(MessageReader reader)
    {
        int count = reader.ReadInt32();
        ProtectList = [];
        for (int i = 0; i < count; i++)
            ProtectList.Add(reader.ReadByte());
    }
    public static bool CanUseKillButton(byte playerId)
        => !Main.PlayerStates[playerId].IsDead
        && (ProtectLimit.TryGetValue(playerId, out var x) ? x : 1) >= 1;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(id) ? 5f : 300f;
    public static string GetSkillLimit(byte playerId) => Utils.ColorString(CanUseKillButton(playerId) ? Utils.GetRoleColor(CustomRoles.Medic).ShadeColor(0.25f) : Color.gray, ProtectLimit.TryGetValue(playerId, out var protectLimit) ? $"({protectLimit})" : "Invalid");
    public static bool InProtect(byte id) => ProtectList.Contains(id) && Main.PlayerStates.TryGetValue(id, out var ps) && !ps.IsDead;
    public static void OnCheckMurderFormedicaler(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return;
        if (!CanUseKillButton(killer.PlayerId)) return;
        if (ProtectList.Contains(target.PlayerId)) return;

        ProtectLimit[killer.PlayerId]--;

        SendRPC(killer.PlayerId);
        ProtectList.Add(target.PlayerId);
        TempMarkProtected = target.PlayerId;
        SendRPCForProtectList();

        killer.SetKillCooldown();

        switch (WhoCanSeeProtect.GetInt())
        {
            case 0:
                //killer.RpcGuardAndKill(target);
                killer.RPCPlayCustomSound("Shield");
                target.RPCPlayCustomSound("Shield");
                break;
            case 1:
                //killer.RpcGuardAndKill(target);
                killer.RPCPlayCustomSound("Shield");
                break;
            case 2:
                target.RPCPlayCustomSound("Shield");
                break;
        }

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : {ProtectLimit[killer.PlayerId]} shields left", "Medic");
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;
        if (!ProtectList.Contains(target.PlayerId)) return false;

        SendRPCForProtectList();

        //killer.RpcGuardAndKill(target);
        killer.SetKillCooldown(ResetCooldown.GetFloat());

        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        switch (KnowShieldBroken.GetInt())
        {
            case 0:
                target.RpcGuardAndKill(target);
                Main.AllPlayerControls.Where(x => playerIdList.Contains(x.PlayerId) && !x.Data.IsDead).Do(x => x.Notify(Translator.GetString("MedicKillerTryBrokenShieldTargetForMedic")));
                Main.AllPlayerControls.Where(x => ProtectList.Contains(x.PlayerId)).Do(x => x.Notify(Translator.GetString("MedicKillerTryBrokenShieldTargetForTarget")));
                break;
            case 1:
                Main.AllPlayerControls.Where(x => playerIdList.Contains(x.PlayerId) && !x.Data.IsDead).Do(x => x.Notify(Translator.GetString("MedicKillerTryBrokenShieldTargetForMedic")));
                break;
            case 2:
                target.RpcGuardAndKill(target);
                Main.AllPlayerControls.Where(x => ProtectList.Contains(x.PlayerId)).Do(x => x.Notify(Translator.GetString("MedicKillerTryBrokenShieldTargetForTarget")));
                break;
            default:
                break;
        }

        Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : Shield Shatter from the Medic", "Medic");
        return true;
    }
    public static void OnCheckMark()
    {
        if (!ShieldDeactivatesWhenMedicDies.GetBool()) return;

        if (ShieldDeactivationIsVisible.GetInt() == 1)
        {
            TempMarkProtected = byte.MaxValue;
            foreach (byte id in playerIdList.ToArray())
            {
                foreach (byte id2 in ProtectList.ToArray())
                {
                    Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(id), SpecifyTarget: Utils.GetPlayerById(id2));
                }
            }
        }
    }
    public static void IsDead(PlayerControl target)
    {
        if (!target.Is(CustomRoles.Medic)) return;
        if (!ShieldDeactivatesWhenMedicDies.GetBool()) return;

        if (!playerIdList.All(x => !Utils.GetPlayerById(x).IsAlive())) return; // If not all Medic-s are dead, return

        foreach (byte pc in ProtectList.ToArray())
        {
            Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(pc), SpecifyTarget: target);
        }
        Utils.NotifyRoles(SpecifySeer: target);

        ProtectList.Clear();
        Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : Medic is dead", "Medic");

        if (ShieldDeactivationIsVisible.GetInt() == 0)
        {
            TempMarkProtected = byte.MaxValue;
        }
    }
}