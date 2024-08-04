using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Crewmate
{
    static class DecreeExtension
    {
        public static bool IsEnabled(this President.Decree decree) => President.DecreeSettings[decree][0].GetBool();
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
            CustomRoles.ParityCop,
            CustomRoles.Jailor,
            CustomRoles.SecurityGuard,
            CustomRoles.NiceGuesser,
            CustomRoles.Inquirer,
            CustomRoles.Farseer
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
            int id = 647850;
            const TabGroup tab = TabGroup.CrewmateRoles;
            const CustomRoles role = CustomRoles.President;
            Options.SetupSingleRoleOptions(id++, tab, role, hideMaxSetting: true);
            foreach (var decree in Enum.GetValues<Decree>())
            {
                DecreeSettings[decree] =
                [
                    new BooleanOptionItem(++id, $"President.Decree.{decree}", true, tab)
                        .SetParent(Options.CustomRoleSpawnChances[role])
                ];

                switch (decree)
                {
                    case Decree.Reveal:
                        DecreeSettings[decree].AddRange(new[]
                        {
                            new BooleanOptionItem(++id, "President.Reveal.CanBeConvertedAfterRevealing", false, tab)
                                .SetParent(DecreeSettings[decree][0]),
                            new BooleanOptionItem(++id, "President.Reveal.ConvertedPresidentCanReveal", false, tab)
                                .SetParent(DecreeSettings[decree][0])
                        });
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
            if (Instances.Any(x => x.IsDeclassification))
                opt.SetBool(BoolOptionNames.AnonymousVotes, false);
        }

        public static void UseDecree(PlayerControl pc, string message)
        {
            var president = Instances.FirstOrDefault(x => x.PresidentId == pc.PlayerId);
            if (president == null || president.Used)
            {
                Utils.SendMessage(Translator.GetString("President.DecreeAlreadyChosenThisMeetingMessage"), pc.PlayerId);
                return;
            }

            if (!Utils.GetPlayerById(president.PresidentId).IsAlive()) return;

            if (!int.TryParse(message, out var num) || num > 5)
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
                    if (!DecreeSettings[decree][2].GetBool() && pc.GetCustomSubRoles().Any(x => x.IsConverted())) return;
                    Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.Everyone"), Translator.GetString($"President.Decree.{decree}")));
                    break;
                case Decree.Finish:
                    MeetingHud.Instance?.RpcClose();
                    Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.Everyone"), Translator.GetString($"President.Decree.{decree}")));
                    break;
                case Decree.Declassification:
                    president.IsDeclassification = true;
                    Utils.SyncAllSettings();
                    Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.Everyone"), Translator.GetString($"President.Decree.{decree}")));
                    break;
                case Decree.GovernmentSupport:
                    foreach (var player in Main.AllPlayerControls)
                    {
                        if (!player.IsCrewmate()) continue;
                        switch (Main.PlayerStates[player.PlayerId].Role)
                        {
                            case SabotageMaster sm:
                                sm.UsedSkillCount--;
                                break;
                            case NiceHacker when NiceHacker.UseLimit.ContainsKey(player.PlayerId):
                                NiceHacker.UseLimit[player.PlayerId]++;
                                break;
                            default:
                                player.RpcIncreaseAbilityUseLimitBy(1f);
                                break;
                        }
                    }

                    Utils.SendMessage(string.Format(Translator.GetString("President.UsedDecreeMessage.Everyone"), Translator.GetString($"President.Decree.{decree}")));
                    break;
                case Decree.Investigation:
                    Utils.SendMessage("\n", pc.PlayerId, Utils.GetRemainingKillers(president: true));
                    break;
                case Decree.GovernmentRecruiting:
                    if (MeetingHud.Instance?.playerStates?.FirstOrDefault(x => x.TargetPlayerId == pc.PlayerId)?.DidVote == true) return;
                    president.IsRecruiting = true;
                    break;
            }

            president.UsedDecrees.Add(decree);
            president.Used = true;
        }

        public static string GetHelpMessage() => Enum.GetValues<Decree>().Where(x => x.IsEnabled()).Aggregate("<size=80%>", (acc, x) => $"{acc}{Translator.GetString($"President.Decree.{x}")}: {(int)x}\n") + $"\n{Translator.GetString("President.Help")}</size>";

        public override void AfterMeetingTasks()
        {
            IsDeclassification = false;
            IsRecruiting = false;
            Used = false;
        }

        public override bool KnowRole(PlayerControl seer, PlayerControl target)
        {
            if (base.KnowRole(seer, target)) return true;
            return target.PlayerId == PresidentId && IsRevealed;
        }

        public override bool OnVote(PlayerControl voter, PlayerControl target)
        {
            if (voter.PlayerId != PresidentId || !IsRecruiting || Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;
            if (!target.Is(CustomRoleTypes.Crewmate) || target.IsConverted()) return true;

            CustomRoles role = GovernmentRecruitRoles[DecreeSettings[Decree.GovernmentRecruiting][1].GetValue()];
            if (role.GetDYRole() == RoleTypes.Impostor && target.GetRoleTypes() != RoleTypes.Impostor) role = CustomRoles.NiceGuesser;
            target.RpcSetCustomRole(role);
            Utils.SendMessage("\n", target.PlayerId, Translator.GetString("President.Recruit.TargetNotifyMessage"));
            Main.DontCancelVoteList.Add(voter.PlayerId);
            return true;
        }
    }
}