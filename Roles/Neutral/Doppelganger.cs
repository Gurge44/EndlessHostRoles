using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Roles.Impostor;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Neutral;

public class Doppelganger : RoleBase
{
    private const int Id = 648100;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem MaxSteals;
    private static OptionItem ResetMode;
    private static OptionItem ResetTimer;

    public static Dictionary<byte, string> DoppelVictim = [];
    public static Dictionary<byte, GameData.PlayerOutfit> DoppelPresentSkin = [];
    public static Dictionary<byte, int> TotalSteals = [];
    public static Dictionary<byte, GameData.PlayerOutfit> DoppelDefaultSkin = [];

    private static readonly string[] ResetModes =
    [
        "DGRM.None",
        "DGRM.OnMeeting",
        "DGRM.AfterTime"
    ];

    private byte DGId;
    private long StealTimeStamp;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Doppelganger);
        MaxSteals = IntegerOptionItem.Create(Id + 10, "DoppelMaxSteals", new(1, 14, 1), 9, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger]);
        KillCooldown = FloatOptionItem.Create(Id + 11, "KillCooldown", new(0f, 180f, 0.5f), 20f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger])
            .SetValueFormat(OptionFormat.Seconds);
        ResetMode = StringOptionItem.Create(Id + 12, "DGResetMode", ResetModes, 0, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger]);
        ResetTimer = FloatOptionItem.Create(Id + 13, "DGResetTimer", new(0f, 60f, 1f), 30f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Doppelganger])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        playerIdList = [];
        DoppelVictim = [];
        TotalSteals = [];
        DoppelPresentSkin = [];
        DoppelDefaultSkin = [];
        DGId = byte.MaxValue;
        StealTimeStamp = 0;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        DGId = playerId;
        TotalSteals.Add(playerId, 0);
        var pc = Utils.GetPlayerById(playerId);
        if (playerId == PlayerControl.LocalPlayer.PlayerId && Main.NickName.Length != 0) DoppelVictim[playerId] = Main.NickName;
        else DoppelVictim[playerId] = pc.Data.PlayerName;
        DoppelDefaultSkin[playerId] = pc.CurrentOutfit;
        StealTimeStamp = 0;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    void SendRPC(byte playerId)
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
        if (!TotalSteals.TryAdd(PlayerId, 0))
            TotalSteals[PlayerId] = Limit;
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

    //overloading
    public static GameData.PlayerOutfit Set(GameData.PlayerOutfit instance, string playerName, int colorId, string hatId, string skinId, string visorId, string petId, string nameplateId)
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

    static void RpcChangeSkin(PlayerControl pc, GameData.PlayerOutfit newOutfit)
    {
        var sender = CustomRpcSender.Create(name: $"Doppelganger.RpcChangeSkin({pc.Data.PlayerName})");

        pc.SetName(newOutfit.PlayerName);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetName)
            .Write(newOutfit.PlayerName)
            .EndRpc();

        Main.AllPlayerNames[pc.PlayerId] = newOutfit.PlayerName;

        pc.SetColor(newOutfit.ColorId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetColor)
            .Write(newOutfit.ColorId)
            .EndRpc();

        pc.SetHat(newOutfit.HatId, newOutfit.ColorId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetHatStr)
            .Write(newOutfit.HatId)
            .EndRpc();

        pc.SetSkin(newOutfit.SkinId, newOutfit.ColorId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetSkinStr)
            .Write(newOutfit.SkinId)
            .EndRpc();

        pc.SetVisor(newOutfit.VisorId, newOutfit.ColorId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetVisorStr)
            .Write(newOutfit.VisorId)
            .EndRpc();

        pc.SetPet(newOutfit.PetId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetPetStr)
            .Write(newOutfit.PetId)
            .EndRpc();

        pc.SetNamePlate(newOutfit.NamePlateId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetNamePlateStr)
            .Write(newOutfit.NamePlateId)
            .EndRpc();

        sender.SendMessage();
        DoppelPresentSkin[pc.PlayerId] = newOutfit;
    }

    public static bool OnCheckMurderEnd(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || Camouflage.IsCamouflage || Camouflager.IsActive || Main.PlayerStates[killer.PlayerId].Role is not Doppelganger { IsEnable: true } dg) return true;
        if (target.IsShifted())
        {
            Logger.Info("Target was shapeshifting", "Doppelganger");
            return true;
        }

        if (TotalSteals[killer.PlayerId] >= MaxSteals.GetInt())
        {
            TotalSteals[killer.PlayerId] = MaxSteals.GetInt();
            return true;
        }

        TotalSteals[killer.PlayerId]++;

        dg.StealTimeStamp = Utils.TimeStamp;

        string kname;
        if (killer.PlayerId == PlayerControl.LocalPlayer.PlayerId && Main.NickName.Length != 0) kname = Main.NickName;
        else kname = killer.Data.PlayerName;
        string tname;
        if (target.PlayerId == PlayerControl.LocalPlayer.PlayerId && Main.NickName.Length != 0) tname = Main.NickName;
        else tname = target.Data.PlayerName;

        var killerSkin = Set(new(), kname, killer.CurrentOutfit.ColorId, killer.CurrentOutfit.HatId, killer.CurrentOutfit.SkinId, killer.CurrentOutfit.VisorId, killer.CurrentOutfit.PetId, killer.CurrentOutfit.NamePlateId);

        var targetSkin = Set(new(), tname, target.CurrentOutfit.ColorId, target.CurrentOutfit.HatId, target.CurrentOutfit.SkinId, target.CurrentOutfit.VisorId, target.CurrentOutfit.PetId, target.CurrentOutfit.NamePlateId);

        DoppelVictim[target.PlayerId] = tname;


        RpcChangeSkin(target, killerSkin);
        Logger.Info("Changed target skin", "Doppelganger");
        RpcChangeSkin(killer, targetSkin);
        Logger.Info("Changed killer skin", "Doppelganger");

        dg.SendRPC(killer.PlayerId);
        Utils.NotifyRoles();
        killer.ResetKillCooldown();
        killer.SetKillCooldown();

        return true;
    }

    public override void OnReportDeadBody()
    {
        if (ResetMode.GetValue() == 1 && TotalSteals[DGId] > 0)
        {
            var pc = Utils.GetPlayerById(DGId);
            var currentTarget = Main.AllPlayerControls.FirstOrDefault(x => x.GetRealName() == DoppelVictim[pc.PlayerId]);
            if (currentTarget != null)
            {
                RpcChangeSkin(currentTarget, DoppelPresentSkin[currentTarget.PlayerId]);
                RpcChangeSkin(pc, DoppelDefaultSkin[pc.PlayerId]);
                DoppelVictim[pc.PlayerId] = string.Empty;
            }
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (ResetMode.GetValue() == 2 && TotalSteals[pc.PlayerId] > 0 && Utils.TimeStamp - StealTimeStamp > ResetTimer.GetInt())
        {
            var currentTarget = Main.AllPlayerControls.FirstOrDefault(x => x.GetRealName() == DoppelVictim[pc.PlayerId]);
            if (currentTarget != null)
            {
                RpcChangeSkin(currentTarget, DoppelPresentSkin[currentTarget.PlayerId]);
                RpcChangeSkin(pc, DoppelDefaultSkin[pc.PlayerId]);
                DoppelVictim[pc.PlayerId] = string.Empty;

                if (GameStates.IsInTask)
                {
                    Utils.NotifyRoles();
                }
            }
        }
    }

    public override string GetProgressText(byte playerId, bool comms) => Utils.ColorString(TotalSteals[playerId] < MaxSteals.GetInt() ? Utils.GetRoleColor(CustomRoles.Doppelganger).ShadeColor(0.25f) : Color.gray, TotalSteals.TryGetValue(playerId, out var stealLimit) ? $"({MaxSteals.GetInt() - stealLimit})" : "Invalid");
}