namespace EHR.Impostor;

public class Ventriloquist : RoleBase
{
    public static bool On;

    private static OptionItem UseLimit;
    private static OptionItem VentriloquistAbilityUseGainWithEachKill;

    public byte Target;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(649650, TabGroup.ImpostorRoles, CustomRoles.Ventriloquist);

        UseLimit = new FloatOptionItem(649652, "AbilityUseLimit", new(0, 20, 0.05f), 0, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Ventriloquist])
            .SetValueFormat(OptionFormat.Times);

        VentriloquistAbilityUseGainWithEachKill = new FloatOptionItem(649653, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 2f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Ventriloquist])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Target = byte.MaxValue;
        playerId.SetAbilityUseLimit(UseLimit.GetFloat());
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        var command = $"/target {target.PlayerId}";
        ChatCommands.TargetCommand(shapeshifter, command, command.Split(' '));
    }
}