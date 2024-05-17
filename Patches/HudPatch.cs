using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Roles.AddOns.Common;
using EHR.Roles.Crewmate;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using HarmonyLib;
using Il2CppSystem.Text;
using TMPro;
using UnityEngine;
using static EHR.Translator;
using Object = UnityEngine.Object;

namespace EHR.Patches;

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

    private static long LastNullError;

    //public static GameObject TempLowerInfoText;
    public static void Postfix(HudManager __instance)
    {
        try
        {
            LoadingScreen.Update();

            if (!GameStates.IsModHost) return;
            var player = PlayerControl.LocalPlayer;
            if (player == null) return;

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                if ((!AmongUsClient.Instance.IsGameStarted || !GameStates.IsOnlineGame) && player.CanMove)
                {
                    player.Collider.offset = new(0f, 127f);
                }
            }

            if (Math.Abs(player.Collider.offset.y - 127f) < 0.1f)
            {
                if (!Input.GetKey(KeyCode.LeftControl) || (AmongUsClient.Instance.IsGameStarted && GameStates.IsOnlineGame))
                {
                    player.Collider.offset = new(0f, -0.3636f);
                }
            }

            if (__instance == null) return;

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
                    OverriddenRolesText = Object.Instantiate(__instance.KillButton.cooldownTimerText, __instance.transform, true);
                    OverriddenRolesText.alignment = TextAlignmentOptions.Right;
                    OverriddenRolesText.verticalAlignment = VerticalAlignmentOptions.Top;
                    OverriddenRolesText.transform.localPosition = new(4.9f, 0.8f, 0);
                    OverriddenRolesText.overflowMode = TextOverflowModes.Overflow;
                    OverriddenRolesText.enableWordWrapping = false;
                    OverriddenRolesText.color = Color.white;
                    OverriddenRolesText.fontSize = OverriddenRolesText.fontSizeMax = OverriddenRolesText.fontSizeMin = 2.5f;
                }

                if (Main.SetRoles.Count > 0 || Main.SetAddOns.Count > 0)
                {
                    Dictionary<byte, string> resultText = [];
                    bool first = true;
                    foreach (var item in Main.SetRoles)
                    {
                        var pc = Utils.GetPlayerById(item.Key);
                        string prefix = first ? string.Empty : "\n";
                        string text = $"{prefix}{(item.Key == 0 ? "Host" : $"{(pc == null ? $"ID {item.Key}" : $"{pc.GetRealName()}")}")} - <color={Main.RoleColors.GetValueOrDefault(item.Value, "#ffffff")}>{GetString(item.Value.ToString())}</color>";
                        resultText[item.Key] = text;
                        first = false;
                    }

                    if (Main.SetRoles.Count == 0) first = true;
                    foreach (var item in Main.SetAddOns)
                    {
                        foreach (var role in item.Value)
                        {
                            var pc = Utils.GetPlayerById(item.Key);
                            if (resultText.ContainsKey(item.Key))
                            {
                                string text = $" <#ffffff>(</color><color={Main.RoleColors.GetValueOrDefault(role, "#ffffff")}>{GetString(role.ToString())}</color><#ffffff>)</color>";
                                resultText[item.Key] += text;
                            }
                            else
                            {
                                string prefix = first ? string.Empty : "\n";
                                string text = $"{prefix}{(item.Key == 0 ? "Host" : $"{(pc == null ? $"ID {item.Key}" : $"{pc.GetRealName()}")}")} - <#ffffff>(</color><color={Main.RoleColors.GetValueOrDefault(role, "#ffffff")}>{GetString(role.ToString())}</color><#ffffff>)</color>";
                                resultText[item.Key] = text;
                                first = false;
                            }
                        }
                    }

                    foreach (var role in Main.SetRoles)
                    {
                        if (!Main.SetAddOns.TryGetValue(role.Key, out var addons)) continue;

                        var pc = Utils.GetPlayerById(role.Key);
                        if (pc == null) continue;

                        foreach (var addon in addons)
                        {
                            if (!CustomRolesHelper.CheckAddonConflict(addon, pc))
                            {
                                resultText[role.Key] += " <#ff0000>(!)</color>";
                                break;
                            }
                        }
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

            bool shapeshifting = player.IsShifted();

            if (SetHudActivePatch.IsActive)
            {
                if (player.IsAlive() || Options.CurrentGameMode != CustomGameMode.Standard)
                {
                    bool usesPetInsteadOfKill = player.GetCustomRole().UsesPetInsteadOfKill();
                    if (usesPetInsteadOfKill)
                    {
                        __instance.PetButton?.OverrideText(GetString("KillButtonText"));
                    }

                    ActionButton usedButton = __instance.KillButton;
                    if (usesPetInsteadOfKill) usedButton = __instance.PetButton;

                    Main.PlayerStates[player.PlayerId].Role.SetButtonTexts(__instance, player.PlayerId);

                    switch (player.GetCustomRole())
                    {
                        case CustomRoles.Sentry:
                            __instance.PetButton?.OverrideText(GetString("SentryPetButtonText"));
                            break;
                        case CustomRoles.FireWorks:
                            __instance.AbilityButton?.OverrideText((Main.PlayerStates[player.PlayerId].Role as FireWorks).nowFireWorksCount == 0 ? GetString("FireWorksExplosionButtonText") : GetString("FireWorksInstallAtionButtonText"));
                            break;
                        case CustomRoles.Swiftclaw:
                            __instance.PetButton?.OverrideText(GetString("SwiftclawKillButtonText"));
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
                        case CustomRoles.Arsonist:
                            __instance.KillButton?.OverrideText(GetString("ArsonistDouseButtonText"));
                            __instance.ImpostorVentButton.buttonLabelText.text = GetString("ArsonistVentButtonText");
                            break;
                        case CustomRoles.Farseer:
                            usedButton?.OverrideText(GetString("FarseerKillButtonText"));
                            break;
                        case CustomRoles.Capitalism:
                            __instance.KillButton?.OverrideText(GetString("CapitalismButtonText"));
                            break;
                        case CustomRoles.Pelican:
                            __instance.KillButton?.OverrideText(GetString("PelicanButtonText"));
                            break;
                        case CustomRoles.Analyst:
                            usedButton?.OverrideText(GetString("AnalyzerKillButtonText"));
                            break;
                        case CustomRoles.Pursuer:
                            __instance.KillButton?.OverrideText(GetString("PursuerButtonText"));
                            break;
                        case CustomRoles.Postman:
                            __instance.KillButton?.OverrideText(GetString("PostmanKillButtonText"));
                            break;
                        case CustomRoles.Escort:
                            usedButton?.OverrideText(GetString("EscortKillButtonText"));
                            break;
                        case CustomRoles.Glitch:
                            __instance.SabotageButton?.OverrideText(GetString("HackButtonText"));
                            break;
                        case CustomRoles.FFF:
                            __instance.KillButton?.OverrideText(GetString("FFFButtonText"));
                            break;
                        case CustomRoles.Gaulois:
                            usedButton?.OverrideText(GetString("GauloisKillButtonText"));
                            break;
                        case CustomRoles.Aid:
                        case CustomRoles.DonutDelivery:
                        case CustomRoles.Medic:
                            usedButton?.OverrideText(GetString("MedicalerButtonText"));
                            break;
                        case CustomRoles.Gamer:
                            __instance.KillButton?.OverrideText(GetString("GamerButtonText"));
                            break;
                        case CustomRoles.BallLightning:
                            __instance.KillButton?.OverrideText(GetString("BallLightningButtonText"));
                            break;
                        case CustomRoles.Sapper:
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
                                __instance.AbilityButton?.SetUsesRemaining((int)player.GetAbilityUseLimit());
                            }

                            break;
                        case CustomRoles.QuickShooter:
                            if (Options.UsePets.GetBool())
                            {
                                __instance.PetButton?.OverrideText(GetString("QuickShooterShapeshiftText"));
                            }
                            else
                            {
                                __instance.AbilityButton?.OverrideText(GetString("QuickShooterShapeshiftText"));
                                __instance.AbilityButton?.SetUsesRemaining(QuickShooter.ShotLimit.GetValueOrDefault(PlayerControl.LocalPlayer.PlayerId, 0));
                            }

                            break;
                        case CustomRoles.Camouflager:
                            __instance.AbilityButton?.OverrideText(GetString("CamouflagerShapeshiftText"));
                            __instance.AbilityButton?.SetUsesRemaining((int)PlayerControl.LocalPlayer.GetAbilityUseLimit());
                            break;
                        case CustomRoles.OverKiller:
                            __instance.KillButton?.OverrideText(GetString("OverKillerButtonText"));
                            break;
                        case CustomRoles.KB_Normal:
                            __instance.KillButton?.OverrideText(GetString("GamerButtonText"));
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
                                __instance.AbilityButton?.SetUsesRemaining((int)player.GetAbilityUseLimit());
                            }

                            break;
                        case CustomRoles.Sheriff:
                            usedButton?.OverrideText(GetString("SheriffKillButtonText"));
                            break;
                        case CustomRoles.Crusader:
                            usedButton?.OverrideText(GetString("CrusaderKillButtonText"));
                            break;
                        case CustomRoles.Totocalcio:
                            __instance.KillButton?.OverrideText(GetString("TotocalcioKillButtonText"));
                            break;
                        case CustomRoles.Romantic:
                            __instance.KillButton?.OverrideText(!Romantic.HasPickedPartner ? GetString("RomanticKillButtonText") : GetString("MedicalerButtonText"));
                            break;
                        case CustomRoles.VengefulRomantic:
                            __instance.KillButton?.OverrideText(GetString("VengefulRomanticKillButtonText"));
                            break;
                        case CustomRoles.Succubus:
                            __instance.KillButton?.OverrideText(GetString("SuccubusKillButtonText"));
                            break;
                        case CustomRoles.Amnesiac:
                            ActionButton amneButton = Amnesiac.RememberMode.GetValue() == 0 ? __instance.KillButton : __instance.ReportButton;
                            amneButton?.OverrideText(GetString("RememberButtonText"));
                            break;
                        case CustomRoles.Monarch:
                            usedButton?.OverrideText(GetString("MonarchKillButtonText"));
                            break;
                        case CustomRoles.Deputy:
                            usedButton?.OverrideText(GetString("DeputyHandcuffText"));
                            break;
                        case CustomRoles.Hangman:
                            if (shapeshifting) __instance.KillButton?.OverrideText(GetString("HangmanKillButtonTextDuringSS"));
                            __instance.AbilityButton?.SetUsesRemaining((int)player.GetAbilityUseLimit());
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
                            __instance.AbilityButton?.SetUsesRemaining((int)PlayerControl.LocalPlayer.GetAbilityUseLimit());
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
                        LowerInfoText = Object.Instantiate(__instance.KillButton.cooldownTimerText, __instance.transform, true);
                        LowerInfoText.alignment = TextAlignmentOptions.Center;
                        LowerInfoText.transform.localPosition = new(0, -2f, 0);
                        LowerInfoText.overflowMode = TextOverflowModes.Overflow;
                        LowerInfoText.enableWordWrapping = false;
                        LowerInfoText.color = Color.white;
                        LowerInfoText.fontSize = LowerInfoText.fontSizeMax = LowerInfoText.fontSizeMin = 2.7f;
                    }

                    var state = Main.PlayerStates[player.PlayerId];

                    LowerInfoText.text = Options.CurrentGameMode switch
                    {
                        CustomGameMode.SoloKombat => SoloKombatManager.GetHudText(),
                        CustomGameMode.FFA when player.IsHost() => FFAManager.GetHudText(),
                        CustomGameMode.MoveAndStop when player.IsHost() => MoveAndStopManager.HUDText,
                        CustomGameMode.HotPotato when player.IsHost() => HotPotatoManager.GetSuffixText(player.PlayerId),
                        CustomGameMode.HideAndSeek when player.IsHost() => HnSManager.GetSuffixText(player, player, isHUD: true),
                        CustomGameMode.Standard => state.Role.GetSuffix(player, player, true, GameStates.IsMeeting) + state.SubRoles switch
                        {
                            { } s when s.Contains(CustomRoles.Asthmatic) => Asthmatic.GetSuffixText(player.PlayerId),
                            _ => string.Empty
                        },
                        _ => string.Empty,
                    };
                    if (GetCD_HUDText() != string.Empty) LowerInfoText.text = $"{GetCD_HUDText()}\n{LowerInfoText.text}";

                    string GetCD_HUDText() => !Options.UsePets.GetBool() || !Main.AbilityCD.TryGetValue(player.PlayerId, out var CD)
                        ? string.Empty
                        : string.Format(GetString("CDPT"), CD.TOTALCD - (Utils.TimeStamp - CD.START_TIMESTAMP) + 1);

                    LowerInfoText.enabled = LowerInfoText.text != string.Empty;

                    if ((!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay) || GameStates.IsMeeting)
                    {
                        LowerInfoText.enabled = false;
                    }

                    if (player.CanUseKillButton() && !player.GetCustomRole().UsesPetInsteadOfKill())
                    {
                        __instance.KillButton?.ToggleVisible(player.IsAlive() && GameStates.IsInTask);
                        player.Data.Role.CanUseKillButton = true;
                    }
                    else
                    {
                        __instance.KillButton?.SetDisabled();
                        __instance.KillButton?.ToggleVisible(false);
                    }

                    bool CanUseVent = (player.CanUseImpostorVentButton() || player.inVent) && GameStates.IsInTask;
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
                __instance.ToggleMapVisible(new()
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
        catch (NullReferenceException e)
        {
            if (LastNullError >= Utils.TimeStamp) return;
            LastNullError = Utils.TimeStamp + 2;
            Utils.ThrowException(e);
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ToggleHighlight))]
class ToggleHighlightPatch
{
    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");

    public static void Postfix(PlayerControl __instance /*[HarmonyArgument(0)] bool active,*/ /*[HarmonyArgument(1)] RoleTeamTypes team*/)
    {
        if (!GameStates.IsInTask) return;
        var player = PlayerControl.LocalPlayer;

        if (player.CanUseKillButton())
        {
            __instance.cosmetics.currentBodySprite.BodySprite.material.SetColor(OutlineColor, Utils.GetRoleColor(player.GetCustomRole()));
        }
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.SetOutline))]
class SetVentOutlinePatch
{
    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int AddColor = Shader.PropertyToID("_AddColor");

