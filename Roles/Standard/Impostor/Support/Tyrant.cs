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
        StartSetup(659450)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 3f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.25f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        Degraded = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Degraded[playerId] = [];
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

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncRoleData, SendOption.Reliable);
        writer.Write(TyrantId);
        writer.Write(2);
        writer.Write(removeList.Count);
        foreach (byte id in removeList)
        {
            Degraded[TyrantId].Remove(id);
            writer.Write(id);
        }
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Degraded[TyrantId].Add(reader.ReadByte());
                break;
            case 2:
                int length = reader.ReadInt32();
                for (var i = 0; i < length; i++) Degraded[TyrantId].Remove(reader.ReadByte());
                break;
        }
    }

    public static bool IsDegraded(PlayerControl target)
    {
        return Degraded.Values.Any(x => x.Contains(target.PlayerId)) || target.Is(CustomRoles.Degraded);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("TyrantButtonText"));
    }
}
