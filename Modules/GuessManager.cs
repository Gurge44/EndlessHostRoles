using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AmongUs.GameOptions;
using EHR.Gamemodes;
using EHR.Modules;
using EHR.Roles;
using HarmonyLib;
using Hazel;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static EHR.Translator;

namespace EHR;

public static class GuessManager
{
    private const int MaxOneScreenRole = 40;
    private static readonly int Mask = Shader.PropertyToID("_Mask");
    public static HashSet<byte> Guessers = [];
    private static int Page;
    private static GameObject GuesserUI;
    private static Dictionary<CustomRoleTypes, List<Transform>> RoleButtons;
    private static Dictionary<CustomRoleTypes, SpriteRenderer> RoleSelectButtons;
    private static List<SpriteRenderer> PageButtons;
    private static CustomRoleTypes CurrentTeamType;

    public static TextMeshPro TextTemplate;

    public static string GetFormatString()
    {
        return Main.EnumeratePlayerControls().Aggregate(GetString("PlayerIdList"), (current, pc) => current + $"\n{pc.PlayerId.ToString()} → {pc.GetRealName()}");
    }

    public static bool CheckCommand(ref string msg, string command, bool exact, out bool spamRequired)
    {
        Utils.CheckServerCommand(ref msg, out spamRequired);
        
        string[] comList = command.Split('|');

        foreach (string str in comList)
        {
            if (exact)
            {
                if (msg == "/" + str) return true;
            }
            else
            {
                if (msg.StartsWith("/" + str))
                {
                    msg = msg.Replace("/" + str, string.Empty);
                    return true;
                }
            }
        }

        return false;
    }

    public static bool GuesserMsg(PlayerControl pc, string msg, bool isUI = false, bool ssMenu = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsMeeting || MeetingHud.Instance.state is MeetingHud.VoteStates.Results or MeetingHud.VoteStates.Proceeding || pc == null) return false;

        bool hasGuessingRole = pc.GetCustomRole() is CustomRoles.NiceGuesser or CustomRoles.EvilGuesser or CustomRoles.Doomsayer or CustomRoles.Judge or CustomRoles.Swapper or CustomRoles.Councillor or CustomRoles.NecroGuesser or CustomRoles.Augur;
        if (!hasGuessingRole && !pc.Is(CustomRoles.Guesser) && !Options.GuesserMode.GetBool()) return false;

        int operate; // 1: ID, 2: Guess
        msg = msg.ToLower().TrimStart().TrimEnd();

        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id", true, out bool spamRequired)) operate = 1;
        else if (CheckCommand(ref msg, "shoot|guess|bet|st|bt|猜|赌", false, out spamRequired)) operate = 2;
        else return false;

