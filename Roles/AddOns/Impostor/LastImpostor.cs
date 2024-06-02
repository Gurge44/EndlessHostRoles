namespace EHR.Roles.AddOns.Impostor;

public class LastImpostor : IAddon
{
    private const int Id = 15900;
    public static byte currentId = byte.MaxValue;

    public static OptionItem KillCooldown;
    public AddonTypes Type => AddonTypes.ImpOnly;

    public void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.Addons, CustomRoles.LastImpostor);
        KillCooldown = new FloatOptionItem(Id + 15, "SansReduceKillCooldown", new(5f, 95f, 5f), 50f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.LastImpostor])
            .SetValueFormat(OptionFormat.Percent);
    }

    public static void Init() => currentId = byte.MaxValue;
    public static void Add(byte id) => currentId = id;

    public static void SetKillCooldown()
    {
        if (currentId == byte.MaxValue) return;
        if (!Main.AllPlayerKillCooldown.ContainsKey(currentId)) return;
        Main.AllPlayerKillCooldown[currentId] -= Main.AllPlayerKillCooldown[currentId] * (KillCooldown.GetFloat() / 100);
    }

    public static bool CanBeLastImpostor(PlayerControl pc) => pc.IsAlive() && !pc.Is(CustomRoles.LastImpostor) && pc.Is(CustomRoleTypes.Impostor);

    public static void SetSubRole()
    {
        if (currentId != byte.MaxValue || !AmongUsClient.Instance.AmHost) return;
        if (Options.CurrentGameMode != CustomGameMode.Standard || !CustomRoles.LastImpostor.IsEnable() || Main.AliveImpostorCount != 1) return;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (CanBeLastImpostor(pc))
            {
                pc.RpcSetCustomRole(CustomRoles.LastImpostor);
                Add(pc.PlayerId);
                SetKillCooldown();
                pc.SyncSettings();
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                break;
            }
        }
    }
}