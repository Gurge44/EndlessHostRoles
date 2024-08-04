using EHR.Modules;

namespace EHR;

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

        foreach (var kvp in player.Data.Outfits)
        {
            if (kvp.Value.PetId == "")
            {
                kvp.Value.PetId = petId;
            }
        }

        var sender = CustomRpcSender.Create(name: $"Set Pet to {petId} for {player.GetNameWithRole()}");
        sender.AutoStartRpc(player.NetId, (byte)RpcCalls.SetPetStr)
            .Write(petId)
            .EndRpc();
        sender.SendMessage();

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