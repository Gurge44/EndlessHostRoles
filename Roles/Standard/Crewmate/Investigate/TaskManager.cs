namespace EHR.Roles;

internal class TaskManager : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(5575, TabGroup.CrewmateRoles, CustomRoles.TaskManager);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public override void GetProgressText(byte playerId, bool comms, StringBuilder resultText)
    {
        resultText.Append(Utils.GetTaskCount(playerId, comms))
            .Append(" <color=#777777>-</color> <color=#00ffa5>");

        if (comms) resultText.Append('?');
        else resultText.Append(GameData.Instance.CompletedTasks);

        resultText.Append("</color><color=#ffffff>/")
            .Append(GameData.Instance.TotalTasks)
            .Append("</color>");
    }
}