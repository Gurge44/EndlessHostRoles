using EHR.Gamemodes;

namespace EHR.Roles;

internal class Hider : RoleBase, IHideAndSeekRole
{
    private static bool On;

    public const int StartId = 69_211_101;

    private static OptionItem Vision;
    private static OptionItem Speed;
    private static OptionItem TimeDecreaseOnShortTaskComplete;
    private static OptionItem TimeDecreaseOnCommonTaskComplete;
    private static OptionItem TimeDecreaseOnLongTaskComplete;
    private static OptionItem TimeDecreaseOnSituationalTaskComplete;
    private static OptionItem TimeDecreaseOnOtherTaskComplete;

    public override bool IsEnable => On;
    public Team Team => Team.Crewmate;
    public int Chance => 100;
    public int Count => PlayerControl.AllPlayerControls.Count;
    public float RoleSpeed => Speed.GetFloat();
    public float RoleVision => Vision.GetFloat();

    public override void SetupCustomOption()
    {
        Vision = new FloatOptionItem(StartId, "HiderVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(52, 94, 235, byte.MaxValue));

        Speed = new FloatOptionItem(StartId + 1, "HiderSpeed", new(0.05f, 5f, 0.05f), 1.4f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(52, 94, 235, byte.MaxValue));

        TimeDecreaseOnShortTaskComplete = new IntegerOptionItem(StartId + 2, "TimeDecreaseOnShortTaskComplete", new(0, 60, 1), 5, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue));

        TimeDecreaseOnCommonTaskComplete = new IntegerOptionItem(StartId + 3, "TimeDecreaseOnCommonTaskComplete", new(0, 60, 1), 10, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue));

        TimeDecreaseOnLongTaskComplete = new IntegerOptionItem(StartId + 4, "TimeDecreaseOnLongTaskComplete", new(0, 60, 1), 15, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue));

        TimeDecreaseOnSituationalTaskComplete = new IntegerOptionItem(StartId + 5, "TimeDecreaseOnSituationalTaskComplete", new(0, 60, 1), 20, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue));

        TimeDecreaseOnOtherTaskComplete = new IntegerOptionItem(StartId + 6, "TimeDecreaseOnOtherTaskComplete", new(0, 60, 1), 5, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue));
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public static void OnSpecificTaskComplete(PlayerControl pc, PlayerTask task)
    {
        int time = !AddTasksFromListPatch.DisableTasksSettings.TryGetValue(task.TaskType, out OptionItem setting)
            ? TimeDecreaseOnOtherTaskComplete.GetInt()
            : setting.Parent.Name switch
            {
                "DisableShortTasks" => TimeDecreaseOnShortTaskComplete.GetInt(),
                "DisableCommonTasks" => TimeDecreaseOnCommonTaskComplete.GetInt(),
                "DisableLongTasks" => TimeDecreaseOnLongTaskComplete.GetInt(),
                "DisableOtherTasks" => TimeDecreaseOnSituationalTaskComplete.GetInt(),
                _ => TimeDecreaseOnOtherTaskComplete.GetInt()
            };

        CustomHnS.TimeLeft -= time;
        pc.Notify(string.Format(Translator.GetString("TimeDecreased"), time));
        if (60 - CustomHnS.TimeLeft % 60 <= time) Utils.NotifyRoles();
    }
}