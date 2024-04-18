using System.Collections.Generic;
using EHR.Modules;

namespace EHR.Roles.Neutral;

public class Pursuer : RoleBase
{
    private const int Id = 10200;
    private static List<byte> playerIdList = [];
    private static List<byte> notActiveList = [];

    public static OptionItem PursuerSkillCooldown;
    public static OptionItem PursuerSkillLimitTimes;

    private List<byte> clientList = [];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pursuer);
        PursuerSkillCooldown = FloatOptionItem.Create(Id + 10, "PursuerSkillCooldown", new(0.5f, 60f, 0.5f), 20f, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pursuer])
            .SetValueFormat(OptionFormat.Seconds);
        PursuerSkillLimitTimes = IntegerOptionItem.Create(Id + 11, "PursuerSkillLimitTimes", new(1, 99, 1), 2, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pursuer])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        playerIdList = [];
        clientList = [];
        notActiveList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(PursuerSkillLimitTimes.GetInt());
        clientList = [];

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool CanUseKillButton(PlayerControl pc)
        => !Main.PlayerStates[pc.PlayerId].IsDead
           && pc.GetAbilityUseLimit() >= 1;

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(Utils.GetPlayerById(id)) ? PursuerSkillCooldown.GetFloat() : 0f;
    bool CanBeClient(PlayerControl pc) => pc != null && pc.IsAlive() && !GameStates.IsMeeting && !clientList.Contains(pc.PlayerId);
    static bool CanSeel(byte playerId) => playerIdList.Contains(playerId) && playerId.GetAbilityUseLimit() > 0;

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (CanBeClient(target) && CanSeel(killer.PlayerId))
            SeelToClient(killer, target);
        return false;
    }

    void SeelToClient(PlayerControl pc, PlayerControl target)
    {
        if (pc == null || target == null || !pc.Is(CustomRoles.Pursuer)) return;
        pc.RpcRemoveAbilityUse();
        clientList.Add(target.PlayerId);
        notActiveList.Add(pc.PlayerId);
        pc.SetKillCooldown();
        pc.RPCPlayCustomSound("Bet");
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
    }

    public static bool OnClientMurder(PlayerControl pc)
    {
        foreach (var id in playerIdList)
        {
            if (!Main.PlayerStates.ContainsKey(id)) continue;
            if (Main.PlayerStates[id].Role is not Pursuer { IsEnable: true } ps) continue;
            if (!ps.clientList.Contains(pc.PlayerId) || notActiveList.Contains(pc.PlayerId)) continue;

            // Get rid of this nonsense of killing the player for no reason
            // Just reset their KCD instead
            pc.SetKillCooldown();

            pc.Notify(Translator.GetString("ShotBlank"));
            ps.clientList.Remove(pc.PlayerId);
            notActiveList.Add(pc.PlayerId);

            return true;
        }

        return false;
    }

    public override void OnReportDeadBody()
    {
        notActiveList.Clear();
    }
}