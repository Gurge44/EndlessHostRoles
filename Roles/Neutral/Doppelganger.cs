using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Impostor;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral;

public class Doppelganger : RoleBase
{
    private const int Id = 648100;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;
    private static OptionItem MaxSteals;
    private static OptionItem ResetMode;
    private static OptionItem ResetTimer;

    public static int LocalPlayerChangeSkinTimes;

    public static Dictionary<byte, string> DoppelVictim = [];
    public static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> DoppelPresentSkin = [];
    private static Dictionary<byte, int> TotalSteals = [];
    private static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> DoppelDefaultSkin = [];

    private static readonly string[] ResetModes =
    [
        "DGRM.None",
        "DGRM.OnMeeting",
        "DGRM.AfterTime"
    ];

    private byte DGId;
    private long StealTimeStamp;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Doppelganger);

        MaxSteals = new IntegerOptionItem(Id + 10, "DoppelMaxSteals", new(1, 14, 1), 9, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger]);

        KillCooldown = new FloatOptionItem(Id + 11, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 14, "CanVent", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger]);

        ImpostorVision = new BooleanOptionItem(Id + 15, "ImpostorVision", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger]);

        ResetMode = new StringOptionItem(Id + 12, "DGResetMode", ResetModes, 0, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger]);

        ResetTimer = new FloatOptionItem(Id + 13, "DGResetTimer", new(0f, 60f, 1f), 30f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        DoppelVictim = [];
        TotalSteals = [];
        DoppelPresentSkin = [];
        DoppelDefaultSkin = [];
        DGId = byte.MaxValue;
        StealTimeStamp = 0;

        LocalPlayerChangeSkinTimes = 0;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        DGId = playerId;
        TotalSteals[playerId] = 0;
        PlayerControl pc = Utils.GetPlayerById(playerId);

        if (playerId == PlayerControl.LocalPlayer.PlayerId && Main.NickName.Length != 0)
            DoppelVictim[playerId] = Main.NickName;
        else
            DoppelVictim[playerId] = pc.Data.PlayerName;

        DoppelDefaultSkin[playerId] = pc.CurrentOutfit;
        StealTimeStamp = 0;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDoppelgangerStealLimit, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(TotalSteals[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        if (!TotalSteals.TryAdd(PlayerId, 0)) TotalSteals[PlayerId] = Limit;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ImpostorVision.GetBool());
    }

    private static NetworkedPlayerInfo.PlayerOutfit Set(NetworkedPlayerInfo.PlayerOutfit instance, string playerName, int colorId, string hatId, string skinId, string visorId, string petId, string nameplateId)
    {
        instance.PlayerName = playerName;
        instance.ColorId = colorId;
        instance.HatId = hatId;
        instance.SkinId = skinId;
        instance.VisorId = visorId;
        instance.PetId = petId;
        instance.NamePlateId = nameplateId;
        return instance;
    }

    private static void RpcChangeSkin(PlayerControl pc, NetworkedPlayerInfo.PlayerOutfit newOutfit)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        
        var sender = CustomRpcSender.Create($"Doppelganger.RpcChangeSkin({pc.Data.PlayerName})", SendOption.Reliable);

        pc.SetName(newOutfit.PlayerName);

        sender.AutoStartRpc(pc.NetId, RpcCalls.SetName)
            .Write(pc.Data.NetId)
            .Write(newOutfit.PlayerName)
            .EndRpc();

        Main.AllPlayerNames[pc.PlayerId] = newOutfit.PlayerName;

        pc.SetColor(newOutfit.ColorId);

        sender.AutoStartRpc(pc.NetId, RpcCalls.SetColor)
            .Write(pc.Data.NetId)
            .Write((byte)newOutfit.ColorId)
            .EndRpc();

        pc.SetHat(newOutfit.HatId, newOutfit.ColorId);
        pc.Data.DefaultOutfit.HatSequenceId += 10;

        sender.AutoStartRpc(pc.NetId, RpcCalls.SetHatStr)
            .Write(newOutfit.HatId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetHatStr))
            .EndRpc();

        pc.SetSkin(newOutfit.SkinId, newOutfit.ColorId);
        pc.Data.DefaultOutfit.SkinSequenceId += 10;

        sender.AutoStartRpc(pc.NetId, RpcCalls.SetSkinStr)
            .Write(newOutfit.SkinId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
            .EndRpc();

        pc.SetVisor(newOutfit.VisorId, newOutfit.ColorId);
        pc.Data.DefaultOutfit.VisorSequenceId += 10;

        sender.AutoStartRpc(pc.NetId, RpcCalls.SetVisorStr)
            .Write(newOutfit.VisorId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
            .EndRpc();

        pc.SetPet(newOutfit.PetId);
        pc.Data.DefaultOutfit.PetSequenceId += 10;

        sender.AutoStartRpc(pc.NetId, RpcCalls.SetPetStr)
            .Write(newOutfit.PetId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetPetStr))
            .EndRpc();

        pc.SetNamePlate(newOutfit.NamePlateId);
        pc.Data.DefaultOutfit.NamePlateSequenceId += 10;

        sender.AutoStartRpc(pc.NetId, RpcCalls.SetNamePlateStr)
            .Write(newOutfit.NamePlateId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetNamePlateStr))
            .EndRpc();

        sender.SendMessage();
        DoppelPresentSkin[pc.PlayerId] = newOutfit;

        if (pc.AmOwner)
        {
            LocalPlayerChangeSkinTimes++;
            if (LocalPlayerChangeSkinTimes >= 2) Achievements.Type.Mimicry.Complete();
        }
    }

    public static void OnCheckMurderEnd(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || Camouflage.IsCamouflage || Camouflager.IsActive || Main.PlayerStates[killer.PlayerId].Role is not Doppelganger { IsEnable: true } dg) return;

        if (target.IsShifted())
        {
            Logger.Info("Target was shapeshifting", "Doppelganger");
            return;
        }

        if (TotalSteals[killer.PlayerId] >= MaxSteals.GetInt())
        {
            TotalSteals[killer.PlayerId] = MaxSteals.GetInt();
            return;
        }

        TotalSteals[killer.PlayerId]++;

        dg.StealTimeStamp = Utils.TimeStamp;

        string kname;

        if (killer.AmOwner && Main.NickName.Length != 0)
            kname = Main.NickName;
        else
            kname = killer.Data.PlayerName;

        string tname;

        if (target.AmOwner && Main.NickName.Length != 0)
            tname = Main.NickName;
        else
            tname = target.Data.PlayerName;

        NetworkedPlayerInfo.PlayerOutfit killerSkin = Set(new(), kname, killer.CurrentOutfit.ColorId, killer.CurrentOutfit.HatId, killer.CurrentOutfit.SkinId, killer.CurrentOutfit.VisorId, killer.CurrentOutfit.PetId, killer.CurrentOutfit.NamePlateId);

        NetworkedPlayerInfo.PlayerOutfit targetSkin = Set(new(), tname, target.CurrentOutfit.ColorId, target.CurrentOutfit.HatId, target.CurrentOutfit.SkinId, target.CurrentOutfit.VisorId, target.CurrentOutfit.PetId, target.CurrentOutfit.NamePlateId);

        DoppelVictim[target.PlayerId] = tname;


        RpcChangeSkin(target, killerSkin);
        Logger.Info("Changed target skin", "Doppelganger");
        RpcChangeSkin(killer, targetSkin);
        Logger.Info("Changed killer skin", "Doppelganger");

        target.Notify(Translator.GetString("DoppelgangerWarning"));

        dg.SendRPC(killer.PlayerId);
        Utils.NotifyRoles();
        killer.ResetKillCooldown();
        killer.SetKillCooldown();

        target.SetRealKiller(killer);
    }

    public override void OnReportDeadBody()
    {
        try
        {
            if (ResetMode.GetValue() == 1 && TotalSteals[DGId] > 0)
            {
                PlayerControl pc = Utils.GetPlayerById(DGId);
                if (pc == null) return;

                PlayerControl currentTarget = Main.AllPlayerControls.FirstOrDefault(x => x?.GetRealName() == DoppelVictim[pc.PlayerId]);

                if (currentTarget != null)
                {
                    RpcChangeSkin(currentTarget, DoppelPresentSkin[currentTarget.PlayerId]);
                    RpcChangeSkin(pc, DoppelDefaultSkin[pc.PlayerId]);
                    DoppelVictim[pc.PlayerId] = string.Empty;
                }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (ResetMode.GetValue() == 2 && TotalSteals[pc.PlayerId] > 0 && Utils.TimeStamp - StealTimeStamp > ResetTimer.GetInt())
        {
            PlayerControl currentTarget = Main.AllPlayerControls.FirstOrDefault(x => x.GetRealName() == DoppelVictim[pc.PlayerId]);

            if (currentTarget != null)
            {
                RpcChangeSkin(currentTarget, DoppelPresentSkin[currentTarget.PlayerId]);
                RpcChangeSkin(pc, DoppelDefaultSkin[pc.PlayerId]);
                DoppelVictim[pc.PlayerId] = string.Empty;

                if (GameStates.IsInTask) Utils.NotifyRoles();
            }
        }
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return Utils.ColorString(TotalSteals[playerId] < MaxSteals.GetInt() ? Utils.GetRoleColor(CustomRoles.Doppelganger).ShadeColor(0.25f) : Color.gray, TotalSteals.TryGetValue(playerId, out int stealLimit) ? $"({MaxSteals.GetInt() - stealLimit})" : "Invalid");
    }
}