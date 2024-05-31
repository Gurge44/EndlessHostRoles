namespace EHR.GameMode.HideAndSeekRoles
{
    public class Taskinator : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem Vision;
        public static OptionItem Speed;
        public static OptionItem CanWinWhenDead;

        public override bool IsEnable => On;
        public Team Team => Team.Neutral;
        public int Chance => CustomRoles.Taskinator.GetMode();
        public int Count => CustomRoles.Taskinator.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_3001, TabGroup.NeutralRoles, CustomRoles.Taskinator, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_3003, "TaskinatorVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(86, 29, 209, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Taskinator]);
            Speed = new FloatOptionItem(69_213_3004, "TaskinatorSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(86, 29, 209, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Taskinator]);
            CanWinWhenDead = new BooleanOptionItem(69_213_3005, "TaskinatorCanWinAfterDeath", true, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(new(86, 29, 209, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Taskinator]);
            Options.OverrideTasksData.Create(69_213_3006, TabGroup.NeutralRoles, CustomRoles.Taskinator);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            if (completedTaskCount + 1 >= totalTaskCount && (pc.IsAlive() || CanWinWhenDead.GetBool()))
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Taskinator);
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                HnSManager.AddFoxesToWinners();
            }
        }
    }
}