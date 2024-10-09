namespace EHR;

public static class PetsPatch
{
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