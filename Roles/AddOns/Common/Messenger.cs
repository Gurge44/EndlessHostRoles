using System.Collections.Generic;

namespace EHR.AddOns.Common
{
    public class Messenger : IAddon
    {
        public static Dictionary<byte, (int MessageNum, bool Sent)> Messages = [];
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(649394, CustomRoles.Messenger, canSetNum: true, teamSpawnOptions: true);
        }
    }
}