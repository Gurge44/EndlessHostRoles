namespace EHR.GameMode.HideAndSeekRoles
{
    public class Agent : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem Vision;
        public static OptionItem Speed;

        public override bool IsEnable => On;
        public Team Team => Team.Impostor;
        public int Chance => CustomRoles.Agent.GetMode();
        public int Count => CustomRoles.Agent.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_2001, TabGroup.ImpostorRoles, CustomRoles.Agent, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_2003, "AgentVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(255, 143, 143, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Agent]);
            Speed = new FloatOptionItem(69_213_2004, "AgentSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(255, 143, 143, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Agent]);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }
    }
}