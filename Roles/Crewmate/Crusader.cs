using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Crewmate;

public class Crusader : RoleBase
{
    private const int Id = 20050;
    private static List<byte> PlayerIdList = [];

    public static List<byte> ForCrusade = [];

    private static OptionItem SkillLimitOpt;
    private static OptionItem SkillCooldown;
    public static OptionItem UsePet;

    private float CurrentKillCooldown;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Crusader);

        SkillCooldown = new FloatOptionItem(Id + 10, "CrusaderSkillCooldown", new(2.5f, 60f, 0.5f), 30f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Crusader])
            .SetValueFormat(OptionFormat.Seconds);

        SkillLimitOpt = new IntegerOptionItem(Id + 11, "CrusaderSkillLimit", new(1, 10, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Crusader])
            .SetValueFormat(OptionFormat.Times);

        UsePet = Options.CreatePetUseSetting(Id + 12, CustomRoles.Crusader);
    }

    public override void Init()
    {
        PlayerIdList = [];
        CurrentKillCooldown = SkillCooldown.GetFloat();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(SkillLimitOpt.GetFloat());
        CurrentKillCooldown = SkillCooldown.GetFloat();
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return !Main.PlayerStates[pc.PlayerId].IsDead
               && pc.GetAbilityUseLimit() >= 1;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CanUseKillButton(Utils.GetPlayerById(id)) ? CurrentKillCooldown : 15f;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() <= 0) return false;

        ForCrusade.Remove(target.PlayerId);
        ForCrusade.Add(target.PlayerId);
        killer.RpcRemoveAbilityUse();
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        target.RpcGuardAndKill(killer);
        return false;
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = false;
        countsAs = 2;
    }
}