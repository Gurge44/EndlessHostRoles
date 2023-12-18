using HarmonyLib;
using Il2CppSystem.Text;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
class HudManagerPatch
{
    //public static bool ShowDebugText;
    //public static int LastCallNotifyRolesPerSecond;
    public static int NowCallNotifyRolesCount;
    public static int LastSetNameDesyncCount;
    //public static int LastFPS;
    //public static int NowFrameCount;
    //public static float FrameRateTimer;
    public static TextMeshPro LowerInfoText;
    private static TextMeshPro OverriddenRolesText;
    //public static GameObject TempLowerInfoText;
    public static void Postfix(HudManager __instance)
    {
        if (!GameStates.IsModHost) return;
        var player = PlayerControl.LocalPlayer;
        if (player == null) return;
        //壁抜け
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if ((!AmongUsClient.Instance.IsGameStarted || !GameStates.IsOnlineGame)
                && player.CanMove)
            {
                player.Collider.offset = new Vector2(0f, 127f);
            }
        }
        //壁抜け解除
        if (player.Collider.offset.y == 127f)
        {
            if (!Input.GetKey(KeyCode.LeftControl) || (AmongUsClient.Instance.IsGameStarted && GameStates.IsOnlineGame))
            {
                player.Collider.offset = new Vector2(0f, -0.3636f);
            }
        }
        if (GameStates.IsLobby)
        {
            var POM = GameObject.Find("PlayerOptionsMenu(Clone)");
            __instance.GameSettings.text = POM != null ? string.Empty : OptionShower.GetTextNoFresh();
            __instance.GameSettings.fontSizeMin =
            __instance.GameSettings.fontSizeMax = 1f;
        }

        if (AmongUsClient.Instance.AmHost)
        {
            if (OverriddenRolesText == null)
            {
                OverriddenRolesText = Object.Instantiate(__instance.KillButton.cooldownTimerText);
                OverriddenRolesText.alignment = TextAlignmentOptions.Right;
                OverriddenRolesText.verticalAlignment = VerticalAlignmentOptions.Top;
                OverriddenRolesText.transform.parent = __instance.transform;
                OverriddenRolesText.transform.localPosition = new Vector3(4.9f, 0.8f, 0);
                OverriddenRolesText.overflowMode = TextOverflowModes.Overflow;
                OverriddenRolesText.enableWordWrapping = false;
                OverriddenRolesText.color = Color.white;
                OverriddenRolesText.fontSize = OverriddenRolesText.fontSizeMax = OverriddenRolesText.fontSizeMin = 2f;
            }

            if (Main.SetRoles.Any() || Main.SetAddOns.Any())
            {
                Dictionary<byte, string> resultText = [];
                bool first = true;
                foreach (var item in Main.SetRoles)
                {
                    var pc = Utils.GetPlayerById(item.Key);
                    string prefix = first ? string.Empty : "\n";
                    string text = $"{prefix}{(item.Key == 0 ? "Host" : $"{(pc == null ? $"ID {item.Key}" : $"{pc.GetRealName()}")}")} - <color={(Main.roleColors.TryGetValue(item.Value, out var roleColor) ? roleColor : "#ffffff")}>{GetString(item.Value.ToString())}</color>";
                    resultText[item.Key] = text;
                    first = false;
                }
                if (!Main.SetRoles.Any()) first = true;
                foreach (var item in Main.SetAddOns)
                {
                    for (int i = 0; i < item.Value.Count; i++)
                    {
                        CustomRoles role = item.Value[i];
                        var pc = Utils.GetPlayerById(item.Key);
                        if (resultText.ContainsKey(item.Key))
                        {
                            string text = $" <#ffffff>(</color><color={(Main.roleColors.TryGetValue(role, out var roleColor) ? roleColor : "#ffffff")}>{GetString(role.ToString())}</color><#ffffff>)</color>";
                            resultText[item.Key] += text;
                        }
                        else
                        {
                            string prefix = first ? string.Empty : "\n";
                            string text = $"{prefix}{(item.Key == 0 ? "Host" : $"{(pc == null ? $"ID {item.Key}" : $"{pc.GetRealName()}")}")} - <#ffffff>(</color><color={(Main.roleColors.TryGetValue(role, out var roleColor) ? roleColor : "#ffffff")}>{GetString(role.ToString())}</color><#ffffff>)</color>";
                            resultText[item.Key] = text;
                            first = false;
                        }
                    }
                }
                bool stop = false;
                foreach (var roles in Main.SetRoles)
                {
                    if (!Main.SetAddOns.ContainsKey(roles.Key)) continue;
                    foreach (var addons in Main.SetAddOns)
                    {
                        if (!Main.SetRoles.ContainsKey(addons.Key)) continue;
                        foreach (var addon in addons.Value)
                        {
                            if (!CustomRolesHelper.CheckAddonConflictV2(addon, roles.Value))
                            {
                                resultText[roles.Key] += $" <#ff0000>(!)</color>";
                                stop = true;
                                break;
                            }
                        }
                        if (stop) break;
                    }
                    if (stop) break;
                }
                OverriddenRolesText.text = string.Join(string.Empty, resultText.Values);
            }
            else
            {
                OverriddenRolesText.text = string.Empty;
            }

            OverriddenRolesText.enabled = OverriddenRolesText.text != string.Empty;
        }

        // The following will not be executed unless the game is in progress
        if (!AmongUsClient.Instance.IsGameStarted) return;

        Utils.CountAlivePlayers();

        bool shapeshifting = player.shapeshifting;

