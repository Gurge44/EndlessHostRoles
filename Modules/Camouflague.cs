using AmongUs.Data;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using System.Collections.Generic;

namespace EHR;

static class PlayerOutfitExtension
{
    public static GameData.PlayerOutfit Set(this GameData.PlayerOutfit instance, string playerName, int colorId, string hatId, string skinId, string visorId, string petId, string nameplateId)
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
    static GameData.PlayerOutfit CamouflageOutfit = new GameData.PlayerOutfit().Set("", 15, "", "", "", "", ""); // Default

    public static bool IsCamouflage;
    public static Dictionary<byte, GameData.PlayerOutfit> PlayerSkins = [];

    public static List<byte> ResetSkinAfterDeathPlayers = [];

    public static void Init()
    {
        IsCamouflage = false;
        PlayerSkins.Clear();
        ResetSkinAfterDeathPlayers = [];

        switch (Options.KPDCamouflageMode.GetValue())
        {
            case 0: // Default
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 15, "", "", "", "", "");
                break;

            case 1: // Host's outfit
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", DataManager.Player.Customization.Color, DataManager.Player.Customization.Hat, DataManager.Player.Customization.Skin, DataManager.Player.Customization.Visor, DataManager.Player.Customization.Pet, "");
                break;

            case 2: // Karpe
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 13, "hat_pk05_Plant", "", "visor_BubbleBumVisor", "", "");
                break;

            case 3: // Lauryn
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 13, "hat_rabbitEars", "skin_Bananaskin", "visor_BubbleBumVisor", "pet_Pusheen", "");
                break;

            case 4: // Moe
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 0, "hat_mira_headset_yellow", "skin_SuitB", "visor_lollipopCrew", "pet_EmptyPet", "");
                break;

            case 5: // Pyro
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 17, "hat_pkHW01_Witch", "skin_greedygrampaskin", "visor_Plsno", "pet_Pusheen", "");
                break;

            case 6: // ryuk
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 7, "hat_crownDouble", "skin_D2Saint14", "visor_anime", "pet_Bush", "");
                break;

            case 7: // Gurge44
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 7, "hat_pk04_Snowman", "", "", "", "");
                break;

            case 8: // TommyXL
                CamouflageOutfit = new GameData.PlayerOutfit()
                    .Set("", 17, "hat_baseball_Black", "skin_Scientist-Darkskin", "visor_pusheenSmileVisor", "pet_Pip", "");
                break;
        }

        if (Options.UsePets.GetBool() && CamouflageOutfit.PetId == "")
        {
            string[] pets = Options.PetToAssign;
            string pet = pets[Options.PetToAssignToEveryone.GetValue()];
            string petId = pet == "pet_RANDOM_FOR_EVERYONE" ? pets[IRandom.Instance.Next(0, pets.Length - 1)] : pet;
            CamouflageOutfit.PetId = petId;
        }
    }

    public static void CheckCamouflage()
    {
        if (!(AmongUsClient.Instance.AmHost && (Options.CommsCamouflage.GetBool() || Camouflager.On))) return;

        var oldIsCamouflage = IsCamouflage;

        IsCamouflage = (Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Camouflager.IsActive;

        if (oldIsCamouflage != IsCamouflage)
        {
            foreach (var pc in Main.AllPlayerControls)
            {
                RpcSetSkin(pc);

                if (!IsCamouflage && !pc.IsAlive())
                {
                    PetsPatch.RpcRemovePet(pc);
                }
            }

            Utils.NotifyRoles(NoCache: true);
        }
    }

    public static void RpcSetSkin(PlayerControl target, bool ForceRevert = false, bool RevertToDefault = false, bool GameEnd = false)
    {
        if (!(AmongUsClient.Instance.AmHost && (Options.CommsCamouflage.GetBool() || Camouflager.On))) return;
        if (target == null) return;

        var id = target.PlayerId;

        if (IsCamouflage && Main.PlayerStates[id].IsDead)
        {
            return;
        }

        var newOutfit = CamouflageOutfit;

        if (!IsCamouflage || ForceRevert)
        {
            //コミュサボ解除または強制解除

            if (id.IsPlayerShifted() && !RevertToDefault)
            {
                //シェイプシフターなら今の姿のidに変更
                id = Main.ShapeshiftTarget[id];
            }

            if (!GameEnd && Doppelganger.DoppelPresentSkin.TryGetValue(id, out GameData.PlayerOutfit value)) newOutfit = value;
            else
            {
                if (GameEnd && Doppelganger.DoppelVictim.TryGetValue(id, out string value1))
                {
                    var dpc = Utils.GetPlayerById(id);
                    dpc?.RpcSetName(value1);
                }

                newOutfit = PlayerSkins[id];
            }
        }

        // if the current Outfit is the same, return it
        if (newOutfit.Compare(target.Data.DefaultOutfit)) return;

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