    public static void Postfix(Vent __instance, [HarmonyArgument(1)] ref bool mainTarget)
    {
        Color color = PlayerControl.LocalPlayer.GetRoleColor();
        __instance.myRend.material.SetColor(OutlineColor, color);
        __instance.myRend.material.SetColor(AddColor, mainTarget ? color : Color.clear);
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.SetHudActive), [typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool)])]
class SetHudActivePatch
{
    public static bool IsActive;

    public static void Prefix( /*HudManager __instance,*/ [HarmonyArgument(2)] ref bool isActive)
    {
        isActive &= !GameStates.IsMeeting;
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
            case CustomGameMode.HotPotato:
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
            case CustomGameMode.HideAndSeek:
                __instance.ReportButton?.ToggleVisible(false);
                __instance.SabotageButton?.ToggleVisible(false);
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
            case CustomRoles.Pelican:
            case CustomRoles.FFF:
            case CustomRoles.Medic:
            case CustomRoles.Gamer:
            case CustomRoles.DarkHide:
            case CustomRoles.Farseer:
            case CustomRoles.Crusader:
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(false);
                __instance.ImpostorVentButton?.ToggleVisible(false);
                break;

            case CustomRoles.KB_Normal:
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(false);
                __instance.ReportButton?.ToggleVisible(false);
                break;
            case CustomRoles.Parasite:
            case CustomRoles.Refugee:
                __instance.SabotageButton?.ToggleVisible(true);
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
        __instance.SabotageButton?.ToggleVisible(player.CanUseSabotage());
    }
}

