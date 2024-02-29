using TOHE.Modules;

namespace TOHE;

public static class PetsPatch
{
    public static void SetPet(PlayerControl player, string petId)
    {
        if (player.Is(CustomRoles.GM)) return;
        if (player.AmOwner)
        {
            player.SetPet(petId);
            return;
        }

        var outfit = player.Data.Outfits[PlayerOutfitType.Default];
        if (outfit.PetId == string.Empty || outfit.PetId == "") outfit.PetId = petId;
        RPC.SendGameData(player.GetClientId());
    }
    public static void RpcRemovePet(PlayerControl pc)
    {
        if (pc == null || !pc.Data.IsDead) return;
        if (!GameStates.IsInGame) return;
        if (!Options.RemovePetsAtDeadPlayers.GetBool()) return;
        if (pc.CurrentOutfit.PetId == "") return;

        var sender = CustomRpcSender.Create(name: "Remove Pet From Dead Player");

        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetPetStr)
            .Write("")
            .EndRpc();
        sender.SendMessage();
    }
}