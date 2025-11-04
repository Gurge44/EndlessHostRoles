using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Neutral;

public class Backstabber : RoleBase
{
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    public static OptionItem RevealAfterKilling;

    private WinningTeam Team;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 649150;
        Options.SetupRoleOptions(id++, TabGroup.NeutralRoles, CustomRoles.Backstabber);

        KillCooldown = new FloatOptionItem(++id, "KillCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Backstabber])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(++id, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Backstabber]);

        HasImpostorVision = new BooleanOptionItem(++id, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Backstabber]);

        RevealAfterKilling = new BooleanOptionItem(++id, "Backstabber.RevealAfterKilling", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Backstabber]);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Team = WinningTeam.Crew;
        playerId.SetAbilityUseLimit(1);
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

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() > 0f;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return;

        Team targetTeam = target.GetTeam();

        Team = targetTeam switch
        {
            EHR.Team.Impostor => WinningTeam.NK,
            _ => WinningTeam.Imp
        };

        if (targetTeam == EHR.Team.Crewmate && killer.AmOwner)
            Achievements.Type.StabbingTheBack.Complete();

        killer.RpcRemoveAbilityUse();
        killer.Notify(string.Format(Translator.GetString("Backstabber.MurderNotify"), Utils.ColorString(targetTeam.GetColor(), Translator.GetString(targetTeam.ToString())), Translator.GetString($"BackstabberTeam.{Team}")), 10f);
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;
        return target.Is(CustomRoles.Backstabber) && RevealAfterKilling.GetBool() && target.GetAbilityUseLimit() == 0f;
    }

    public bool CheckWin()
    {
        return CustomWinnerHolder.WinnerTeam switch
        {
            CustomWinner.Crewmate => Team == WinningTeam.Crew,
            CustomWinner.Impostor => Team == WinningTeam.Imp,
            CustomWinner.Neutrals => Team == WinningTeam.NK,
            CustomWinner.None => false,
            CustomWinner.Default or CustomWinner.Draw or CustomWinner.Error => true,
            _ => Team == WinningTeam.NK
        };
    }

    private enum WinningTeam
    {
        Crew,
        Imp,
        NK
    }
}