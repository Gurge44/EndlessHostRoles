using System.Collections;
using System.Collections.Generic;
using AmongUs.Data;
using EHR.AddOns.Common;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using Hazel;

namespace EHR;

internal static class PlayerOutfitExtension
{
    public static NetworkedPlayerInfo.PlayerOutfit Set(this NetworkedPlayerInfo.PlayerOutfit instance, string playerName, int colorId, string hatId, string skinId, string visorId, string petId, string nameplateId)
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

    public static bool Compare(this NetworkedPlayerInfo.PlayerOutfit instance, NetworkedPlayerInfo.PlayerOutfit targetOutfit)
    {
        return instance.ColorId == targetOutfit.ColorId &&
               instance.HatId == targetOutfit.HatId &&
               instance.SkinId == targetOutfit.SkinId &&
               instance.VisorId == targetOutfit.VisorId &&
               instance.PetId == targetOutfit.PetId;
    }

    public static string GetString(this NetworkedPlayerInfo.PlayerOutfit instance)
    {
        return $"{instance.PlayerName} Color:{instance.ColorId} {instance.HatId} {instance.SkinId} {instance.VisorId} {instance.PetId}";
    }
}

public static class Camouflage
{
    private static NetworkedPlayerInfo.PlayerOutfit CamouflageOutfit = new NetworkedPlayerInfo.PlayerOutfit().Set("", 15, "", "", "", "", ""); // Default

    public static bool IsCamouflage;
    public static bool BlockCamouflage;
    public static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> PlayerSkins = [];
    public static List<byte> ResetSkinAfterDeathPlayers = [];
    private static HashSet<byte> WaitingForSkinChange = [];

    private static int SkippedCamoTimes;
    private static int CamoTimesThisGame;
    public static int CamoTimesThisRound;

    public static void Init()
    {
        IsCamouflage = false;
        PlayerSkins = [];
        ResetSkinAfterDeathPlayers = [];
        WaitingForSkinChange = [];

        SkippedCamoTimes = 0;
        CamoTimesThisGame = 0;
        CamoTimesThisRound = 0;

        CamouflageOutfit = Options.KPDCamouflageMode.GetValue() switch
        {
            0 => new NetworkedPlayerInfo.PlayerOutfit().Set("", 15, "", "", "", "", ""), // Default
            1 => new NetworkedPlayerInfo.PlayerOutfit().Set("", DataManager.Player.Customization.Color, DataManager.Player.Customization.Hat, DataManager.Player.Customization.Skin, DataManager.Player.Customization.Visor, DataManager.Player.Customization.Pet, ""), // Host
            2 => new NetworkedPlayerInfo.PlayerOutfit().Set("", 13, "hat_pk05_Plant", "", "visor_BubbleBumVisor", "", ""), // Karpe
            7 => new NetworkedPlayerInfo.PlayerOutfit().Set("", 7, "hat_pk04_Snowman", "", "", "", ""), // Gurge44
            8 => new NetworkedPlayerInfo.PlayerOutfit().Set("", 17, "hat_baseball_Black", "skin_Scientist-Darkskin", "visor_pusheenSmileVisor", "pet_Pip", ""), // TommyXL
            _ => CamouflageOutfit
        };

        SetPetForOutfitIfNecessary(CamouflageOutfit);
    }

    public static void SetPetForOutfitIfNecessary(NetworkedPlayerInfo.PlayerOutfit outfit)
    {
        if (Options.UsePets.GetBool())
            outfit.PetId = PetsHelper.GetPetId();
    }

    private static bool ShouldCamouflage(bool alreadyCamouflaged)
    {
        if (Camouflager.On && Camouflager.IsActive) return true;

        switch (Main.CurrentMap)
        {
            case MapNames.Fungle when Options.CommsCamouflageDisableOnFungle.GetBool():
            case MapNames.MiraHQ when Options.CommsCamouflageDisableOnMira.GetBool():
                return false;
        }

        if (Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool())
        {
            if (!alreadyCamouflaged)
            {
                if (Options.CommsCamouflageLimitSetChance.GetBool() && IRandom.Instance.Next(100) >= Options.CommsCamouflageLimitChance.GetInt()) return false;
                if (Options.CommsCamouflageLimitSetFrequency.GetBool() && ++SkippedCamoTimes < Options.CommsCamouflageLimitFrequency.GetInt()) return false;

                if (Options.CommsCamouflageLimitSetMaxTimes.GetBool())
                {
                    if (CamoTimesThisGame++ >= Options.CommsCamouflageLimitMaxTimesPerGame.GetInt()) return false;
                    if (CamoTimesThisRound++ >= Options.CommsCamouflageLimitMaxTimesPerRound.GetInt()) return false;
                }
            }

            return true;
        }

        return false;
    }

    public static bool CheckCamouflage()
    {
        if (!AmongUsClient.Instance.AmHost || (!Options.CommsCamouflage.GetBool() && !Camouflager.On)) return false;

        bool oldIsCamouflage = IsCamouflage;

        IsCamouflage = ShouldCamouflage(oldIsCamouflage);

        if (oldIsCamouflage != IsCamouflage)
        {
            Logger.Info($"IsCamouflage: {IsCamouflage}", "CheckCamouflage");
            WaitingForSkinChange = [];
            Main.Instance.StartCoroutine(UpdateCamouflageStatusAsync());
            if (Options.CommsCamouflageSetSameSpeed.GetBool()) Utils.MarkEveryoneDirtySettings();
            if (!Utils.DoRPC) return true;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncCamouflage, SendOption.Reliable);
            writer.Write(IsCamouflage);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            return true;
        }

