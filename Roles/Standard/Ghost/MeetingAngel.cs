using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Roles;

public class MeetingAngel : IGhostRole
{
    private static OptionItem NumVotesNegated;
    private static OptionItem NumMeetingsCooldown;
    private static readonly Dictionary<TeamOptions, OptionItem> CanProtectOptions = [];
    
    public Team Team => Team.Crewmate;
    public RoleTypes RoleTypes => RoleTypes.GuardianAngel;
    public int Cooldown => 5;

    private int LastUsedAtMeetingNum = int.MinValue;
    public byte TargetId = 252;
    
    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        if (LastUsedAtMeetingNum + NumMeetingsCooldown.GetInt() > MeetingStates.MeetingNum) return;

        TeamOptions targetTeam = target.GetTeam() switch
        {
            Team.Crewmate => TeamOptions.Crewmate,
            Team.Impostor => TeamOptions.Impostor,
            Team.Coven => TeamOptions.Coven,
            Team.Neutral => target.GetCustomRole().GetNeutralRoleCategory() switch
            {
                RoleOptionType.Neutral_Killing => TeamOptions.NeutralKilling,
                RoleOptionType.Neutral_Evil => TeamOptions.NeutralEvil,
                RoleOptionType.Neutral_Pariah => TeamOptions.NeutralPariah,
                RoleOptionType.Neutral_Benign => TeamOptions.NeutralBenign,
                _ => default(TeamOptions)
            },
            _ => default(TeamOptions)
        };
        if (CanProtectOptions.TryGetValue(targetTeam, out OptionItem optionItem) && !optionItem.GetBool()) return;
        
        LastUsedAtMeetingNum = MeetingStates.MeetingNum;
        TargetId = target.PlayerId;
    }
    
    public void OnAssign(PlayerControl pc)
    {
        LastUsedAtMeetingNum = int.MinValue;
        TargetId = 252;
    }
    
    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(658600, TabGroup.OtherRoles, CustomRoles.MeetingAngel);
        
        NumVotesNegated = new IntegerOptionItem(658602, "MeetingAngel.NumVotesNegated", new(1, 30, 1), 2, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.MeetingAngel])
            .SetValueFormat(OptionFormat.Votes);

        NumMeetingsCooldown = new IntegerOptionItem(658603, "MeetingAngel.NumMeetingsCooldown", new(1, 30, 1), 2, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.MeetingAngel]);
        
        Enum.GetValues<TeamOptions>()[1..].Do(x => CanProtectOptions[x] = new BooleanOptionItem(658603 + (int)x, $"MeetingAngel.CanProtectOptions.{x}", x == TeamOptions.NeutralBenign, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.MeetingAngel]));
    }

    public static void NegateVotes(Dictionary<byte, int> votingData, MeetingHud.VoterState[] states)
    {
        foreach ((CustomRoles _, IGhostRole instance) in GhostRolesManager.AssignedGhostRoles.Values)
        {
            if (instance is not MeetingAngel { TargetId: < 252 } meetingAngel) continue;
            
            int negateNum = NumVotesNegated.GetInt();
            votingData[meetingAngel.TargetId] -= negateNum;
                
            int negated = 0;
                
            for (var index = 0; index < states.Length && negated < negateNum; index++)
            {
                ref MeetingHud.VoterState state = ref states[index];

                if (state.VotedForId == meetingAngel.TargetId)
                {
                    state.VotedForId = 254;
                    negated++;
                }
            }

            Utils.SendMessage(string.Format(Translator.GetString("MeetingAngel.NegatedMsg"), negated, meetingAngel.TargetId.ColoredPlayerName()), title: CustomRoles.MeetingAngel.ToColoredString());
            meetingAngel.TargetId = 252;
        }
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    enum TeamOptions
    {
        Crewmate,
        Impostor,
        NeutralKilling,
        NeutralEvil,
        NeutralPariah,
        NeutralBenign,
        Coven
    }
}