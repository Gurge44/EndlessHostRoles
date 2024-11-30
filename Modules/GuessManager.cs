using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EHR.AddOns.Common;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static EHR.Translator;


namespace EHR
{
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

        public static string GetFormatString()
        {
            string text = GetString("PlayerIdList");

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                var id = pc.PlayerId.ToString();
                string name = pc.GetRealName();
                text += $"\n{id} → {name}";
            }

            return text;
        }

        private static bool CheckCommand(ref string msg, string command, bool exact = true)
        {
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
            string originMsg = msg;

            if (!AmongUsClient.Instance.AmHost) return false;

            if (!GameStates.IsMeeting || pc == null) return false;

            if (pc.GetCustomRole() is not (CustomRoles.NiceGuesser or CustomRoles.EvilGuesser or CustomRoles.Doomsayer or CustomRoles.Judge or CustomRoles.NiceSwapper or CustomRoles.Councillor or CustomRoles.NecroGuesser) && !pc.Is(CustomRoles.Guesser) && !Options.GuesserMode.GetBool()) return false;

            int operate; // 1: ID, 2: Guess
            msg = msg.ToLower().TrimStart().TrimEnd();

            if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id"))
                operate = 1;
            else if (CheckCommand(ref msg, "shoot|guess|bet|st|bt|猜|赌", false))
                operate = 2;
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
                        if (!isUI)
                            Utils.SendMessage(GetString("GuessDead"), pc.PlayerId);
                        else
                            pc.ShowPopUp(GetString("GuessDead"));

                        return true;
                    }

                    if (pc.Is(CustomRoles.Lyncher) && Lyncher.GuessMode.GetValue() == 2) goto SkipCheck;

                    if ((!pc.Is(CustomRoles.NiceGuesser) && pc.IsCrewmate() && !Options.CrewmatesCanGuess.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.Judge) && !pc.Is(CustomRoles.NiceSwapper)) ||
                        (!pc.Is(CustomRoles.EvilGuesser) && pc.IsImpostor() && !Options.ImpostorsCanGuess.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.Councillor)) ||
                        (pc.IsNeutralKiller() && !Options.NeutralKillersCanGuess.GetBool() && !pc.Is(CustomRoles.Guesser)) ||
                        (pc.GetCustomRole().IsNonNK() && !Options.PassiveNeutralsCanGuess.GetBool() && !pc.Is(CustomRoles.Guesser) && pc.GetCustomRole() is not CustomRoles.Doomsayer and not CustomRoles.NecroGuesser) ||
                        (pc.Is(CustomRoles.Lyncher) && Lyncher.GuessMode.GetValue() == 0))
                    {
                        if ((pc.Is(CustomRoles.Madmate) || pc.IsConverted()) && Options.BetrayalAddonsCanGuess.GetBool()) goto SkipCheck;

                        ShowMessage("GuessNotAllowed");
                        return true;
                    }

                    SkipCheck:

                    if (pc.GetCustomRole() is CustomRoles.Lyncher or CustomRoles.NecroGuesser ||
                        (pc.Is(CustomRoles.NiceGuesser) && Options.GGTryHideMsg.GetBool()) ||
                        (pc.Is(CustomRoles.EvilGuesser) && Options.EGTryHideMsg.GetBool()) ||
                        (pc.Is(CustomRoles.Doomsayer) && Doomsayer.DoomsayerTryHideMsg.GetBool()) ||
                        (pc.Is(CustomRoles.Guesser) && Guesser.GTryHideMsg.GetBool()) || (Options.GuesserMode.GetBool() && Options.HideGuesserCommands.GetBool()))
                        ChatManager.SendPreviousMessagesToAll();
                    else if (pc.AmOwner && !isUI) Utils.SendMessage(originMsg, 255, pc.GetRealName());

                    if (!MsgToPlayerAndRole(msg, out byte targetId, out CustomRoles role, out string error))
                    {
                        ShowMessage(error);
                        return true;
                    }

                    PlayerControl target = Utils.GetPlayerById(targetId);

                    if (target != null)
                    {
                        Main.GuesserGuessed.TryAdd(pc.PlayerId, 0);

                        var guesserSuicide = false;

                        switch (pc.Is(CustomRoles.NecroGuesser))
                        {
                            case true when (target.IsAlive() || target.Is(CustomRoles.Gravestone) || (Options.SeeEjectedRolesInMeeting.GetBool() && Main.PlayerStates[targetId].deathReason == PlayerState.DeathReason.Vote)):
                                ShowMessage(target.IsAlive() ? "NecroGuesser.TargetAliveError" : "NecroGuesser.TargetRevealedError");
                                return true;
                            case false when !target.IsAlive():
                                ShowMessage("GuessNull");
                                return true;
                        }

                        if (CopyCat.Instances.Any(x => x.CopyCatPC.PlayerId == pc.PlayerId))
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
                                CustomRoles.Recruit => Options.RecruitCanBeGuessed,
                                CustomRoles.Contagious => Options.ContagiousCanBeGuessed,
                                CustomRoles.Undead => Options.UndeadCanBeGuessed,
                                CustomRoles.Egoist => Options.EgoistCanBeGuessed,
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

                        switch (pc.GetCustomRole())
                        {
                            case CustomRoles.NiceGuesser when Main.GuesserGuessed[pc.PlayerId] >= Options.GGCanGuessTime.GetInt():
                                ShowMessage("GGGuessMax");
                                return true;
                            case CustomRoles.EvilGuesser when Main.GuesserGuessed[pc.PlayerId] >= Options.EGCanGuessTime.GetInt():
                                ShowMessage("EGGuessMax");
                                return true;
                            case CustomRoles.Shifter when !Shifter.CanGuess.GetBool():
                            case CustomRoles.Phantasm when !Options.PhantomCanGuess.GetBool():
                            case CustomRoles.Terrorist when !Options.TerroristCanGuess.GetBool():
                            case CustomRoles.Workaholic when !Workaholic.WorkaholicCanGuess.GetBool():
                            case CustomRoles.God when !God.GodCanGuess.GetBool():
                            case CustomRoles.Executioner when Executioner.Target[pc.PlayerId] == target.PlayerId && Executioner.KnowTargetRole.GetBool() && !Executioner.CanGuessTarget.GetBool():
                                ShowMessage("GuessDisabled");
                                return true;
                            case CustomRoles.Monarch when role == CustomRoles.Knighted:
                                ShowMessage("GuessKnighted");
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
                                        ((role.IsNeutral() || target.IsNeutralKiller()) && !Doomsayer.DCanGuessNeutrals.GetBool()))
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
                            case CustomRoles.Phantasm:
                                ShowMessage("GuessPhantom");
                                return true;
                            case CustomRoles.Snitch when target.GetTaskState().RemainingTasksCount <= Snitch.RemainingTasksToBeFound:
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
                            case CustomRoles.Eraser when Eraser.ErasedPlayers.Contains(target.PlayerId) && pc.Is(CustomRoles.Eraser):
                            case CustomRoles.NiceEraser when NiceEraser.ErasedPlayers.Contains(target.PlayerId) && pc.Is(CustomRoles.NiceEraser):
                                ShowMessage("GuessErased");
                                return true;
                            case CustomRoles.Tank when !Tank.CanBeGuessed.GetBool():
                                ShowMessage("GuessTank");
                                return true;
                            case CustomRoles.DonutDelivery when DonutDelivery.IsUnguessable(pc, target):
                            case CustomRoles.Shifter:
                            case CustomRoles.Car:
                            case CustomRoles.Goose when !Goose.CanBeGuessed.GetBool():
                            case CustomRoles.Disco:
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
                            ShowMessage("GuessOnbound");

                            if (Onbound.GuesserSuicides.GetBool())
                            {
                                if (DoubleShot.CheckGuess(pc, isUI)) return true;
                                guesserSuicide = true;
                            }
                            else return true;
                        }

                        if (target.Is(CustomRoles.Lovers) && Lovers.GuessAbility.GetValue() == 0)
                        {
                            ShowMessage("GuessLovers");
                            return true;
                        }

                        if (Jailor.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == target.PlayerId))
                        {
                            if (!isUI) Utils.SendMessage(GetString("CantGuessJailed"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                            else pc.ShowPopUp($"{Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle"))}\n{GetString("CantGuessJailed")}");

                            return true;
                        }

                        if (Jailor.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == pc.PlayerId && role != CustomRoles.Jailor))
                        {
                            if (!isUI) Utils.SendMessage(GetString("JailedCanOnlyGuessJailor"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                            else pc.ShowPopUp($"{Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle"))}\n{GetString("JailedCanOnlyGuessJailor")}");

                            return true;
                        }

                        if (Markseeker.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Markseeker { TargetRevealed: true } ms && ms.MarkedId == target.PlayerId))
                        {
                            ShowMessage("GuessMarkseekerTarget");
                            return true;
                        }

                        // Check whether add-on guessing is allowed
                        if (!forceAllowGuess)
                        {
                            switch (pc.GetCustomRole())
                            {
                                // Assassin & Nice Guesser Can't Guess Addons
                                case CustomRoles.EvilGuesser when role.IsAdditionRole() && !Options.EGCanGuessAdt.GetBool():
                                case CustomRoles.NiceGuesser when role.IsAdditionRole() && !Options.GGCanGuessAdt.GetBool():
                                    ShowMessage("GuessAdtRole");
                                    return true;
                                // Guesser (add-on) Can't Guess Addons
                                default:
                                    if (role.IsAdditionRole() && pc.Is(CustomRoles.Guesser) && !Guesser.GCanGuessAdt.GetBool())
                                    {
                                        ShowMessage("GuessAdtRole");
                                        return true;
                                    }

                                    break;
                            }

                            // Guesser Mode Can/Can't Guess Addons
                            if (Options.GuesserMode.GetBool())
                            {
                                if (role.IsAdditionRole() && !Options.CanGuessAddons.GetBool())
                                {
                                    if ((Options.ImpostorsCanGuess.GetBool() && pc.Is(CustomRoleTypes.Impostor) && !(pc.GetCustomRole() == CustomRoles.EvilGuesser || pc.Is(CustomRoles.Guesser))) ||
                                        (Options.CrewmatesCanGuess.GetBool() && pc.Is(CustomRoleTypes.Crewmate) && !(pc.GetCustomRole() == CustomRoles.NiceGuesser || pc.Is(CustomRoles.Guesser))) ||
                                        ((Options.NeutralKillersCanGuess.GetBool() || Options.PassiveNeutralsCanGuess.GetBool()) && pc.Is(CustomRoleTypes.Neutral) && !(pc.GetCustomRole() is CustomRoles.Ritualist or CustomRoles.Doomsayer || pc.Is(CustomRoles.Guesser))))
                                    {
                                        ShowMessage("GuessAdtRole");
                                        return true;
                                    }
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
                        else if (pc.Is(CustomRoles.NiceGuesser) && target.IsCrewmate() && !Options.GGCanGuessCrew.GetBool() && !pc.Is(CustomRoles.Madmate))
                        {
                            if (!isUI) Utils.SendMessage(GetString("GuessCrewRole"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromGurge44")));
                            else pc.ShowPopUp($"{Utils.ColorString(Color.cyan, GetString("MessageFromGurge44"))}\n{GetString("GuessCrewRole")}");

                            return true;
                        }
                        else if (pc.Is(CustomRoles.EvilGuesser) && target.IsImpostor() && !Options.EGCanGuessImp.GetBool())
                        {
                            if (!isUI) Utils.SendMessage(GetString("GuessImpRole"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromGurge44")));
                            else pc.ShowPopUp($"{Utils.ColorString(Color.cyan, GetString("MessageFromGurge44"))}\n{GetString("GuessImpRole")}");

                            return true;
                        }
                        else if (!target.Is(role))
                        {
                            if (DoubleShot.CheckGuess(pc, isUI)) return true;

                            guesserSuicide = true;
                            Logger.Msg($"{guesserSuicide}", "guesserSuicide3");
                        }

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

                        Main.GuesserGuessed[pc.PlayerId]++;

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

                            if (Doomsayer.GuessesCountPerMeeting >= Doomsayer.MaxNumberOfGuessesPerMeeting.GetInt()) Doomsayer.CantGuess = true;

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

                        string Name = dp.GetRealName();
                        if (!Options.DisableKillAnimationOnGuess.GetBool()) CustomSoundsManager.RPCPlayCustomSoundAll("Gunfire");

                        LateTask.New(() =>
                        {
                            Main.PlayerStates[dp.PlayerId].deathReason = PlayerState.DeathReason.Gambled;
                            dp.SetRealKiller(pc);
                            dp.RpcGuesserMurderPlayer();

                            if (dp.Is(CustomRoles.Medic)) Medic.IsDead(dp);

                            if (pc.Is(CustomRoles.Doomsayer) && pc.PlayerId != dp.PlayerId)
                            {
                                Doomsayer.GuessingToWin[pc.PlayerId]++;
                                Doomsayer.SendRPC(pc);

                                if (!Doomsayer.GuessedRoles.Contains(role)) Doomsayer.GuessedRoles.Add(role);

                                Doomsayer.CheckCountGuess(pc);
                            }

                            GuessManagerRole.OnGuess(dp, pc);

                            Utils.AfterPlayerDeathTasks(dp, true);
                            Utils.NotifyRoles(GameStates.IsMeeting, NoCache: true);

                            LateTask.New(() => { Utils.SendMessage(string.Format(GetString("GuessKill"), Name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceGuesser), GetString("GuessKillTitle"))); }, 0.6f, "Guess Msg");

                            if (pc.Is(CustomRoles.Doomsayer) && pc.PlayerId != dp.PlayerId) LateTask.New(() => Utils.SendMessage(string.Format(GetString("DoomsayerGuessCountMsg"), Doomsayer.GuessingToWin[pc.PlayerId]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doomsayer), GetString("DoomsayerGuessCountTitle"))), 0.7f, "Doomsayer Guess Msg 2");
                            if (pc.Is(CustomRoles.TicketsStealer) && pc.PlayerId != dp.PlayerId) LateTask.New(() => Utils.SendMessage(string.Format(GetString("TicketsStealerGetTicket"), (int)(Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == pc.PlayerId) * Options.TicketsPerKill.GetFloat()))), 0.7f, log: false);
                            if (pc.Is(CustomRoles.Pickpocket) && pc.PlayerId != dp.PlayerId) LateTask.New(() => Utils.SendMessage(string.Format(GetString("PickpocketGetVote"), (int)(Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == pc.PlayerId) * Pickpocket.VotesPerKill.GetFloat()))), 0.7f, log: false);
                        }, 0.2f, "Guesser Kill");

                        if (guesserSuicide && pc.IsLocalPlayer())
                            Achievements.Type.BadLuckOrBadObservation.Complete();
                    }

                    break;
                }
            }

            return true;

            void ShowMessage(string str)
            {
                if (!isUI) Utils.SendMessage(GetString(str), pc.PlayerId);
                else pc.ShowPopUp(GetString(str));
            }
        }

        public static void RpcGuesserMurderPlayer(this PlayerControl pc /*, float delay = 0f*/)
        {
            // DEATH STUFF //
            try
            {
                GameEndChecker.ShouldNotCheck = true;
                pc.Data.IsDead = true;
                pc.RpcExileV2();
                Main.PlayerStates[pc.PlayerId].SetDead();

                var meetingHud = MeetingHud.Instance;
                ProcessGuess(pc, meetingHud);

                foreach (PlayerVoteArea playerVoteArea in meetingHud.playerStates)
                {
                    if (playerVoteArea.VotedFor != pc.PlayerId) continue;

                    playerVoteArea.UnsetVote();

                    PlayerControl voteAreaPlayer = Utils.GetPlayerById(playerVoteArea.TargetPlayerId);

                    if (!voteAreaPlayer.AmOwner) meetingHud.RpcClearVote(voteAreaPlayer.GetClientId());
                    else meetingHud.ClearVote();
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

        private static void ProcessGuess(PlayerControl pc, MeetingHud meetingHud)
        {
            HudManager hudManager = DestroyableSingleton<HudManager>.Instance;
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

            if (voteArea.DidVote) voteArea.UnsetVote();

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
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
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
                if (pc == null || !pc.IsAlive()) continue;

                GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
                GameObject targetBox = Object.Instantiate(template, pva.transform);
                targetBox.name = "ShootButton";
                targetBox.transform.localPosition = new(-0.95f, 0.03f, -1.31f);
                var renderer = targetBox.GetComponent<SpriteRenderer>();
                renderer.sprite = CustomButton.Get("TargetIcon");
                var button = targetBox.GetComponent<PassiveButton>();
                button.OnClick.RemoveAllListeners();
                PlayerVoteArea pva1 = pva;
                button.OnClick.AddListener((Action)(() => GuesserOnClick(pva1.TargetPlayerId, __instance)));
            }
        }

        private static void GuesserSelectRole(CustomRoleTypes Role, bool SetPage = true)
        {
            CurrentTeamType = Role;
            if (SetPage) Page = 1;

            foreach (KeyValuePair<CustomRoleTypes, List<Transform>> RoleButton in RoleButtons)
            {
                var index = 0;

                foreach (Transform RoleBtn in RoleButton.Value)
                {
                    if (RoleBtn == null) continue;

                    index++;

                    if (index <= (Page - 1) * 40)
                    {
                        RoleBtn.gameObject.SetActive(false);
                        continue;
                    }

                    if (Page * 40 < index)
                    {
                        RoleBtn.gameObject.SetActive(false);
                        continue;
                    }

                    RoleBtn.gameObject.SetActive(RoleButton.Key == Role);
                }
            }

            foreach (KeyValuePair<CustomRoleTypes, SpriteRenderer> RoleButton in RoleSelectButtons)
            {
                if (RoleButton.Value == null) continue;

                RoleButton.Value.color = new(0, 0, 0, RoleButton.Key == Role ? 1 : 0.25f);
            }
        }

        private static void GuesserOnClick(byte playerId, MeetingHud __instance)
        {
            PlayerControl pc = Utils.GetPlayerById(playerId);
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
                Transform buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
                Transform maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
                Transform smallButtonTemplate = __instance.playerStates[0].Buttons.transform.Find("CancelButton");
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

                var tabCount = 0;

                for (var index = 0; index < 4; index++)
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
                        if (!Guesser.GCanGuessAdt.GetBool() && index == 3) continue;
                    }
                    else if (Options.GuesserMode.GetBool() && !PlayerControl.LocalPlayer.Is(CustomRoles.Guesser) && (!PlayerControl.LocalPlayer.Is(CustomRoles.Lyncher) || Lyncher.GuessMode.GetValue() != 2))
                    {
                        if (!Options.CrewCanGuessCrew.GetBool() && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Crewmate) && index == 0) continue;
                        if (!Options.ImpCanGuessImp.GetBool() && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && index == 1) continue;
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
                    Teamlabel.text = GetString("Type" + (CustomRoleTypes)index);
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

                    if ((RoleButtons[CurrentTeamType].Count / MaxOneScreenRole) + (RoleButtons[CurrentTeamType].Count % MaxOneScreenRole != 0 ? 1 : 0) < Page)
                    {
                        Page -= 1;
                        PageButtons[1].color = new(1, 1, 1, 0.1f);
                    }
                    else if ((RoleButtons[CurrentTeamType].Count / MaxOneScreenRole) + (RoleButtons[CurrentTeamType].Count % MaxOneScreenRole != 0 ? 1 : 0) < Page + 1) PageButtons[1].color = new(1, 1, 1, 0.1f);

                    if (Page <= 1)
                    {
                        Page = 1;
                        PageButtons[0].color = new(1, 1, 1, 0.1f);
                    }

                    GuesserSelectRole(CurrentTeamType, false);
                }

                static void CreatePage(bool IsNext, MeetingHud __instance, Transform container)
                {
                    Transform buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
                    Transform maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
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

                    Pagebutton.GetComponent<PassiveButton>().OnClick.AddListener((Action)ClickEvent);

                    PageButtons.Add(Pagebutton.GetComponent<SpriteRenderer>());
                    return;

                    void ClickEvent()
                    {
                        if (IsNext)
                            Page += 1;
                        else
                            Page -= 1;

                        if (Page < 1) Page = 1;

                        ReloadPage();
                    }
                }

                if (PlayerControl.LocalPlayer.IsAlive())
                {
                    CreatePage(false, __instance, container);
                    CreatePage(true, __instance, container);
                }

                CustomRoles[] sortedRoles = Enum.GetValues<CustomRoles>().OrderBy(x => GetString($"{x}")).ToArray();

                foreach (CustomRoles role in sortedRoles)
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
                        or CustomRoles.GuardianAngelEHR
                       )
                        continue;

                    if (!role.IsEnable() && !role.RoleExist(true) && !role.IsConverted()) continue;

                    if (!CustomGameMode.Standard.IsActiveOrIntegrated() || HnSManager.AllHnSRoles.Contains(role)) continue;

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
                    if (!RoleButtons.ContainsKey(role.GetCustomRoleTypes())) RoleButtons.Add(role.GetCustomRoleTypes(), []);

                    RoleButtons[role.GetCustomRoleTypes()].Add(button);
                    buttons.Add(button);
                    int row = i[(int)role.GetCustomRoleTypes()] / 5;
                    int col = i[(int)role.GetCustomRoleTypes()] % 5;
                    buttonParent.localPosition = new(-3.47f + (1.75f * col), 1.5f - (0.45f * row), -200f);
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
                    {
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

                                if (AmongUsClient.Instance.AmHost)
                                    GuesserMsg(PlayerControl.LocalPlayer, $"/bt {playerId} {GetString(role.ToString())}", true);
                                else
                                    SendRPC(playerId, role);

                                // Reset the GUI
                                __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                                Object.Destroy(container.gameObject);
                                TextTemplate.enabled = false;
                            }
                        }));
                    }

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
            Logger.Msg($"{reader}", "GuessManager - MessageReader reader");
            Logger.Msg($"{pc}", "GuessManager - PlayerControl pc");

            int PlayerId = reader.ReadInt32();
            Logger.Msg($"{PlayerId}", "GuessManager - Player Id");

            var role = (CustomRoles)reader.ReadInt32();
            Logger.Msg($"{role}", "GuessManager - Role Int32");
            Logger.Msg($"{GetString(role.ToString())}", "GuessManager - Role String");

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
        private static class StartMeetingPatch
        {
            public static void Postfix(MeetingHud __instance)
            {
                PlayerControl lp = PlayerControl.LocalPlayer;
                if (!lp.IsAlive()) return;

                bool canGuess = lp.Is(CustomRoles.Guesser) || lp.GetCustomRole() switch
                {
                    CustomRoles.EvilGuesser => true,
                    CustomRoles.NiceGuesser => true,
                    CustomRoles.NecroGuesser => true,
                    CustomRoles.Doomsayer when !Doomsayer.CantGuess => true,
                    CustomRoles.Lyncher when Lyncher.GuessMode.GetValue() == 2 => true,
                    _ when Options.GuesserMode.GetBool() => lp.GetTeam() switch
                    {
                        Team.Impostor => Options.ImpostorsCanGuess.GetBool(),
                        Team.Crewmate => Options.CrewmatesCanGuess.GetBool(),
                        Team.Neutral when lp.IsNeutralKiller() => Options.NeutralKillersCanGuess.GetBool(),
                        Team.Neutral => Options.PassiveNeutralsCanGuess.GetBool(),
                        _ => false
                    },
                    _ => false
                };

                if (canGuess) CreateGuesserButton(__instance);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
        private static class MeetingHudOnDestroyGuesserUIClose
        {
            public static void Postfix()
            {
                Object.Destroy(TextTemplate.gameObject);
            }
        }
    }
}