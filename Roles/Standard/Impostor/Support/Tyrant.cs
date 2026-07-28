using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Tyrant : RoleBase
{
    public static bool On;
    public static Dictionary<byte, HashSet<byte>> Degraded = [];
    private List<byte> ScheduledAssigns = [];
    private byte TyrantId;

    private static OptionItem KillCooldown;
    private static OptionItem AbilityUseLimit;
    public static OptionItem AbilityUseGainWithEachKill;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(659400)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 3f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.25f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        Degraded.Clear();
    }

    public override void Add(byte playerId)
    {
        On = true;
        Degraded[playerId] = new();
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
        TyrantId = playerId;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1f || IsDegraded(target)) return true;

        return killer.CheckDoubleTrigger(target, () =>
        {
            ScheduledAssigns.Add(target.PlayerId);
            Degraded[killer.PlayerId].Add(target.PlayerId);
            killer.RpcRemoveAbilityUse();
            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, target.PlayerId);
        });
    }

    public override void OnReportDeadBody()
    {
        var tyrant = TyrantId.GetPlayer();
        if (!tyrant || !tyrant.IsAlive()) return;
        foreach (byte id in ScheduledAssigns)
        {
            var pc = id.GetPlayer();
            if (!pc) continue;

            pc.RpcSetCustomRole(CustomRoles.Degraded);
        }

        ScheduledAssigns.Clear();
    }

    // this method is used in case target's Degraded addon was removed, e.g. Cleansed
    public override void AfterMeetingTasks()
    {
        List<byte> removeList = [];

        foreach (byte id in Degraded[TyrantId])
        {
            var pc = id.GetPlayer();

            if (!pc || !pc.IsAlive())
                continue;

            if (!pc.Is(CustomRoles.Degraded))
                removeList.Add(id);
        }

        foreach (byte id in removeList)
        {
            Degraded[TyrantId].Remove(id);
            Utils.SendRPC(CustomRPC.SyncRoleData, TyrantId, 2, id);
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        if (!Degraded.TryGetValue(TyrantId, out var degraded))
            Degraded[TyrantId] = degraded = [];

        switch (reader.ReadPackedInt32())
        {
            case 1:
                degraded.Add(reader.ReadByte());
                break;
            case 2:
                degraded.Remove(reader.ReadByte());
                break;
        }
    }

    public bool IsDegraded(PlayerControl target)
    {
        if (target.PlayerId == TyrantId) return false;
        return Degraded.Values.Any(x => x.Contains(target.PlayerId)) || target.Is(CustomRoles.Degraded);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("TyrantButtonText"));
    }
}
