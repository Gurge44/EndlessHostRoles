using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate;

internal class Mayor : RoleBase
{
    public static bool On;
    public static Dictionary<byte, int> MayorUsedButtonCount = [];

    public static OptionItem MayorAdditionalVote;
    public static OptionItem MayorHasPortableButton;
    public static OptionItem MayorNumOfUseButton;
    public static OptionItem MayorHideVote;
    public static OptionItem MayorRevealWhenDoneTasks;
    public static OptionItem MayorSeesVoteColorsWhenDoneTasks;
    public static OptionItem MayorCanGainVotes;
    public static OptionItem MayorTasksPerVoteGain;
    public static OptionItem MaxMayorTaskVotes;

    public override bool IsEnable => On;

    private float VoteDecimal;
    public int TaskVotes;

    public override void Add(byte playerId)
    {
        On = true;
        MayorUsedButtonCount[playerId] = 0;
    }

    public override void Init()
    {
        On = false;
    }

    public override void Remove(byte playerId)
    {
        MayorUsedButtonCount.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown =
            !MayorUsedButtonCount.TryGetValue(playerId, out int count) || count < MayorNumOfUseButton.GetInt()
                ? opt.GetInt(Int32OptionNames.EmergencyCooldown)
                : 300f;

        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (!MayorHasPortableButton.GetBool()) return;

        if (UsePets.GetBool())
            hud.PetButton.buttonLabelText.text = Translator.GetString("MayorVentButtonText");
        else
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("MayorVentButtonText");
    }

    public override void OnPet(PlayerControl pc)
    {
        Button(pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        pc.MyPhysics?.RpcBootFromVent(vent.Id);
        Button(pc);
    }

    private static void Button(PlayerControl pc)
    {
        if (!MayorHasPortableButton.GetBool()) return;

        if (MayorUsedButtonCount.TryGetValue(pc.PlayerId, out int count) && count < MayorNumOfUseButton.GetInt())
            pc.ReportDeadBody(null);
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (!MayorCanGainVotes.GetBool()) return;
        
        int maxVotes = MaxMayorTaskVotes.GetInt();
        if (TaskVotes >= maxVotes) return;
            
        VoteDecimal += MayorTasksPerVoteGain.GetFloat();

        while (VoteDecimal >= 1)
        {
            VoteDecimal -= 1;
            TaskVotes++;
        }

        if (TaskVotes > maxVotes)
            TaskVotes = maxVotes;
    }

    public override void SetupCustomOption()
    {
        SetupRoleOptions(9500, TabGroup.CrewmateRoles, CustomRoles.Mayor);
        
        MayorAdditionalVote = new IntegerOptionItem(9510, "MayorAdditionalVote", new(0, 90, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor])
            .SetValueFormat(OptionFormat.Votes);

        MayorHasPortableButton = new BooleanOptionItem(9511, "MayorHasPortableButton", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);

        MayorNumOfUseButton = new IntegerOptionItem(9512, "MayorNumOfUseButton", new(1, 90, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(MayorHasPortableButton)
            .SetValueFormat(OptionFormat.Times);

        MayorHideVote = new BooleanOptionItem(9513, "MayorHideVote", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);

        MayorRevealWhenDoneTasks = new BooleanOptionItem(9514, "MayorRevealWhenDoneTasks", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);

        MayorSeesVoteColorsWhenDoneTasks = new BooleanOptionItem(9515, "MayorSeesVoteColorsWhenDoneTasks", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);

        MayorCanGainVotes = new BooleanOptionItem(9520, "MayorCanGainVotes", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);

        MayorTasksPerVoteGain = new FloatOptionItem(9521, "MayorTasksPerVoteGain", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles)
            .SetParent(MayorCanGainVotes);

        MaxMayorTaskVotes = new IntegerOptionItem(id: 9522, "MaxMayorTaskVotes", new(1, 10, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(MayorCanGainVotes);
        
        OverrideTasksData.Create(9516, TabGroup.CrewmateRoles, CustomRoles.Mayor);
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = false;
        countsAs = 1 + MayorAdditionalVote.GetInt() + TaskVotes;
    }
}