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

        var setEmpty = petId == "";
        if (player.Data.DefaultOutfit.PetId == "" || setEmpty) player.Data.DefaultOutfit.PetId = petId;
        if (player.CurrentOutfit.PetId == "" || setEmpty) player.CurrentOutfit.PetId = petId;
        foreach (var kvp in player.Data.Outfits)
        {
            if (kvp.Value.PetId == "" || setEmpty)
            {
                kvp.Value.PetId = petId;
            }
        }

        var sender = CustomRpcSender.Create(name: $"Set Pet to {petId} for {player.GetNameWithRole()}");
        sender.AutoStartRpc(player.NetId, (byte)RpcCalls.SetPetStr)
            .Write(petId)
            .Write(player.GetNextRpcSequenceId(RpcCalls.SetPetStr))
            .EndRpc();
        sender.SendMessage();
    }

    public static void RpcRemovePet(PlayerControl pc)
    {
        if (pc == null || !pc.Data.IsDead || pc.IsAlive()) return;
        if (!GameStates.IsInGame) return;
        if (!Options.RemovePetsAtDeadPlayers.GetBool()) return;
        if (pc.CurrentOutfit.PetId == "") return;

        var sender = CustomRpcSender.Create(name: "Remove Pet From Dead Player");

        pc.SetPet("");
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetPetStr)
            .Write("")
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetPetStr))
            .EndRpc();
        sender.SendMessage();
    }
}