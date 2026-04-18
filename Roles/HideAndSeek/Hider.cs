using EHR.Gamemodes;

namespace EHR.Roles;

internal class Hider : RoleBase, IHideAndSeekRole
{
    private static bool On;

    public static int StartId = 69_211_199;

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
        var textOpt = new TextOptionItem(StartId, "Hider", TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetHeader(true);

        textOpt.SetValue(1, false, false); // Set Value as 1, becouse TextOptionItem "Hider" is parent option

        Vision = new FloatOptionItem(69_211_101, "HiderVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(52, 94, 235, byte.MaxValue))
            .SetParent(textOpt);

        Speed = new FloatOptionItem(69_211_102, "HiderSpeed", new(0.05f, 5f, 0.05f), 1.4f, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(52, 94, 235, byte.MaxValue))
            .SetParent(textOpt);

        TimeDecreaseOnShortTaskComplete = new IntegerOptionItem(69_211_103, "TimeDecreaseOnShortTaskComplete", new(0, 60, 1), 5, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue))
            .SetParent(textOpt);

        TimeDecreaseOnCommonTaskComplete = new IntegerOptionItem(69_211_104, "TimeDecreaseOnCommonTaskComplete", new(0, 60, 1), 10, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue))
            .SetParent(textOpt);

        TimeDecreaseOnLongTaskComplete = new IntegerOptionItem(69_211_105, "TimeDecreaseOnLongTaskComplete", new(0, 60, 1), 15, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue))
            .SetParent(textOpt);

        TimeDecreaseOnSituationalTaskComplete = new IntegerOptionItem(69_211_106, "TimeDecreaseOnSituationalTaskComplete", new(0, 60, 1), 20, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue))
            .SetParent(textOpt);

        TimeDecreaseOnOtherTaskComplete = new IntegerOptionItem(69_211_107, "TimeDecreaseOnOtherTaskComplete", new(0, 60, 1), 5, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(52, 94, 235, byte.MaxValue))
            .SetParent(textOpt);
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
        if (60 - (CustomHnS.TimeLeft % 60) <= time) Utils.NotifyRoles();
    }
}