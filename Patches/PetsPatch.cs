namespace EHR;

public static class PetsPatch
{
    public static void RpcRemovePet(PlayerControl pc)
    {
        if (pc == null || !pc.Data.IsDead || pc.IsAlive()) return;

        if (!GameStates.IsInGame) return;

        if (!Options.RemovePetsAtDeadPlayers.GetBool()) return;

        if (pc.CurrentOutfit.PetId == "") return;

        var sender = CustomRpcSender.Create("Remove Pet From Dead Player");

        pc.SetPet("");
        pc.Data.DefaultOutfit.PetSequenceId += 10;

        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetPetStr)
            .Write("")
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetPetStr))
            .EndRpc();

        sender.SendMessage();
    }

    public static string GetPetId()
    {
        string[] pets = Options.PetToAssign;
        string pet = pets[Options.PetToAssignToEveryone.GetValue()];
        string petId = pet == "pet_RANDOM_FOR_EVERYONE" ? pets[IRandom.Instance.Next(0, pets.Length - 1)] : pet;
        return petId;
    }
}