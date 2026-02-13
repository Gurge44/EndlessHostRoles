using System.Linq;
using Hazel;

namespace EHR.Roles;

public class LastImpostor : IAddon
{
    private const int Id = 15900;
    public static byte CurrentId = byte.MaxValue;

    private static OptionItem Reduction;
    public AddonTypes Type => AddonTypes.ImpOnly;

    public void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.Addons, CustomRoles.LastImpostor, zeroOne: true);

        Reduction = new FloatOptionItem(Id + 15, "ArroganceReduceKillCooldown", new(5f, 95f, 5f), 20f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.LastImpostor])
            .SetValueFormat(OptionFormat.Percent);
    }

    public static void Init()
    {
        CurrentId = byte.MaxValue;
    }

    private static void Add(byte id)
    {
        CurrentId = id;
    }

    public static void SetKillCooldown()
    {
        if (CurrentId == byte.MaxValue) return;

        if (!Main.AllPlayerKillCooldown.TryGetValue(CurrentId, out float cd)) return;

        float minus = cd * (Reduction.GetFloat() / 100f);
        Main.AllPlayerKillCooldown[CurrentId] -= minus;
        Logger.Info($"{CurrentId.ColoredPlayerName().RemoveHtmlTags()}'s cooldown is {Main.AllPlayerKillCooldown[CurrentId]}s", "LastImpostor");
    }

    private static bool CanBeLastImpostor(PlayerControl pc)
    {
        return pc.IsAlive() && !pc.Is(CustomRoles.LastImpostor) && pc.Is(CustomRoleTypes.Impostor);
    }

    public static void SetSubRole()
    {
        if (CurrentId != byte.MaxValue || !AmongUsClient.Instance.AmHost) return;

        if (Options.CurrentGameMode != CustomGameMode.Standard || !CustomRoles.LastImpostor.IsEnable() || Main.EnumerateAlivePlayerControls().Count(pc => pc.Is(CustomRoleTypes.Impostor)) != 1) return;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (CanBeLastImpostor(pc))
            {
                pc.RpcSetCustomRole(CustomRoles.LastImpostor);
                Add(pc.PlayerId);
                SetKillCooldown();

                var sender = CustomRpcSender.Create("LastImpostor", SendOption.Reliable);
                var hasValue = false;
                hasValue |= sender.SyncSettings(pc);
                hasValue |= sender.NotifyRolesSpecific(pc, pc, out sender, out bool cleared);
                if (cleared) hasValue = false;

                if (Main.KillTimers.TryGetValue(pc.PlayerId, out float timer) &&
                    Main.AllPlayerKillCooldown.TryGetValue(pc.PlayerId, out float cd) &&
                    timer > cd)
                    hasValue |= sender.SetKillCooldown(pc);

                sender.SendMessage(!hasValue);

                break;
            }
        }
    }
}