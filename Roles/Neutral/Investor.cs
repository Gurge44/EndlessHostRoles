using System;
using System.Collections.Generic;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Investor : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityCooldown;
    private static OptionItem PercentNeed;

    public HashSet<byte> MarkedPlayers = [];

    public bool IsWon
    {
        get
        {
            int marked = MarkedPlayers.Count;
            MarkedPlayers.IntersectWith(CustomWinnerHolder.WinnerIds);
            int won = MarkedPlayers.Count;
            int percentNeeded = PercentNeed.GetInt();
            return marked > 0 && (int)Math.Round(won * 100f / marked) >= percentNeeded;
        }
    }

    public override void SetupCustomOption()
    {
        StartSetup(652600)
            .AutoSetupOption(ref AbilityUseLimit, 5, new IntegerValueRule(1, 15, 1), OptionFormat.Players)
            .AutoSetupOption(ref AbilityCooldown, 15, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref PercentNeed, 50, new IntegerValueRule(5, 100, 5), OptionFormat.Percent);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        MarkedPlayers = [];
        playerId.SetAbilityUseLimit(playerId);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() > 0f;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetInt();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (MarkedPlayers.Add(target.PlayerId))
        {
            killer.RpcRemoveAbilityUse();
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, target.PlayerId);
            killer.SetKillCooldown(AbilityCooldown.GetInt());
        }

        return false;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        MarkedPlayers.Add(reader.ReadByte());
    }
}