using System.Collections.Generic;
using AmongUs.Data;
using EHR.Impostor;
using EHR.Neutral;

namespace EHR
{
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

        public static void Init()
        {
            IsCamouflage = false;
            PlayerSkins = [];
            ResetSkinAfterDeathPlayers = [];
            WaitingForSkinChange = [];

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
            if (Options.UsePets.GetBool() && outfit.PetId == "")
            {
                string[] pets = Options.PetToAssign;
                string pet = pets[Options.PetToAssignToEveryone.GetValue()];
                string petId = pet == "pet_RANDOM_FOR_EVERYONE" ? pets[IRandom.Instance.Next(0, pets.Length - 1)] : pet;
                outfit.PetId = petId;
            }
        }

        public static bool CheckCamouflage()
        {
            if (!AmongUsClient.Instance.AmHost || (!Options.CommsCamouflage.GetBool() && !Camouflager.On)) return false;

            bool oldIsCamouflage = IsCamouflage;

            IsCamouflage = (Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Camouflager.IsActive;

            if (oldIsCamouflage != IsCamouflage)
            {
                Logger.Info($"IsCamouflage: {IsCamouflage}", "CheckCamouflage");

                WaitingForSkinChange = [];

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (pc.inVent || pc.walkingToVent || pc.onLadder || pc.inMovingPlat)
                    {
                        WaitingForSkinChange.Add(pc.PlayerId);
                        continue;
                    }

                    RpcSetSkin(pc);

                    if (!IsCamouflage && !pc.IsAlive()) PetsPatch.RpcRemovePet(pc);
                }

                Utils.NotifyRoles(NoCache: true);
                return true;
            }

            return false;
        }

        public static void RpcSetSkin(PlayerControl target, bool ForceRevert = false, bool RevertToDefault = false, bool GameEnd = false)
        {
            if (!AmongUsClient.Instance.AmHost || (!Options.CommsCamouflage.GetBool() && !Camouflager.On) || target == null || (BlockCamouflage && !ForceRevert && !RevertToDefault && !GameEnd)) return;

            Logger.Info($"New outfit for {target.GetNameWithRole()}", "Camouflage.RpcSetSkin");

            byte id = target.PlayerId;

            if (IsCamouflage && !target.IsAlive() && target.Data.IsDead)
            {
                Logger.Info("Player is dead, returning", "Camouflage.RpcSetSkin");
                return;
            }

            NetworkedPlayerInfo.PlayerOutfit newOutfit = CamouflageOutfit;

            if (!IsCamouflage || ForceRevert)
            {
                if (id.IsPlayerShifted() && !RevertToDefault) id = Main.ShapeshiftTarget[id];

                if (!GameEnd && Doppelganger.DoppelPresentSkin.TryGetValue(id, out NetworkedPlayerInfo.PlayerOutfit value))
                    newOutfit = value;
                else
                {
                    if (GameEnd && Doppelganger.DoppelVictim.TryGetValue(id, out string value1))
                    {
                        PlayerControl dpc = Utils.GetPlayerById(id);
                        dpc?.RpcSetName(value1);
                    }

                    newOutfit = PlayerSkins[id];
                }
            }

            SetPetForOutfitIfNecessary(newOutfit);

            // if the current Outfit is the same, return
            if (newOutfit.Compare(target.Data.DefaultOutfit))
            {
                Logger.Info("Outfit is the same, returning", "Camouflage.RpcSetSkin");
                return;
            }

            Logger.Info($"Setting new outfit: {newOutfit.GetString()}", "Camouflage.RpcSetSkin");

            var sender = CustomRpcSender.Create($"Camouflage.RpcSetSkin({target.Data.PlayerName})");

            target.SetColor(newOutfit.ColorId);

            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetColor)
                .Write(target.Data.NetId)
                .Write((byte)newOutfit.ColorId)
                .EndRpc();

            target.SetHat(newOutfit.HatId, newOutfit.ColorId);
            target.Data.DefaultOutfit.HatSequenceId += 10;

            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetHatStr)
                .Write(newOutfit.HatId)
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetHatStr))
                .EndRpc();

            target.SetSkin(newOutfit.SkinId, newOutfit.ColorId);
            target.Data.DefaultOutfit.SkinSequenceId += 10;

            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetSkinStr)
                .Write(newOutfit.SkinId)
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
                .EndRpc();

            target.SetVisor(newOutfit.VisorId, newOutfit.ColorId);
            target.Data.DefaultOutfit.VisorSequenceId += 10;

            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetVisorStr)
                .Write(newOutfit.VisorId)
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
                .EndRpc();

            target.SetPet(newOutfit.PetId);
            target.Data.DefaultOutfit.PetSequenceId += 10;

            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetPetStr)
                .Write(newOutfit.PetId)
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetPetStr))
                .EndRpc();

            sender.SendMessage();
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (!WaitingForSkinChange.Contains(pc.PlayerId) || pc.inVent || pc.walkingToVent || pc.onLadder || pc.inMovingPlat) return;

            RpcSetSkin(pc);
            WaitingForSkinChange.Remove(pc.PlayerId);

            if (!IsCamouflage && !pc.IsAlive()) PetsPatch.RpcRemovePet(pc);

            Utils.NotifyRoles(SpecifySeer: pc, NoCache: true);
            Utils.NotifyRoles(SpecifyTarget: pc, NoCache: true);
        }
    }
}