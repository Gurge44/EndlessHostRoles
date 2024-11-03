using System.Linq;

namespace EHR.AddOns.Common
{
    public class Sleep : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(644293, CustomRoles.Sleep, canSetNum: true, teamSpawnOptions: true);
        }

        public static void CheckGlowNearby(PlayerControl pc)
        {
            if (!pc.IsAlive() || !GameStates.IsInTask) return;

            Vector2 pos = pc.Pos();

            if (Main.AllAlivePlayerControls.Any(x => x.Is(CustomRoles.Glow) && Vector2.Distance(x.Pos(), pos) <= 1.5f))
            {
                Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Sleep);
                pc.MarkDirtySettings();
            }
        }
    }
}