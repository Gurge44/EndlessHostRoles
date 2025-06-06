namespace EHR.GameMode.HideAndSeekRoles;

public class Venter : RoleBase, IHideAndSeekRole
{
    public static bool On;

    public static OptionItem UseLimit;
    public static OptionItem Vision;
    public static OptionItem Speed;

    public override bool IsEnable => On;
    public Team Team => Team.Impostor;
    public int Chance => CustomRoles.Venter.GetMode();
    public int Count => CustomRoles.Venter.GetCount();
    public float RoleSpeed => Speed.GetFloat();
    public float RoleVision => Vision.GetFloat();

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(69_211_1001, TabGroup.ImpostorRoles, CustomRoles.Venter, CustomGameMode.HideAndSeek);

        Vision = new FloatOptionItem(69_211_1003, "VenterVision", new(0.05f, 5f, 0.05f), 0.25f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(105, 65, 65, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Venter]);

        Speed = new FloatOptionItem(69_213_1004, "VenterSpeed", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(105, 65, 65, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Venter]);

        UseLimit = new FloatOptionItem(69_213_1007, "AbilityUseLimit", new(0, 20, 0.05f), 3, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(105, 65, 65, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Venter]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(UseLimit.GetFloat());
    }

    public override void Init()
    {
        On = false;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (pc.GetAbilityUseLimit() < 1f)
            pc.MyPhysics?.RpcBootFromVent(vent.Id);
        else
            pc.RpcRemoveAbilityUse();
    }
}