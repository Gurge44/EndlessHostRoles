using System.Collections.Generic;
using UnityEngine;

namespace TOHE.Roles.Crewmate;

public static class Crusader
{
    private static readonly int Id = 20050;
    private static List<byte> playerIdList = [];

    public static OptionItem SkillLimitOpt;
    public static OptionItem SkillCooldown;
    public static OptionItem UsePet;

    public static Dictionary<byte, float> CurrentKillCooldown = [];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Crusader);
        SkillCooldown = FloatOptionItem.Create(Id + 10, "CrusaderSkillCooldown", new(2.5f, 60f, 2.5f), 20f, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Crusader])
            .SetValueFormat(OptionFormat.Seconds);
        SkillLimitOpt = IntegerOptionItem.Create(Id + 11, "CrusaderSkillLimit", new(1, 10, 1), 2, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Crusader])
            .SetValueFormat(OptionFormat.Times);
        UsePet = Options.CreatePetUseSetting(Id + 12, CustomRoles.Crusader);
    }
    public static void Init()
    {
        playerIdList = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(SkillLimitOpt.GetInt());
        CurrentKillCooldown.Add(playerId, SkillCooldown.GetFloat());

        if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static bool CanUseKillButton(byte playerId)
        => !Main.PlayerStates[playerId].IsDead
        && (playerId.GetAbilityUseLimit() >= 1;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(id) ? CurrentKillCooldown[id] : 0f;
    public static string GetSkillLimit(byte playerId) => Utils.GetAbilityUseLimitDisplay(playerId);
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() <= 0) return false;
        Main.ForCrusade.Remove(target.PlayerId);
        Main.ForCrusade.Add(target.PlayerId);
        killer.RpcRemoveAbilityUse();
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        //killer.RpcGuardAndKill(target);
        target.RpcGuardAndKill(killer);
        return false;
    }
}