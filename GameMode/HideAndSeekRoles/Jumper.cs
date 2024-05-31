using AmongUs.GameOptions;

namespace EHR.GameMode.HideAndSeekRoles
{
    public class Jumper : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem VentCooldown;
        public static OptionItem MaxInVentTime;
        public static OptionItem UseLimit;
        public static OptionItem Vision;
        public static OptionItem Speed;

        public override bool IsEnable => On;
        public Team Team => Team.Crewmate;
        public int Chance => CustomRoles.Jumper.GetMode();
        public int Count => CustomRoles.Jumper.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_501, TabGroup.CrewmateRoles, CustomRoles.Jumper, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_503, "JumperVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(221, 245, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jumper]);
            Speed = new FloatOptionItem(69_213_504, "JumperSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(221, 245, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jumper]);
            VentCooldown = new FloatOptionItem(69_213_505, "VentCooldown", new(0f, 60f, 0.5f), 20f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(221, 245, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jumper]);
            MaxInVentTime = new FloatOptionItem(69_213_506, "MaxInVentTime", new(0f, 60f, 0.5f), 3f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(221, 245, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jumper]);
            UseLimit = new IntegerOptionItem(69_213_507, "AbilityUseLimit", new(0, 60, 1), 3, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(new(221, 245, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jumper]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(UseLimit.GetFloat());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            if (physics.myPlayer.GetAbilityUseLimit() < 1f)
            {
                LateTask.New(() => { physics.RpcBootFromVent(ventId); }, 0.5f, "Jumper no uses boot from vent");
            }
            else physics.myPlayer.RpcRemoveAbilityUse();
        }
    }
}