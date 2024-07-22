using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Impostor;

internal class QuickShooter : RoleBase
{
    private const int Id = 1800;
    public static List<byte> playerIdList = [];
    private static OptionItem KillCooldown;
    private static OptionItem MeetingReserved;
    public static OptionItem ShapeshiftCooldown;

    public static Dictionary<byte, int> ShotLimit = [];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.QuickShooter);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.QuickShooter])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftCooldown = new FloatOptionItem(Id + 12, "QuickShooterShapeshiftCooldown", new(0f, 180f, 2.5f), 40f, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.QuickShooter])
            .SetValueFormat(OptionFormat.Seconds);
        MeetingReserved = new IntegerOptionItem(Id + 14, "MeetingReserved", new(0, 15, 1), 1, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.QuickShooter])
            .SetValueFormat(OptionFormat.Pieces);
    }

    public override void Init()
    {
        playerIdList = [];
        ShotLimit = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        ShotLimit.TryAdd(playerId, 0);
    }

    private static void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetQuickShooterShotLimit, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(ShotLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte QuickShooterId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        ShotLimit.TryAdd(QuickShooterId, Limit);
        ShotLimit[QuickShooterId] = Limit;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = ShapeshiftCooldown.GetFloat();
        else
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
    {
        if (Main.KillTimers[pc.PlayerId] <= 0 && (shapeshifting || Options.UseUnshiftTrigger.GetBool()))
        {
            Store(pc);
        }

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (Main.KillTimers[pc.PlayerId] <= 0)
        {
            Store(pc);
        }

        return false;
    }

    private static void Store(PlayerControl pc)
    {
        ShotLimit[pc.PlayerId]++;
        SendRPC(pc.PlayerId);
        pc.ResetKillCooldown();
        pc.SetKillCooldown();
        pc.Notify(Translator.GetString("QuickShooterStoraging"));
        Logger.Info($"{Utils.GetPlayerById(pc.PlayerId)?.GetNameWithRole().RemoveHtmlTags()} : Remaining: {ShotLimit[pc.PlayerId]} bullets", "QuickShooter");
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void OnReportDeadBody()
    {
        Dictionary<byte, int> NewSL = [];
        foreach (var sl in ShotLimit)
            NewSL.Add(sl.Key, Math.Clamp(sl.Value, 0, MeetingReserved.GetInt()));
        foreach (var sl in NewSL)
        {
            ShotLimit[sl.Key] = sl.Value;
            SendRPC(sl.Key);
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        ShotLimit.TryAdd(killer.PlayerId, 0);
        if (ShotLimit[killer.PlayerId] > 0) LateTask.New(() => { killer.SetKillCooldown(0.01f); }, 0.01f, "QuickShooterKill: Set KCD to 0s");
        ShotLimit[killer.PlayerId]--;
        ShotLimit[killer.PlayerId] = Math.Max(ShotLimit[killer.PlayerId], 0);
        SendRPC(killer.PlayerId);
        return true;
    }

    public override string GetProgressText(byte playerId, bool comms) => Utils.ColorString(ShotLimit.ContainsKey(playerId) && ShotLimit[playerId] > 0 ? Utils.GetRoleColor(CustomRoles.QuickShooter).ShadeColor(0.25f) : Color.gray, ShotLimit.TryGetValue(playerId, out var shotLimit) ? $"({shotLimit})" : "Invalid");

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (Options.UsePets.GetBool())
        {
            hud.PetButton?.OverrideText(Translator.GetString("QuickShooterShapeshiftText"));
        }
        else
        {
            hud.AbilityButton?.OverrideText(Translator.GetString("QuickShooterShapeshiftText"));
            hud.AbilityButton?.SetUsesRemaining(ShotLimit.GetValueOrDefault(PlayerControl.LocalPlayer.PlayerId, 0));
        }
    }
}