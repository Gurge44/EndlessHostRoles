namespace TOHE;

public static class PetsPatch
{
    public static void SetPet(PlayerControl player, string petId, bool applyNow = false)
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
}