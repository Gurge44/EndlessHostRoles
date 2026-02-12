using AmongUs.GameOptions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

internal class Enderman : RoleBase
{
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem Time;
    private static OptionItem ImpostorVision;
    private static int Id => 643200;

    private byte EndermanId = byte.MaxValue;

    public override bool IsEnable => EndermanId != byte.MaxValue;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Enderman);

        KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman]);

        Time = new IntegerOptionItem(Id + 4, "EndermanSecondsBeforeTP", new(1, 60, 1), 7, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman])
            .SetValueFormat(OptionFormat.Seconds);

        ImpostorVision = new BooleanOptionItem(Id + 5, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman]);
    }

    public override void Init()
    {
        
    }

    public override void Add(byte playerId)
    {
        EndermanId = playerId;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return base.CanUseSabotage(pc) || (pc.IsAlive() && !(UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool()));
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());
        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool()) AURoleOptions.PhantomCooldown = Time.GetInt() + 2f;
    }


    public override void OnPet(PlayerControl pc)
    {
        MarkPosition(pc);
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        MarkPosition(pc);
        return pc.Is(CustomRoles.Mischievous);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        MarkPosition(pc);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        MarkPosition(shapeshifter);
        return false;
    }

    private void MarkPosition(PlayerControl pc)
    {
        if (!IsEnable || pc.HasAbilityCD()) return;

        int time = Time.GetInt();
        pc.AddAbilityCD(time + 2);
        Vector2 pos = pc.Pos();
        LateTask.New(() =>
        {
            if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || pc == null || !pc.IsAlive()) return;
            pc.TP(pos);
        }, time);
        pc.Notify(GetString("MarkDone"));
    }
}