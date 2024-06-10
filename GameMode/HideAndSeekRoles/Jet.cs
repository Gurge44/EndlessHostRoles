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
            Options.SetupRoleOptions(69_211_701, TabGroup.CrewmateRoles, CustomRoles.Jet, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_703, "JetVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            Speed = new FloatOptionItem(69_213_704, "JetSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            DashCooldown = new FloatOptionItem(69_213_705, "JetCooldown", new(0f, 60f, 0.5f), 20f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            DashDuration = new FloatOptionItem(69_213_706, "JetDuration", new(1f, 30f, 1f), 5f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            DashSpeed = new FloatOptionItem(69_213_707, "JetSpeedIncreased", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(66, 245, 75, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jet]);
            UseLimit = new IntegerOptionItem(69_213_708, "AbilityUseLimit", new(0, 60, 1), 3, TabGroup.CrewmateRoles)
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
            Main.AllPlayerSpeed[playerId] = DashStatus.IsDashing ? DashSpeed.GetFloat() : RoleSpeed;
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