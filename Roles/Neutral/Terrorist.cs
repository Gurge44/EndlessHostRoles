using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Neutral;

internal class Terrorist : RoleBase
{
    public override bool IsEnable => false;
    
    public static OptionItem CanTerroristSuicideWin;
    public static OptionItem TerroristCanGuess;
    public static OptionItem CanVent;
    public static OptionItem VentCooldown;
    public static OptionItem MaxInVentTime;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(11500, TabGroup.NeutralRoles, CustomRoles.Terrorist);

        CanTerroristSuicideWin = new BooleanOptionItem(11510, "CanTerroristSuicideWin", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);

        TerroristCanGuess = new BooleanOptionItem(11511, "CanGuess", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
        
        CanVent = new BooleanOptionItem(11520, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);

        VentCooldown = new IntegerOptionItem(11521, "VentCooldown", new(0, 120, 1), 10, TabGroup.NeutralRoles)
            .SetParent(CanVent)
            .SetValueFormat(OptionFormat.Seconds);
        
        MaxInVentTime = new IntegerOptionItem(11522, "MaxInVentTime", new(0, 120, 1), 5, TabGroup.NeutralRoles)
            .SetParent(CanVent)
            .SetValueFormat(OptionFormat.Seconds);

        OverrideTasksData.Create(11512, TabGroup.NeutralRoles, CustomRoles.Terrorist);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (CanVent.GetBool())
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetInt();
            AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetInt();
        }
    }
}