using AmongUs.Data;
using HarmonyLib;
using System.Collections.Generic;
using TOHE.Roles.Impostor;

namespace TOHE;

static class PlayerOutfitExtension
{
    public static GameData.PlayerOutfit Set(this GameData.PlayerOutfit instance, string playerName, int colorId, string hatId, string skinId, string visorId, string petId)
    {
        instance.PlayerName = playerName;
        instance.ColorId = colorId;
        instance.HatId = hatId;
        instance.SkinId = skinId;
        instance.VisorId = visorId;
        instance.PetId = petId;
        return instance;
    }
    public static bool Compare(this GameData.PlayerOutfit instance, GameData.PlayerOutfit targetOutfit)
    {
        return instance.ColorId == targetOutfit.ColorId &&
                instance.HatId == targetOutfit.HatId &&
                instance.SkinId == targetOutfit.SkinId &&
                instance.VisorId == targetOutfit.VisorId &&
                instance.PetId == targetOutfit.PetId;

    }
    public static string GetString(this GameData.PlayerOutfit instance)
    {
        return $"{instance.PlayerName} Color:{instance.ColorId} {instance.HatId} {instance.SkinId} {instance.VisorId} {instance.PetId}";
    }
}
public static class Camouflage
{
    static GameData.PlayerOutfit CamouflageOutfit = new GameData.PlayerOutfit().Set("", 15, "", "", "", ""); // Default

    public static bool IsCamouflage;
    public static Dictionary<byte, GameData.PlayerOutfit> PlayerSkins = new();

    public static void Init()
    {
        IsCamouflage = false;
        PlayerSkins.Clear();

        switch (Options.KPDCamouflageMode.GetValue())
        { 
            case 0: // Default
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 15, "", "", "", "");
                break;

            case 1: // Host's outfit
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", DataManager.Player.Customization.Color, DataManager.Player.Customization.Hat, DataManager.Player.Customization.Skin, DataManager.Player.Customization.Visor, DataManager.Player.Customization.Pet);
                break;

            case 2: // Karpe
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 13, "hat_pk05_Plant", "", "visor_BubbleBumVisor", "");
                break;

            case 3: // Loonie
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 12, "hat_pkHW01_Wolf", "skin_scarfskin", "visor_Carrot", "pet_Pusheen");
                break;

            case 4: // Lauryn
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 13, "hat_rabbitEars", "skin_Bananaskin", "visor_BubbleBumVisor", "pet_Pusheen");
                break;

            case 5: // Moe
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 4, "hat_mira_headset_yellow", "skin_SuitB", "visor_lollipopCrew", "pet_EmptyPet");
                break;
        }
    }
    public static void CheckCamouflage()
    {
        if (!(AmongUsClient.Instance.AmHost && (Options.CommsCamouflage.GetBool() || CustomRoles.Camouflager.IsEnable()))) return;

        var oldIsCamouflage = IsCamouflage;

        IsCamouflage = (Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Camouflager.IsActive;

        if (oldIsCamouflage != IsCamouflage)
        {
            Main.AllPlayerControls.Do(pc => RpcSetSkin(pc));
            Utils.NotifyRoles();
        }
    }
    public static void RpcSetSkin(PlayerControl target, bool ForceRevert = false, bool RevertToDefault = false)
    {
        if (!(AmongUsClient.Instance.AmHost && (Options.CommsCamouflage.GetBool() || CustomRoles.Camouflager.IsEnable()))) return;
        if (target == null) return;

        var id = target.PlayerId;

        if (IsCamouflage)
        {
            //コミュサボ中

            //死んでいたら処理しない
            if (Main.PlayerStates[id].IsDead) return;
        }

        var newOutfit = CamouflageOutfit;

        if (!IsCamouflage || ForceRevert)
        {
            //コミュサボ解除または強制解除

            if (Main.CheckShapeshift.TryGetValue(id, out var shapeshifting) && shapeshifting && !RevertToDefault)
            {
                //シェイプシフターなら今の姿のidに変更
                id = Main.ShapeshiftTarget[id];
            }

            newOutfit = PlayerSkins[id];
        }
        Logger.Info($"newOutfit={newOutfit.GetString()}", "RpcSetSkin");

        var sender = CustomRpcSender.Create(name: $"Camouflage.RpcSetSkin({target.Data.PlayerName})");

        target.SetColor(newOutfit.ColorId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetColor)
            .Write(newOutfit.ColorId)
            .EndRpc();

        target.SetHat(newOutfit.HatId, newOutfit.ColorId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetHatStr)
            .Write(newOutfit.HatId)
            .EndRpc();

        target.SetSkin(newOutfit.SkinId, newOutfit.ColorId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetSkinStr)
            .Write(newOutfit.SkinId)
            .EndRpc();

        target.SetVisor(newOutfit.VisorId, newOutfit.ColorId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetVisorStr)
            .Write(newOutfit.VisorId)
            .EndRpc();

        target.SetPet(newOutfit.PetId);
        sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetPetStr)
            .Write(newOutfit.PetId)
            .EndRpc();

        sender.SendMessage();
    }
}