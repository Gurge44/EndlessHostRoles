using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EHR.AddOns.Common;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
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
    private static int Page;
    private static GameObject GuesserUI;
    private static Dictionary<CustomRoleTypes, List<Transform>> RoleButtons;
    private static Dictionary<CustomRoleTypes, SpriteRenderer> RoleSelectButtons;
    private static List<SpriteRenderer> PageButtons;
    private static CustomRoleTypes CurrentTeamType;

    public static TextMeshPro TextTemplate;
    private static readonly int Mask = Shader.PropertyToID("_Mask");

    private static List<GameObject> IDPanels = [];

    public static string GetFormatString()
    {
        string text = GetString("PlayerIdList");
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            string id = pc.PlayerId.ToString();
            string name = pc.GetRealName();
            text += $"\n{id} → {name}";
        }

        return text;
    }

    private static bool CheckCommand(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
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

/*
    public static byte GetColorFromMsg(string msg)
    {
        if (ConfirmIncludeMsg(msg, "红|紅|red")) return 0;
        if (ConfirmIncludeMsg(msg, "蓝|藍|深蓝|blue")) return 1;
        if (ConfirmIncludeMsg(msg, "绿|綠|深绿|green")) return 2;
        if (ConfirmIncludeMsg(msg, "粉红|粉紅|pink")) return 3;
        if (ConfirmIncludeMsg(msg, "橘|橘|orange")) return 4;
        if (ConfirmIncludeMsg(msg, "黄|黃|yellow")) return 5;
        if (ConfirmIncludeMsg(msg, "黑|黑|black")) return 6;
        if (ConfirmIncludeMsg(msg, "白|白|white")) return 7;
        if (ConfirmIncludeMsg(msg, "紫|紫|perple")) return 8;
        if (ConfirmIncludeMsg(msg, "棕|棕|brown")) return 9;
        if (ConfirmIncludeMsg(msg, "青|青|cyan")) return 10;
        if (ConfirmIncludeMsg(msg, "黄绿|黃綠|浅绿|lime")) return 11;
        if (ConfirmIncludeMsg(msg, "红褐|紅褐|深红|maroon")) return 12;
        if (ConfirmIncludeMsg(msg, "玫红|玫紅|浅粉|rose")) return 13;
        if (ConfirmIncludeMsg(msg, "焦黄|焦黃|淡黄|banana")) return 14;
        if (ConfirmIncludeMsg(msg, "灰|灰|gray")) return 15;
        if (ConfirmIncludeMsg(msg, "茶|茶|tan")) return 16;
        if (ConfirmIncludeMsg(msg, "珊瑚|珊瑚|coral")) return 17;
        return byte.MaxValue;
    }

    private static bool ConfirmIncludeMsg(string msg, string key)
    {
        var keys = key.Split('|');
        return keys.Any(msg.Contains);
    }
*/

    public static bool GuesserMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsMeeting || pc == null) return false;
        if (!pc.Is(CustomRoles.NiceGuesser) && !pc.Is(CustomRoles.EvilGuesser) && !pc.Is(CustomRoles.Doomsayer) && !pc.Is(CustomRoles.Judge) && !pc.Is(CustomRoles.NiceSwapper) && !pc.Is(CustomRoles.Councillor) && !pc.Is(CustomRoles.Guesser) && !Options.GuesserMode.GetBool()) return false;

        int operate; // 1: ID, 2: Guess
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommand(ref msg, "shoot|guess|bet|st|bt|猜|赌", false)) operate = 2;
        else
        {
            Logger.Msg("Not a guessing command", "Msg Guesser");
            return false;
        }

        Logger.Msg(msg, "Msg Guesser");
        Logger.Msg($"{operate}", "Operate");

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GetFormatString(), pc.PlayerId);
                return true;
            case 2:
            {
                if (!pc.IsAlive())
                {
                    if (!isUI) Utils.SendMessage(GetString("GuessDead"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("GuessDead"));
                    return true;
                }

                if (pc.Is(CustomRoles.Lyncher) && Lyncher.GuessMode.GetValue() == 2) goto SkipCheck;

                if ((!pc.Is(CustomRoles.NiceGuesser) && pc.IsCrewmate() && !Options.CrewmatesCanGuess.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.Judge) && !pc.Is(CustomRoles.NiceSwapper)) ||
                    (!pc.Is(CustomRoles.EvilGuesser) && pc.IsImpostor() && !Options.ImpostorsCanGuess.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.Councillor)) ||
                    (pc.IsNeutralKiller() && !Options.NeutralKillersCanGuess.GetBool() && !pc.Is(CustomRoles.Guesser)) ||
                    (pc.GetCustomRole().IsNonNK() && !Options.PassiveNeutralsCanGuess.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.Doomsayer)) ||
                    (pc.Is(CustomRoles.Lyncher) && Lyncher.GuessMode.GetValue() == 0))
                {
                    if (!isUI) Utils.SendMessage(GetString("GuessNotAllowed"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("GuessNotAllowed"));
                    return true;
                }

                SkipCheck:

                if ((pc.Is(CustomRoles.NiceGuesser) && Options.GGTryHideMsg.GetBool()) ||
                    (pc.Is(CustomRoles.EvilGuesser) && Options.EGTryHideMsg.GetBool()) ||
                    (pc.Is(CustomRoles.Doomsayer) && Doomsayer.DoomsayerTryHideMsg.GetBool()) ||
                    (pc.Is(CustomRoles.Guesser) && Guesser.GTryHideMsg.GetBool()) || (Options.GuesserMode.GetBool() && Options.HideGuesserCommands.GetBool()))
                    ChatManager.SendPreviousMessagesToAll();
                else if (pc.AmOwner && !isUI) Utils.SendMessage(originMsg, 255, pc.GetRealName());

                if (!MsgToPlayerAndRole(msg, out byte targetId, out CustomRoles role, out string error))
                {
                    if (!isUI) Utils.SendMessage(error, pc.PlayerId);
                    else pc.ShowPopUp(error);
                    return true;
                }

                var target = Utils.GetPlayerById(targetId);
                if (target != null)
                {
                    bool guesserSuicide = false;
                    if (CopyCat.Instances.Any(x => x.CopyCatPC.PlayerId == pc.PlayerId))
                    {
                        if (!isUI) Utils.SendMessage(GetString("GuessDisabled"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("GuessDisabled"));
                        return true;
                    }

                    if (CustomTeamManager.AreInSameCustomTeam(pc.PlayerId, targetId) && !CustomTeamManager.IsSettingEnabledForPlayerTeam(targetId, CTAOption.GuessEachOther))
                    {
                        if (!isUI) Utils.SendMessage(GetString("GuessSameCTAPlayer"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("GuessSameCTAPlayer"));
                        return true;
                    }

                    Main.GuesserGuessed.TryAdd(pc.PlayerId, 0);

                    bool forceAllowGuess = role is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor or CustomRoles.Lovers && Lovers.GuessAbility.GetValue() == 2;

                    if (role == CustomRoles.Lovers && Lovers.GuessAbility.GetValue() == 0)
                    {
                        if (!isUI) Utils.SendMessage(GetString("GuessLovers"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("GuessLovers"));
                        return true;
                    }

                    switch (pc.GetCustomRole())
                    {
                        case CustomRoles.NiceGuesser when Main.GuesserGuessed[pc.PlayerId] >= Options.GGCanGuessTime.GetInt():
                            if (!isUI) Utils.SendMessage(GetString("GGGuessMax"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GGGuessMax"));
                            return true;
                        case CustomRoles.EvilGuesser when Main.GuesserGuessed[pc.PlayerId] >= Options.EGCanGuessTime.GetInt():
                            if (!isUI) Utils.SendMessage(GetString("EGGuessMax"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("EGGuessMax"));
                            return true;
                        case CustomRoles.Phantasm when !Options.PhantomCanGuess.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessDisabled"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessDisabled"));
                            return true;
                        case CustomRoles.Terrorist when !Options.TerroristCanGuess.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessDisabled"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessDisabled"));
                            return true;
                        case CustomRoles.Workaholic when !Workaholic.WorkaholicCanGuess.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessDisabled"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessDisabled"));
                            return true;
                        case CustomRoles.God when !Options.GodCanGuess.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessDisabled"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessDisabled"));
                            return true;
                        case CustomRoles.Monarch when role == CustomRoles.Knighted:
                            if (!isUI) Utils.SendMessage(GetString("GuessKnighted"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessKnighted"));
                            return true;
                        case CustomRoles.Executioner when Executioner.Target[pc.PlayerId] == target.PlayerId && Executioner.KnowTargetRole.GetBool() && !Executioner.CanGuessTarget.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessDisabled"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessDisabled"));
                            return true;
                        case CustomRoles.Doomsayer:
                            if (Doomsayer.CantGuess)
                            {
                                if (!isUI) Utils.SendMessage(GetString("DoomsayerCantGuess"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("DoomsayerCantGuess"));
                                return true;
                            }

                            if (role.IsImpostor() && !Doomsayer.DCanGuessImpostors.GetBool() && !forceAllowGuess)
                            {
                                if (!isUI) Utils.SendMessage(GetString("GuessNotAllowed"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("GuessNotAllowed"));
                                return true;
                            }

                            if (target.IsCrewmate() && !Doomsayer.DCanGuessCrewmates.GetBool() && !forceAllowGuess)
                            {
                                if (!isUI) Utils.SendMessage(GetString("GuessNotAllowed"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("GuessNotAllowed"));
                                return true;
                            }

                            if (role.IsNeutral() && !Doomsayer.DCanGuessNeutrals.GetBool())
                            {
                                if (!isUI) Utils.SendMessage(GetString("GuessNotAllowed"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("GuessNotAllowed"));
                                return true;
                            }

                            if (role.IsAdditionRole() && !Doomsayer.DCanGuessAdt.GetBool() && !forceAllowGuess)
                            {
                                if (!isUI) Utils.SendMessage(GetString("GuessAdtRole"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("GuessAdtRole"));
                                return true;
                            }

                            break;
                    }

                    if (Medic.ProtectList.Contains(target.PlayerId) && !Medic.GuesserIgnoreShield.GetBool())
                    {
                        if (!isUI) Utils.SendMessage(GetString("GuessShielded"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("GuessShielded"));
                        return true;
                    }

                    switch (role)
                    {
                        case CustomRoles.Crewmate or CustomRoles.CrewmateEHR when CrewmateVanillaRoles.VanillaCrewmateCannotBeGuessed.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessVanillaCrewmate"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessVanillaCrewmate"));
                            return true;
                        case CustomRoles.Workaholic when Workaholic.WorkaholicVisibleToEveryone.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessWorkaholic"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessWorkaholic"));
                            return true;
                        case CustomRoles.Doctor when Options.DoctorVisibleToEveryone.GetBool() && !target.HasEvilAddon():
                            if (!isUI) Utils.SendMessage(GetString("GuessDoctor"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessDoctor"));
                            return true;
                        case CustomRoles.Marshall when !Marshall.CanBeGuessedOnTaskCompletion.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessMarshallTask"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessMarshall"));
                            return true;
                        case CustomRoles.Monarch when pc.Is(CustomRoles.Knighted):
                            if (!isUI) Utils.SendMessage(GetString("GuessMonarch"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessMonarch"));
                            return true;
                        case CustomRoles.Mayor when Mayor.MayorRevealWhenDoneTasks.GetBool() && target.GetTaskState().IsTaskFinished:
                            if (!isUI) Utils.SendMessage(GetString("GuessMayor"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessMayor"));
                            return true;
                        case CustomRoles.Bait when Options.BaitNotification.GetBool():
                            if (!isUI) Utils.SendMessage(GetString("GuessNotifiedBait"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessNotifiedBait"));
                            return true;
                        case CustomRoles.Pestilence:
                            if (!isUI) Utils.SendMessage(GetString("GuessPestilence"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessPestilence"));
                            guesserSuicide = true;
                            break;
                        case CustomRoles.Phantasm:
                            if (!isUI) Utils.SendMessage(GetString("GuessPhantom"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessPhantom"));
                            return true;
                        case CustomRoles.Snitch when target.GetTaskState().RemainingTasksCount <= Snitch.RemainingTasksToBeFound:
                            if (!isUI) Utils.SendMessage(GetString("EGGuessSnitchTaskDone"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("EGGuessSnitchTaskDone"));
                            return true;
                        case CustomRoles.Merchant when Merchant.IsBribedKiller(pc, target):
                            if (!isUI) Utils.SendMessage(GetString("BribedByMerchant2"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("BribedByMerchant2"));
                            return true;
                        case CustomRoles.SuperStar:
                            if (!isUI) Utils.SendMessage(GetString("GuessSuperStar"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessSuperStar"));
                            return true;
                        case CustomRoles.DonutDelivery when DonutDelivery.IsUnguessable(pc, target):
                        case CustomRoles.Shifter:
                        case CustomRoles.Goose when !Goose.CanBeGuessed.GetBool():
                        case CustomRoles.Disco:
                        case CustomRoles.Glow:
                        case CustomRoles.LastImpostor:
                            if (!isUI) Utils.SendMessage(GetString("GuessObviousAddon"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("GuessObviousAddon"));
                            return true;
                        case CustomRoles.GM:
                            Utils.SendMessage(GetString("GuessGM"), pc.PlayerId);
                            return true;
                    }

                    if (target.Is(CustomRoles.Onbound))
                    {
                        if (!isUI) Utils.SendMessage(GetString("GuessOnbound"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("GuessOnbound"));

                        if (Onbound.GuesserSuicides.GetBool()) guesserSuicide = true;
                        else return true;
                    }

                    if (target.Is(CustomRoles.Lovers) && Lovers.GuessAbility.GetValue() == 0)
                    {
                        if (!isUI) Utils.SendMessage(GetString("GuessLovers"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("GuessLovers"));
                        return true;
                    }

                    if (Jailor.playerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == target.PlayerId))
                    {
                        if (!isUI) Utils.SendMessage(GetString("CantGuessJailed"), pc.PlayerId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                        else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")) + "\n" + GetString("CantGuessJailed"));
                        return true;
                    }

                    if (Jailor.playerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == pc.PlayerId && role != CustomRoles.Jailor))
                    {
                        if (!isUI) Utils.SendMessage(GetString("JailedCanOnlyGuessJailor"), pc.PlayerId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                        else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")) + "\n" + GetString("JailedCanOnlyGuessJailor"));
                        return true;
                    }

                    // Check whether add-on guessing is allowed
                    if (!forceAllowGuess)
                    {
                        switch (pc.GetCustomRole())
                        {
                            // Assassin Can't Guess Addons
                            case CustomRoles.EvilGuesser when role.IsAdditionRole() && !Options.EGCanGuessAdt.GetBool():
                                if (!isUI) Utils.SendMessage(GetString("GuessAdtRole"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("GuessAdtRole"));
                                return true;
                            // Nice Guesser Can't Guess Addons
                            case CustomRoles.NiceGuesser when role.IsAdditionRole() && !Options.GGCanGuessAdt.GetBool():
                                if (!isUI) Utils.SendMessage(GetString("GuessAdtRole"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("GuessAdtRole"));
                                return true;
                            // Guesser (add-on) Can't Guess Addons
                            default:
                                if (role.IsAdditionRole() && pc.Is(CustomRoles.Guesser) && !Guesser.GCanGuessAdt.GetBool())
                                {
                                    if (!isUI) Utils.SendMessage(GetString("GuessAdtRole"), pc.PlayerId);
                                    else pc.ShowPopUp(GetString("GuessAdtRole"));
                                    return true;
                                }

                                break;
                        }

                        // Guesser Mode Can/Can't Guess Addons
                        if (Options.GuesserMode.GetBool())
                        {
                            if (role.IsAdditionRole() && !Options.CanGuessAddons.GetBool())
                            {
                                // Impostors Can't Guess Addons
                                if (Options.ImpostorsCanGuess.GetBool() && pc.Is(CustomRoleTypes.Impostor) && !(pc.GetCustomRole() == CustomRoles.EvilGuesser || pc.Is(CustomRoles.Guesser)))
                                {
                                    if (!isUI) Utils.SendMessage(GetString("GuessAdtRole"), pc.PlayerId);
                                    else pc.ShowPopUp(GetString("GuessAdtRole"));
                                    return true;
                                }

                                // Crewmates Can't Guess Addons
                                if (Options.CrewmatesCanGuess.GetBool() && pc.Is(CustomRoleTypes.Crewmate) && !(pc.GetCustomRole() == CustomRoles.NiceGuesser || pc.Is(CustomRoles.Guesser)))
                                {
                                    if (!isUI) Utils.SendMessage(GetString("GuessAdtRole"), pc.PlayerId);
                                    else pc.ShowPopUp(GetString("GuessAdtRole"));
                                    return true;
                                }

                                // Neutrals Can't Guess Addons
                                if ((Options.NeutralKillersCanGuess.GetBool() || Options.PassiveNeutralsCanGuess.GetBool()) && pc.Is(CustomRoleTypes.Neutral) && !(pc.GetCustomRole() is CustomRoles.Ritualist or CustomRoles.Doomsayer || pc.Is(CustomRoles.Guesser)))
                                {
                                    if (!isUI) Utils.SendMessage(GetString("GuessAdtRole"), pc.PlayerId);
                                    else pc.ShowPopUp(GetString("GuessAdtRole"));
                                    return true;
                                }
                            }
                        }
                    }

                    if (pc.PlayerId == target.PlayerId)
                    {
                        if (!isUI) Utils.SendMessage(GetString("LaughToWhoGuessSelf"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromKPD")));
                        else pc.ShowPopUp(Utils.ColorString(Color.cyan, GetString("MessageFromKPD")) + "\n" + GetString("LaughToWhoGuessSelf"));

                        if (DoubleShot.CheckGuess(pc, isUI)) return true;
                        guesserSuicide = true;
                    }
                    else if (pc.Is(CustomRoles.NiceGuesser) && target.Is(CustomRoleTypes.Crewmate) && !Options.GGCanGuessCrew.GetBool() && !pc.Is(CustomRoles.Madmate))
                    {
                        if (DoubleShot.CheckGuess(pc, isUI)) return true;

                        guesserSuicide = true;
                        Logger.Msg($"{guesserSuicide}", "guesserSuicide1");
                    }
                    else if (pc.Is(CustomRoles.EvilGuesser) && target.Is(CustomRoleTypes.Impostor) && !Options.EGCanGuessImp.GetBool())
                    {
                        if (DoubleShot.CheckGuess(pc, isUI)) return true;

                        guesserSuicide = true;
                        Logger.Msg($"{guesserSuicide}", "guesserSuicide2");
                    }
                    else if (!target.Is(role))
                    {
                        if (DoubleShot.CheckGuess(pc, isUI)) return true;

                        guesserSuicide = true;
                        Logger.Msg($"{guesserSuicide}", "guesserSuicide3");
                    }

                    if (Options.GuesserDoesntDieOnMisguess.GetBool())
                    {
                        if (!isUI) Utils.SendMessage(GetString("MisguessButNoSuicide"), pc.PlayerId, Utils.ColorString(Color.yellow, GetString("MessageFromGurge44")));
                        else pc.ShowPopUp(Utils.ColorString(Color.yellow, GetString("MessageFromGurge44")) + "\n" + GetString("MisguessButNoSuicide"));
                        return true;
                    }


                    // -----------------------------------------------------------------------------------------------------------------------------------


                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} guessed {target.GetNameWithRole().RemoveHtmlTags()}", "Guesser");

                    var dp = guesserSuicide ? pc : target;
                    target = dp;

                    Logger.Info($"Player：{target.GetRealName().RemoveHtmlTags()} was guessed by {pc.GetRealName().RemoveHtmlTags()}", "Guesser");

                    Main.GuesserGuessed[pc.PlayerId]++;

                    if (pc.Is(CustomRoles.Doomsayer) && Doomsayer.AdvancedSettings.GetBool())
                    {
                        if (Doomsayer.GuessesCountPerMeeting >= Doomsayer.MaxNumberOfGuessesPerMeeting.GetInt() && pc.PlayerId != dp.PlayerId)
                        {
                            if (!isUI) Utils.SendMessage(GetString("DoomsayerCantGuess"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("DoomsayerCantGuess"));
                            return true;
                        }

                        Doomsayer.GuessesCountPerMeeting++;

                        if (Doomsayer.GuessesCountPerMeeting >= Doomsayer.MaxNumberOfGuessesPerMeeting.GetInt())
                            Doomsayer.CantGuess = true;

                        if (!Doomsayer.KillCorrectlyGuessedPlayers.GetBool() && pc.PlayerId != dp.PlayerId)
                        {
                            if (!isUI) Utils.SendMessage(GetString("DoomsayerCorrectlyGuessRole"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("DoomsayerCorrectlyGuessRole"));

                            if (Doomsayer.GuessedRoles.Contains(role))
                            {
                                LateTask.New(() => { Utils.SendMessage(GetString("DoomsayerGuessSameRoleAgainMsg"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doomsayer), GetString("DoomsayerGuessCountTitle"))); }, 0.7f, "Doomsayer Guess Same Role Again Msg");
                            }
                            else
                            {
                                Doomsayer.GuessingToWin[pc.PlayerId]++;
                                Doomsayer.SendRPC(pc);
                                Doomsayer.GuessedRoles.Add(role);

                                LateTask.New(() => { Utils.SendMessage(string.Format(GetString("DoomsayerGuessCountMsg"), Doomsayer.GuessingToWin[pc.PlayerId]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doomsayer), GetString("DoomsayerGuessCountTitle"))); }, 0.7f, "Doomsayer Guess Msg 1");
                            }

                            Doomsayer.CheckCountGuess(pc);

                            return true;
                        }

                        if (Doomsayer.DoesNotSuicideWhenMisguessing.GetBool() && pc.PlayerId == dp.PlayerId)
                        {
                            if (!isUI) Utils.SendMessage(GetString("DoomsayerNotCorrectlyGuessRole"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("DoomsayerNotCorrectlyGuessRole"));

                            if (Doomsayer.MisguessRolePrevGuessRoleUntilNextMeeting.GetBool())
                            {
                                Doomsayer.CantGuess = true;
                            }

                            return true;
                        }
                    }

                    string Name = dp.GetRealName();
                    if (!Options.DisableKillAnimationOnGuess.GetBool()) CustomSoundsManager.RPCPlayCustomSoundAll("Gunfire");

                    LateTask.New(() =>
                    {
                        Main.PlayerStates[dp.PlayerId].deathReason = PlayerState.DeathReason.Gambled;
                        dp.SetRealKiller(pc);
                        dp.RpcGuesserMurderPlayer();

                        if (dp.Is(CustomRoles.Medic))
                            Medic.IsDead(dp);

                        if (pc.Is(CustomRoles.Doomsayer) && pc.PlayerId != dp.PlayerId)
                        {
                            Doomsayer.GuessingToWin[pc.PlayerId]++;
                            Doomsayer.SendRPC(pc);

                            if (!Doomsayer.GuessedRoles.Contains(role))
                                Doomsayer.GuessedRoles.Add(role);

                            Doomsayer.CheckCountGuess(pc);
                        }

                        GuessManagerRole.OnGuess(dp, pc);

                        Utils.AfterPlayerDeathTasks(dp, true);

                        Utils.NotifyRoles(isForMeeting: GameStates.IsMeeting, NoCache: true);

                        LateTask.New(() => { Utils.SendMessage(string.Format(GetString("GuessKill"), Name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceGuesser), GetString("GuessKillTitle"))); }, 0.6f, "Guess Msg");

                        if (pc.Is(CustomRoles.Doomsayer) && pc.PlayerId != dp.PlayerId)
                        {
                            LateTask.New(() => { Utils.SendMessage(string.Format(GetString("DoomsayerGuessCountMsg"), Doomsayer.GuessingToWin[pc.PlayerId]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doomsayer), GetString("DoomsayerGuessCountTitle"))); }, 0.7f, "Doomsayer Guess Msg 2");
                        }
                    }, 0.2f, "Guesser Kill");
                }

                break;
            }
        }

        return true;
    }

    public static void RpcGuesserMurderPlayer(this PlayerControl pc /*, float delay = 0f*/)
    {
        // DEATH STUFF //
        try
        {
            GameEndChecker.ShouldNotCheck = true;
            var amOwner = pc.AmOwner;
            pc.Data.IsDead = true;
            pc.RpcExileV2();
            Main.PlayerStates[pc.PlayerId].SetDead();
            var meetingHud = MeetingHud.Instance;
            var hudManager = DestroyableSingleton<HudManager>.Instance;
            SoundManager.Instance.PlaySound(pc.KillSfx, false, 0.8f);
            if (!Options.DisableKillAnimationOnGuess.GetBool()) hudManager.KillOverlay.ShowKillAnimation(pc.Data, pc.Data);
            if (amOwner)
            {
                hudManager.ShadowQuad.gameObject.SetActive(false);
                pc.cosmetics.nameText.GetComponent<MeshRenderer>().material.SetInt(Mask, 0);
                pc.RpcSetScanner(false);
                ImportantTextTask importantTextTask = new GameObject("_Player").AddComponent<ImportantTextTask>();
                importantTextTask.transform.SetParent(AmongUsClient.Instance.transform, false);
                meetingHud.SetForegroundForDead();
            }

            PlayerVoteArea voteArea = MeetingHud.Instance.playerStates.First(
                x => x.TargetPlayerId == pc.PlayerId
            );
            if (voteArea.DidVote) voteArea.UnsetVote();
            voteArea.AmDead = true;
            voteArea.Overlay.gameObject.SetActive(true);
            voteArea.Overlay.color = Color.white;
            voteArea.XMark.gameObject.SetActive(true);
            voteArea.XMark.transform.localScale = Vector3.one;
            foreach (var playerVoteArea in meetingHud.playerStates)
            {
                if (playerVoteArea.VotedFor != pc.PlayerId) continue;
                playerVoteArea.UnsetVote();
                var voteAreaPlayer = Utils.GetPlayerById(playerVoteArea.TargetPlayerId);
                if (!voteAreaPlayer.AmOwner) continue;
                meetingHud.ClearVote();
            }

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.GuessKill, SendOption.Reliable);
            writer.Write(pc.PlayerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        finally
        {
            GameEndChecker.ShouldNotCheck = false;
        }
    }

    public static void RpcClientGuess(PlayerControl pc)
    {
        var amOwner = pc.AmOwner;
        var meetingHud = MeetingHud.Instance;
        var hudManager = DestroyableSingleton<HudManager>.Instance;
        SoundManager.Instance.PlaySound(pc.KillSfx, false, 0.8f);
        if (!Options.DisableKillAnimationOnGuess.GetBool()) hudManager.KillOverlay.ShowKillAnimation(pc.Data, pc.Data);
        if (amOwner)
        {
            hudManager.ShadowQuad.gameObject.SetActive(false);
            pc.cosmetics.nameText.GetComponent<MeshRenderer>().material.SetInt(Mask, 0);
            pc.RpcSetScanner(false);
            ImportantTextTask importantTextTask = new GameObject("_Player").AddComponent<ImportantTextTask>();
            importantTextTask.transform.SetParent(AmongUsClient.Instance.transform, false);
            meetingHud.SetForegroundForDead();
        }

        PlayerVoteArea voteArea = MeetingHud.Instance.playerStates.First(
            x => x.TargetPlayerId == pc.PlayerId
        );
        if (voteArea.DidVote) voteArea.UnsetVote();
        voteArea.AmDead = true;
        voteArea.Overlay.gameObject.SetActive(true);
        voteArea.Overlay.color = Color.white;
        voteArea.XMark.gameObject.SetActive(true);
        voteArea.XMark.transform.localScale = Vector3.one;
        foreach (var playerVoteArea in meetingHud.playerStates)
        {
            if (playerVoteArea.VotedFor != pc.PlayerId) continue;
            playerVoteArea.UnsetVote();
            var voteAreaPlayer = Utils.GetPlayerById(playerVoteArea.TargetPlayerId);
            if (!voteAreaPlayer.AmOwner) continue;
            meetingHud.ClearVote();
        }
    }

    public static bool MsgToPlayerAndRole(string msg, out byte id, out CustomRoles role, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

        Regex r = new("\\d+");
        MatchCollection mc = r.Matches(msg);
        string result = string.Empty;
        for (int i = 0; i < mc.Count; i++)
        {
            result += mc[i]; // The matching result is a complete number, and no splicing is required here.
        }

        if (byte.TryParse(result, out byte num))
        {
            id = num;
        }
        else
        {
            // It is not the player number, it determines whether it is the color or not.
            //byte color = GetColorFromMsg(msg);
            // Okay, I don’t know how to get the color of a certain player. I’ll fill it in later.
            id = byte.MaxValue;
            error = GetString("GuessHelp");
            role = new();
            return false;
        }

        // Determine whether the selected player is reasonable
        PlayerControl target = Utils.GetPlayerById(id);
        if (target == null || target.Data.IsDead)
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
        foreach (var pva in __instance.playerStates)
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;
            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.95f, 0.03f, -1.31f);
            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = CustomButton.Get("TargetIcon");
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            var pva1 = pva;
            button.OnClick.AddListener((Action)(() => GuesserOnClick(pva1.TargetPlayerId, __instance)));
        }
    }

    public static void CreateIDLabels(MeetingHud __instance)
    {
        DestroyIDLabels();
        const int max = 2;
        foreach (var pva in __instance.playerStates)
        {
            var levelDisplay = pva.transform.FindChild("PlayerLevel").gameObject;
            var panel = Object.Instantiate(levelDisplay, pva.transform, true);
            var panelTransform = panel.transform;
            var background = panel.GetComponent<SpriteRenderer>();
            background.color = Palette.Purple;
            background.sortingOrder = max - 1;
            panelTransform.SetAsFirstSibling();
            panelTransform.localPosition = new(-1.21f, -0.05f, 0f);
            var levelLabel = panelTransform.FindChild("LevelLabel").GetComponents<TextMeshPro>()[0];
            levelLabel.DestroyTranslator();
            levelLabel.text = "ID";
            levelLabel.sortingOrder = max;
            var levelNumber = panelTransform.FindChild("LevelNumber").GetComponent<TextMeshPro>();
            levelNumber.text = pva.TargetPlayerId.ToString();
            levelNumber.sortingOrder = max;
            IDPanels.Add(panel);
        }
    }

    public static void DestroyIDLabels()
    {
        IDPanels.ForEach(Object.Destroy);
        IDPanels = [];
    }

    static void GuesserSelectRole(CustomRoleTypes Role, bool SetPage = true)
    {
        CurrentTeamType = Role;
        if (SetPage) Page = 1;
        foreach (var RoleButton in RoleButtons)
        {
            int index = 0;
            foreach (var RoleBtn in RoleButton.Value)
            {
                if (RoleBtn == null) continue;
                index++;
                if (index <= (Page - 1) * 40)
                {
                    RoleBtn.gameObject.SetActive(false);
                    continue;
                }

                if ((Page * 40) < index)
                {
                    RoleBtn.gameObject.SetActive(false);
                    continue;
                }

                RoleBtn.gameObject.SetActive(RoleButton.Key == Role);
            }
        }

        foreach (var RoleButton in RoleSelectButtons)
        {
            if (RoleButton.Value == null) continue;
            RoleButton.Value.color = new(0, 0, 0, RoleButton.Key == Role ? 1 : 0.25f);
        }
    }

    static void GuesserOnClick(byte playerId, MeetingHud __instance)
    {
        var pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || GuesserUI != null || !GameStates.IsVoting) return;

        try
        {
            Page = 1;
            RoleButtons = [];
            RoleSelectButtons = [];
            PageButtons = [];
            __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(false));

            Transform container = Object.Instantiate(GameObject.Find("PhoneUI").transform, __instance.transform);
            container.transform.localPosition = new(0, 0, -200f);
            GuesserUI = container.gameObject;

            List<int> i = [0, 0, 0, 0];
            var buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
            var maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
            var smallButtonTemplate = __instance.playerStates[0].Buttons.transform.Find("CancelButton");
            TextTemplate.enabled = true;
            if (TextTemplate.transform.FindChild("RoleTextMeeting") != null) Object.Destroy(TextTemplate.transform.FindChild("RoleTextMeeting").gameObject);

            Transform exitButtonParent = new GameObject().transform;
            exitButtonParent.SetParent(container);
            Transform exitButton = Object.Instantiate(buttonTemplate, exitButtonParent);
            exitButton.FindChild("ControllerHighlight").gameObject.SetActive(false);
            Transform exitButtonMask = Object.Instantiate(maskTemplate, exitButtonParent);
            exitButtonMask.transform.localScale = new(2.88f, 0.8f, 1f);
            exitButtonMask.transform.localPosition = new(0f, 0f, 1f);
            exitButton.gameObject.GetComponent<SpriteRenderer>().sprite = smallButtonTemplate.GetComponent<SpriteRenderer>().sprite;
            exitButtonParent.transform.localPosition = new(3.88f, 2.12f, -200f);
            exitButtonParent.transform.localScale = new(0.22f, 0.9f, 1f);
            exitButtonParent.transform.SetAsFirstSibling();
            exitButton.GetComponent<PassiveButton>().OnClick.RemoveAllListeners();
            exitButton.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() =>
            {
                __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                Object.Destroy(container.gameObject);
            }));
            exitButton.GetComponent<PassiveButton>();

            List<Transform> buttons = [];
            Transform selectedButton = null;

            int tabCount = 0;
            for (int index = 0; index < 4; index++)
            {
                if (PlayerControl.LocalPlayer.Is(CustomRoles.EvilGuesser))
                {
                    if (!Options.EGCanGuessImp.GetBool() && index == 1) continue;
                    if (!Options.EGCanGuessAdt.GetBool() && index == 3) continue;
                }
                else if (PlayerControl.LocalPlayer.Is(CustomRoles.NiceGuesser))
                {
                    if (!Options.GGCanGuessCrew.GetBool() && index == 0) continue;
                    if (!Options.GGCanGuessAdt.GetBool() && index == 3) continue;
                }
                else if (PlayerControl.LocalPlayer.Is(CustomRoles.Doomsayer))
                {
                    if (!Doomsayer.DCanGuessCrewmates.GetBool() && index == 0) continue;
                    if (!Doomsayer.DCanGuessImpostors.GetBool() && index == 1) continue;
                    if (!Doomsayer.DCanGuessNeutrals.GetBool() && index == 2) continue;
                    if (!Doomsayer.DCanGuessAdt.GetBool() && index == 3) continue;
                }
                else if (PlayerControl.LocalPlayer.Is(CustomRoles.Guesser))
                {
                    // if (!Options.GCanGuessCrew.GetBool() && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Crewmate) && index == 0) continue;
                    // if (!Options.GCanGuessImp.GetBool() && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && index == 1) continue;
                    if (!Guesser.GCanGuessAdt.GetBool() && index == 3) continue;
                }
                else if (Options.GuesserMode.GetBool())
                {
                    if (!Options.CrewCanGuessCrew.GetBool() && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Crewmate) && index == 0) continue;
                    if (!Options.ImpCanGuessImp.GetBool() && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && index == 1) continue;
                    //    if (index == 2) continue;
                    if (!Options.CanGuessAddons.GetBool() && index == 3) continue;
                }

                Transform TeambuttonParent = new GameObject().transform;
                TeambuttonParent.SetParent(container);
                Transform Teambutton = Object.Instantiate(buttonTemplate, TeambuttonParent);
                Teambutton.FindChild("ControllerHighlight").gameObject.SetActive(false);
                Object.Instantiate(maskTemplate, TeambuttonParent);
                TextMeshPro Teamlabel = Object.Instantiate(TextTemplate, Teambutton);
                Teambutton.GetComponent<SpriteRenderer>().sprite = CustomButton.Get("GuessPlateWithKPD");
                RoleSelectButtons.Add((CustomRoleTypes)index, Teambutton.GetComponent<SpriteRenderer>());
                TeambuttonParent.localPosition = new(-2.75f + (tabCount++ * 1.73f), 2.225f, -200);
                TeambuttonParent.localScale = new(0.53f, 0.53f, 1f);
                Teamlabel.color = (CustomRoleTypes)index switch
                {
                    CustomRoleTypes.Crewmate => new(140, 255, 255, byte.MaxValue),
                    CustomRoleTypes.Impostor => new(255, 25, 25, byte.MaxValue),
                    CustomRoleTypes.Neutral => new(255, 171, 27, byte.MaxValue),
                    CustomRoleTypes.Addon => new Color32(255, 154, 206, byte.MaxValue),
                    _ => throw new NotImplementedException()
                };
                Logger.Info(Teamlabel.color.ToString(), ((CustomRoleTypes)index).ToString());
                Teamlabel.text = GetString("Type" + ((CustomRoleTypes)index));
                Teamlabel.alignment = TextAlignmentOptions.Center;
                Teamlabel.transform.localPosition = new(0, 0, Teamlabel.transform.localPosition.z);
                Teamlabel.transform.localScale *= 1.6f;
                Teamlabel.autoSizeTextContainer = true;

                if (PlayerControl.LocalPlayer.IsAlive()) CreateTeamButton(Teambutton, (CustomRoleTypes)index);
                continue;

                static void CreateTeamButton(Component Teambutton, CustomRoleTypes type)
                {
                    Teambutton.GetComponent<PassiveButton>().OnClick.AddListener((UnityAction)(() =>
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
                if ((RoleButtons[CurrentTeamType].Count / MaxOneScreenRole + (RoleButtons[CurrentTeamType].Count % MaxOneScreenRole != 0 ? 1 : 0)) < Page)
                {
                    Page -= 1;
                    PageButtons[1].color = new(1, 1, 1, 0.1f);
                }
                else if ((RoleButtons[CurrentTeamType].Count / MaxOneScreenRole + (RoleButtons[CurrentTeamType].Count % MaxOneScreenRole != 0 ? 1 : 0)) < Page + 1)
                {
                    PageButtons[1].color = new(1, 1, 1, 0.1f);
                }

                if (Page <= 1)
                {
                    Page = 1;
                    PageButtons[0].color = new(1, 1, 1, 0.1f);
                }

                GuesserSelectRole(CurrentTeamType, false);
            }

            static void CreatePage(bool IsNext, MeetingHud __instance, Transform container)
            {
                var buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
                var maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
                __instance.playerStates[0].Buttons.transform.Find("CancelButton");
                Transform PagebuttonParent = new GameObject().transform;
                PagebuttonParent.SetParent(container);
                Transform Pagebutton = Object.Instantiate(buttonTemplate, PagebuttonParent);
                Pagebutton.FindChild("ControllerHighlight").gameObject.SetActive(false);
                Object.Instantiate(maskTemplate, PagebuttonParent);
                TextMeshPro Pagelabel = Object.Instantiate(TextTemplate, Pagebutton);
                Pagebutton.GetComponent<SpriteRenderer>().sprite = CustomButton.Get("GuessPlateWithKPD");
                PagebuttonParent.localPosition = IsNext ? new(3.535f, -2.2f, -200) : new(-3.475f, -2.2f, -200);
                PagebuttonParent.localScale = new(0.55f, 0.55f, 1f);
                Pagelabel.color = Color.white;
                Pagelabel.text = GetString(IsNext ? "NextPage" : "PreviousPage");
                Pagelabel.alignment = TextAlignmentOptions.Center;
                Pagelabel.transform.localPosition = new(0, 0, Pagelabel.transform.localPosition.z);
                Pagelabel.transform.localScale *= 1.6f;
                Pagelabel.autoSizeTextContainer = true;
                if (!IsNext && Page <= 1) Pagebutton.GetComponent<SpriteRenderer>().color = new(1, 1, 1, 0.1f);
                Pagebutton.GetComponent<PassiveButton>().OnClick.AddListener((Action)(ClickEvent));

                PageButtons.Add(Pagebutton.GetComponent<SpriteRenderer>());
                return;

                void ClickEvent()
                {
                    if (IsNext) Page += 1;
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

            var sortedRoles = Enum.GetValues<CustomRoles>().OrderBy(x => GetString($"{x}")).ToArray();
            foreach (var role in sortedRoles)
            {
                if (role is CustomRoles.GM
                    or CustomRoles.SpeedBooster
                    or CustomRoles.Engineer
                    or CustomRoles.Crewmate
                    or CustomRoles.Oblivious
                    or CustomRoles.Scientist
                    or CustomRoles.Impostor
                    or CustomRoles.Shapeshifter
                    or CustomRoles.Flashman
                    or CustomRoles.Disco
                    or CustomRoles.Giant
                    or CustomRoles.NotAssigned
                    or CustomRoles.KB_Normal
                    or CustomRoles.Paranoia
                    or CustomRoles.SuperStar
                    or CustomRoles.Konan
                    or CustomRoles.Oblivious
                    or CustomRoles.GuardianAngelEHR
                   ) continue;

                if (!role.IsEnable() && !role.RoleExist(countDead: true) && !role.IsConverted()) continue;
                if (Options.CurrentGameMode != CustomGameMode.Standard || HnSManager.AllHnSRoles.Contains(role)) continue;

                CreateRole(role);
            }

            void CreateRole(CustomRoles role)
            {
                if (40 <= i[(int)role.GetCustomRoleTypes()]) i[(int)role.GetCustomRoleTypes()] = 0;
                Transform buttonParent = new GameObject().transform;
                buttonParent.SetParent(container);
                Transform button = Object.Instantiate(buttonTemplate, buttonParent);
                button.FindChild("ControllerHighlight").gameObject.SetActive(false);
                Object.Instantiate(maskTemplate, buttonParent);
                TextMeshPro label = Object.Instantiate(TextTemplate, button);
                button.GetComponent<SpriteRenderer>().sprite = CustomButton.Get("GuessPlate");
                if (!RoleButtons.ContainsKey(role.GetCustomRoleTypes()))
                {
                    RoleButtons.Add(role.GetCustomRoleTypes(), []);
                }

                RoleButtons[role.GetCustomRoleTypes()].Add(button);
                buttons.Add(button);
                int row = i[(int)role.GetCustomRoleTypes()] / 5;
                int col = i[(int)role.GetCustomRoleTypes()] % 5;
                buttonParent.localPosition = new(-3.47f + 1.75f * col, 1.5f - 0.45f * row, -200f);
                buttonParent.localScale = new(0.55f, 0.55f, 1f);
                label.text = GetString(role.ToString());
                label.color = Utils.GetRoleColor(role);
                label.alignment = TextAlignmentOptions.Center;
                label.transform.localPosition = new(0, 0, label.transform.localPosition.z);
                label.transform.localScale *= 1.6f;
                label.autoSizeTextContainer = true;
                _ = i[(int)role.GetCustomRoleTypes()];

                button.GetComponent<PassiveButton>().OnClick.RemoveAllListeners();
                if (PlayerControl.LocalPlayer.IsAlive())
                    button.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() =>
                    {
                        if (selectedButton != button)
                        {
                            selectedButton = button;
                            buttons.ForEach(x => x.GetComponent<SpriteRenderer>().color = x == selectedButton ? Utils.GetRoleColor(PlayerControl.LocalPlayer.GetCustomRole()) : Color.white);
                        }
                        else
                        {
                            if (!(__instance.state == MeetingHud.VoteStates.Voted || __instance.state == MeetingHud.VoteStates.NotVoted) || !PlayerControl.LocalPlayer.IsAlive()) return;

                            Logger.Msg($"Click: {pc.GetNameWithRole().RemoveHtmlTags()} => {role}", "Guesser UI");

                            if (AmongUsClient.Instance.AmHost) GuesserMsg(PlayerControl.LocalPlayer, $"/bt {playerId} {GetString(role.ToString())}", true);
                            else SendRPC(playerId, role);

                            // Reset the GUI
                            __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                            Object.Destroy(container.gameObject);
                            TextTemplate.enabled = false;
                        }
                    }));
                i[(int)role.GetCustomRoleTypes()]++;
            }

            container.transform.localScale *= 0.75f;
            GuesserSelectRole(CustomRoleTypes.Crewmate);
            ReloadPage();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Guesser UI");
            return;
        }

        PlayerControl.LocalPlayer.RPCPlayCustomSound("Gunload");
    }

    // Modded non-host client guess role/add-on
    private static void SendRPC(int playerId, CustomRoles role)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (int)CustomRPC.Guess, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write((int)role);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        Logger.Msg($"{reader}", "MessageReader reader");
        Logger.Msg($"{pc}", "PlayerControl pc");

        int PlayerId = reader.ReadInt32();
        Logger.Msg($"{PlayerId}", "Player Id");

        CustomRoles role = (CustomRoles)reader.ReadInt32();
        Logger.Msg($"{role}", "Role Int32");
        Logger.Msg($"{GetString(role.ToString())}", "Role String");

        GuesserMsg(pc, $"/bt {PlayerId} {GetString(role.ToString())}", true);
    }

    /*
        public static void TryHideMsg()
        {
            ChatUpdatePatch.DoBlockChat = true;
            List<CustomRoles> roles = Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x is not CustomRoles.NotAssigned and not CustomRoles.KB_Normal).ToList();
            var rd = IRandom.Instance;
            string msg = Utils.EmptyMessage();
            string[] command = ["bet", "bt", "guess", "gs", "shoot", "st", "赌", "猜", "审判", "tl", "判", "审"];
            var x = Main.AllAlivePlayerControls;
            var totalAlive = Main.AllAlivePlayerControls.Length;
            for (int i = 0; i < 20; i++)
            {
                //msg = "/";
                //if (rd.Next(1, 100) < 20)
                //{
                //    msg += "id";
                //}
                //else
                //{
                //    msg += command[rd.Next(0, command.Length - 1)];
                //    msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                //    msg += rd.Next(0, 15).ToString();
                //    msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                //    CustomRoles role = roles[rd.Next(0, roles.Count)];
                //    msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                //    msg += Utils.GetRoleName(role);
                //}
                var player = x[rd.Next(0, totalAlive)];
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
                var writer = CustomRpcSender.Create("MessagesToSend");
                writer.StartMessage();
                writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
                    .Write(msg)
                    .EndRpc();
                writer.EndMessage();
                writer.SendMessage();
            }

            ChatUpdatePatch.DoBlockChat = false;
        }
    */

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            var lp = PlayerControl.LocalPlayer;
            bool alive = lp.IsAlive();
            if (Options.GuesserMode.GetBool())
            {
                CustomRoles role = lp.GetCustomRole();
                if (alive && role.IsImpostor() && Options.ImpostorsCanGuess.GetBool())
                    CreateGuesserButton(__instance);
                else if (role is CustomRoles.EvilGuesser && !Options.ImpostorsCanGuess.GetBool())
                    CreateGuesserButton(__instance);

                if (alive && lp.IsCrewmate() && Options.CrewmatesCanGuess.GetBool())
                    CreateGuesserButton(__instance);
                else if (role is CustomRoles.NiceGuesser && !Options.CrewmatesCanGuess.GetBool())
                    CreateGuesserButton(__instance);

                if (alive && lp.IsNeutralKiller() && Options.NeutralKillersCanGuess.GetBool())
                    CreateGuesserButton(__instance);
                if (alive && role.IsNonNK() && Options.PassiveNeutralsCanGuess.GetBool())
                    CreateGuesserButton(__instance);
                else if (role is CustomRoles.Doomsayer && !Options.PassiveNeutralsCanGuess.GetBool() && !Doomsayer.CantGuess)
                    CreateGuesserButton(__instance);
            }
            else
            {
                if (alive && lp.Is(CustomRoles.EvilGuesser))
                    CreateGuesserButton(__instance);

                if (alive && lp.Is(CustomRoles.NiceGuesser))
                    CreateGuesserButton(__instance);

                if (alive && lp.Is(CustomRoles.Doomsayer) && !Doomsayer.CantGuess)
                    CreateGuesserButton(__instance);

                if (alive && lp.Is(CustomRoles.Guesser))
                    CreateGuesserButton(__instance);
            }

            CreateIDLabels(__instance);
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
    class MeetingHudOnDestroyGuesserUIClose
    {
        public static void Postfix()
        {
            Object.Destroy(TextTemplate.gameObject);
        }
    }
}