        if (SetHudActivePatch.IsActive)
        {
            if (player.IsAlive() || Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop)
            {
                //MOD入り用のボタン下テキスト変更
                switch (player.GetCustomRole())
                {
                    case CustomRoles.Sniper:
                        Sniper.OverrideShapeText(player.PlayerId);
                        break;
                    case CustomRoles.FireWorks:
                        if (FireWorks.nowFireWorksCount[player.PlayerId] == 0)
                            __instance.AbilityButton?.OverrideText(GetString("FireWorksExplosionButtonText"));
                        else
                            __instance.AbilityButton?.OverrideText(GetString("FireWorksInstallAtionButtonText"));
                        break;
                    //case CustomRoles.SerialKiller:
                    //    SerialKiller.GetAbilityButtonText(__instance, player);
                    //    break;
                    case CustomRoles.Warlock:
                        bool curse = Main.isCurseAndKill.TryGetValue(player.PlayerId, out bool wcs) && wcs;
                        if (!shapeshifting && !curse)
                            __instance.KillButton?.OverrideText(GetString("WarlockCurseButtonText"));
                        else
                            __instance.KillButton?.OverrideText(GetString("KillButtonText"));
                        if (!shapeshifting && curse)
                            __instance.AbilityButton?.OverrideText(GetString("WarlockShapeshiftButtonText"));
                        break;
                    case CustomRoles.Miner:
                        if (Options.UsePets.GetBool()) __instance.PetButton?.OverrideText(GetString("MinerTeleButtonText"));
                        else __instance.AbilityButton?.OverrideText(GetString("MinerTeleButtonText"));
                        break;
                    case CustomRoles.Escapee:
                        if (Options.UsePets.GetBool()) __instance.PetButton?.OverrideText(GetString("EscapeeAbilityButtonText"));
                        else __instance.AbilityButton?.OverrideText(GetString("EscapeeAbilityButtonText"));
                        break;
                    case CustomRoles.Pestilence:
                        __instance.KillButton?.OverrideText(GetString("KillButtonText"));
                        break;
                    case CustomRoles.PlagueDoctor:
                    case CustomRoles.PlagueBearer:
                        __instance.KillButton?.OverrideText(GetString("InfectiousKillButtonText"));
                        break;
                    case CustomRoles.Witch:
                        Witch.GetAbilityButtonText(__instance);
                        break;
                    case CustomRoles.HexMaster:
                        HexMaster.GetAbilityButtonText(__instance);
                        break;
                    case CustomRoles.Vampire:
                        Vampire.SetKillButtonText();
                        break;
                    case CustomRoles.Poisoner:
                        Poisoner.SetKillButtonText();
                        break;
                    case CustomRoles.Arsonist:
                        __instance.KillButton?.OverrideText(GetString("ArsonistDouseButtonText"));
                        __instance.ImpostorVentButton.buttonLabelText.text = GetString("ArsonistVentButtonText");
                        break;
                    case CustomRoles.Revolutionist:
                        __instance.KillButton?.OverrideText(GetString("RevolutionistDrawButtonText"));
                        __instance.ImpostorVentButton.buttonLabelText.text = GetString("RevolutionistVentButtonText");
                        break;
                    case CustomRoles.Farseer:
                        __instance.KillButton?.OverrideText(GetString("FarseerKillButtonText"));
                        break;
                    case CustomRoles.Puppeteer:
                        __instance.KillButton?.OverrideText(GetString("PuppeteerOperateButtonText"));
                        break;
                    //case CustomRoles.NWitch:
                    //    __instance.KillButton.OverrideText($"{GetString("WitchControlButtonText")}");
                    //    break;
                    //case CustomRoles.BountyHunter:
                    //    BountyHunter.SetAbilityButtonText(__instance);
                    //    break;
                    case CustomRoles.EvilTracker:
                        EvilTracker.GetAbilityButtonText(__instance, player.PlayerId);
                        break;
                    case CustomRoles.Innocent:
                        __instance.KillButton?.OverrideText(GetString("InnocentButtonText"));
                        break;
                    case CustomRoles.Capitalism:
                        __instance.KillButton?.OverrideText(GetString("CapitalismButtonText"));
                        break;
                    case CustomRoles.Pelican:
                        __instance.KillButton?.OverrideText(GetString("PelicanButtonText"));
                        break;
                    //case CustomRoles.Counterfeiter:
                    //    __instance.KillButton.OverrideText(GetString("CounterfeiterButtonText"));
                    //    break;
                    case CustomRoles.Analyzer:
                        __instance.KillButton?.OverrideText(GetString("AnalyzerKillButtonText"));
                        break;
                    case CustomRoles.Witness:
                        __instance.KillButton?.OverrideText(GetString("WitnessButtonText"));
                        break;
                    case CustomRoles.Pursuer:
                        __instance.KillButton?.OverrideText(GetString("PursuerButtonText"));
                        break;
                    case CustomRoles.Gangster:
                        Gangster.SetKillButtonText(player.PlayerId);
                        break;
                    case CustomRoles.NSerialKiller:
                    case CustomRoles.Enderman:
                    case CustomRoles.Mycologist:
                    case CustomRoles.Bubble:
                    case CustomRoles.Hookshot:
                    case CustomRoles.Sprayer:
                    case CustomRoles.Magician:
                    case CustomRoles.WeaponMaster:
                    case CustomRoles.Reckless:
                    case CustomRoles.Pyromaniac:
                    case CustomRoles.Eclipse:
                    case CustomRoles.Vengeance:
                    case CustomRoles.HeadHunter:
                    case CustomRoles.Imitator:
                    case CustomRoles.Werewolf:
                    case CustomRoles.RuthlessRomantic:
                    case CustomRoles.Juggernaut:
                    case CustomRoles.Jackal:
                    case CustomRoles.Virus:
                    case CustomRoles.BloodKnight:
                    case CustomRoles.SwordsMan:
                    case CustomRoles.Parasite:
                    case CustomRoles.Refugee:
                    case CustomRoles.Traitor:
                    case CustomRoles.Ritualist:
                    case CustomRoles.Spiritcaller:
                    case CustomRoles.DarkHide:
                    case CustomRoles.Maverick:
                        __instance.KillButton?.OverrideText(GetString("KillButtonText"));
                        break;
                    case CustomRoles.Postman:
                        __instance.KillButton?.OverrideText(GetString("PostmanKillButtonText"));
                        break;
                    case CustomRoles.Escort:
                        __instance.KillButton?.OverrideText(GetString("EscortKillButtonText"));
                        break;
                    case CustomRoles.Glitch:
                        __instance.SabotageButton?.OverrideText(GetString("HackButtonText"));
                        break;
                    case CustomRoles.FFF:
                        __instance.KillButton?.OverrideText(GetString("FFFButtonText"));
                        break;
                    case CustomRoles.Gaulois:
                        __instance.KillButton?.OverrideText(GetString("GauloisKillButtonText"));
                        break;
                    case CustomRoles.Aid:
                    case CustomRoles.DonutDelivery:
                    case CustomRoles.Medic:
                        __instance.KillButton?.OverrideText(GetString("MedicalerButtonText"));
                        break;
                    case CustomRoles.Gamer:
                        __instance.KillButton?.OverrideText(GetString("GamerButtonText"));
                        break;
                    case CustomRoles.BallLightning:
                        __instance.KillButton?.OverrideText(GetString("BallLightningButtonText"));
                        break;
                    case CustomRoles.Sapper:
                    case CustomRoles.Bomber:
                    case CustomRoles.Nuker:
                        if (Options.UsePets.GetBool()) __instance.PetButton?.OverrideText(GetString("BomberShapeshiftText"));
                        else __instance.AbilityButton?.OverrideText(GetString("BomberShapeshiftText"));
                        break;
                    case CustomRoles.Twister:
                        if (Options.UsePets.GetBool())
                        {
                            __instance.PetButton?.OverrideText(GetString("TwisterButtonText"));
                        }
                        else
                        {
                            __instance.AbilityButton?.OverrideText(GetString("TwisterButtonText"));
                            __instance.AbilityButton?.SetUsesRemaining((int)Twister.TwistLimit[player.PlayerId]);
                        }
                        break;
                    case CustomRoles.ImperiusCurse:
                        __instance.AbilityButton?.OverrideText(GetString("ImperiusCurseButtonText"));
                        break;
                    case CustomRoles.QuickShooter:
                        if (Options.UsePets.GetBool())
                        {
                            __instance.PetButton?.OverrideText(GetString("QuickShooterShapeshiftText"));
                        }
                        else
                        {
                            __instance.AbilityButton?.OverrideText(GetString("QuickShooterShapeshiftText"));
                            __instance.AbilityButton?.SetUsesRemaining(QuickShooter.ShotLimit.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var qx) ? qx : 0);
                        }
                        break;
                    case CustomRoles.Provocateur:
                        __instance.KillButton?.OverrideText(GetString("ProvocateurButtonText"));
                        break;
                    case CustomRoles.Camouflager:
                        __instance.AbilityButton?.OverrideText(GetString("CamouflagerShapeshiftText"));
                        if (Camouflager.CamoLimit.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var x)) __instance.AbilityButton?.SetUsesRemaining((int)x);
                        break;
                    case CustomRoles.OverKiller:
                        __instance.KillButton?.OverrideText(GetString("OverKillerButtonText"));
                        break;
                    case CustomRoles.Assassin:
                        Assassin.SetKillButtonText(player.PlayerId);
                        Assassin.GetAbilityButtonText(__instance, player.PlayerId);
                        break;
                    case CustomRoles.Undertaker:
                        Undertaker.SetKillButtonText(player.PlayerId);
                        Undertaker.GetAbilityButtonText(__instance, player.PlayerId);
                        break;
                    case CustomRoles.Hacker:
                        Hacker.GetAbilityButtonText(__instance, player.PlayerId);
                        break;
                    case CustomRoles.KB_Normal:
                        __instance.KillButton?.OverrideText(GetString("GamerButtonText"));
                        break;
                    case CustomRoles.Cleaner:
                        __instance.ReportButton?.OverrideText(GetString("CleanerReportButtonText"));
                        break;
                    case CustomRoles.Medusa:
                        __instance.KillButton?.OverrideText(GetString("KillButtonText"));
                        __instance.ReportButton?.OverrideText(GetString("MedusaReportButtonText"));
                        break;
                    case CustomRoles.Vulture:
                        __instance.ReportButton?.OverrideText(GetString("VultureEatButtonText"));
                        break;
                    case CustomRoles.Disperser:
                        if (Options.UsePets.GetBool())
                        {
                            __instance.PetButton?.OverrideText(GetString("DisperserVentButtonText"));
                        }
                        else
                        {
                            __instance.AbilityButton?.OverrideText(GetString("DisperserVentButtonText"));
                            __instance.AbilityButton?.SetUsesRemaining((int)Disperser.DisperserLimit[player.PlayerId]);
                        }
                        break;
                    case CustomRoles.Swooper:
                        __instance.ImpostorVentButton?.OverrideText(GetString(Swooper.IsInvis(PlayerControl.LocalPlayer.PlayerId) ? "SwooperRevertVentButtonText" : "SwooperVentButtonText"));
                        __instance.ImpostorVentButton?.OverrideText(GetString(Swooper.CanGoInvis(PlayerControl.LocalPlayer.PlayerId) ? "SwooperVentButtonText" : "VentButtonText"));
                        break;
                    case CustomRoles.Wraith:
                        __instance.KillButton?.OverrideText(GetString("KillButtonText"));
                        __instance.ImpostorVentButton?.OverrideText(GetString(Wraith.IsInvis(PlayerControl.LocalPlayer.PlayerId) ? "WraithRevertVentButtonText" : "WraithVentButtonText"));
                        __instance.ImpostorVentButton?.OverrideText(GetString(Swooper.CanGoInvis(PlayerControl.LocalPlayer.PlayerId) ? "WraithVentButtonText" : "VentButtonText"));
                        break;
                    case CustomRoles.Chameleon:
                        __instance.AbilityButton?.OverrideText(GetString(Chameleon.IsInvis(PlayerControl.LocalPlayer.PlayerId) ? "ChameleonRevertDisguise" : "ChameleonDisguise"));
                        break;
                    case CustomRoles.Mario:
                        __instance.AbilityButton.buttonLabelText.text = GetString("MarioVentButtonText");
                        __instance.AbilityButton?.SetUsesRemaining(Options.MarioVentNumWin.GetInt() - (Main.MarioVentCount.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var mx) ? mx : 0));
                        break;
                    case CustomRoles.Veteran:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton.buttonLabelText.text = GetString("VeteranVentButtonText");
                        else
                            __instance.AbilityButton.buttonLabelText.text = GetString("VeteranVentButtonText");
                        break;
                    case CustomRoles.TimeMaster:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton.buttonLabelText.text = GetString("TimeMasterVentButtonText");
                        else
                            __instance.AbilityButton.buttonLabelText.text = GetString("TimeMasterVentButtonText");
                        break;
                    case CustomRoles.Grenadier:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton.buttonLabelText.text = GetString("GrenadierVentButtonText");
                        else
                            __instance.AbilityButton.buttonLabelText.text = GetString("GrenadierVentButtonText");
                        break;
                    case CustomRoles.Lighter:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton.buttonLabelText.text = GetString("LighterVentButtonText");
                        else
                            __instance.AbilityButton.buttonLabelText.text = GetString("LighterVentButtonText");
                        break;
                    case CustomRoles.SecurityGuard:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton.buttonLabelText.text = GetString("SecurityGuardVentButtonText");
                        else
                            __instance.AbilityButton.buttonLabelText.text = GetString("SecurityGuardVentButtonText");
                        break;
                    case CustomRoles.Ventguard:
                        __instance.AbilityButton.buttonLabelText.text = GetString("VentguardVentButtonText");
                        break;
                    case CustomRoles.Mayor:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton.buttonLabelText.text = GetString("MayorVentButtonText");
                        else
                            __instance.AbilityButton.buttonLabelText.text = GetString("MayorVentButtonText");
                        break;
                    case CustomRoles.Paranoia:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton.buttonLabelText.text = GetString("ParanoiaVentButtonText");
                        else
                            __instance.AbilityButton.buttonLabelText.text = GetString("ParanoiaVentButtonText");
                        break;
                    case CustomRoles.Sheriff:
                        __instance.KillButton?.OverrideText(GetString("SheriffKillButtonText"));
                        break;
                    case CustomRoles.Crusader:
                        __instance.KillButton?.OverrideText(GetString("CrusaderKillButtonText"));
                        break;
                    case CustomRoles.Totocalcio:
                        __instance.KillButton?.OverrideText(GetString("TotocalcioKillButtonText"));
                        break;
                    case CustomRoles.Romantic:
                        if (Romantic.BetTimes.TryGetValue(player.PlayerId, out var timesV1) && timesV1 >= 1) __instance.KillButton?.OverrideText(GetString("RomanticKillButtonText"));
                        else __instance.KillButton?.OverrideText(GetString("MedicalerButtonText"));
                        break;
                    case CustomRoles.VengefulRomantic:
                        __instance.KillButton?.OverrideText(GetString("VengefulRomanticKillButtonText"));
                        break;
                    case CustomRoles.Penguin:
                        __instance.KillButton?.OverrideText(Penguin.OverrideKillButtonText());
                        __instance.AbilityButton?.OverrideText(Penguin.GetAbilityButtonText());
                        __instance.AbilityButton?.ToggleVisible(Penguin.CanUseAbilityButton());
                        break;
                    case CustomRoles.Succubus:
                        __instance.KillButton?.OverrideText(GetString("SuccubusKillButtonText"));
                        break;
                    case CustomRoles.CursedSoul:
                        __instance.KillButton?.OverrideText(GetString("CursedSoulKillButtonText"));
                        break;
                    case CustomRoles.Admirer:
                        __instance.KillButton?.OverrideText(GetString("AdmireButtonText"));
                        break;
                    case CustomRoles.Amnesiac:
                        __instance.KillButton?.OverrideText(GetString("RememberButtonText"));
                        break;
                    case CustomRoles.DovesOfNeace:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton.buttonLabelText.text = GetString("DovesOfNeaceVentButtonText");
                        else
                            __instance.AbilityButton.buttonLabelText.text = GetString("DovesOfNeaceVentButtonText");
                        break;
                    case CustomRoles.Infectious:
                        __instance.KillButton?.OverrideText(GetString("InfectiousKillButtonText"));
                        break;
                    case CustomRoles.Monarch:
                        __instance.KillButton?.OverrideText(GetString("MonarchKillButtonText"));
                        break;
                    case CustomRoles.Deputy:
                        __instance.KillButton?.OverrideText(GetString("DeputyHandcuffText"));
                        break;
                    case CustomRoles.Hangman:
                        if (shapeshifting) __instance.KillButton?.OverrideText(GetString("HangmanKillButtonTextDuringSS"));
                        __instance.AbilityButton?.SetUsesRemaining((int)Hangman.HangLimit[player.PlayerId]);
                        break;
                    case CustomRoles.Sidekick:
                        __instance.KillButton?.OverrideText(GetString("KillButtonText"));
                        __instance.ImpostorVentButton?.OverrideText(GetString("ReportButtonText"));
                        __instance.SabotageButton?.OverrideText(GetString("SabotageButtonText"));
                        break;
                    case CustomRoles.Addict:
                        __instance.AbilityButton?.OverrideText(GetString("AddictVentButtonText"));
                        break;
                    case CustomRoles.Alchemist:
                        if (Options.UsePets.GetBool())
                            __instance.PetButton?.OverrideText(GetString("AlchemistVentButtonText"));
                        else
                            __instance.AbilityButton?.OverrideText(GetString("AlchemistVentButtonText"));
                        break;
                    case CustomRoles.Dazzler:
                        __instance.AbilityButton?.OverrideText(GetString("DazzleButtonText"));
                        if (Dazzler.DazzleLimit.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var y)) __instance.AbilityButton?.SetUsesRemaining((int)y);
                        break;
                    case CustomRoles.Deathpact:
                        __instance.AbilityButton?.OverrideText(GetString("DeathpactButtonText"));
                        break;
                    case CustomRoles.Devourer:
                        __instance.AbilityButton?.OverrideText(GetString("DevourerButtonText"));
                        break;
                }

                if (LowerInfoText == null)
                {
                    //TempLowerInfoText = new GameObject("CountdownText");
                    //TempLowerInfoText.transform.position = new Vector3(0f, -2f, 1f);
                    //LowerInfoText = TempLowerInfoText.AddComponent<TextMeshPro>();
                    //LowerInfoText.text = string.Format(GetString("CountdownText"));
                    LowerInfoText = Object.Instantiate(__instance.KillButton.cooldownTimerText);
                    LowerInfoText.alignment = TextAlignmentOptions.Center;
                    LowerInfoText.transform.parent = __instance.transform;
                    LowerInfoText.transform.localPosition = new Vector3(0, -2f, 0);
                    LowerInfoText.overflowMode = TextOverflowModes.Overflow;
                    LowerInfoText.enableWordWrapping = false;
                    LowerInfoText.color = Color.white;
                    LowerInfoText.fontSize = LowerInfoText.fontSizeMax = LowerInfoText.fontSizeMin = 2f;
                }

                LowerInfoText.text = Options.CurrentGameMode switch
                {
                    CustomGameMode.SoloKombat => SoloKombatManager.GetHudText(),
                    CustomGameMode.FFA when player.PlayerId == 0 => FFAManager.GetHudText(),
                    CustomGameMode.MoveAndStop when player.PlayerId == 0 => MoveAndStopManager.HUDText,
                    CustomGameMode.Standard => player.GetCustomRole() switch
                    {
                        CustomRoles.BountyHunter => BountyHunter.GetTargetText(player, true),
                        CustomRoles.Witch => Witch.GetSpellModeText(player, true),
                        CustomRoles.HexMaster => HexMaster.GetHexModeText(player, true),
                        CustomRoles.FireWorks => FireWorks.GetStateText(player),
                        CustomRoles.Swooper => Swooper.GetHudText(player),
                        CustomRoles.Wraith => Wraith.GetHudText(player),
                        CustomRoles.HeadHunter => HeadHunter.GetHudText(player),
                        CustomRoles.Alchemist => Alchemist.GetHudText(player),
                        CustomRoles.Chameleon => Chameleon.GetHudText(player),
                        CustomRoles.Werewolf => Werewolf.GetHudText(player),
                        CustomRoles.BloodKnight => BloodKnight.GetHudText(player),
                        CustomRoles.Glitch => Glitch.GetHudText(player),
                        CustomRoles.NiceHacker => NiceHacker.GetHudText(player),
                        CustomRoles.Wildling => Wildling.GetHudText(player),
                        CustomRoles.Doormaster => Doormaster.GetHudText(player),
                        CustomRoles.Tether => Tether.GetHudText(player),
                        CustomRoles.YinYanger => YinYanger.ModeText,
                        CustomRoles.WeaponMaster => WeaponMaster.GetHudAndProgressText(),
                        CustomRoles.Postman => Postman.GetHudText(player),
                        CustomRoles.Chronomancer => Chronomancer.GetHudText(),
                        CustomRoles.Mafioso => Mafioso.GetHUDText(),
                        CustomRoles.Druid => Druid.GetHUDText(player),
                        CustomRoles.Librarian => Librarian.GetSelfSuffixAndHUDText(player.PlayerId),
                        CustomRoles.PlagueDoctor => PlagueDoctor.GetLowerTextOthers(player, isForHud: true),
                        CustomRoles.Stealth => Stealth.GetSuffix(player, isHUD: true),
                        CustomRoles.Hookshot => Hookshot.SuffixText,
                        _ => string.Empty,
                    },
                    _ => string.Empty,
                };
                if (GetCD_HUDText() != string.Empty) LowerInfoText.text = $"{GetCD_HUDText()}\n{LowerInfoText.text}";
                string GetCD_HUDText() => !Options.UsePets.GetBool() || !Main.AbilityCD.TryGetValue(player.PlayerId, out var CD)
                        ? string.Empty
                        : string.Format(GetString("CDPT"), CD.TOTALCD - (Utils.GetTimeStamp() - CD.START_TIMESTAMP) + 1);

                LowerInfoText.enabled = LowerInfoText.text != string.Empty;

                if ((!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay) || GameStates.IsMeeting)
                {
                    LowerInfoText.enabled = false;
                }

                if (player.CanUseKillButton())
                {
                    __instance.KillButton?.ToggleVisible(player.IsAlive() && GameStates.IsInTask);
                    player.Data.Role.CanUseKillButton = true;
                }
                else
                {
                    __instance.KillButton?.SetDisabled();
                    __instance.KillButton?.ToggleVisible(false);
                }

                bool CanUseVent = player.CanUseImpostorVentButton() && GameStates.IsInTask;
                __instance.ImpostorVentButton?.ToggleVisible(CanUseVent);
                player.Data.Role.CanVent = CanUseVent;
            }
            else
            {
                __instance.ReportButton?.Hide();
                __instance.ImpostorVentButton?.Hide();
                __instance.KillButton?.Hide();
                __instance.AbilityButton?.Show();
                __instance.AbilityButton?.OverrideText(GetString(StringNames.HauntAbilityName));
            }
        }


        if (Input.GetKeyDown(KeyCode.Y) && AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
        {
            __instance.ToggleMapVisible(new MapOptions()
            {
                Mode = MapOptions.Modes.Sabotage,
                AllowMovementWhileMapOpen = true
            });
            if (player.AmOwner)
            {
                player.MyPhysics.inputHandler.enabled = true;
                ConsoleJoystick.SetMode_Task();
            }
        }

        if (AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame) RepairSender.enabled = false;
        if (Input.GetKeyDown(KeyCode.RightShift) && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
        {
            RepairSender.enabled = !RepairSender.enabled;
            RepairSender.Reset();
        }
        if (RepairSender.enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0)) RepairSender.Input(0);
            if (Input.GetKeyDown(KeyCode.Alpha1)) RepairSender.Input(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RepairSender.Input(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) RepairSender.Input(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) RepairSender.Input(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) RepairSender.Input(5);
            if (Input.GetKeyDown(KeyCode.Alpha6)) RepairSender.Input(6);
            if (Input.GetKeyDown(KeyCode.Alpha7)) RepairSender.Input(7);
            if (Input.GetKeyDown(KeyCode.Alpha8)) RepairSender.Input(8);
            if (Input.GetKeyDown(KeyCode.Alpha9)) RepairSender.Input(9);
            if (Input.GetKeyDown(KeyCode.Return)) RepairSender.InputEnter();
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ToggleHighlight))]
class ToggleHighlightPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] bool active, [HarmonyArgument(1)] RoleTeamTypes team)
    {
        var player = PlayerControl.LocalPlayer;
        if (!GameStates.IsInTask) return;

        if (player.CanUseKillButton())
        {
            __instance.cosmetics.currentBodySprite.BodySprite.material.SetColor("_OutlineColor", Utils.GetRoleColor(player.GetCustomRole()));
        }
    }
}
[HarmonyPatch(typeof(Vent), nameof(Vent.SetOutline))]
class SetVentOutlinePatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(1)] ref bool mainTarget)
    {
        Color color = PlayerControl.LocalPlayer.GetRoleColor();
        __instance.myRend.material.SetColor("_OutlineColor", color);
        __instance.myRend.material.SetColor("_AddColor", mainTarget ? color : Color.clear);
    }
}
[HarmonyPatch(typeof(HudManager), nameof(HudManager.SetHudActive), [typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool)])]
class SetHudActivePatch
{
    public static bool IsActive;
    public static void Prefix(HudManager __instance, [HarmonyArgument(2)] ref bool isActive)
    {
        isActive &= !GameStates.IsMeeting;
        return;
    }
    public static void Postfix(HudManager __instance, [HarmonyArgument(2)] bool isActive)
    {
        __instance?.ReportButton?.ToggleVisible(!GameStates.IsLobby && isActive);
        if (!GameStates.IsModHost) return;
        if (__instance == null)
        {
            Logger.Fatal("HudManager __instance ended up being null", "SetHudActivePatch.Postfix");
            return;
        }
        IsActive = isActive;
        if (!isActive) return;

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.MoveAndStop:
                __instance.ReportButton?.ToggleVisible(false);
                __instance.KillButton?.ToggleVisible(false);
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.ImpostorVentButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(false);
                return;
            case CustomGameMode.FFA:
                __instance.ReportButton?.ToggleVisible(false);
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(false);
                return;
        }

