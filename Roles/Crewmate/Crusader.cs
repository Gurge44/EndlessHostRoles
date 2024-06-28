using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Crewmate;

public class Crusader : RoleBase
{
    private const int Id = 20050;
    private static List<byte> playerIdList = [];

    public static List<byte> ForCrusade = [];

    public static OptionItem SkillLimitOpt;
    public static OptionItem SkillCooldown;
    public static OptionItem UsePet;

    public float CurrentKillCooldown;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Crusader);
        SkillCooldown = new FloatOptionItem(Id + 10, "CrusaderSkillCooldown", new(2.5f, 60f, 2.5f), 20f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Crusader])
            .SetValueFormat(OptionFormat.Seconds);
        SkillLimitOpt = new IntegerOptionItem(Id + 11, "CrusaderSkillLimit", new(1, 10, 1), 2, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Crusader])
            .SetValueFormat(OptionFormat.Times);
        UsePet = Options.CreatePetUseSetting(Id + 12, CustomRoles.Crusader);
    }

    public override void Init()
    {
        playerIdList = [];
        CurrentKillCooldown = SkillCooldown.GetFloat();
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(SkillLimitOpt.GetInt());
        CurrentKillCooldown = SkillCooldown.GetFloat();

        if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool CanUseKillButton(PlayerControl pc)
        => !Main.PlayerStates[pc.PlayerId].IsDead
           && (pc.GetAbilityUseLimit() >= 1);

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(Utils.GetPlayerById(id)) ? CurrentKillCooldown : 15f;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;
    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

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
}