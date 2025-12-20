using System.Collections.Generic;
using EHR.Modules;

namespace EHR.Neutral;

public class Pursuer : RoleBase
{
    private const int Id = 10200;
    private static List<byte> PlayerIdList = [];
    private static List<byte> NotActiveList = [];

    public static OptionItem PursuerSkillCooldown;
    public static OptionItem PursuerSkillLimitTimes;

    private List<byte> clientList = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pursuer);

        PursuerSkillCooldown = new FloatOptionItem(Id + 10, "PursuerSkillCooldown", new(0.5f, 60f, 0.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pursuer])
            .SetValueFormat(OptionFormat.Seconds);

        PursuerSkillLimitTimes = new IntegerOptionItem(Id + 11, "PursuerSkillLimitTimes", new(1, 99, 1), 2, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pursuer])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        clientList = [];
        NotActiveList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(PursuerSkillLimitTimes.GetFloat());
        clientList = [];
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
        Main.AllPlayerKillCooldown[id] = CanUseKillButton(Utils.GetPlayerById(id)) ? PursuerSkillCooldown.GetFloat() : 0f;
    }

    private bool CanBeClient(PlayerControl pc)
    {
        return pc != null && pc.IsAlive() && !GameStates.IsMeeting && !clientList.Contains(pc.PlayerId);
    }

    private static bool CanSeel(byte playerId)
    {
        return PlayerIdList.Contains(playerId) && playerId.GetAbilityUseLimit() > 0;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (CanBeClient(target) && CanSeel(killer.PlayerId)) SeelToClient(killer, target);

        return false;
    }

    private void SeelToClient(PlayerControl pc, PlayerControl target)
    {
        if (pc == null || target == null || !pc.Is(CustomRoles.Pursuer)) return;

        pc.RpcRemoveAbilityUse();
        clientList.Add(target.PlayerId);
        NotActiveList.Add(pc.PlayerId);
        pc.SetKillCooldown();
        pc.RPCPlayCustomSound("Clothe");
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
    }

    public static bool OnClientMurder(PlayerControl pc)
    {
        foreach (byte id in PlayerIdList)
        {
            if (!Main.PlayerStates.TryGetValue(id, out PlayerState state)) continue;

            if (state.Role is not Pursuer { IsEnable: true } ps) continue;

            if (!ps.clientList.Contains(pc.PlayerId) || NotActiveList.Contains(pc.PlayerId)) continue;

            // Get rid of this nonsense of killing the player for no reason
            // Just reset their KCD instead
            pc.SetKillCooldown();

            pc.Notify(Translator.GetString("ShotBlank"));
            ps.clientList.Remove(pc.PlayerId);
            NotActiveList.Add(pc.PlayerId);

            return true;
        }

        return false;
    }

    public override void OnReportDeadBody()
    {
        NotActiveList.Clear();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("PursuerButtonText"));
    }

}
