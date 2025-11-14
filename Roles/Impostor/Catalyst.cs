using System.Collections.Generic;
using System.Linq;

namespace EHR.Impostor;

public class Catalyst : RoleBase
{
    public static bool On;
    private static List<CustomRoles> Addons = [];

    private static OptionItem GiveAddonsToSelf;
    private static OptionItem GiveOnlyEnabledAddons;
    public static OptionItem RemoveGivenAddonsAfterDeath;

    public override bool IsEnable => On;

    public Dictionary<byte, List<CustomRoles>> GivenAddons;

    public override void SetupCustomOption()
    {
        StartSetup(656700)
            .AutoSetupOption(ref GiveAddonsToSelf, true)
            .AutoSetupOption(ref GiveOnlyEnabledAddons, true)
            .AutoSetupOption(ref RemoveGivenAddonsAfterDeath, false);
    }

    public override void Init()
    {
        On = false;
        Addons = Options.GroupedAddons[AddonTypes.Helpful].ToList();
        if (GiveOnlyEnabledAddons.GetBool()) Addons.RemoveAll(x => !x.IsEnable());
    }

    public override void Add(byte playerId)
    {
        On = true;
        GivenAddons = [];
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.Is(Team.Impostor))
            {
                if (!GiveAddonsToSelf.GetBool() && pc.PlayerId == killer.PlayerId) continue;
                CustomRoles addon = Addons.FindAll(x => !pc.Is(x) && !x.IsNotAssignableMidGame() && CustomRolesHelper.CheckAddonConflict(x, pc)).RandomElement();
                if (addon == default(CustomRoles)) continue;
                pc.RpcSetCustomRole(addon);
                pc.Notify(Translator.GetString("Catalyst.AddonGivenNotify"));

                if (!GivenAddons.TryGetValue(pc.PlayerId, out var given)) GivenAddons[pc.PlayerId] = [addon];
                else given.Add(addon);
            }
        }
    }
}