[HarmonyPatch(typeof(VentButton), nameof(VentButton.DoClick))]
class VentButtonDoClickPatch
{
    public static bool Animating;

    public static void Prefix()
    {
        Animating = true;
        _ = new LateTask(() => { Animating = false; }, 0.6f, log: false);
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
            if (player.Is(CustomRoleTypes.Impostor) || player.CanUseSabotage() || player.Is(CustomRoles.Glitch) || player.Is(CustomRoles.WeaponMaster) || player.Is(CustomRoles.Magician) || player.Is(CustomRoles.Parasite) || player.Is(CustomRoles.Refugee) || (player.Is(CustomRoles.Jackal) && Jackal.CanSabotage.GetBool()) || (player.Is(CustomRoles.Traitor) && Traitor.CanSabotage.GetBool()))
                opts.Mode = MapOptions.Modes.Sabotage;
            else
                opts.Mode = MapOptions.Modes.Normal;
        }

        if (Main.GodMode.Value) opts.ShowLivePlayerPosition = true;

        return true;
    }
}

[HarmonyPatch(typeof(InfectedOverlay), nameof(InfectedOverlay.Update))]
class SabotageMapPatch
{
    public static Dictionary<SystemTypes, TextMeshPro> TimerTexts = [];

    public static void Postfix(InfectedOverlay __instance)
    {
        float perc = __instance.sabSystem.PercentCool;
        int total = __instance.sabSystem.initialCooldown ? 10 : 30;
        if (SabotageSystemTypeRepairDamagePatch.IsCooldownModificationEnabled) total = (int)SabotageSystemTypeRepairDamagePatch.ModifiedCooldownSec;
        int remaining = Math.Clamp(total - (int)Math.Ceiling((1f - perc) * total) + 1, 0, total);

        foreach (var mr in __instance.rooms)
        {
            if (mr.special == null || mr.special.transform == null) continue;
            var room = mr.room;
            if (!TimerTexts.ContainsKey(room))
            {
                TimerTexts[room] = Object.Instantiate(HudManager.Instance.KillButton.cooldownTimerText, mr.special.transform, true);
                TimerTexts[room].alignment = TextAlignmentOptions.Center;
                TimerTexts[room].transform.localPosition = mr.special.transform.localPosition;
                TimerTexts[room].transform.localPosition = new(0, -0.4f, 0f);
                TimerTexts[room].overflowMode = TextOverflowModes.Overflow;
                TimerTexts[room].enableWordWrapping = false;
                TimerTexts[room].color = Color.white;
                TimerTexts[room].fontSize = TimerTexts[room].fontSizeMax = TimerTexts[room].fontSizeMin = 2.5f;
                TimerTexts[room].sortingOrder = 100;
            }

            bool isActive = Utils.IsActive(room);
            bool isOtherActive = TimerTexts.Keys.Any(Utils.IsActive);
            bool doorBlock = __instance.DoorsPreventingSabotage;
            TimerTexts[room].text = $"<b><#ff{(isActive || isOtherActive || doorBlock ? "00" : "ff")}00>{(!isActive && !isOtherActive && !doorBlock ? remaining : GetString(isActive && !doorBlock ? "SabotageActiveIndicator" : "SabotageDisabledIndicator"))}</color></b>";
            TimerTexts[room].enabled = remaining > 0 || isActive || isOtherActive || doorBlock;
        }
    }
}

[HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
class TaskPanelBehaviourPatch
{
    public static void Postfix(TaskPanelBehaviour __instance)
    {
        if (!GameStates.IsModHost) return;
        PlayerControl player = PlayerControl.LocalPlayer;

        var taskText = __instance.taskText.text;
        if (taskText == "None") return;

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
                    AllText = list.Where(x => SummaryText.ContainsKey(x.Item2)).Aggregate(AllText, (current, id) => current + "\r\n" + SummaryText[id.Item2]);

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
                    AllText = list2.Where(x => SummaryText2.ContainsKey(x.Item2)).Aggregate(AllText, (current, id) => current + "\r\n" + SummaryText2[id.Item2]);

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
                    list3 = [.. list3.OrderBy(x => !Utils.GetPlayerById(x.Item2).IsAlive())];
                    foreach (var id in list3.Where(x => SummaryText3.ContainsKey(x.Item2)).ToArray())
                    {
                        bool alive = Utils.GetPlayerById(id.Item2).IsAlive();
                        AllText += $"{(!alive ? "<#777777>" : string.Empty)}<size=1.6>\r\n{(alive ? SummaryText3[id.Item2] : SummaryText3[id.Item2].RemoveHtmlTags())}{(!alive ? $"  <#ff0000>{GetString("Dead")}</color>" : string.Empty)}</size>";
                    }

                    break;

                case CustomGameMode.HotPotato:

                    List<string> SummaryText4 = [];
                    SummaryText4.AddRange(from id in Main.PlayerStates.Keys let pc = Utils.GetPlayerById(id) let name = pc.GetRealName().RemoveHtmlTags().Replace("\r\n", string.Empty) let alive = pc.IsAlive() select $"{(!alive ? "<size=70%><#777777>" : "<size=80%>")}{HotPotatoManager.GetIndicator(id)}{Utils.ColorString(Main.PlayerColors[id], name)}{(!alive ? $"</color>  <#ff0000>{GetString("Dead")}</color></size>" : "</size>")}");

                    AllText += $"\r\n\r\n{string.Join('\n', SummaryText4)}";

                    break;

                case CustomGameMode.HideAndSeek:

                    AllText += $"\r\n\r\n{HnSManager.GetTaskBarText()}";

                    break;
            }

            __instance.taskText.text = AllText;
        }

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
            SystemType *= 10;
            SystemType += num;
        }
        else
        {
            amount *= 10;
            amount += num;
        }
    }

    public static void InputEnter()
    {
        if (!TypingAmount)
        {
            TypingAmount = true;
        }
        else
        {
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
        return SystemType + "(" + ((SystemTypes)SystemType) + ")\r\n" + amount;
    }
}

// The following code comes from Crowded https://github.com/CrowdedMods/CrowdedMod/blob/master/src/CrowdedMod/Patches/CreateGameOptionsPatches.cs