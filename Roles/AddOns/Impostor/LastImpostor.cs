namespace EHR.AddOns.Impostor
{
    public class LastImpostor : IAddon
    {
        private const int Id = 15900;
        public static byte CurrentId = byte.MaxValue;

        private static OptionItem Reduction;
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(Id, TabGroup.Addons, CustomRoles.LastImpostor);

            Reduction = new FloatOptionItem(Id + 15, "SansReduceKillCooldown", new(5f, 95f, 5f), 20f, TabGroup.Addons)
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

                    if (Main.KillTimers.TryGetValue(pc.PlayerId, out float timer) &&
                        Main.AllPlayerKillCooldown.TryGetValue(pc.PlayerId, out float cd) &&
                        timer > cd)
                        pc.SetKillCooldown();

                    break;
                }
            }
        }
    }
}