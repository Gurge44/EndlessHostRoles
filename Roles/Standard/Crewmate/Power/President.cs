using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Patches;

namespace EHR.Roles;

internal static class DecreeExtension
{
    public static bool IsEnabled(this President.Decree decree)
    {
        return President.DecreeSettings[decree][0].GetBool();
    }
}

public class President : RoleBase
{
    public enum Decree
    {
        Reveal,
        Finish,
        Declassification,
        GovernmentSupport,
        Investigation,
        GovernmentRecruiting
    }

    public static bool On;
    private static List<President> Instances = [];
    internal static readonly Dictionary<Decree, List<OptionItem>> DecreeSettings = [];

    private static readonly CustomRoles[] GovernmentRecruitRoles =
    [
        CustomRoles.Sheriff,
        CustomRoles.Bodyguard,
        CustomRoles.Inspector,
        CustomRoles.Jailor,
        CustomRoles.SecurityGuard,
        CustomRoles.NiceGuesser,
        CustomRoles.Inquirer,
        CustomRoles.Investigator
    ];

    private bool IsDeclassification;
    private bool IsRecruiting;

    private byte PresidentId;
    private bool Used;
    private HashSet<Decree> UsedDecrees;

    public override bool IsEnable => On;

    public bool IsRevealed => UsedDecrees.Contains(Decree.Reveal);

