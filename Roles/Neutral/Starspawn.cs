using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Starspawn : RoleBase
{
    public static bool On;
    private static List<Starspawn> Instances = [];

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityCooldown;

    public static bool IsDayBreak;
    public bool HasUsedDayBreak;

    public HashSet<byte> IsolatedPlayers = [];

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645700)
            .AutoSetupOption(ref AbilityUseLimit, 2f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
        IsDayBreak = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
        IsolatedPlayers = [];
        HasUsedDayBreak = false;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override void AfterMeetingTasks()
    {
        IsDayBreak = false;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        IsolatedPlayers.Add(target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, target.PlayerId);
        return false;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        IsolatedPlayers.Add(reader.ReadByte());
    }

    public static bool CheckInteraction(PlayerControl killer, PlayerControl target)
    {
        return !killer.Is(Team.Crewmate) || !Instances.Any(x => x.IsolatedPlayers.Contains(target.PlayerId));
    }
}