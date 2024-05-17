namespace EHR.Roles.Neutral;

// 来源：https://github.com/Yumenopai/TownOfHost_Y
public class Imitator : ISettingHolder
{
    private const int Id = 11950;

    public static OptionItem OddKillCooldown;
    public static OptionItem EvenKillCooldown;
    public static OptionItem AfterMeetingKillCooldown;
    public static OptionItem CanVent;
    public static OptionItem HasImpostorVision;

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Imitator);
        OddKillCooldown = FloatOptionItem.Create(Id + 10, "OddKillCooldown", new(0f, 60f, 0.5f), 27.5f, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator])
            .SetValueFormat(OptionFormat.Seconds);
        EvenKillCooldown = FloatOptionItem.Create(Id + 11, "EvenKillCooldown", new(0f, 30f, 0.5f), 15f, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator])
            .SetValueFormat(OptionFormat.Seconds);
        AfterMeetingKillCooldown = FloatOptionItem.Create(Id + 12, "AfterMeetingKillCooldown", new(0f, 30f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 13, "CanVent", true, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 14, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator]);
    }
}