        return false;
    }

    private static IEnumerator UpdateCamouflageStatusAsync()
    {
        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            if (pc.inVent || pc.walkingToVent || pc.onLadder || pc.inMovingPlat)
            {
                WaitingForSkinChange.Add(pc.PlayerId);
                continue;
            }

            RpcSetSkin(pc);

            yield return null;
        }

        yield return Utils.NotifyEveryoneAsync(5);
    }

    public static void RpcSetSkin(PlayerControl target, bool forceRevert = false, bool revertToDefault = false, bool gameEnd = false, bool revive = false, bool notCommsOrCamo = false, CustomRpcSender sender = null)
    {
        if (!AmongUsClient.Instance.AmHost || (!Options.CommsCamouflage.GetBool() && !Camouflager.On && !revive && !notCommsOrCamo) || target == null || (BlockCamouflage && !forceRevert && !revertToDefault && !gameEnd && !revive && !notCommsOrCamo)) return;

        Logger.Info($"New outfit for {target.GetNameWithRole()}", "Camouflage.RpcSetSkin");

        byte id = target.PlayerId;

        if (IsCamouflage && !target.IsAlive() && target.Data.IsDead && !revive)
        {
            Logger.Info("Player is dead, returning", "Camouflage.RpcSetSkin");
            return;
        }

        NetworkedPlayerInfo.PlayerOutfit newOutfit = CamouflageOutfit;

        if (!IsCamouflage || forceRevert)
        {
            if (id.IsPlayerShifted() && !revertToDefault) id = Main.ShapeshiftTarget[id];

            if (!gameEnd && Doppelganger.DoppelPresentSkin.TryGetValue(id, out NetworkedPlayerInfo.PlayerOutfit value))
                newOutfit = value;
            else
            {
                if (gameEnd && Doppelganger.DoppelVictim.TryGetValue(id, out string value1))
                {
                    PlayerControl dpc = Utils.GetPlayerById(id);
                    dpc?.RpcSetName(value1);
                }

                newOutfit = PlayerSkins[id];
            }
        }

        if (target.Is(CustomRoles.BananaMan))
            newOutfit = BananaMan.GetOutfit(Main.AllPlayerNames.GetValueOrDefault(target.PlayerId, "Banana"));

        SetPetForOutfitIfNecessary(newOutfit);
        
        if (!target.IsAlive())
        {
            var killer = target.GetRealKiller();

            if (Options.AnonymousBodies.GetBool() || target.Is(CustomRoles.Hidden) || (killer != null && killer.Is(CustomRoles.Concealer)))
                newOutfit = new NetworkedPlayerInfo.PlayerOutfit().Set(Translator.GetString("Dead"), 15, "", "", "", "", "");
            else if (Options.RemovePetsAtDeadPlayers.GetBool())
                newOutfit.PetId = string.Empty;
        }

        // if the current Outfit is the same, return
        if (newOutfit.Compare(target.Data.DefaultOutfit))
        {
            Logger.Info("Outfit is the same, returning", "Camouflage.RpcSetSkin");
            return;
        }

        Logger.Info($"Setting new outfit: {newOutfit.GetString()}", "Camouflage.RpcSetSkin");

        bool noSender = sender == null;
        if (noSender) sender = CustomRpcSender.Create($"Camouflage.RpcSetSkin({target.Data.PlayerName})", SendOption.Reliable);
        WriteToSender(sender, target, newOutfit);
        if (noSender) sender.SendMessage();
    }

    private static void WriteToSender(CustomRpcSender sender, PlayerControl target, NetworkedPlayerInfo.PlayerOutfit newOutfit)
    {
        target.SetColor(newOutfit.ColorId);

        sender.AutoStartRpc(target.NetId, RpcCalls.SetColor)
            .Write(target.Data.NetId)
            .Write((byte)newOutfit.ColorId)
            .EndRpc();

        target.SetHat(newOutfit.HatId, newOutfit.ColorId);
        target.Data.DefaultOutfit.HatSequenceId += 10;

        sender.AutoStartRpc(target.NetId, RpcCalls.SetHatStr)
            .Write(newOutfit.HatId)
            .Write(target.GetNextRpcSequenceId(RpcCalls.SetHatStr))
            .EndRpc();

        target.SetSkin(newOutfit.SkinId, newOutfit.ColorId);
        target.Data.DefaultOutfit.SkinSequenceId += 10;

        sender.AutoStartRpc(target.NetId, RpcCalls.SetSkinStr)
            .Write(newOutfit.SkinId)
            .Write(target.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
            .EndRpc();

        target.SetVisor(newOutfit.VisorId, newOutfit.ColorId);
        target.Data.DefaultOutfit.VisorSequenceId += 10;

        sender.AutoStartRpc(target.NetId, RpcCalls.SetVisorStr)
            .Write(newOutfit.VisorId)
            .Write(target.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
            .EndRpc();

        target.SetPet(newOutfit.PetId);
        target.Data.DefaultOutfit.PetSequenceId += 10;

        sender.AutoStartRpc(target.NetId, RpcCalls.SetPetStr)
            .Write(newOutfit.PetId)
            .Write(target.GetNextRpcSequenceId(RpcCalls.SetPetStr))
            .EndRpc();
    }

    public static void OnFixedUpdate(PlayerControl pc)
    {
        if (pc.AmOwner) CheckCamouflage();

        if (!WaitingForSkinChange.Contains(pc.PlayerId) || pc.inVent || pc.walkingToVent || pc.onLadder || pc.inMovingPlat) return;

        RpcSetSkin(pc);
        WaitingForSkinChange.Remove(pc.PlayerId);

        if (!IsCamouflage && !pc.IsAlive()) PetsHelper.RpcRemovePet(pc);

        Utils.NotifyRoles(SpecifySeer: pc);
        Utils.NotifyRoles(SpecifyTarget: pc);
    }
}