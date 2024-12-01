using System;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Leery : RoleBase
{
    public static bool On;

    private static OptionItem Radius;
    private static OptionItem Duration;
    private static OptionItem ShowNearestPlayerName;
    private static OptionItem ShowProgress;

    private int Count;

    private byte CurrentTarget;
    private long InvestigationEndTS;
    private byte LeeryId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645750)
            .AutoSetupOption(ref Radius, 1f, new FloatValueRule(0.1f, 10f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref Duration, 15, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ShowNearestPlayerName, true)
            .AutoSetupOption(ref ShowProgress, true, overrideParent: ShowNearestPlayerName);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        LeeryId = playerId;
        CurrentTarget = byte.MaxValue;
        InvestigationEndTS = 0;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || !pc.IsAlive() || Count++ < 5) return;

        Count = 0;

        Vector2 pos = pc.Pos();
        float radius = Radius.GetFloat();
        PlayerControl[] nearbyPlayers = Utils.GetPlayersInRadius(radius, pos).Without(pc).ToArray();

        if (nearbyPlayers.Length == 0)
        {
            CurrentTarget = byte.MaxValue;
            InvestigationEndTS = 0;
            SendRPC();
            return;
        }

        PlayerControl nearestPlayer = nearbyPlayers.MinBy(p => Vector2.Distance(pos, p.Pos()));

        if (nearestPlayer.PlayerId == CurrentTarget && InvestigationEndTS == 0) return;

        if (nearestPlayer.PlayerId != CurrentTarget)
        {
            CurrentTarget = nearestPlayer.PlayerId;
            InvestigationEndTS = Utils.TimeStamp + Duration.GetInt();
            SendRPC();
            return;
        }

        if (Utils.TimeStamp < InvestigationEndTS) return;

        InvestigationEndTS = 0;
        SendRPC();
        if (!nearestPlayer.IsCrewmate()) pc.Notify(Translator.GetString("LeeryNotify"));
    }

    private void SendRPC()
    {
        Utils.SendRPC(CustomRPC.SyncRoleData, LeeryId, CurrentTarget, InvestigationEndTS);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        CurrentTarget = reader.ReadByte();
        InvestigationEndTS = long.Parse(reader.ReadString());
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != LeeryId || seer.PlayerId != target.PlayerId || meeting || hud || !ShowNearestPlayerName.GetBool() || InvestigationEndTS == 0 || !seer.IsAlive()) return string.Empty;

        string text = string.Format(Translator.GetString("LeerySuffix"), CurrentTarget.ColoredPlayerName());
            
        if (ShowProgress.GetBool())
        {
            long now = Utils.TimeStamp;
            float percentage = (float)(InvestigationEndTS - now) / Duration.GetInt();
            text += $" {100 - (int)Math.Round(percentage * 100f)}%";
        }

        return text;
    }
}