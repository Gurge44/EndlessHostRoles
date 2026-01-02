using System.Collections.Generic;
namespace EHR.Roles;

public class Messenger : IAddon
{
    public static HashSet<byte> Sent = [];
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(649392, CustomRoles.Messenger, canSetNum: true, teamSpawnOptions: true);
    }
}