using System.Collections.Generic;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Poache : CovenBase
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem KillDelay;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    public static HashSet<byte> PoachedPlayers = [];
    private static List<byte> KillDelays = [];

    private byte PoacheId;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650030)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillDelay, 3, new IntegerValueRule(1, 30, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, false);
    }

    public override void Init()
    {
        On = false;
        PoachedPlayers = [];
        KillDelays = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        PoacheId = playerId;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!HasNecronomicon)
        {
            PoachedPlayers.Add(target.PlayerId);
            Utils.SendRPC(CustomRPC.SyncRoleData, PoacheId, 1, target.PlayerId);
        }
        else
        {
            KillDelays.Add(target.PlayerId);
            _ = new CountdownTimer(KillDelay.GetInt(), () =>
            {
                if (!KillDelays.Remove(target.PlayerId)) return;
                target.Suicide(PlayerState.DeathReason.Poison, realKiller: killer);
            }, onCanceled: () => KillDelays.Remove(target.PlayerId));
        }

        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    public override void AfterMeetingTasks()
    {
        PoachedPlayers.Clear();
        Utils.SendRPC(CustomRPC.SyncRoleData, PoacheId, 2);
    }

    public override void OnReportDeadBody()
    {
        KillDelays.ForEach(x => x.GetPlayer()?.Suicide(PlayerState.DeathReason.Poison, realKiller: PoacheId.GetPlayer()));
        KillDelays.Clear();
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                PoachedPlayers.Add(reader.ReadByte());
                break;
            case 2:
                PoachedPlayers.Clear();
                break;
        }
    }
}
