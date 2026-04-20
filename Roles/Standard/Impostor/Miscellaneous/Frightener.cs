using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Frightener : RoleBase
{
    public static bool On;
    private static List<Frightener> Instances = [];

    private static OptionItem KillOnEmergencyMeetingCall;
    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public override bool IsEnable => On;

    private byte FrightenerId;
    private HashSet<byte> AlarmedPlayers;

    public override void SetupCustomOption()
    {
        StartSetup(658400)
            .AutoSetupOption(ref KillOnEmergencyMeetingCall, true)
            .AutoSetupOption(ref AbilityCooldown, 5f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 15f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 1.5f, new FloatValueRule(0f, 5f, 0.25f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        FrightenerId = playerId;
        AlarmedPlayers = [];
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifting && AlarmedPlayers.Add(target.PlayerId))
        {
            LateTask.New(() =>
            {
                AlarmedPlayers.Remove(target.PlayerId);
                if (ReportDeadBodyPatch.MeetingStarted || GameStates.IsEnded || !GameStates.IsInTask) return;
                Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: shapeshifter);
            }, AbilityDuration.GetFloat(), "Frightener Ability");
            
            Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: shapeshifter);
            Utils.SendRPC(CustomRPC.SyncRoleData, FrightenerId, target.PlayerId);
        }

        return false;
    }

    public static bool OnAnyoneReportDeadBody(PlayerControl reporter, bool emergencyMeeting)
    {
        if (emergencyMeeting && !KillOnEmergencyMeetingCall.GetBool()) return true;

        foreach (Frightener instance in Instances)
        {
            if (instance.AlarmedPlayers.Contains(reporter.PlayerId))
            {
                reporter.Suicide(PlayerState.DeathReason.Frightened, instance.FrightenerId.GetPlayer());
                return false;
            }
        }

        return true;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        byte id = reader.ReadByte();
        AlarmedPlayers.Add(id);
        LateTask.New(() => AlarmedPlayers.Remove(id), AbilityDuration.GetFloat() - 0.5f, "Frightener Ability NHMC");
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != FrightenerId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || AlarmedPlayers.Count == 0) return string.Empty;
        return string.Format(Translator.GetString("Frightener.Suffix"), string.Join(", ", AlarmedPlayers.Select(x => x.ColoredPlayerName())));
    }
}