    public override void SetupCustomOption()
    {
        var id = 647850;
        const TabGroup tab = TabGroup.CrewmateRoles;
        const CustomRoles role = CustomRoles.President;
        Options.SetupSingleRoleOptions(id++, tab, role, hideMaxSetting: true);

        foreach (Decree decree in Enum.GetValues<Decree>())
        {
            DecreeSettings[decree] =
            [
                new BooleanOptionItem(++id, $"President.Decree.{decree}", true, tab)
                    .SetParent(Options.CustomRoleSpawnChances[role])
            ];

            switch (decree)
            {
                case Decree.Reveal:
                    DecreeSettings[decree].AddRange(
                    [
                        new BooleanOptionItem(++id, "President.Reveal.CanBeConvertedAfterRevealing", false, tab)
                            .SetParent(DecreeSettings[decree][0]),
                        new BooleanOptionItem(++id, "President.Reveal.ConvertedPresidentCanReveal", false, tab)
                            .SetParent(DecreeSettings[decree][0])
                    ]);

                    break;
                case Decree.GovernmentRecruiting:
                    DecreeSettings[decree].Add(new StringOptionItem(++id, "President.GovernmentRecruiting.RecruitedRole", GovernmentRecruitRoles.Select(x => x.ToColoredString()).ToArray(), 0, tab, noTranslation: true)
                        .SetParent(DecreeSettings[decree][0]));

                    break;
            }
        }
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
        PresidentId = playerId;
        UsedDecrees = [];
        IsDeclassification = false;
        IsRecruiting = false;
        Used = false;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (IsRevealed)
        {
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision / 2f);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultCrewmateVision / 2f);
        }
    }

    public static void OnAnyoneApplyGameOptions(IGameOptions opt)
    {
        bool value = Main.RealOptionsData.GetBool(BoolOptionNames.AnonymousVotes);
        
        if (!GameStates.IsMeeting)
        {
            opt.SetBool(BoolOptionNames.AnonymousVotes, value);
            return;
        }

        if (Instances.Any(x => x.IsDeclassification)) value = false;
        opt.SetBool(BoolOptionNames.AnonymousVotes, value);
    }

    public static void UseDecree(PlayerControl pc, string message)
    {
        President president = Instances.FirstOrDefault(x => x.PresidentId == pc.PlayerId);

        if (president == null || president.Used)
        {
            Utils.SendMessage(Translator.GetString("President.DecreeAlreadyChosenThisMeetingMessage"), pc.PlayerId);
            return;
        }

        if (!Utils.GetPlayerById(president.PresidentId).IsAlive()) return;

        if (!int.TryParse(message, out int num) || num > 5)
        {
            Utils.SendMessage(GetHelpMessage(), pc.PlayerId);
            return;
        }

        var decree = (Decree)num;

        if (!decree.IsEnabled() || president.UsedDecrees.Contains(decree))
        {
            Utils.SendMessage(Translator.GetString("President.DecreeAlreadyUsedMessage"), pc.PlayerId);
            return;
        }

        switch (decree)
        {
            case Decree.Reveal:
                if (!DecreeSettings[decree][1].GetBool()) pc.RpcSetCustomRole(CustomRoles.Loyal);

                if ((!DecreeSettings[decree][2].GetBool() && pc.IsConverted()) || pc.Is(CustomRoles.Bloodlust)) return;

                Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.Everyone"), Translator.GetString($"President.Decree.{decree}")), importance: MessageImportance.High);
                Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.RevealMessage"), pc.PlayerId.ColoredPlayerName()), importance: MessageImportance.High);
                break;
            case Decree.Finish:
                MeetingHudRpcClosePatch.AllowClose = true;
                MeetingHud.Instance?.RpcClose();
                Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.Everyone"), Translator.GetString($"President.Decree.{decree}")), importance: MessageImportance.High);
                break;
            case Decree.Declassification:
                president.IsDeclassification = true;
                Utils.MarkEveryoneDirtySettings();
                Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.Everyone"), Translator.GetString($"President.Decree.{decree}")), importance: MessageImportance.High);
                break;
            case Decree.GovernmentSupport:
                foreach (PlayerControl player in Main.EnumeratePlayerControls())
                {
                    if (!player.IsCrewmate()) continue;

                    switch (Main.PlayerStates[player.PlayerId].Role)
                    {
                        case Hacker when Hacker.UseLimit.ContainsKey(player.PlayerId):
                            Hacker.UseLimit[player.PlayerId]++;
                            break;
                        default:
                            player.RpcIncreaseAbilityUseLimitBy(1f);
                            break;
                    }
                }

                Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.Everyone"), Translator.GetString($"President.Decree.{decree}")), importance: MessageImportance.High);
                break;
            case Decree.Investigation:
                Utils.SendMessage("\n", pc.PlayerId, Utils.GetRemainingKillers(showAll: true), importance: MessageImportance.High);
                break;
            case Decree.GovernmentRecruiting:
                if (MeetingHud.Instance?.playerStates?.FirstOrDefault(x => x.TargetPlayerId == pc.PlayerId)?.DidVote == true) return;

                president.IsRecruiting = true;
                break;
        }

        president.UsedDecrees.Add(decree);
        president.Used = true;
    }

    public static string GetHelpMessage()
    {
        return Enum.GetValues<Decree>().Where(x => x.IsEnabled()).Aggregate("<size=80%>", (acc, x) => $"{acc}{Translator.GetString($"President.Decree.{x}")}: {(int)x}\n") + $"\n{Translator.GetString("President.Help")}</size>";
    }

    public override void AfterMeetingTasks()
    {
        IsDeclassification = false;
        IsRecruiting = false;
        Used = false;
    }

    public override void OnReportDeadBody()
    {
        AfterMeetingTasks();
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;

        return target.PlayerId == PresidentId && IsRevealed;
    }

    public override bool OnVote(PlayerControl voter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return false;
        if (voter.PlayerId != PresidentId || !IsRecruiting || Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;

        if (!target.Is(CustomRoleTypes.Crewmate) || target.IsConverted())
        {
            IsRecruiting = false;
            return true;
        }

        CustomRoles role = GovernmentRecruitRoles[DecreeSettings[Decree.GovernmentRecruiting][1].GetValue()];

        target.RpcSetCustomRole(role);
        target.RpcChangeRoleBasis(role);

        Utils.SendMessage("\n", target.PlayerId, Translator.GetString("President.Recruit.TargetNotifyMessage"), importance: MessageImportance.High);
        Main.DontCancelVoteList.Add(voter.PlayerId);
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        OnVote(shapeshifter, target);
    }
}