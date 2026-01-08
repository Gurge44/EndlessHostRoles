using Hazel;

namespace EHR.Modules;

public static class PetsHelper
{
    public static void RpcRemovePet(PlayerControl pc)
    {
        const string petId = "";
        if (!GameStates.IsInGame || !Options.RemovePetsAtDeadPlayers.GetBool() || pc == null || !pc.Data.IsDead || pc.IsAlive() || pc.CurrentOutfit.PetId == petId) return;

        SetPet(pc, petId);
    }

    public static void SetPet(PlayerControl pc, string petId)
    {
        var sender = CustomRpcSender.Create("PetsHelper.SetPet", SendOption.Reliable);

        try { pc.SetPet(petId); }
        catch { }
        
        try { pc.Data.DefaultOutfit.PetSequenceId += 10; }
        catch { }

        sender.AutoStartRpc(pc.NetId, RpcCalls.SetPetStr)
            .Write(petId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetPetStr))
            .EndRpc();

        sender.SendMessage();
    }

    public static string GetPetId()
    {
        try
        {
            string[] pets = Options.PetToAssign;
            string pet = pets[Options.PetToAssignToEveryone.GetValue()];
            string petId = pet == "pet_RANDOM_FOR_EVERYONE" ? pets[IRandom.Instance.Next(0, pets.Length - 1)] : pet;
            return string.IsNullOrWhiteSpace(petId) ? "pet_test" : petId;
        }
        catch { return "pet_test"; }
    }
}