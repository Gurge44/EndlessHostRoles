using AmongUs.GameOptions;

namespace EHR.GameMode.HideAndSeekRoles
{
    public class Jet : RoleBase, IHideAndSeekRole
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
        public Team Team => Team.Crewmate;
        public int Chance => CustomRoles.Jet.GetMode();
        public int Count => CustomRoles.Jet.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_301, TabGroup.CrewmateRoles, CustomRoles.Jet, CustomGameMode.HideAndSeek);
            Vision = FloatOptionItem.Create(69_211_303, "JetVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            Speed = FloatOptionItem.Create(69_213_304, "JetSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            DashCooldown = FloatOptionItem.Create(69_213_305, "JetCooldown", new(0f, 60f, 0.5f), 20f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            DashDuration = FloatOptionItem.Create(69_213_306, "JetDuration", new(1f, 30f, 1f), 5f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            DashSpeed = FloatOptionItem.Create(69_213_307, "JetSpeedIncreased", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            UseLimit = IntegerOptionItem.Create(69_213_308, "AbilityUseLimit", new(0, 60, 1), 3, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
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

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            Main.AllPlayerSpeed[playerId] = DashStatus.IsDashing ? DashSpeed.GetFloat() : Speed.GetFloat();
            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
        }

        public override void OnPet(PlayerControl pc)
        {
            if (pc.HasAbilityCD() || DashStatus.IsDashing) return;

            DashStatus.IsDashing = true;
            DashStatus.DashEndTime = Utils.TimeStamp + DashStatus.Duration;
            pc.AddAbilityCD(DashStatus.Cooldown + DashStatus.Duration);
            pc.MarkDirtySettings();
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
    }
}