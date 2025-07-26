namespace EHR.AddOns.Common;

public class BananaMan : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(652700, CustomRoles.BananaMan, canSetNum: true, teamSpawnOptions: true);
    }

    public static NetworkedPlayerInfo.PlayerOutfit GetOutfit(string name)
    {
        return new NetworkedPlayerInfo.PlayerOutfit().Set(name, 14, "hat_pk04_Banana", "", "", "", "nameplate_shh");
    }
}