        var player = PlayerControl.LocalPlayer;
        if (player == null) return;
        switch (player.GetCustomRole())
        {
            case CustomRoles.Sheriff:
            case CustomRoles.Arsonist:
            case CustomRoles.SwordsMan:
            case CustomRoles.Deputy:
            case CustomRoles.Monarch:
            //case CustomRoles.NWitch:
            case CustomRoles.Innocent:
            //case CustomRoles.Reverie:
            case CustomRoles.Pelican:
            case CustomRoles.Revolutionist:
            case CustomRoles.FFF:
            case CustomRoles.Medic:
            case CustomRoles.Gamer:
            case CustomRoles.DarkHide:
            case CustomRoles.Provocateur:
            case CustomRoles.Farseer:
            case CustomRoles.Crusader:
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(false);
                __instance.ImpostorVentButton?.ToggleVisible(false);
                break;

            case CustomRoles.Minimalism:
            case CustomRoles.KB_Normal:
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(false);
                __instance.ReportButton?.ToggleVisible(false);
                break;
            case CustomRoles.Parasite:
            case CustomRoles.Refugee:
                __instance.SabotageButton?.ToggleVisible(true);
                break;
            case CustomRoles.Jackal:
                Jackal.SetHudActive(__instance, isActive);
                break;
            case CustomRoles.Sidekick:
                Sidekick.SetHudActive(__instance, isActive);
                break;
            case CustomRoles.Traitor:
                Traitor.SetHudActive(__instance, isActive);
                break;
            case CustomRoles.Glitch:
                Glitch.SetHudActive(__instance, isActive);
                break;
            case CustomRoles.Magician:
                __instance.SabotageButton?.ToggleVisible(true);
                break;

        }

