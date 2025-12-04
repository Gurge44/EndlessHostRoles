using System.Collections.Generic;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Poache : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem KillDelay;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    public static HashSet<byte> PoachedPlayers = [];
    private static List<(byte ID, long KillTimeStamp)> KillDelays = [];

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

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!HasNecronomicon)
        {
            PoachedPlayers.Add(target.PlayerId);
            Utils.SendRPC(CustomRPC.SyncRoleData, PoacheId, 1, target.PlayerId);
        }
        else KillDelays.Add((target.PlayerId, Utils.TimeStamp + KillDelay.GetInt()));

        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (lowLoad || GameStates.IsMeeting || ExileController.Instance || !Main.IntroDestroyed || !pc.IsAlive() || !KillDelays.FindFirst(x => x.ID == pc.PlayerId, out (byte ID, long KillTimeStamp) killData) || Utils.TimeStamp < killData.KillTimeStamp) return;

        pc.Suicide(PlayerState.DeathReason.Poison, realKiller: PoacheId.GetPlayer());
        KillDelays.Remove(killData);
    }

    public override void AfterMeetingTasks()
    {
        PoachedPlayers.Clear();
        Utils.SendRPC(CustomRPC.SyncRoleData, PoacheId, 2);
    }

    public override void OnReportDeadBody()
    {
        KillDelays.ForEach(x => x.ID.GetPlayer()?.Suicide(PlayerState.DeathReason.Poison, realKiller: PoacheId.GetPlayer()));
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
