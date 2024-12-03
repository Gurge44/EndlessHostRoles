using AmongUs.GameOptions;

namespace EHR.Neutral;

public class Vortex : RoleBase
{
    private const int Id = 645650;
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem IfTargetCannotBeTeleported;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Vortex);

        KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vortex])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vortex]);

        HasImpostorVision = new BooleanOptionItem(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vortex]);

        IfTargetCannotBeTeleported = new StringOptionItem(Id + 5, "Vortex.IfTargetCannotBeTeleported", ["Vortex.NoTPMode.Block", "Vortex.NoTPMode.DoWithoutTP"], 0, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vortex]);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!killer.RpcCheckAndMurder(target, true)) return false;

        if (!target.TPToRandomVent())
        {
            killer.Notify(Translator.GetString("TargetCannotBeTeleported"));
            return IfTargetCannotBeTeleported.GetValue() == 1;
        }

        LateTask.New(() => target.Suicide(PlayerState.DeathReason.Kill, killer), 0.2f, log: false);
        killer.SetKillCooldown();
        return false;
    }
}