        if (Main.PlayerStates.TryGetValue(player.PlayerId, out var ps) && ps.SubRoles.Contains(CustomRoles.Oblivious))
        {
            __instance.ReportButton?.ToggleVisible(false);
        }
        __instance.KillButton?.ToggleVisible(player.CanUseKillButton());
        __instance.ImpostorVentButton?.ToggleVisible(player.CanUseImpostorVentButton());
        __instance.SabotageButton?.ToggleVisible(player.CanUseSabotage() && isActive);
    }
}
[HarmonyPatch(typeof(VentButton), nameof(VentButton.DoClick))]
class VentButtonDoClickPatch
{
    public static bool Prefix(VentButton __instance)
    {
        var pc = PlayerControl.LocalPlayer;
        {
            if (!pc.Is(CustomRoles.Swooper) || !pc.Is(CustomRoles.Wraith) || !pc.Is(CustomRoles.Chameleon) || pc.inVent || __instance.currentTarget == null || !pc.CanMove || !__instance.isActiveAndEnabled) return true;
            pc?.MyPhysics?.RpcEnterVent(__instance.currentTarget.Id);
            return false;
        }
    }
}
[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Show))]
class MapBehaviourShowPatch
{
    public static bool Prefix(MapBehaviour __instance, ref MapOptions opts)
    {
        if (GameStates.IsMeeting) return true;

        var player = PlayerControl.LocalPlayer;

        if (player.GetCustomRole() == CustomRoles.NiceHacker && NiceHacker.playerIdList.ContainsKey(player.PlayerId))
        {
            Logger.Info("Modded Client uses Map", "NiceHacker");
            NiceHacker.MapHandle(player, __instance, opts);
        }
        else if (opts.Mode is MapOptions.Modes.Normal or MapOptions.Modes.Sabotage)
        {
            if (player.Is(CustomRoleTypes.Impostor) || player.Is(CustomRoles.Glitch) || player.Is(CustomRoles.WeaponMaster) || player.Is(CustomRoles.Magician) || player.Is(CustomRoles.Parasite) || player.Is(CustomRoles.Refugee) || (player.Is(CustomRoles.Jackal) && Jackal.CanUseSabotage.GetBool()) || (player.Is(CustomRoles.Traitor) && Traitor.CanUseSabotage.GetBool()))
                opts.Mode = MapOptions.Modes.Sabotage;
            else
                opts.Mode = MapOptions.Modes.Normal;
        }

        return true;
    }
}
[HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
class TaskPanelBehaviourPatch
{
    // タスク表示の文章が更新・適用された後に実行される
    public static void Postfix(TaskPanelBehaviour __instance)
    {
        if (!GameStates.IsModHost) return;
        PlayerControl player = PlayerControl.LocalPlayer;

        var taskText = __instance.taskText.text;
        if (taskText == "None") return;

        // 役職説明表示
        if (!player.GetCustomRole().IsVanilla())
        {
            var RoleWithInfo = $"<size=80%>{player.GetDisplayRoleName()}:\r\n{player.GetRoleInfo()}</size>";
            if (Options.CurrentGameMode == CustomGameMode.MoveAndStop) RoleWithInfo = $"{GetString("TaskerInfo")}\r\n";

            var AllText = Utils.ColorString(player.GetRoleColor(), RoleWithInfo);

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.Standard:

                    var lines = taskText.Split("\r\n</color>\n")[0].Split("\r\n\n")[0].Split("\r\n");
                    StringBuilder sb = new();
                    foreach (string eachLine in lines)
                    {
                        var line = eachLine.Trim();
                        if ((line.StartsWith("<color=#FF1919FF>") || line.StartsWith("<color=#FF0000FF>")) && sb.Length < 1 && !line.Contains('(')) continue;
                        sb.Append(line + "\r\n");
                    }
                    if (sb.Length > 1)
                    {
                        var text = sb.ToString().TrimEnd('\n').TrimEnd('\r');
                        if (!Utils.HasTasks(player.Data, false) && sb.ToString().Count(s => s == '\n') >= 2)
                            text = $"{Utils.ColorString(Utils.GetRoleColor(player.GetCustomRole()).ShadeColor(0.2f), GetString("FakeTask"))}\r\n{text}";
                        AllText += $"\r\n\r\n<size=70%>{text}</size>";
                    }

                    if (MeetingStates.FirstMeeting)
                    {
                        AllText += $"\r\n\r\n</color><size=65%>{GetString("PressF1ShowMainRoleDes")}";
                        if (Main.PlayerStates.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var ps) && ps.SubRoles.Count > 0)
                            AllText += $"\r\n{GetString("PressF2ShowAddRoleDes")}";
                        AllText += "</size>";
                    }

                    break;

                case CustomGameMode.SoloKombat:

                    var lpc = PlayerControl.LocalPlayer;

                    AllText += "\r\n";
                    AllText += $"\r\n{GetString("PVP.ATK")}: {lpc.ATK()}";
                    AllText += $"\r\n{GetString("PVP.DF")}: {lpc.DF()}";
                    AllText += $"\r\n{GetString("PVP.RCO")}: {lpc.HPRECO()}";
                    AllText += "\r\n";

                    Dictionary<byte, string> SummaryText = [];
                    foreach (var id in Main.PlayerStates.Keys)
                    {
                        string name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
                        string summary = $"{Utils.GetProgressText(id)}  {Utils.ColorString(Main.PlayerColors[id], name)}";
                        if (Utils.GetProgressText(id).Trim() == string.Empty) continue;
                        SummaryText[id] = summary;
                    }

                    List<(int, byte)> list = [];
                    foreach (var id in Main.PlayerStates.Keys) list.Add((SoloKombatManager.GetRankOfScore(id), id));
                    list.Sort();
                    foreach (var id in list.Where(x => SummaryText.ContainsKey(x.Item2))) AllText += "\r\n" + SummaryText[id.Item2];

                    AllText = $"<size=70%>{AllText}</size>";

                    break;

                case CustomGameMode.FFA:

                    Dictionary<byte, string> SummaryText2 = [];
                    foreach (var id in Main.PlayerStates.Keys)
                    {
                        string name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
                        string summary = $"{Utils.GetProgressText(id)}  {Utils.ColorString(Main.PlayerColors[id], name)}";
                        if (Utils.GetProgressText(id).Trim() == string.Empty) continue;
                        SummaryText2[id] = summary;
                    }

                    List<(int, byte)> list2 = [];
                    foreach (var id in Main.PlayerStates.Keys) list2.Add((FFAManager.GetRankOfScore(id), id));
                    list2.Sort();
                    foreach (var id in list2.Where(x => SummaryText2.ContainsKey(x.Item2))) AllText += "\r\n" + SummaryText2[id.Item2];

                    AllText = $"<size=70%>{AllText}</size>";

                    break;

                case CustomGameMode.MoveAndStop:

                    Dictionary<byte, string> SummaryText3 = [];
                    foreach (var id in Main.PlayerStates.Keys.ToArray())
                    {
                        string name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
                        string summary = $"{Utils.GetProgressText(id)}  {Utils.ColorString(Main.PlayerColors[id], name)}";
                        if (Utils.GetProgressText(id).Trim() == string.Empty) continue;
                        SummaryText3[id] = summary;
                    }

                    var lines1 = taskText.Split("\r\n</color>\n")[0].Split("\r\n\n")[0].Split("\r\n");
                    StringBuilder sb1 = new();
                    foreach (string eachLine in lines1)
                    {
                        var line = eachLine.Trim();
                        if ((line.StartsWith("<color=#FF1919FF>") || line.StartsWith("<color=#FF0000FF>")) && sb1.Length < 1 && !line.Contains('(')) continue;
                        sb1.Append(line + "\r\n");
                    }
                    if (sb1.Length > 1)
                    {
                        var text = sb1.ToString().TrimEnd('\n').TrimEnd('\r');
                        if (!Utils.HasTasks(player.Data, false) && sb1.ToString().Count(s => s == '\n') >= 2)
                            text = $"{Utils.ColorString(Utils.GetRoleColor(player.GetCustomRole()).ShadeColor(0.2f), GetString("FakeTask"))}\r\n{text}";
                        AllText += $"<size=70%>\r\n{text}\r\n</size>";
                    }

                    List<(int, byte)> list3 = [];
                    foreach (var id in Main.PlayerStates.Keys) list3.Add((MoveAndStopManager.GetRankOfScore(id), id));
                    list3.Sort();
                    foreach (var id in list3.Where(x => SummaryText3.ContainsKey(x.Item2)).ToArray())
                    {
                        bool alive = Utils.GetPlayerById(id.Item2).IsAlive();
                        AllText += $"{(!alive ? "<#777777>" : string.Empty)}<size=1.6>\r\n{(alive ? SummaryText3[id.Item2] : SummaryText3[id.Item2].RemoveHtmlTags())}{(!alive ? "  <#ff0000>DEAD</color>" : string.Empty)}</size>";
                    }

                    break;
            }

            __instance.taskText.text = AllText;
        }

        // RepairSenderの表示
        if (RepairSender.enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            __instance.taskText.text = RepairSender.GetText();
    }
}

class RepairSender
{
    public static bool enabled;
    public static bool TypingAmount;

    public static int SystemType;
    public static int amount;

    public static void Input(int num)
    {
        if (!TypingAmount)
        {
            //SystemType入力中
            SystemType *= 10;
            SystemType += num;
        }
        else
        {
            //Amount入力中
            amount *= 10;
            amount += num;
        }
    }
    public static void InputEnter()
    {
        if (!TypingAmount)
        {
            //SystemType入力中
            TypingAmount = true;
        }
        else
        {
            //Amount入力中
            Send();
        }
    }
    public static void Send()
    {
        ShipStatus.Instance.RpcUpdateSystem((SystemTypes)SystemType, (byte)amount);
        Reset();
    }
    public static void Reset()
    {
        TypingAmount = false;
        SystemType = 0;
        amount = 0;
    }
    public static string GetText()
    {
        return SystemType.ToString() + "(" + ((SystemTypes)SystemType).ToString() + ")\r\n" + amount;
    }
}