        Logger.Msg(msg, "Msg Guesser");
        Logger.Msg($"{operate}", "Operate");

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GetFormatString(), pc.PlayerId);
                break;
            case 2:
            {
                if (!pc.IsAlive())
                {
                    ShowMessage("GuessDead");
                    return true;
                }

                if (!Options.CanGuessDuringDiscussionTime.GetBool() && MeetingHud.Instance && MeetingHud.Instance.state is MeetingHud.VoteStates.Discussion or MeetingHud.VoteStates.Animating && Main.RealOptionsData.GetInt(Int32OptionNames.DiscussionTime) > 0)
                {
                    ShowMessage("GuessDuringDiscussion");
                    return true;
                }

                if (pc.Is(CustomRoles.Decryptor) && Decryptor.GuessMode.GetValue() == 2) goto SkipCheck;

                if ((pc.IsCrewmate() && !Options.CrewmatesCanGuess.GetBool()) ||
                    (pc.IsImpostor() && !Options.ImpostorsCanGuess.GetBool()) ||
                    (pc.Is(CustomRoleTypes.Coven) && !Options.CovenCanGuess.GetBool()) ||
                    (pc.IsNeutralKiller() && !Options.NeutralKillersCanGuess.GetBool()) ||
                    (pc.GetCustomRole().IsNonNK() && !Options.PassiveNeutralsCanGuess.GetBool()) ||
                    (pc.Is(CustomRoles.Decryptor) && Decryptor.GuessMode.GetValue() == 0) ||
                    (Options.GuesserNumRestrictions.GetBool() && !Guessers.Contains(pc.PlayerId)))
                {
                    if (pc.GetCustomRole() is CustomRoles.EvilGuesser or CustomRoles.NiceGuesser) goto SkipCheck;
                    if (pc.Is(CustomRoles.Guesser)) goto SkipCheck;
                    if (hasGuessingRole) goto SkipCheck;
                    if ((pc.Is(CustomRoles.Madmate) || pc.IsConverted()) && Options.BetrayalAddonsCanGuess.GetBool()) goto SkipCheck;

                    ShowMessage("GuessNotAllowed");
                    return true;
                }

                SkipCheck:

                if (!isUI && !ssMenu && spamRequired && (pc.GetCustomRole() is CustomRoles.Decryptor or CustomRoles.NecroGuesser ||
                     (pc.Is(CustomRoles.NiceGuesser) && Options.GGTryHideMsg.GetBool()) ||
                     (pc.Is(CustomRoles.EvilGuesser) && Options.EGTryHideMsg.GetBool()) ||
                     (pc.Is(CustomRoles.Doomsayer) && Doomsayer.DoomsayerTryHideMsg.GetBool()) ||
                     (pc.Is(CustomRoles.Guesser) && Guesser.GTryHideMsg.GetBool()) || (Options.GuesserMode.GetBool() && Options.HideGuesserCommands.GetBool())))
                    Utils.SendMessage("\n", pc.PlayerId, GetString("NoSpamAnymoreUseCmd"));

                if (!MsgToPlayerAndRole(msg, out byte targetId, out CustomRoles role, out string error))
                {
                    ShowMessage(error);
                    return true;
                }
                
                if ((pc.IsCrewmate() && role.IsCrewmate() && !Options.CrewCanGuessCrew.GetBool()) ||
                    (pc.IsImpostor() && role.IsImpostor() && !Options.ImpCanGuessImp.GetBool()))
                {
                    ShowMessage("GuessTeamMate");
                    return true;
                }

                PlayerControl target = Utils.GetPlayerById(targetId);

                if (target != null)
                {
                    Main.GuesserGuessed.TryAdd(pc.PlayerId, 0);
                    Main.GuesserGuessedMeeting.TryAdd(pc.PlayerId, 0);

                    var guesserSuicide = false;

                    switch (pc.Is(CustomRoles.NecroGuesser))
                    {
                        case true when target.IsAlive() || target.Is(CustomRoles.Gravestone) || ((Options.SeeEjectedRolesInMeeting.GetBool() || Options.CEMode.GetValue() == 2) && Main.PlayerStates[targetId].deathReason == PlayerState.DeathReason.Vote):
                            ShowMessage(target.IsAlive() ? "NecroGuesser.TargetAliveError" : "NecroGuesser.TargetRevealedError");
                            return true;
                        case false when !target.IsAlive():
                            ShowMessage("GuessNull");
                            return true;
                    }

                    if (!pc.Is(CustomRoles.Guesser) && CopyCat.Instances.Exists(x => x.CopyCatPC == pc))
                    {
                        ShowMessage("GuessDisabled");
                        return true;
                    }

                    if (CustomTeamManager.AreInSameCustomTeam(pc.PlayerId, targetId) && !CustomTeamManager.IsSettingEnabledForPlayerTeam(targetId, CTAOption.GuessEachOther))
                    {
                        ShowMessage("GuessSameCTAPlayer");
                        return true;
                    }

                    bool hasGuessSetting = Options.AddonGuessSettings.TryGetValue(role, out OptionItem guessSetting);

                    if (hasGuessSetting && guessSetting.GetValue() == 1)
                    {
                        ShowMessage("GuessDisabledAddonOverride");
                        return true;
                    }

                    OptionItem convertedGuessSetting = null;

                    if (role == CustomRoles.Egoist || role.IsConverted())
                    {
                        convertedGuessSetting = role switch
                        {
                            CustomRoles.Charmed => Options.CharmedCanBeGuessed,
                            CustomRoles.Contagious => Options.ContagiousCanBeGuessed,
                            CustomRoles.Undead => Options.UndeadCanBeGuessed,
                            CustomRoles.Egoist => Options.EgoistCanBeGuessed,
                            CustomRoles.Entranced => Options.EntrancedCanBeGuessed,
                            _ => null
                        };

                        if (convertedGuessSetting?.GetValue() == 1)
                        {
                            ShowMessage("GuessDisabledAddonOverride");
                            return true;
                        }
                    }

                    bool forceAllowGuess = (hasGuessSetting && guessSetting.GetValue() == 0) || convertedGuessSetting?.GetValue() == 0 || (role is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor or CustomRoles.Lovers && Lovers.GuessAbility.GetValue() == 2);

                    if (role == CustomRoles.Lovers && Lovers.GuessAbility.GetValue() == 0)
                    {
                        ShowMessage("GuessLovers");
                        return true;
                    }

                    if (pc.GetCustomRole() is not (CustomRoles.Augur or CustomRoles.EvilGuesser or CustomRoles.NiceGuesser or CustomRoles.Doomsayer) && (Main.GuesserGuessed[pc.PlayerId] >= Options.GuesserMaxKillsPerGame.GetInt() || Main.GuesserGuessedMeeting[pc.PlayerId] >= Options.GuesserMaxKillsPerMeeting.GetInt()))
                    {
                        ShowMessage("GGGuessMax");
                        return true;
                    }

                    switch (pc.GetCustomRole())
                    {
                        case CustomRoles.Augur when !((Augur)Main.PlayerStates[pc.PlayerId].Role).CanGuess:
                        case CustomRoles.Augur when Main.GuesserGuessed[pc.PlayerId] >= Augur.MaxGuessesPerGame.GetInt():
                        case CustomRoles.Augur when Main.GuesserGuessedMeeting[pc.PlayerId] >= Augur.MaxGuessesPerMeeting.GetInt():
                        case CustomRoles.NiceGuesser when Main.GuesserGuessed[pc.PlayerId] >= Options.GGCanGuessTime.GetInt():
                        case CustomRoles.EvilGuesser when Main.GuesserGuessed[pc.PlayerId] >= Options.EGCanGuessTime.GetInt():
                            ShowMessage("GGGuessMax");
                            return true;
                        case CustomRoles.Shifter when !Shifter.CanGuess.GetBool():
                        case CustomRoles.Specter when !Options.PhantomCanGuess.GetBool():
                        case CustomRoles.Terrorist when !Terrorist.TerroristCanGuess.GetBool():
                        case CustomRoles.Workaholic when !Workaholic.WorkaholicCanGuess.GetBool():
                        case CustomRoles.God when !God.GodCanGuess.GetBool():
                        case CustomRoles.Executioner when Executioner.Target[pc.PlayerId] == target.PlayerId && Executioner.KnowTargetRole.GetBool() && !Executioner.CanGuessTarget.GetBool():
                            ShowMessage("GuessDisabled");
                            return true;
                        case CustomRoles.Monarch when role == CustomRoles.Knighted:
                            ShowMessage("GuessKnighted");
                            return true;
                        case CustomRoles.Berserker when ((Berserker)Main.PlayerStates[pc.PlayerId].Role).Form >= 4:
                            ShowMessage("GuessBerserker");
                            return true;
                        case CustomRoles.Doomsayer:
                            if (Doomsayer.CantGuess)
                            {
                                ShowMessage("DoomsayerCantGuess");
                                return true;
                            }

                            if ((
                                    (target.IsImpostor() && !Doomsayer.DCanGuessImpostors.GetBool()) ||
                                    (target.IsCrewmate() && !Doomsayer.DCanGuessCrewmates.GetBool()) ||
                                    ((role.IsNeutral() || target.IsNeutralKiller()) && !Doomsayer.DCanGuessNeutrals.GetBool()) ||
                                    (role.IsCoven() && !Doomsayer.DCanGuessCoven.GetBool()))
                                && !forceAllowGuess)
                            {
                                ShowMessage("GuessNotAllowed");
                                return true;
                            }

                            if (role.IsAdditionRole() && !Doomsayer.DCanGuessAdt.GetBool() && !forceAllowGuess)
                            {
                                ShowMessage("GuessAdtRole");
                                return true;
                            }

                            break;
                    }

                    if (Medic.ProtectList.Contains(target.PlayerId) && !Medic.GuesserIgnoreShield.GetBool())
                    {
                        ShowMessage("GuessShielded");
                        return true;
                    }

                    switch (role)
                    {
                        case CustomRoles.Crewmate or CustomRoles.CrewmateEHR when CrewmateVanillaRoles.VanillaCrewmateCannotBeGuessed.GetBool():
                            ShowMessage("GuessVanillaCrewmate");
                            return true;
                        case CustomRoles.Workaholic when Workaholic.WorkaholicVisibleToEveryone.GetBool():
                            ShowMessage("GuessWorkaholic");
                            return true;
                        case CustomRoles.Doctor when Options.DoctorVisibleToEveryone.GetBool() && !target.HasEvilAddon():
                            ShowMessage("GuessDoctor");
                            return true;
                        case CustomRoles.Marshall when target.Is(CustomRoles.Marshall) && !Marshall.CanBeGuessedOnTaskCompletion.GetBool() && target.GetTaskState().IsTaskFinished:
                            ShowMessage("GuessMarshallTask");
                            return true;
                        case CustomRoles.Monarch when pc.Is(CustomRoles.Knighted):
                            ShowMessage("GuessMonarch");
                            return true;
                        case CustomRoles.Mayor when target.Is(CustomRoles.Mayor) && Mayor.MayorRevealWhenDoneTasks.GetBool() && target.GetTaskState().IsTaskFinished:
                            ShowMessage("GuessMayor");
                            return true;
                        case CustomRoles.Bait when Options.BaitNotification.GetBool():
                            ShowMessage("GuessNotifiedBait");
                            return true;
                        case CustomRoles.Pestilence:
                            ShowMessage("GuessPestilence");
                            if (DoubleShot.CheckGuess(pc, isUI)) return true;
                            guesserSuicide = true;
                            break;
                        case CustomRoles.Specter:
                            ShowMessage("GuessPhantom");
                            return true;
                        case CustomRoles.Snitch when pc.IsSnitchTarget() && target.GetTaskState().RemainingTasksCount <= Snitch.RemainingTasksToBeFound:
                            ShowMessage("EGGuessSnitchTaskDone");
                            return true;
                        case CustomRoles.Merchant when Merchant.IsBribedKiller(pc, target):
                            ShowMessage("BribedByMerchant2");
                            return true;
                        case CustomRoles.SuperStar:
                            ShowMessage("GuessSuperStar");
                            return true;
                        case CustomRoles.Backstabber when target.Is(CustomRoles.Backstabber) && Backstabber.RevealAfterKilling.GetBool() && target.GetAbilityUseLimit() == 0f:
                            ShowMessage("GuessBackstabber");
                            return true;
                        case CustomRoles.President when Main.PlayerStates[target.PlayerId].Role is President { IsRevealed: true }:
                            ShowMessage("GuessPresident");
                            return true;
                        case CustomRoles.EvilEraser when EvilEraser.ErasedPlayers.Contains(target.PlayerId) && pc.Is(CustomRoles.EvilEraser):
                        case CustomRoles.NiceEraser when NiceEraser.ErasedPlayers.Contains(target.PlayerId) && pc.Is(CustomRoles.NiceEraser):
                            ShowMessage("GuessErased");
                            return true;
                        case CustomRoles.Tank when !Tank.CanBeGuessed.GetBool():
                            ShowMessage("GuessTank");
                            return true;
                        case CustomRoles.Ankylosaurus:
                            ShowMessage("GuessAnkylosaurus");
                            return true;
                        case CustomRoles.Car:
                        case CustomRoles.DonutDelivery when DonutDelivery.IsUnguessable(pc, target):
                        case CustomRoles.Shifter:
                        case CustomRoles.Speedrunner when target.Is(CustomRoles.Speedrunner) && !pc.Is(Team.Crewmate) && target.GetTaskState().CompletedTasksCount >= Speedrunner.SpeedrunnerNotifyAtXTasksLeft.GetInt() && Speedrunner.SpeedrunnerNotifyKillers.GetBool():
                        case CustomRoles.Goose when !Goose.CanBeGuessed.GetBool():
                        case CustomRoles.BananaMan:
                        case CustomRoles.Disco:
                        case CustomRoles.Flash:
                        case CustomRoles.Giant:
                        case CustomRoles.Glow:
                        case CustomRoles.LastImpostor:
                            ShowMessage("GuessObviousAddon");
                            return true;
                        case CustomRoles.GM:
                            Utils.SendMessage(GetString("GuessGM"), pc.PlayerId);
                            return true;
                    }

                    if (target.Is(CustomRoles.Onbound))
                    {
                        if (!Onbound.NumBlocked.TryGetValue(target.PlayerId, out HashSet<byte> attempters))
                            Onbound.NumBlocked[target.PlayerId] = attempters = [];
                        
                        if (attempters.Count < Onbound.MaxAttemptsBlocked.GetInt())
                        {
                            attempters.Add(pc.PlayerId);
                            ShowMessage("GuessOnbound");

                            if (Onbound.GuesserSuicides.GetBool())
                            {
                                if (DoubleShot.CheckGuess(pc, isUI)) return true;
                                guesserSuicide = true;
                            }
                            else return true;
                        }
                        else if (attempters.Contains(pc.PlayerId))
                        {
                            ShowMessage("GuessOnbound");
                            return true;
                        }
                    }

                    if (Jailor.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == target.PlayerId))
                    {
                        if (!isUI) Utils.SendMessage(GetString("CantGuessJailed"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                        else pc.ShowPopUp($"{Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle"))}\n{GetString("CantGuessJailed")}");

                        Logger.Info($"Player {pc.GetNameWithRole().RemoveHtmlTags()} tried to guess jailed player {target.GetNameWithRole().RemoveHtmlTags()}", "Guesser");
                        return true;
                    }

                    if (Jailor.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == pc.PlayerId && role != CustomRoles.Jailor))
                    {
                        if (!isUI) Utils.SendMessage(GetString("JailedCanOnlyGuessJailor"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                        else pc.ShowPopUp($"{Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle"))}\n{GetString("JailedCanOnlyGuessJailor")}");

                        Logger.Info($"Player {pc.GetNameWithRole().RemoveHtmlTags()} tried to guess {target.GetNameWithRole().RemoveHtmlTags()} while jailed", "Guesser");
                        return true;
                    }

                    if (Markseeker.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Markseeker { TargetRevealed: true } ms && ms.MarkedId == target.PlayerId))
                    {
                        ShowMessage("GuessMarkseekerTarget");
                        return true;
                    }

                    // Check whether Add-on guessing is allowed
                    if (!forceAllowGuess && role.IsAdditionRole())
                    {
                        switch (pc.GetCustomRole())
                        {
                            // Evil & Nice Guessers Can't Guess Addons
                            case CustomRoles.EvilGuesser when !Options.EGCanGuessAdt.GetBool():
                            case CustomRoles.NiceGuesser when !Options.GGCanGuessAdt.GetBool():
                                ShowMessage("GuessAdtRole");
                                return true;
                            // Guesser (Add-on) Can't Guess Add-ons
                            default:
                                if (pc.Is(CustomRoles.Guesser) && !Guesser.GCanGuessAdt.GetBool())
                                {
                                    ShowMessage("GuessAdtRole");
                                    return true;
                                }

                                break;
                        }

                        // Guesser Mode Can/Can't Guess Addons
                        if (Options.GuesserMode.GetBool() && !Options.CanGuessAddons.GetBool())
                        {
                            if ((Options.ImpostorsCanGuess.GetBool() && pc.Is(CustomRoleTypes.Impostor) && !(pc.GetCustomRole() == CustomRoles.EvilGuesser || pc.Is(CustomRoles.Guesser))) ||
                                (Options.CrewmatesCanGuess.GetBool() && pc.Is(CustomRoleTypes.Crewmate) && !(pc.GetCustomRole() == CustomRoles.NiceGuesser || pc.Is(CustomRoles.Guesser))) ||
                                (Options.CovenCanGuess.GetBool() && pc.Is(CustomRoleTypes.Coven) && !(pc.GetCustomRole() == CustomRoles.Augur || pc.Is(CustomRoles.Guesser))) ||
                                ((Options.NeutralKillersCanGuess.GetBool() || Options.PassiveNeutralsCanGuess.GetBool()) && pc.Is(CustomRoleTypes.Neutral) && !(pc.GetCustomRole() is CustomRoles.Ritualist or CustomRoles.Doomsayer || pc.Is(CustomRoles.Guesser))))
                            {
                                ShowMessage("GuessAdtRole");
                                return true;
                            }
                        }
                    }

                    if (pc.PlayerId == target.PlayerId)
                    {
                        if (!isUI) Utils.SendMessage(GetString("LaughToWhoGuessSelf"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromKPD")));
                        else pc.ShowPopUp($"{Utils.ColorString(Color.cyan, GetString("MessageFromKPD"))}\n{GetString("LaughToWhoGuessSelf")}");

                        if (DoubleShot.CheckGuess(pc, isUI)) return true;
                        guesserSuicide = true;
                    }
                    else if (pc.Is(CustomRoles.NiceGuesser) && role.IsCrewmate() && !Options.GGCanGuessCrew.GetBool() && !pc.Is(CustomRoles.Madmate))
                    {
                        if (!isUI) Utils.SendMessage(GetString("GuessCrewRole"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromGurge44")));
                        else pc.ShowPopUp($"{Utils.ColorString(Color.cyan, GetString("MessageFromGurge44"))}\n{GetString("GuessCrewRole")}");

                        return true;
                    }
                    else if (pc.Is(CustomRoles.EvilGuesser) && role.IsImpostor() && !Options.EGCanGuessImp.GetBool())
                    {
                        if (!isUI) Utils.SendMessage(GetString("GuessImpRole"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromGurge44")));
                        else pc.ShowPopUp($"{Utils.ColorString(Color.cyan, GetString("MessageFromGurge44"))}\n{GetString("GuessImpRole")}");

                        return true;
                    }
                    else if (!target.Is(role) && !target.Is(CustomRoles.Unbound))
                    {
                        if (DoubleShot.CheckGuess(pc, isUI)) return true;

                        guesserSuicide = true;
                        Logger.Msg($"{guesserSuicide}", "guesserSuicide3");
                    }

                    Main.GuesserGuessed[pc.PlayerId]++;
                    Main.GuesserGuessedMeeting[pc.PlayerId]++;

                    if (guesserSuicide && Options.GuesserDoesntDieOnMisguess.GetBool())
                    {
                        if (!isUI) Utils.SendMessage(GetString("MisguessButNoSuicide"), pc.PlayerId, Utils.ColorString(Color.yellow, GetString("MessageFromGurge44")));
                        else pc.ShowPopUp($"{Utils.ColorString(Color.yellow, GetString("MessageFromGurge44"))}\n{GetString("MisguessButNoSuicide")}");

                        return true;
                    }


                    // -----------------------------------------------------------------------------------------------------------------------------------


                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} guessed {target.GetNameWithRole().RemoveHtmlTags()}", "Guesser");
                    PlayerControl dp = guesserSuicide ? pc : target;
                    target = dp;

                    Logger.Info($"Player: {target.GetRealName().RemoveHtmlTags()} was guessed by {pc.GetRealName().RemoveHtmlTags()}", "Guesser");

                    if (pc.IsHost()) Utils.FlashColor(guesserSuicide ? new(1f, 0f, 0f, 0.3f) : new(0f, 1f, 0f, 0.3f));

                    if (Main.PlayerStates[pc.PlayerId].Role is NecroGuesser ng)
                    {
                        if (!guesserSuicide) ng.GuessedPlayers++;
                        ShowMessage(!guesserSuicide ? "NecroGuesser.GuessCorrect" : "NecroGuesser.GuessIncorrect");
                        return true;
                    }

                    if (pc.Is(CustomRoles.Doomsayer) && Doomsayer.AdvancedSettings.GetBool())
                    {
                        if (Doomsayer.GuessesCountPerMeeting >= Doomsayer.MaxNumberOfGuessesPerMeeting.GetInt() && pc.PlayerId != dp.PlayerId)
                        {
                            ShowMessage("DoomsayerCantGuess");
                            return true;
                        }

                        Doomsayer.GuessesCountPerMeeting++;

                        if (Doomsayer.GuessesCountPerMeeting >= Doomsayer.MaxNumberOfGuessesPerMeeting.GetInt())
                            Doomsayer.CantGuess = true;

                        if (!Doomsayer.KillCorrectlyGuessedPlayers.GetBool() && pc.PlayerId != dp.PlayerId)
                        {
                            ShowMessage("DoomsayerCorrectlyGuessRole");

                            if (Doomsayer.GuessedRoles.Contains(role))
                                LateTask.New(() => Utils.SendMessage(GetString("DoomsayerGuessSameRoleAgainMsg"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doomsayer), GetString("DoomsayerGuessCountTitle"))), 0.7f, "Doomsayer Guess Same Role Again Msg");
                            else
                            {
                                Doomsayer.GuessingToWin[pc.PlayerId]++;
                                Doomsayer.SendRPC(pc);
                                Doomsayer.GuessedRoles.Add(role);

                                LateTask.New(() => Utils.SendMessage(string.Format(GetString("DoomsayerGuessCountMsg"), Doomsayer.GuessingToWin[pc.PlayerId]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doomsayer), GetString("DoomsayerGuessCountTitle"))), 0.7f, "Doomsayer Guess Msg 1");
                            }

                            Doomsayer.CheckCountGuess(pc);

                            return true;
                        }

                        if (Doomsayer.DoesNotSuicideWhenMisguessing.GetBool() && pc.PlayerId == dp.PlayerId)
                        {
                            ShowMessage("DoomsayerNotCorrectlyGuessRole");

                            if (Doomsayer.MisguessRolePrevGuessRoleUntilNextMeeting.GetBool())
                                Doomsayer.CantGuess = true;

                            return true;
                        }
                    }

                    if (pc.Is(CustomRoles.Augur) && pc.PlayerId == dp.PlayerId)
                    {
                        ShowMessage("Augur.IncorrectGuess");
                        ((Augur)Main.PlayerStates[pc.PlayerId].Role).CanGuess = false;
                        return true;
                    }

                    string name = dp.GetRealName();
                    if (!Options.DisableKillAnimationOnGuess.GetBool()) CustomSoundsManager.RPCPlayCustomSoundAll("Gunfire");

                    LateTask.New(() =>
                    {
                        if (Main.PlayerStates.TryGetValue(dp.PlayerId, out PlayerState state))
                        {
                            state.deathReason = dp.PlayerId == pc.PlayerId && Options.MisguessDeathReason.GetBool() ? PlayerState.DeathReason.Misguess : PlayerState.DeathReason.Gambled;
                            dp.SetRealKiller(pc);
                            dp.RpcGuesserMurderPlayer();
                        }

                        if (dp.Is(CustomRoles.Medic)) Medic.IsDead(dp);

                        if (pc.Is(CustomRoles.Doomsayer) && pc.PlayerId != dp.PlayerId)
                        {
                            Doomsayer.GuessingToWin[pc.PlayerId]++;
                            Doomsayer.SendRPC(pc);

                            if (!Doomsayer.GuessedRoles.Contains(role))
                                Doomsayer.GuessedRoles.Add(role);

                            Doomsayer.CheckCountGuess(pc);
                        }

                        MeetingManager.OnGuess(dp, pc);
                        Utils.AfterPlayerDeathTasks(dp, true);
                        
                        if (pc.AmOwner && pc.Is(CustomRoles.Decryptor) && dp.Is(CustomRoles.God))
                            Achievements.Type.Easypeasy.Complete();

                        LateTask.New(() => Utils.SendMessage(string.Format(GetString("GuessKill"), Main.AllPlayerNames.GetValueOrDefault(dp.PlayerId, name)), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceGuesser), GetString("GuessKillTitle")), importance: MessageImportance.High), 0.6f, "Guess Msg");

                        if (pc.Is(CustomRoles.Doomsayer) && pc.PlayerId != dp.PlayerId) LateTask.New(() => Utils.SendMessage(string.Format(GetString("DoomsayerGuessCountMsg"), Doomsayer.GuessingToWin[pc.PlayerId]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doomsayer), GetString("DoomsayerGuessCountTitle"))), 0.7f, "Doomsayer Guess Msg 2");
                        if (pc.Is(CustomRoles.Stealer) && pc.PlayerId != dp.PlayerId) LateTask.New(() => Utils.SendMessage(string.Format(GetString("StealerGetVote"), (int)(Main.EnumeratePlayerControls().Count(x => x.GetRealKiller()?.PlayerId == pc.PlayerId) * Options.VotesPerKill.GetFloat())), pc.PlayerId), 0.7f, log: false);
                        if (pc.Is(CustomRoles.Pickpocket) && pc.PlayerId != dp.PlayerId) LateTask.New(() => Utils.SendMessage(string.Format(GetString("PickpocketGetVote"), (int)(Main.EnumeratePlayerControls().Count(x => x.GetRealKiller()?.PlayerId == pc.PlayerId) * Pickpocket.VotesPerKill.GetFloat())), pc.PlayerId), 0.7f, log: false);
                    }, 0.2f, "Guesser Kill");

                    if (guesserSuicide && pc.AmOwner)
                        Achievements.Type.BadLuckOrBadObservation.Complete();
                }

                break;
            }
        }

        return true;

        void ShowMessage(string str)
        {
            string text = GetString(str);

            if (!isUI) Utils.SendMessage(text, pc.PlayerId);
            else pc.ShowPopUp(text);

            Logger.Info($"Shown/Sent: {text}", "Guesser Message");
        }
    }

    public static void RpcGuesserMurderPlayer(this PlayerControl pc /*, float delay = 0f*/)
    {
        // DEATH STUFF //
        try
        {
            GameEndChecker.ShouldNotCheck = true;
            Main.PlayerStates[pc.PlayerId].SetDead();
            pc.Data.IsDead = true;
            pc.RpcExileV2();

            var meetingHud = MeetingHud.Instance;
            ProcessGuess(pc, meetingHud);

            foreach (PlayerVoteArea playerVoteArea in meetingHud.playerStates)
            {
                if (playerVoteArea.VotedFor != pc.PlayerId) continue;

                playerVoteArea.UnsetVote();
                meetingHud.SetDirtyBit(1U);
                AmongUsClient.Instance.SendAllStreamedObjects();

                PlayerControl voteAreaPlayer = Utils.GetPlayerById(playerVoteArea.TargetPlayerId);
                if (voteAreaPlayer == null) continue;

                if (!voteAreaPlayer.AmOwner)
                {
                    meetingHud.RpcClearVote(voteAreaPlayer.OwnerId);
                    meetingHud.SetDirtyBit(1U);
                    AmongUsClient.Instance.SendAllStreamedObjects();
                }
                else
                    meetingHud.ClearVote();
            }

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.GuessKill, SendOption.Reliable);
            writer.Write(pc.PlayerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        finally { GameEndChecker.ShouldNotCheck = false; }
    }

    private static void ProcessGuess(PlayerControl pc, MeetingHud meetingHud)
    {
        if (!HudManager.InstanceExists) return;
        HudManager hudManager = HudManager.Instance;
        SoundManager.Instance.PlaySound(pc.KillSfx, false, 0.8f);
        if (!Options.DisableKillAnimationOnGuess.GetBool()) hudManager.KillOverlay.ShowKillAnimation(pc.Data, pc.Data);

        if (pc.AmOwner)
        {
            hudManager.ShadowQuad.gameObject.SetActive(false);
            pc.cosmetics.nameText.GetComponent<MeshRenderer>().material.SetInt(Mask, 0);
            pc.RpcSetScanner(false);
            var importantTextTask = new GameObject("_Player").AddComponent<ImportantTextTask>();
            importantTextTask.transform.SetParent(AmongUsClient.Instance.transform, false);
            meetingHud.SetForegroundForDead();
        }

        PlayerVoteArea voteArea = MeetingHud.Instance.playerStates.First(x => x.TargetPlayerId == pc.PlayerId);

        if (voteArea.DidVote)
        {
            voteArea.UnsetVote();

            if (AmongUsClient.Instance.AmHost)
            {
                meetingHud.SetDirtyBit(1U);
                AmongUsClient.Instance.SendAllStreamedObjects();
                meetingHud.RpcClearVote(pc.OwnerId);
                meetingHud.SetDirtyBit(1U);
                AmongUsClient.Instance.SendAllStreamedObjects();
            }
        }

        voteArea.AmDead = true;
        voteArea.Overlay.gameObject.SetActive(true);
        voteArea.Overlay.color = Color.white;
        voteArea.XMark.gameObject.SetActive(true);
        voteArea.XMark.transform.localScale = Vector3.one;
    }

    public static void RpcClientGuess(PlayerControl pc)
    {
        try
        {
            var meetingHud = MeetingHud.Instance;
            ProcessGuess(pc, meetingHud);

            foreach (PlayerVoteArea playerVoteArea in meetingHud.playerStates)
            {
                if (playerVoteArea.VotedFor != pc.PlayerId) continue;

                playerVoteArea.UnsetVote();

                PlayerControl voteAreaPlayer = Utils.GetPlayerById(playerVoteArea.TargetPlayerId);
                if (!voteAreaPlayer.AmOwner) continue;

                meetingHud.ClearVote();
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static bool MsgToPlayerAndRole(string msg, out byte id, out CustomRoles role, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

        Regex r = new("\\d+");
        MatchCollection mc = r.Matches(msg);
        var result = string.Empty;
        for (var i = 0; i < mc.Count; i++) result += mc[i]; // The matching result is a complete number, and no splicing is required here.

        if (byte.TryParse(result, out byte num))
            id = num;
        else
        {
            id = byte.MaxValue;
            error = GetString("GuessHelp");
            role = new();
            return false;
        }

        // Determine whether the selected player is reasonable
        PlayerControl target = Utils.GetPlayerById(id);

        if (target == null)
        {
            error = GetString("GuessNull");
            role = new();
            return false;
        }

        if (!ChatCommands.GetRoleByName(msg, out role))
        {
            error = GetString("GuessHelp");
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void CreateGuesserButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates)
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);

            if (pc == null) continue;

            bool skip = PlayerControl.LocalPlayer.Is(CustomRoles.NecroGuesser) switch
            {
                true => pc.IsAlive() || pc.Is(CustomRoles.Gravestone) || (Options.SeeEjectedRolesInMeeting.GetBool() && Main.PlayerStates[pva.TargetPlayerId].deathReason == PlayerState.DeathReason.Vote),
                false => !pc.IsAlive()
            };

            if (skip) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.95f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.TargetIcon.png", 150f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            PlayerVoteArea pva1 = pva;
            button.OnClick.AddListener((Action)(() => GuesserOnClick(pva1.TargetPlayerId, __instance)));
        }
    }

    private static void GuesserSelectRole(CustomRoleTypes role, bool setPage = true)
    {
        CurrentTeamType = role;
        if (setPage) Page = 1;

        foreach (KeyValuePair<CustomRoleTypes, List<Transform>> roleButton in RoleButtons)
        {
            var index = 0;

            foreach (Transform roleBtn in roleButton.Value)
            {
                if (roleBtn == null) continue;
                index++;

                if (index <= (Page - 1) * 40 || Page * 40 < index)
                {
                    roleBtn.gameObject.SetActive(false);
                    continue;
                }

                roleBtn.gameObject.SetActive(roleButton.Key == role);
            }
        }

        foreach (KeyValuePair<CustomRoleTypes, SpriteRenderer> roleButton in RoleSelectButtons)
        {
            if (roleButton.Value == null) continue;
            roleButton.Value.color = new(0, 0, 0, roleButton.Key == role ? 1 : 0.25f);
        }
    }

    private static void GuesserOnClick(byte playerId, MeetingHud __instance)
    {
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || GuesserUI != null || MeetingHud.Instance.state is MeetingHud.VoteStates.Results or MeetingHud.VoteStates.Proceeding || Starspawn.IsDayBreak) return;

        try
        {
            Page = 1;
            RoleButtons = [];
            RoleSelectButtons = [];
            PageButtons = [];
            __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(false));

            Transform container = Object.Instantiate(GameObject.Find("PhoneUI").transform, __instance.transform);
            container.transform.localPosition = new Vector3(0, 0, -200f);
            GuesserUI = container.gameObject;

            List<int> i = [0, 0, 0, 0, 0];
            Transform buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
            Transform maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
            Transform smallButtonTemplate = __instance.playerStates[0].Buttons.transform.Find("CancelButton");
            TextTemplate.enabled = true;
            Transform roleTextMeeting = TextTemplate.transform.FindChild("RoleTextMeeting");
            if (roleTextMeeting != null) Object.Destroy(roleTextMeeting.gameObject);
            Transform deathReasonTextMeeting = TextTemplate.transform.FindChild("DeathReasonTextMeeting");
            if (deathReasonTextMeeting != null) Object.Destroy(deathReasonTextMeeting.gameObject);

            Transform exitButtonParent = new GameObject().transform;
            exitButtonParent.SetParent(container);
            Transform exitButton = Object.Instantiate(buttonTemplate, exitButtonParent);
            exitButton.FindChild("ControllerHighlight").gameObject.SetActive(false);
            Transform exitButtonMask = Object.Instantiate(maskTemplate, exitButtonParent);
            exitButtonMask.transform.localScale = new Vector3(2.88f, 0.8f, 1f);
            exitButtonMask.transform.localPosition = new Vector3(0f, 0f, 1f);
            exitButton.gameObject.GetComponent<SpriteRenderer>().sprite = smallButtonTemplate.GetComponent<SpriteRenderer>().sprite;
            exitButtonParent.transform.localPosition = new Vector3(3.88f, 2.12f, -200f);
            exitButtonParent.transform.localScale = new Vector3(0.22f, 0.9f, 1f);
            exitButtonParent.transform.SetAsFirstSibling();
            var passiveButton = exitButton.GetComponent<PassiveButton>();
            passiveButton.OnClick.RemoveAllListeners();
            passiveButton.OnClick.AddListener((Action)(() =>
            {
                __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                Object.Destroy(container.gameObject);
            }));


            List<Transform> buttons = [];
            Transform selectedButton = null;

            var tabCount = 0;

            for (var index = 0; index < 5; index++)
            {
                switch (PlayerControl.LocalPlayer.GetCustomRole(), index)
                {
                    case (CustomRoles.EvilGuesser, 1) when !Options.EGCanGuessImp.GetBool():
                    case (CustomRoles.EvilGuesser, 4) when !Options.EGCanGuessAdt.GetBool():
                    case (CustomRoles.NiceGuesser, 0) when !Options.GGCanGuessCrew.GetBool() && !PlayerControl.LocalPlayer.IsMadmate():
                    case (CustomRoles.NiceGuesser, 4) when !Options.GGCanGuessAdt.GetBool():
                        continue;
                }

                Transform teambuttonParent = new GameObject().transform;
                teambuttonParent.SetParent(container);
                Transform teambutton = Object.Instantiate(buttonTemplate, teambuttonParent);
                teambutton.FindChild("ControllerHighlight").gameObject.SetActive(false);
                Object.Instantiate(maskTemplate, teambuttonParent);
                TextMeshPro teamlabel = Object.Instantiate(TextTemplate, teambutton);
                var spriteRenderer = teambutton.GetComponent<SpriteRenderer>();
                spriteRenderer.sprite = CustomButton.Get("GuessPlate");
                RoleSelectButtons.Add((CustomRoleTypes)index, spriteRenderer);
                teambuttonParent.localPosition = new(-3.10f + (tabCount++ * 1.47f), 2.225f, -200);
                teambuttonParent.localScale = new(0.53f, 0.53f, 1f);

                teamlabel.color = (CustomRoleTypes)index switch
                {
                    CustomRoleTypes.Coven => Team.Coven.GetColor(),
                    CustomRoleTypes.Crewmate => new Color32(140, 255, 255, byte.MaxValue),
                    CustomRoleTypes.Impostor => new Color32(255, 25, 25, byte.MaxValue),
                    CustomRoleTypes.Neutral => new Color32(255, 171, 27, byte.MaxValue),
                    CustomRoleTypes.Addon => new Color32(255, 154, 206, byte.MaxValue),
                    _ => throw new ArgumentOutOfRangeException("The index is out of range, it's an invalid CustomRoleTypes (GuessManager.cs:GuesserOnClick method)", innerException: null)
                };

                Logger.Info(teamlabel.color.ToString(), ((CustomRoleTypes)index).ToString());
                teamlabel.text = GetString("Type" + (CustomRoleTypes)index);
                teamlabel.alignment = TextAlignmentOptions.Center;
                teamlabel.transform.localPosition = new Vector3(0, 0, teamlabel.transform.localPosition.z);
                teamlabel.transform.localScale *= 1.6f;
                teamlabel.autoSizeTextContainer = true;

                if (PlayerControl.LocalPlayer.IsAlive()) CreateTeamButton(teambutton, (CustomRoleTypes)index);
                continue;

                static void CreateTeamButton(Transform teambutton, CustomRoleTypes type)
                {
                    var passiveButton = teambutton.GetComponent<PassiveButton>();
                    passiveButton.OnClick.RemoveAllListeners();
                    passiveButton.OnClick.AddListener((UnityAction)(() =>
                    {
                        GuesserSelectRole(type);
                        ReloadPage();
                    }));
                }
            }

            static void ReloadPage()
            {
                PageButtons[0].color = new(1, 1, 1, 1f);
                PageButtons[1].color = new(1, 1, 1, 1f);

                if (RoleButtons.TryGetValue(CurrentTeamType, out List<Transform> roleButtons))
                {
                    if ((roleButtons.Count / MaxOneScreenRole) + (roleButtons.Count % MaxOneScreenRole != 0 ? 1 : 0) < Page)
                    {
                        Page -= 1;
                        PageButtons[1].color = new(1, 1, 1, 0.1f);
                    }
                    else if ((roleButtons.Count / MaxOneScreenRole) + (roleButtons.Count % MaxOneScreenRole != 0 ? 1 : 0) < Page + 1)
                        PageButtons[1].color = new(1, 1, 1, 0.1f);
                }

                if (Page <= 1)
                {
                    Page = 1;
                    PageButtons[0].color = new(1, 1, 1, 0.1f);
                }

                GuesserSelectRole(CurrentTeamType, false);
            }

            static void CreatePage(bool isNext, MeetingHud __instance, Transform container)
            {
                Transform buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
                Transform maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
                Transform pagebuttonParent = new GameObject().transform;
                pagebuttonParent.SetParent(container);
                Transform pagebutton = Object.Instantiate(buttonTemplate, pagebuttonParent);
                pagebutton.FindChild("ControllerHighlight").gameObject.SetActive(false);
                Object.Instantiate(maskTemplate, pagebuttonParent);
                TextMeshPro pagelabel = Object.Instantiate(TextTemplate, pagebutton);
                pagebutton.GetComponent<SpriteRenderer>().sprite = CustomButton.Get("GuessPlateKPD");
                pagebuttonParent.localPosition = isNext ? new(3.535f, -2.2f, -200) : new(-3.475f, -2.2f, -200);
                pagebuttonParent.localScale = new(0.55f, 0.55f, 1f);
                pagelabel.color = Color.white;
                pagelabel.text = GetString(isNext ? "NextPage" : "PreviousPage");
                pagelabel.alignment = TextAlignmentOptions.Center;
                pagelabel.transform.localPosition = new Vector3(0, 0, pagelabel.transform.localPosition.z);
                pagelabel.transform.localScale *= 1.6f;
                pagelabel.autoSizeTextContainer = true;
                if (!isNext && Page <= 1) pagebutton.GetComponent<SpriteRenderer>().color = new(1, 1, 1, 0.1f);
                var passiveButton = pagebutton.GetComponent<PassiveButton>();
                passiveButton.OnClick.RemoveAllListeners();
                passiveButton.OnClick.AddListener((Action)ClickEvent);
                PageButtons.Add(pagebutton.GetComponent<SpriteRenderer>());
                return;

                void ClickEvent()
                {
                    if (isNext) Page += 1;
                    else Page -= 1;

                    if (Page < 1) Page = 1;
                    ReloadPage();
                }
            }

            if (PlayerControl.LocalPlayer.IsAlive())
            {
                CreatePage(false, __instance, container);
                CreatePage(true, __instance, container);
            }

            foreach (CustomRoles role in Main.CustomRoleValues)
            {
                if (!ShowRoleOnUI(role)) continue;

                CreateRole(role);
            }

            void CreateRole(CustomRoles role)
            {
                CustomRoleTypes customRoleTypes = role.GetCustomRoleTypes();
                var customRoleTypesInt = (int)customRoleTypes;
                if (40 <= i[customRoleTypesInt]) i[customRoleTypesInt] = 0;
                Transform buttonParent = new GameObject().transform;
                buttonParent.SetParent(container);
                Transform button = Object.Instantiate(buttonTemplate, buttonParent);
                button.FindChild("ControllerHighlight").gameObject.SetActive(false);
                Object.Instantiate(maskTemplate, buttonParent);
                TextMeshPro label = Object.Instantiate(TextTemplate, button);

                button.GetComponent<SpriteRenderer>().sprite = CustomButton.Get("GuessPlate");

                if (!RoleButtons.ContainsKey(customRoleTypes))
                    RoleButtons.Add(customRoleTypes, []);

                RoleButtons[customRoleTypes].Add(button);
                buttons.Add(button);
                int row = i[customRoleTypesInt] / 5;
                int col = i[customRoleTypesInt] % 5;
                buttonParent.localPosition = new Vector3(-3.47f + (1.75f * col), 1.5f - (0.45f * row), -200f);
                buttonParent.localScale = new Vector3(0.55f, 0.55f, 1f);
                label.text = GetString(role.ToString());
                label.color = Utils.GetRoleColor(role);
                label.alignment = TextAlignmentOptions.Center;
                label.transform.localPosition = new Vector3(0, 0, label.transform.localPosition.z);
                label.transform.localScale *= 1.6f;
                label.autoSizeTextContainer = true;

                var component = button.GetComponent<PassiveButton>();
                component.OnClick.RemoveAllListeners();

                if (PlayerControl.LocalPlayer.IsAlive())
                {
                    component.OnClick.AddListener((Action)(() =>
                    {
                        if (selectedButton != button)
                        {
                            selectedButton = button;
                            buttons.ForEach(x => x.GetComponent<SpriteRenderer>().color = x == selectedButton ? Utils.GetRoleColor(PlayerControl.LocalPlayer.GetCustomRole()) : Main.DarkThemeForMeetingUI.Value ? new Color(0.1f, 0.1f, 0.1f) : Color.white);
                        }
                        else
                        {
                            if (MeetingHud.Instance.state is MeetingHud.VoteStates.Results or MeetingHud.VoteStates.Proceeding || !PlayerControl.LocalPlayer.IsAlive()) return;

                            Logger.Msg($"Click: {pc.GetNameWithRole()} => {role}", "Guesser UI");

                            if (AmongUsClient.Instance.AmHost) GuesserMsg(PlayerControl.LocalPlayer, $"/bt {playerId} {GetString(role.ToString())}", true);
                            else SendRPC(playerId, role);

                            // Reset the GUI
                            __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                            Object.Destroy(container.gameObject);
                            TextTemplate.enabled = false;
                        }
                    }));
                }

                i[customRoleTypesInt]++;
            }

            container.transform.localScale *= 0.75f;
            GuesserSelectRole(CustomRoleTypes.Neutral);
            ReloadPage();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Guesser UI");
            return;
        }

        CustomSoundsManager.Play("Gunload");
    }

    private static bool ShowRoleOnUI(CustomRoles role)
    {
        if (role is
                CustomRoles.GM or
                CustomRoles.Ankylosaurus or
                CustomRoles.BananaMan or
                CustomRoles.Car or
                CustomRoles.Disco or
                CustomRoles.Flash or
                CustomRoles.Giant or
                CustomRoles.LastImpostor or
                CustomRoles.NotAssigned or
                CustomRoles.Shifter or
                CustomRoles.Specter or
                CustomRoles.SuperStar
            )
            return false;

        if (role.IsForOtherGameMode()) return false;
        if (!role.IsEnable() && !role.RoleExist(true) && !CanMakeRoleSpawn(role)) return false;
        return Options.CurrentGameMode == CustomGameMode.Standard && !CustomHnS.AllHnSRoles.Contains(role) && !role.IsGhostRole() && !role.IsVanilla();

        bool CanMakeRoleSpawn(CustomRoles r)
        {
            Dictionary<CustomRoles, CustomRoles> d = new()
            {
                [CustomRoles.Pestilence] = CustomRoles.PlagueBearer,
                [CustomRoles.VengefulRomantic] = CustomRoles.Romantic,
                [CustomRoles.RuthlessRomantic] = CustomRoles.Romantic,
                [CustomRoles.Deathknight] = CustomRoles.Necromancer,
                [CustomRoles.Undead] = CustomRoles.Necromancer,
                [CustomRoles.Sidekick] = CustomRoles.Jackal,
                [CustomRoles.Charmed] = CustomRoles.Cultist,
                [CustomRoles.Contagious] = CustomRoles.Virus,
                [CustomRoles.Entranced] = CustomRoles.Siren
            };
            
            return d.TryGetValue(r, out var baseRole) && baseRole.RoleExist(true);
        }
    }

    // Modded non-host client guess Role/Add-on
    private static void SendRPC(int playerId, CustomRoles role)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (int)CustomRPC.Guess, SendOption.Reliable, AmongUsClient.Instance.HostId);
        writer.Write(playerId);
        writer.Write((int)role);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        Logger.Msg($"{pc.GetNameWithRole()}", "GuessManager - PlayerControl pc");

        int playerId = reader.ReadInt32();
        Logger.Msg($"{playerId}", "GuessManager - Player Id");

        var role = (CustomRoles)reader.ReadInt32();
        Logger.Msg($"{role}", "GuessManager - Role Int32");

        string roleStr = GetString(role.ToString());
        Logger.Msg($"{roleStr}", "GuessManager - Role String");

        GuesserMsg(pc, $"/bt {playerId} {roleStr}", true);
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        //[SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix(MeetingHud __instance)
        {
            bool restrictions = Options.GuesserNumRestrictions.GetBool();

            if (AmongUsClient.Instance.AmHost)
            {
                Main.GuesserGuessedMeeting.SetAllValues(0);

                if (Guessers.Count == 0 && restrictions)
                    InitializeGuesserPlayers();

                HashSet<byte> guessers = Main.EnumerateAlivePlayerControls().Where(x => !x.IsModdedClient() && CanGuess(x, restrictions)).Select(x => x.PlayerId).ToHashSet();
                bool meetingSS = Options.UseMeetingShapeshift.GetBool() && Options.UseMeetingShapeshiftForGuessing.GetBool();
                LateTask.New(() => guessers.Do(x => Utils.SendMessage(GetString(meetingSS ? "YouCanGuessMeetingSS" : "YouCanGuess"), x, GetString("YouCanGuessTitle"), importance: restrictions && MeetingStates.FirstMeeting ? MessageImportance.High : MessageImportance.Medium)), 12f, log: false);
                if (meetingSS) Data = guessers.ToDictionary(x => x, x => new MeetingShapeshiftData(x));
            }

            PlayerControl lp = PlayerControl.LocalPlayer;
            if (!lp.IsAlive()) return;

            if (CanGuess(lp, restrictions))
                CreateGuesserButton(__instance);
        }

        public static bool CanGuess(PlayerControl lp, bool restrictions)
        {
            if ((!Options.UseMeetingShapeshift.GetBool() || !Options.UseMeetingShapeshiftForGuessing.GetBool()) && Banshee.Instances.Exists(x => x.ScreechedPlayers.Contains(lp.PlayerId))) return false; // Vanilla clients can't guess with their chat hidden, so don't let modded clients guess for fairness
            return lp.Is(CustomRoles.Guesser) || lp.GetCustomRole() switch
            {
                CustomRoles.EvilGuesser => true,
                CustomRoles.NiceGuesser => true,
                CustomRoles.NecroGuesser => true,
                CustomRoles.Augur => true,
                CustomRoles.Doomsayer when !Doomsayer.CantGuess => true,
                CustomRoles.Decryptor when Decryptor.GuessMode.GetValue() == 2 => true,
                _ when Options.GuesserMode.GetBool() => lp.GetTeam() switch
                {
                    Team.Impostor => Options.ImpostorsCanGuess.GetBool(),
                    Team.Crewmate => Options.CrewmatesCanGuess.GetBool(),
                    Team.Neutral when lp.IsNeutralKiller() => Options.NeutralKillersCanGuess.GetBool(),
                    Team.Neutral => Options.PassiveNeutralsCanGuess.GetBool(),
                    Team.Coven => Options.CovenCanGuess.GetBool(),
                    _ => false
                } && !(restrictions && !Guessers.Contains(lp.PlayerId)),
                _ => false
            };
        }

        private static void InitializeGuesserPlayers()
        {
            Dictionary<Team, List<PlayerControl>> players = Main.EnumeratePlayerControls()
                .GroupBy(x => x.GetTeam())
                .ToDictionary(x => x.Key, x => x.Shuffle());

            foreach ((Team team, (OptionItem minSetting, OptionItem maxSetting)) in Options.NumGuessersOnEachTeam)
            {
                if (!players.TryGetValue(team, out List<PlayerControl> teamPlayers)) continue;
                int num = IRandom.Instance.Next(minSetting.GetInt(), maxSetting.GetInt() + 1);
                if (team == Team.Neutral) teamPlayers.Sort((x, y) => y.IsNeutralKiller().CompareTo(x.IsNeutralKiller()));
                Guessers.UnionWith(teamPlayers.Take(num).Select(x => x.PlayerId));
            }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
    private static class MeetingHudOnDestroyGuesserUIClose
    {
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix()
        {
            Object.Destroy(TextTemplate.gameObject);
        }
    }

    // ----------------------------------------------------------------------------------------
    // Meeting Shapeshift for Guessing
    // ----------------------------------------------------------------------------------------

    public class MeetingShapeshiftData(byte guesserId)
    {
        private enum State
        {
            WaitingForTargetSelection,
            TeamSelection,
            FirstLetterSelection,
            RoleSelection
        }

        private State CurrentState = State.WaitingForTargetSelection;
        private PlayerControl Target;
        private CustomRoleTypes CurrentTeam;
        private CustomRoles[] ShownRoles;
        private List<CustomRoles> CurrentRoles;
        private CustomRoles SelectedRole;
        private readonly List<ShapeshiftMenuElement> ExistingCNOs = [];
        private readonly Dictionary<uint, string> NetIdToRawDisplay = [];

        public void Reset()
        {
            try
            {
                if (CurrentState == State.WaitingForTargetSelection) return;
                CurrentState = State.WaitingForTargetSelection;
                Target = null;
                CurrentTeam = default(CustomRoleTypes);
                ExistingCNOs.Do(x => x.Despawn());
                ExistingCNOs.Clear();
                NetIdToRawDisplay.Clear();
                PlayerControl pc = guesserId.GetPlayer();
                if (pc != null) Utils.SendGameDataTo(pc.OwnerId);
                Logger.Msg($"Reset Meeting Shapeshift Menu For Guessing ({Main.AllPlayerNames.GetValueOrDefault(guesserId, "Someone")})", "Meeting Shapeshift For Guessing");
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        public void AdvanceStep(PlayerControl target)
        {
            try
            {
                Logger.Info($"Advancing Step ({Main.AllPlayerNames.GetValueOrDefault(guesserId, "Someone")}, from {CurrentState})", "Meeting Shapeshift For Guessing");
            
                switch (CurrentState)
                {
                    case State.WaitingForTargetSelection:
                    {
                        Target = target;
                        CurrentState = State.TeamSelection;
                        SpawnCNOs();
                        break;
                    }
                    case State.TeamSelection:
                    {
                        if (!TryGetDisplay(out string display)) return;

                        if (display == "Cancel")
                        {
                            Reset();
                            return;
                        }

                        CurrentTeam = Enum.Parse<CustomRoleTypes>(display, true);
                        ShownRoles = Main.CustomRoleValues.Where(x => x.GetCustomRoleTypes() == CurrentTeam && ShowRoleOnUI(x)).ToArray();
                        CurrentState = State.FirstLetterSelection;
                        SpawnCNOs();
                        break;
                    }
                    case State.FirstLetterSelection:
                    {
                        if (!TryGetDisplay(out string display)) return;

                        if (display == "Cancel")
                        {
                            Reset();
                            return;
                        }

                        CurrentRoles = ShownRoles.Select(x => (role: x, str: GetString(x.ToString()))).Where(x => display.Split('-').Any(y => x.str.StartsWith(y.Trim(), StringComparison.InvariantCultureIgnoreCase))).Select(x => x.role).ToList();

                        if (CurrentRoles.Count == 0)
                        {
                            Reset();
                            return;
                        }

                        if (CurrentRoles.Count == 1)
                        {
                            // Directly select if there's only one role
                            SelectedRole = CurrentRoles[0];
                            goto case State.RoleSelection;
                        }

                        CurrentState = State.RoleSelection;
                        SpawnCNOs();
                        break;
                    }
                    case State.RoleSelection:
                    {
                        if (SelectedRole == default(CustomRoles))
                        {
                            if (!TryGetDisplay(out string display)) return;

                            if (display == "Cancel")
                            {
                                Reset();
                                return;
                            }

                            SelectedRole = Enum.Parse<CustomRoles>(display, true);
                        }

                        GuesserMsg(guesserId.GetPlayer(), $"/bt {Target.PlayerId} {GetString(SelectedRole.ToString())}", ssMenu: true);
                        Reset();
                        break;
                    }
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }

            return;

            bool TryGetDisplay(out string display)
            {
                if (!NetIdToRawDisplay.TryGetValue(target.NetId, out display))
                    display = string.Empty;

                if (string.IsNullOrWhiteSpace(display))
                {
                    Reset();
                    return false;
                }

                Logger.Info($"Raw display choice: {display}", $"Meeting Shapeshift For Guessing ({Main.AllPlayerNames.GetValueOrDefault(guesserId, "Someone")})");
                return true;
            }
        }

        public void SpawnCNOs()
        {
            try
            {
                IEnumerable<string> choices = CurrentState switch
                {
                    State.TeamSelection => Enum.GetNames<CustomRoleTypes>(),
                    State.FirstLetterSelection => BuildLetterGroups(ShownRoles.Select(x => GetString(x.ToString())).OrderBy(x => x)),
                    State.RoleSelection => CurrentRoles.Select(x => x.ToString()),
                    _ => []
                };

                IEnumerable<string> namePlateIds = CurrentState switch
                {
                    State.TeamSelection => ["nameplate_ripple", "nameplate_seeker", "nameplate_Polus_Lava", "nameplate_Celeste", "nameplate0001"],
                    _ => Enumerable.Repeat(CurrentTeam switch
                    {
                        CustomRoleTypes.Impostor => "nameplate_seeker",
                        CustomRoleTypes.Crewmate => "nameplate_ripple",
                        CustomRoleTypes.Neutral => "nameplate_Polus_Lava",
                        CustomRoleTypes.Coven => "nameplate_Celeste",
                        CustomRoleTypes.Addon => "nameplate0001",
                        _ => ""
                    }, 14)
                };

                choices = choices.Prepend("Cancel");
                namePlateIds = namePlateIds.Prepend("nameplate_candyCanePlate");

                (string choice, string namePlateId)[] data = choices.Zip(namePlateIds, (choice, namePlateId) => (choice, namePlateId)).ToArray();
                var alivePlayerControls = Main.AllAlivePlayerControls;
                int alivePlayerControlsLength = alivePlayerControls.Count - 1;

                Logger.Info($"Set Up Meeting Shapeshift Menu For Guessing ({Main.AllPlayerNames.GetValueOrDefault(guesserId, "Someone")}, {CurrentState})", "Meeting Shapeshift For Guessing");

                // First, use living players to show choices by changing their names
                // The local player can't be used to show a choice (-1)

                StringBuilder sb = new();
                int textIndex = 0;

                int messages = 0;
                int packingLimit = AmongUsClient.Instance.GetMaxMessagePackingLimit();
                
                var skipped = false;
                PlayerControl guesser = guesserId.GetPlayer();
                MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                writer.StartMessage(6);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(guesser.OwnerId);

                for (var i = 0; i < alivePlayerControls.Count && (skipped ? i - 1 : i) < data.Length; i++)
                {
                    string choice = data[skipped ? i - 1 : i].choice;
                    string namePlateId = data[skipped ? i - 1 : i].namePlateId;
                    PlayerControl pc = alivePlayerControls[i];

                    if (pc.PlayerId == guesserId)
                    {
                        skipped = true;
                        continue;
                    }

                    NetIdToRawDisplay[pc.NetId] = choice;
                    string playerName = CurrentState == State.FirstLetterSelection ? choice : GetString(choice).ToUpper();
                
                    sb.Append($"[{playerName}]");
                    textIndex++;
                
                    if (textIndex % 3 == 0) sb.AppendLine();
                    else sb.Append(' ');

                    if (writer.Length > 500 || messages + 2 > packingLimit)
                    {
                        messages = 0;
                        writer.EndMessage();
                        AmongUsClient.Instance.SendOrDisconnect(writer);
                        writer.Clear(SendOption.Reliable);
                        writer.StartMessage(6);
                        writer.Write(AmongUsClient.Instance.GameId);
                        writer.WritePacked(guesser.OwnerId);
                    }
                
                    writer.StartMessage(2);
                    writer.WritePacked(pc.NetId);
                    writer.Write((byte)RpcCalls.SetName);
                    writer.Write(pc.Data.NetId);
                    writer.Write(playerName);
                    writer.Write(false);
                    writer.EndMessage();

                    writer.StartMessage(2);
                    writer.WritePacked(pc.NetId);
                    writer.Write((byte)RpcCalls.SetNamePlateStr);
                    writer.Write(namePlateId);
                    writer.Write(pc.GetNextRpcSequenceId(RpcCalls.SetNamePlateStr));
                    writer.EndMessage();

                    messages += 2;
                }

                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Recycle();

                // If there aren't enough living players, spawn new CNOs to show the rest of choices
            
                // Since CNOs use the local player's NetworkedPlayerInfo, and AU reads the player's name directly from it,
                // it's impossible to show vanilla players all choices accurately with CNOs.
                // No workaround found yet....
                // So we send the remaining choices in chat so the player can identify them

                if (data.Length >= alivePlayerControlsLength)
                {
                    for (int i = alivePlayerControlsLength; i < data.Length; i++)
                    {
                        string choice = data[i].choice;
                        //string namePlateId = data[i].namePlateId;
                    
                        sb.Append($"[{(CurrentState == State.FirstLetterSelection ? choice : GetString(choice).ToUpper())}]");
                        textIndex++;
                    
                        if (textIndex % 3 == 0) sb.AppendLine();
                        else sb.Append(' ');
                    
                        // If there's an existing CNO, reuse it
                        ShapeshiftMenuElement cno;

                        if (ExistingCNOs.Count + alivePlayerControlsLength > i)
                            cno = ExistingCNOs[i - alivePlayerControlsLength];
                        else
                        {
                            cno = new ShapeshiftMenuElement(guesserId);
                            ExistingCNOs.Add(cno);
                        }
                    
                        NetIdToRawDisplay[cno.playerControl.NetId] = choice;
                    }
                
                    // Despawn unused CNOs
                    for (int i = data.Length - alivePlayerControlsLength; i < ExistingCNOs.Count; i++)
                        ExistingCNOs[i].Despawn();
                
                    ExistingCNOs.RemoveRange(data.Length - alivePlayerControlsLength, ExistingCNOs.Count - (data.Length - alivePlayerControlsLength));
                
                    Logger.Info($"Sent {data.Length - alivePlayerControlsLength} CNOs, Reused {ExistingCNOs.Count} Existing CNOs", "Meeting Shapeshift For Guessing");
                }
                else
                {
                    ExistingCNOs.ForEach(x => x.Despawn());
                    ExistingCNOs.Clear();
                }
            
                Utils.SendMessage(sb.ToString().Trim(), guesserId, GetString($"ShapeshiftGuesserUITitle.{CurrentState}"), importance: MessageImportance.High);

                Logger.Info($"Spawned {ExistingCNOs.Count} CNOs, Used {alivePlayerControlsLength} Living Players, Showing {data.Length} Choices", "Meeting Shapeshift For Guessing");
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        // This problem goes beyond my ability to solve it perfectly, so I used AI
        // Even this solution is not perfect, but it should be good enough for most cases
        /// <summary>
        ///     Build up to maxGroups labels (like "[A-B]") from ordered roleNames so:
        ///     - each label covers contiguous starting-prefixes,
        ///     - no label contains more than maxItemsPerGroup roles,
        ///     - if a single starting-prefix has > maxItemsPerGroup roles, it will be subdivided by longer prefixes,
        ///     - tries to balance groups by merging adjacent buckets while respecting the maxItemsPerGroup limit.
        /// </summary>
        public static IEnumerable<string> BuildLetterGroups(
            IEnumerable<string> roleNamesOrdered,
            int maxGroups = 14,
            int maxItemsPerGroup = 14,
            CultureInfo culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            List<string> roles = roleNamesOrdered.ToList();
            if (roles.Count == 0) yield break;

            // Step 1: initial buckets grouped by first grapheme
            var orderedKeys = new List<string>(); // preserve appearance order
            var map = new Dictionary<string, List<string>>();

            foreach (string r in roles)
            {
                string key = GetPrefix(r, 1);

                if (!map.ContainsKey(key))
                {
                    orderedKeys.Add(key);
                    map[key] = [];
                }

                map[key].Add(r);
            }

            // Represent buckets as list of (prefix, rolesList)
            List<(string prefix, List<string> roles)> buckets = orderedKeys.Select(k => (prefix: k, roles: map[k])).ToList();

            // Step 2: For any bucket with count > maxItemsPerGroup, subdivide it by increasing prefix length
            for (var i = 0; i < buckets.Count; ++i)
            {
                if (buckets[i].roles.Count <= maxItemsPerGroup) continue;

                List<string> tooBigRoles = buckets[i].roles;
                var p = 2; // try second grapheme, third, ...

                while (true)
                {
                    var subOrder = new List<string>();
                    var subMap = new Dictionary<string, List<string>>();

                    foreach (string r in tooBigRoles)
                    {
                        string subKey = GetPrefix(r, p);

                        if (!subMap.ContainsKey(subKey))
                        {
                            subOrder.Add(subKey);
                            subMap[subKey] = [];
                        }

                        subMap[subKey].Add(r);
                    }

                    // If any sub-bucket still larger than maxItemsPerGroup, increase p and try again.
                    bool anyTooLarge = subMap.Values.Any(list => list.Count > maxItemsPerGroup);

                    if (!anyTooLarge)
                    {
                        // replace the single too-large bucket with its sub-buckets (in order)
                        var newList = new List<(string prefix, List<string> roles)>();
                        foreach (string k in subOrder) newList.Add((k, subMap[k]));
                        // replace in buckets
                        buckets.RemoveAt(i);
                        buckets.InsertRange(i, newList);
                        i += newList.Count - 1;
                        break;
                    }

                    p++;
                    // safeguard: if p grows beyond the longest role length, break to avoid infinite loop
                    int maxTextElements = tooBigRoles.Max(rr => StringInfo.ParseCombiningCharacters(rr).Length);

                    if (p > maxTextElements)
                    {
                        // As a last resort, split the list into chunks of maxItemsPerGroup preserving order
                        var finalSplit = new List<(string prefix, List<string> roles)>();
                        var idx = 0;

                        while (idx < tooBigRoles.Count)
                        {
                            List<string> slice = tooBigRoles.Skip(idx).Take(maxItemsPerGroup).ToList();
                            // prefix label use first and last role's prefix for clarity (not perfect but safe)
                            string label = GetPrefix(slice.First(), 1);
                            finalSplit.Add((label, slice));
                            idx += maxItemsPerGroup;
                        }

                        buckets.RemoveAt(i);
                        buckets.InsertRange(i, finalSplit);
                        i += finalSplit.Count - 1;
                        break;
                    }
                }
            }

            // Step 3: If we have more buckets than maxGroups, merge adjacent buckets where possible
            // We'll greedily merge the adjacent pair with smallest combined size that doesn't exceed maxItemsPerGroup,
            // repeating until buckets.Count <= maxGroups or no mergeable pair exists.
            while (buckets.Count > maxGroups)
            {
                int bestIdx = -1;
                var bestCombinedSize = int.MaxValue;

                for (var i = 0; i < buckets.Count - 1; ++i)
                {
                    int combined = buckets[i].roles.Count + buckets[i + 1].roles.Count;

                    if (combined <= maxItemsPerGroup && combined < bestCombinedSize)
                    {
                        bestCombinedSize = combined;
                        bestIdx = i;
                    }
                }

                if (bestIdx == -1)
                {
                    // No adjacent pair can be merged without exceeding maxItemsPerGroup.
                    // Absolutely diabolical.
                    // Here we choose to break and output as-is (caller gets <= buckets.Count labels, possibly > maxGroups).
                    break;
                }

                // merge buckets[bestIdx] and buckets[bestIdx+1]
                var mergedRoles = new List<string>(buckets[bestIdx].roles.Count + buckets[bestIdx + 1].roles.Count);
                mergedRoles.AddRange(buckets[bestIdx].roles);
                mergedRoles.AddRange(buckets[bestIdx + 1].roles);
                string mergedPrefix = buckets[bestIdx].prefix; // prefix string for merged block will be the first prefix (label will show range)
                buckets[bestIdx] = (mergedPrefix, mergedRoles);
                buckets.RemoveAt(bestIdx + 1);
            }

            // Step 4: build label strings for each bucket: if bucket covers multiple distinct prefixes, show "first-last" else show "first"
            // But we might have buckets whose prefix string is identical for every role (common case).
            foreach ((string prefix, List<string> roles) bucket in buckets)
            {
                // show all distinct prefixes in this bucket (e.g. "A-B-C" if roles are "Ant", "Bat", "Cat")
                List<string> distinctPrefixes = bucket.roles.Select(r => GetPrefix(r, 1)).Distinct().ToList();
                string label = distinctPrefixes.Count == 1 ? distinctPrefixes[0] : string.Join('-', distinctPrefixes);
                yield return label;
            }

            yield break;

            // Helper: get first p text elements (grapheme clusters), upper-cased by culture.
            string GetPrefix(string s, int p)
            {
                if (string.IsNullOrEmpty(s)) return s;
                var si = new StringInfo(s);
                int take = Math.Min(p, si.LengthInTextElements);
                return culture.TextInfo.ToUpper(si.SubstringByTextElements(0, take));
            }
        }
    }

    public static Dictionary<byte, MeetingShapeshiftData> Data = [];

    public static void OnMeetingShapeshiftReceived(PlayerControl shapeshifter, PlayerControl target)
    {
        if (Data.TryGetValue(shapeshifter.PlayerId, out MeetingShapeshiftData msd))
            msd.AdvanceStep(target);
    }

}
