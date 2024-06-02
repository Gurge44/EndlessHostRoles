using AmongUs.GameOptions;

namespace EHR.GameMode.HideAndSeekRoles
{
    public class Dasher : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem DashCooldown;
        public static OptionItem DashDuration;
        public static OptionItem DashSpeed;
        public static OptionItem UseLimit;
        public static OptionItem Vision;
        public static OptionItem Speed;

        private DashStatus DashStatus;

        public override bool IsEnable => On;
        public Team Team => Team.Impostor;
        public int Chance => CustomRoles.Dasher.GetMode();
        public int Count => CustomRoles.Dasher.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_801, TabGroup.ImpostorRoles, CustomRoles.Dasher, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_803, "DasherVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(245, 66, 176, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dasher]);
            Speed = new FloatOptionItem(69_213_804, "DasherSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(245, 66, 176, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dasher]);
            DashCooldown = new FloatOptionItem(69_213_805, "DasherCooldown", new(0f, 60f, 0.5f), 20f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(245, 66, 176, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dasher]);
            DashDuration = new FloatOptionItem(69_213_806, "DasherDuration", new(1f, 30f, 1f), 5f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(245, 66, 176, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dasher]);
            DashSpeed = new FloatOptionItem(69_213_807, "DasherSpeedIncreased", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(245, 66, 176, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dasher]);
            UseLimit = new IntegerOptionItem(69_213_808, "AbilityUseLimit", new(0, 60, 1), 3, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(new(245, 66, 176, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dasher]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(UseLimit.GetFloat());
            DashStatus = new()
            {
                Cooldown = DashCooldown.GetInt(),
                Duration = DashDuration.GetInt()
            };
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            Main.AllPlayerSpeed[playerId] = DashStatus.IsDashing ? DashSpeed.GetFloat() : HnSManager.IsBlindTime ? Main.MinSpeed : RoleSpeed;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!DashStatus.IsDashing) return;

            long now = Utils.TimeStamp;
            if (DashStatus.DashEndTime <= now)
            {
                DashStatus.IsDashing = false;
                pc.MarkDirtySettings();
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            if (pc.HasAbilityCD() || DashStatus.IsDashing || pc.GetAbilityUseLimit() < 1f) return;

            DashStatus.IsDashing = true;
            DashStatus.DashEndTime = Utils.TimeStamp + DashStatus.Duration;
            pc.AddAbilityCD(DashStatus.Cooldown + DashStatus.Duration);
            pc.MarkDirtySettings();
            pc.RpcRemoveAbilityUse();
        }
    }
}