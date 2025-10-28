using static EHR.Options;

namespace EHR.AddOns.Impostor;

internal class Madmate : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15800, CustomRoles.Madmate, canSetNum: true, canSetChance: false, allowZeroCount: true);

        MadmateSpawnMode = new StringOptionItem(15810, "MadmateSpawnMode", MadmateSpawnModeStrings, 0, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        MadmateCountMode = new StringOptionItem(15811, "MadmateCountMode", MadmateCountModeStrings, 0, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        SheriffCanBeMadmate = new BooleanOptionItem(15812, "SheriffCanBeMadmate", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        MayorCanBeMadmate = new BooleanOptionItem(15813, "MayorCanBeMadmate", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        NGuesserCanBeMadmate = new BooleanOptionItem(15814, "NGuesserCanBeMadmate", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        MarshallCanBeMadmate = new BooleanOptionItem(15815, "MarshallCanBeMadmate", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        InvestigatorCanBeMadmate = new BooleanOptionItem(15816, "InvestigatorCanBeMadmate", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        PresidentCanBeMadmate = new BooleanOptionItem(15817, "PresidentCanBeMadmate", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        SnitchCanBeMadmate = new BooleanOptionItem(15818, "SnitchCanBeMadmate", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);

        MadSnitchTasks = new IntegerOptionItem(15819, "MadSnitchTasks", new(0, 90, 1), 3, TabGroup.Addons)
            .SetParent(SnitchCanBeMadmate)
            .SetValueFormat(OptionFormat.Pieces);

        JudgeCanBeMadmate = new BooleanOptionItem(15820, "JudgeCanBeMadmate", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Madmate]);
    }
}