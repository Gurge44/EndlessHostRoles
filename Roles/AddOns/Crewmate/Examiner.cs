namespace EHR.AddOns.Crewmate;

public class Examiner : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public static OptionItem ExaminerSuspectLimit;
    
    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(654400, CustomRoles.Examiner, canSetNum: true);
        
        ExaminerSuspectLimit = new FloatOptionItem(654410, "DetectiveSuspectLimit", new(1f, 30f, 1f), 4f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Examiner])
            .SetValueFormat(OptionFormat.Players);
    }
}