using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static EHR.Translator;


namespace EHR.Patches;

//[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class HudManagerPatch
{
    private static TextMeshPro LowerInfoText;
    private static TextMeshPro OverriddenRolesText;
    private static TextMeshPro SettingsText;
    private static TextMeshPro AutoGMRotationStatusText;

    public static long AutoGMRotationCooldownTimerEndTS;
    private static long LastNullError;
    public static Color? CooldownTimerFlashColor = null;
    public static string AchievementUnlockedText = string.Empty;

    public static void ClearLowerInfoText()
    {
        LowerInfoText.text = string.Empty;
    }

    public static void Postfix(HudManager __instance)
    {
        try
        {
            LoadingScreen.Update();

            PlayerControl player = PlayerControl.LocalPlayer;
            if (player == null) return;

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                if ((!AmongUsClient.Instance.IsGameStarted || !GameStates.IsOnlineGame) && player.CanMove)
                    player.Collider.offset = new(0f, 127f);
            }

            if (Math.Abs(player.Collider.offset.y - 127f) < 0.1f)
            {
                if (!Input.GetKey(KeyCode.LeftControl) || (AmongUsClient.Instance.IsGameStarted && GameStates.IsOnlineGame))
                    player.Collider.offset = new(0f, -0.3636f);
            }

            if (__instance == null) return;

            if (GameStates.IsLobby)
            {
                if (PingTrackerUpdatePatch.Instance != null && SettingsText == null)
                {
                    SettingsText = Object.Instantiate(__instance.KillButton.cooldownTimerText, __instance.transform, true);
                    SettingsText.name = "EHR_SettingsText";
                    SettingsText.alignment = TextAlignmentOptions.TopLeft;
                    SettingsText.verticalAlignment = VerticalAlignmentOptions.Top;
                    SettingsText.transform.position = AspectPosition.ComputeWorldPosition(Camera.main, AspectPosition.EdgeAlignments.LeftTop, new(0.38f, 0.3f, 0f));
                    SettingsText.fontSize = SettingsText.fontSizeMin = SettingsText.fontSizeMax = 1.2f;
                    SettingsText.overflowMode = TextOverflowModes.Overflow;
                    SettingsText.enableWordWrapping = false;
                }
                else if (PingTrackerUpdatePatch.Instance == null && SettingsText != null)
                {
                    Object.Destroy(SettingsText.gameObject);
                    SettingsText = null;
                }

                if (SettingsText != null)
                {
                    SettingsText.text = OptionShower.GetTextNoFresh();
                    SettingsText.enabled = SettingsText.text != string.Empty;
                }
            }
            else if (SettingsText != null)
            {
                Object.Destroy(SettingsText.gameObject);
                SettingsText = null;
            }

            if (AmongUsClient.Instance.AmHost)
            {
                if (OverriddenRolesText == null)
                {
                    OverriddenRolesText = Object.Instantiate(__instance.KillButton.cooldownTimerText, __instance.transform, true);
                    OverriddenRolesText.alignment = TextAlignmentOptions.Right;
                    OverriddenRolesText.verticalAlignment = VerticalAlignmentOptions.Top;
                    OverriddenRolesText.transform.localPosition = new(2.5f, 2.5f, 0);
                    OverriddenRolesText.overflowMode = TextOverflowModes.Overflow;
                    OverriddenRolesText.enableWordWrapping = false;
                    OverriddenRolesText.color = Color.white;
                    OverriddenRolesText.fontSize = OverriddenRolesText.fontSizeMax = OverriddenRolesText.fontSizeMin = 2.5f;
                }

                if (Main.SetRoles.Count > 0 || Main.SetAddOns.Count > 0)
                {
                    Dictionary<byte, string> resultText = [];
                    var first = true;

                    foreach (KeyValuePair<byte, CustomRoles> item in Main.SetRoles)
                    {
                        PlayerControl pc = Utils.GetPlayerById(item.Key);
                        string prefix = first ? string.Empty : "\n";
                        var text = $"{prefix}{(item.Key == 0 ? "Host" : $"{(pc == null ? $"ID {item.Key}" : $"{pc.GetRealName()}")}")} - <color={Main.RoleColors.GetValueOrDefault(item.Value, "#ffffff")}>{GetString(item.Value.ToString())}</color>";
                        resultText[item.Key] = text;
                        first = false;
                    }

                    if (Main.SetRoles.Count == 0) first = true;

                    foreach (KeyValuePair<byte, List<CustomRoles>> item in Main.SetAddOns)
                    {
                        foreach (CustomRoles role in item.Value)
                        {
                            PlayerControl pc = Utils.GetPlayerById(item.Key);

                            if (resultText.ContainsKey(item.Key))
                            {
                                var text = $" <#ffffff>(</color><color={Main.RoleColors.GetValueOrDefault(role, "#ffffff")}>{GetString(role.ToString())}</color><#ffffff>)</color>";
                                resultText[item.Key] += text;
                            }
                            else
                            {
                                string prefix = first ? string.Empty : "\n";
                                var text = $"{prefix}{(item.Key == 0 ? "Host" : $"{(pc == null ? $"ID {item.Key}" : $"{pc.GetRealName()}")}")} - <#ffffff>(</color><color={Main.RoleColors.GetValueOrDefault(role, "#ffffff")}>{GetString(role.ToString())}</color><#ffffff>)</color>";
                                resultText[item.Key] = text;
                                first = false;
                            }
                        }
                    }

                    OverriddenRolesText.text = string.Join(string.Empty, resultText.Values);
                }
                else
                    OverriddenRolesText.text = string.Empty;

                OverriddenRolesText.enabled = OverriddenRolesText.text != string.Empty;


                if (Options.AutoGMRotationEnabled)
                {
                    if (AutoGMRotationStatusText == null)
                    {
                        AutoGMRotationStatusText = Object.Instantiate(__instance.KillButton.cooldownTimerText, __instance.transform, true);
                        AutoGMRotationStatusText.alignment = TextAlignmentOptions.Left;
                        AutoGMRotationStatusText.verticalAlignment = VerticalAlignmentOptions.Top;
                        AutoGMRotationStatusText.transform.localPosition = new(-2.5f, 2.5f, 0);
                        AutoGMRotationStatusText.overflowMode = TextOverflowModes.Overflow;
                        AutoGMRotationStatusText.enableWordWrapping = false;
                        AutoGMRotationStatusText.color = Color.white;
                        AutoGMRotationStatusText.fontSize = AutoGMRotationStatusText.fontSizeMax = AutoGMRotationStatusText.fontSizeMin = 2.5f;
                    }

                    AutoGMRotationStatusText.text = BuildAutoGMRotationStatusText(false);
                    AutoGMRotationStatusText.enabled = AutoGMRotationStatusText.text != string.Empty && GameStates.IsLobby;
                }
                else if (AutoGMRotationStatusText != null)
                {
                    AutoGMRotationStatusText.text = string.Empty;
                    AutoGMRotationStatusText.enabled = false;
                }
            }
            else if (GameStates.IsLobby)
            {
                new ActionButton[]
                {
                    __instance.ReportButton,
                    __instance.KillButton,
                    __instance.AbilityButton,
                    __instance.ImpostorVentButton,
                    __instance.SabotageButton
                }.Do(x => x?.Hide());
            }
            else if (Options.CurrentGameMode != CustomGameMode.Standard) __instance.ReportButton?.Hide();

            // The following will not be executed unless the game is in progress
            if (!AmongUsClient.Instance.IsGameStarted) return;

            bool shapeshifting = player.IsShifted();

            if (SetHudActivePatch.IsActive)
            {
                if (player.IsAlive() || Options.CurrentGameMode != CustomGameMode.Standard)
                {
                    if (player.Data.Role is ShapeshifterRole ssrole && !player.shapeshifting)
                    {
                        float timer = shapeshifting ? ssrole.durationSecondsRemaining : ssrole.cooldownSecondsRemaining;
                        AbilityButton button = __instance.AbilityButton;

                        if (timer > 0f)
                        {
                            Color color = shapeshifting ? new Color32(0, 165, 255, 255) : Color.white;
                            button.cooldownTimerText.text = Utils.ColorString(color, Mathf.CeilToInt(timer).ToString());
                            button.cooldownTimerText.gameObject.SetActive(true);
                        }
                    }

                    CustomRoles role = player.GetCustomRole();

                    bool usesPetInsteadOfKill = player.UsesPetInsteadOfKill();
                    if (usesPetInsteadOfKill) __instance.PetButton?.OverrideText(GetString("KillButtonText"));

                    ActionButton usedButton = __instance.KillButton;
                    if (usesPetInsteadOfKill) usedButton = __instance.PetButton;

                    __instance.KillButton?.OverrideText(GetString("KillButtonText"));
                    __instance.ReportButton?.OverrideText(GetString("ReportButtonText"));
                    __instance.PetButton?.OverrideText(GetString("PetButtonText"));
                    __instance.ImpostorVentButton?.OverrideText(GetString("VentButtonText"));
                    __instance.SabotageButton?.OverrideText(GetString("SabotageButtonText"));

                    RoleTypes roleTypes = player.GetRoleTypes();
                    __instance.AbilityButton?.OverrideText(GetString($"AbilityButtonText.{roleTypes}"));

                    PlayerState state = Main.PlayerStates[player.PlayerId];

                    state.Role.SetButtonTexts(__instance, player.PlayerId);

                    switch (role)
                    {
                        case CustomRoles.Farseer:
                        case CustomRoles.Escort:
                        case CustomRoles.Gaulois:
                        case CustomRoles.Sheriff:
                        case CustomRoles.Crusader:
                        case CustomRoles.Monarch:
                            usedButton?.OverrideText(GetString($"{role}KillButtonText"));
                            break;
                        case CustomRoles.Analyst:
                            usedButton?.OverrideText(GetString("AnalyzerKillButtonText"));
                            break;
                        case CustomRoles.Aid:
                        case CustomRoles.DonutDelivery:
                        case CustomRoles.Medic:
                            usedButton?.OverrideText(GetString("MedicalerButtonText"));
                            break;
                        case CustomRoles.KB_Normal:
                        case CustomRoles.BedWarsPlayer:
                            __instance.KillButton?.OverrideText(GetString("GamerButtonText"));
                            break;
                        case CustomRoles.Deputy:
                            usedButton?.OverrideText(GetString("DeputyHandcuffText"));
                            break;
                        case CustomRoles.CTFPlayer:
                            __instance.AbilityButton?.OverrideText(GetString("CTF_ButtonText"));
                            break;
                        case CustomRoles.RRPlayer when __instance.AbilityButton != null && RoomRush.VentLimit.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out int ventLimit):
                            __instance.AbilityButton.SetUsesRemaining(ventLimit);
                            break;
                    }

                    if (role.PetActivatedAbility() && Options.CurrentGameMode == CustomGameMode.Standard && player.GetRoleTypes() != RoleTypes.Engineer && !role.OnlySpawnsWithPets() && !role.AlwaysUsesPhantomBase() && !player.GetCustomSubRoles().Any(StartGameHostPatch.BasisChangingAddons.ContainsKey) && role is not CustomRoles.Changeling and not CustomRoles.Assassin && (!role.SimpleAbilityTrigger() || !Options.UsePhantomBasis.GetBool() || !(player.IsNeutralKiller() && Options.UsePhantomBasisForNKs.GetBool())))
                        __instance.AbilityButton?.Hide();

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

                    LowerInfoText.text = Options.CurrentGameMode switch
                    {
                        CustomGameMode.SoloKombat => SoloPVP.GetHudText(),
                        CustomGameMode.FFA when player.IsHost() => FreeForAll.GetHudText(),
                        CustomGameMode.MoveAndStop when player.IsHost() => MoveAndStop.HUDText,
                        CustomGameMode.HotPotato when player.IsHost() => HotPotato.GetSuffixText(player.PlayerId),
                        CustomGameMode.HideAndSeek when player.IsHost() => CustomHnS.GetSuffixText(player, player, true),
                        CustomGameMode.NaturalDisasters => NaturalDisasters.SuffixText(),
                        CustomGameMode.Standard => state.Role.GetSuffix(player, player, true, GameStates.IsMeeting) + GetAddonSuffixes(),
                        _ => string.Empty
                    };

                    string GetAddonSuffixes()
                    {
                        string[] suffixes = state.SubRoles.Select(s => s switch
                        {
                            CustomRoles.Asthmatic => Asthmatic.GetSuffixText(player.PlayerId),
                            CustomRoles.Spurt => Spurt.GetSuffix(player, true),
                            CustomRoles.Dynamo => Dynamo.GetSuffix(player, true),
                            CustomRoles.Deadlined => Deadlined.GetSuffix(player, true),
                            CustomRoles.Introvert => Introvert.GetSelfSuffix(player),
                            _ => string.Empty
                        }).Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToArray();

                        return suffixes.Length > 0
                            ? $"\n{string.Join('\n', suffixes)}"
                            : string.Empty;
                    }

                    string cdHUDText = !Options.UsePets.GetBool() || !Main.AbilityCD.TryGetValue(player.PlayerId, out (long StartTimeStamp, int TotalCooldown) CD)
                        ? string.Empty
                        : string.Format(GetString("CDPT"), CD.TotalCooldown - (Utils.TimeStamp - CD.StartTimeStamp) + 1);

                    bool hasCD = cdHUDText != string.Empty;

                    if (hasCD)
                    {
                        if (CooldownTimerFlashColor.HasValue) cdHUDText = $"<b>{Utils.ColorString(CooldownTimerFlashColor.Value, cdHUDText.RemoveHtmlTags())}</b>";

                        LowerInfoText.text = $"{cdHUDText}\n{LowerInfoText.text}";
                    }

                    if (AchievementUnlockedText != string.Empty)
                    {
                        LowerInfoText.text = LowerInfoText.text == string.Empty
                            ? AchievementUnlockedText
                            : $"{AchievementUnlockedText}\n\n{LowerInfoText.text}\n\n\n\n";
                    }

                    LowerInfoText.enabled = hasCD || LowerInfoText.text != string.Empty;

                    if ((!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay) || GameStates.IsMeeting)
                        LowerInfoText.enabled = false;

                    bool allowedRole = role is CustomRoles.Necromancer or CustomRoles.Deathknight or CustomRoles.Refugee or CustomRoles.Sidekick;

                    if (player.CanUseKillButton() && (allowedRole || !usesPetInsteadOfKill))
                    {
                        __instance.KillButton?.ToggleVisible(player.IsAlive() && GameStates.IsInTask);
                        player.Data.Role.CanUseKillButton = true;
                    }
                    else
                    {
                        __instance.KillButton?.SetDisabled();
                        __instance.KillButton?.ToggleVisible(false);
                    }

                    if (Options.CurrentGameMode != CustomGameMode.Standard)
                        __instance.ReportButton.Hide();

                    __instance.ImpostorVentButton?.ToggleVisible((player.CanUseImpostorVentButton() || (player.inVent && player.GetRoleTypes() != RoleTypes.Engineer)) && GameStates.IsInTask);
                    player.Data.Role.CanVent = player.CanUseVent();

                    if ((usesPetInsteadOfKill && player.Is(CustomRoles.Nimble) && player.GetRoleTypes() == RoleTypes.Engineer) || player.Is(CustomRoles.GM))
                        __instance.AbilityButton.SetEnabled();

                    __instance.SabotageButton.ToggleVisible(player.GetRoleTypes() is RoleTypes.ImpostorGhost or RoleTypes.Impostor or RoleTypes.Phantom or RoleTypes.Shapeshifter);
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

            if (AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame) RepairSender.Enabled = false;

            if (Input.GetKeyDown(KeyCode.RightShift) && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            {
                RepairSender.Enabled = !RepairSender.Enabled;
                RepairSender.Reset();
            }

            if (RepairSender.Enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
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
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static string BuildAutoGMRotationStatusText(bool chatMessage)
    {
        bool includesRandomChoice = Options.AutoGMRotationSlots.Exists(x => x.Slot.GetValue() == 2);
        int index = Options.AutoGMRotationIndex;
        List<CustomGameMode> list = Options.AutoGMRotationCompiled;

        CustomGameMode previousGM = index == 0 ? list[^1] : list[index - 1];
        CustomGameMode currentGM = list[index];
        CustomGameMode nextGM = index == list.Count - 1 ? list[0] : list[index + 1];
        CustomGameMode nextNextGM = index >= list.Count - 2 ? list[1] : list[index + 2];

        var sb = new StringBuilder();
        if (!chatMessage) sb.AppendLine(GetString("AutoGMRotationStatusText"));
        sb.AppendLine("....");
        if (!includesRandomChoice || index > 0) sb.AppendLine($"> {ToString(previousGM)}");
        sb.AppendLine($"<b>{GetString("AutoGMRotationStatusText.NextGM")}: {ToString(currentGM)}</b>");
        if (!includesRandomChoice || index < list.Count - 1) sb.AppendLine(ToString(nextGM));
        if (!includesRandomChoice || index < list.Count - 2) sb.AppendLine(ToString(nextNextGM));
        sb.AppendLine("....");

        if (!chatMessage)
        {
            long timerSecondsLeft = AutoGMRotationCooldownTimerEndTS - Utils.TimeStamp;
            if (timerSecondsLeft > 0) sb.AppendLine(string.Format(GetString("AutoGMRotationStatusText.CooldownTimer"), timerSecondsLeft));
        }

        return sb.ToString().Trim();

        string ToString(CustomGameMode gm) => gm == CustomGameMode.All
            ? GetString("AutoGMRotationStatusText.GMPoll")
            : Utils.ColorString(Main.GameModeColors[gm], GetString(gm.ToString()));
    }
}

[HarmonyPatch(typeof(ActionButton), nameof(ActionButton.SetFillUp))]
internal static class ActionButtonSetFillUpPatch
{
    public static void Postfix(ActionButton __instance, [HarmonyArgument(0)] float timer)
    {
        if (__instance.isCoolingDown && timer is <= 90f and > 0f && !PlayerControl.LocalPlayer.shapeshifting && !VentButtonDoClickPatch.Animating)
        {
            RoleTypes roleType = PlayerControl.LocalPlayer.GetRoleTypes();

            bool usingAbility = roleType switch
            {
                RoleTypes.Engineer => PlayerControl.LocalPlayer.inVent,
                RoleTypes.Shapeshifter => PlayerControl.LocalPlayer.IsShifted(),
                _ => false
            };

            Color color = usingAbility ? new Color32(0, 165, 255, 255) : Color.white;
            __instance.cooldownTimerText.text = Utils.ColorString(color, Mathf.CeilToInt(timer).ToString());
            __instance.cooldownTimerText.gameObject.SetActive(true);
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ToggleHighlight))]
internal static class ToggleHighlightPatch
{
    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");

    public static void Postfix(PlayerControl __instance /*[HarmonyArgument(0)] bool active,*/ /*[HarmonyArgument(1)] RoleTeamTypes team*/)
    {
        if (!GameStates.IsInTask) return;

        PlayerControl player = PlayerControl.LocalPlayer;

        if (player.CanUseKillButton()) __instance.cosmetics.currentBodySprite.BodySprite.material.SetColor(OutlineColor, Utils.GetRoleColor(player.GetCustomRole()));
    }
}

[HarmonyPatch(typeof(KillButton), nameof(KillButton.SetTarget))]
internal static class KillButtonSetTargetPatch
{
    public static bool Prefix()
    {
        return false;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.SetOutline))]
internal static class SetVentOutlinePatch
{
    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int AddColor = Shader.PropertyToID("_AddColor");

    public static void Postfix(Vent __instance, [HarmonyArgument(1)] ref bool mainTarget)
    {
        Color color = PlayerControl.LocalPlayer.GetRoleColor();
        Material material = __instance.myRend.material;
        material.SetColor(OutlineColor, color);
        material.SetColor(AddColor, mainTarget ? color : Color.clear);
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.SetHudActive), typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool))]
internal static class SetHudActivePatch
{
    public static bool IsActive;

    public static void Prefix( /*HudManager __instance,*/ [HarmonyArgument(2)] ref bool isActive)
    {
        isActive &= !GameStates.IsMeeting;
    }

    public static void Postfix(HudManager __instance, [HarmonyArgument(2)] bool isActive)
    {
        if (GameStates.IsLobby || !isActive) __instance?.ReportButton?.ToggleVisible(false);

        if (__instance == null)
        {
            Logger.Fatal("HudManager __instance ended up being null", "SetHudActivePatch.Postfix");
            return;
        }

        IsActive = isActive;
        if (!isActive) return;

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.BedWars:
                __instance.AbilityButton?.ToggleVisible(true);
                __instance.ReportButton?.ToggleVisible(false);
                __instance.KillButton?.ToggleVisible(true);
                __instance.ImpostorVentButton?.ToggleVisible(true);
                __instance.SabotageButton?.ToggleVisible(false);
                return;
            case CustomGameMode.Quiz:
                __instance.KillButton.ToggleVisible(Quiz.AllowKills);
                goto case CustomGameMode.MoveAndStop;
            case CustomGameMode.MoveAndStop:
            case CustomGameMode.HotPotato:
            case CustomGameMode.Speedrun:
            case CustomGameMode.TheMindGame:
            case CustomGameMode.NaturalDisasters:
                __instance.ReportButton?.ToggleVisible(false);
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.ImpostorVentButton?.ToggleVisible(false);
                return;
            case CustomGameMode.FFA:
                __instance.AbilityButton?.ToggleVisible(false);
                goto case CustomGameMode.HideAndSeek;
            case CustomGameMode.RoomRush:
                __instance.ImpostorVentButton?.ToggleVisible(false);
                goto case CustomGameMode.HideAndSeek;
            case CustomGameMode.KingOfTheZones:
                __instance.ReportButton?.ToggleVisible(false);
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(false);
                __instance.KillButton?.ToggleVisible(true);
                __instance.ImpostorVentButton?.ToggleVisible(false);
                return;
            case CustomGameMode.CaptureTheFlag:
                __instance.ReportButton?.ToggleVisible(false);
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(true);
                return;
            case CustomGameMode.HideAndSeek:
                __instance.ReportButton?.ToggleVisible(false);
                __instance.SabotageButton?.ToggleVisible(false);
                return;
            case CustomGameMode.SoloKombat:
                __instance.ImpostorVentButton?.ToggleVisible(SoloPVP.CanVent);
                __instance.KillButton?.ToggleVisible(true);
                __instance.SabotageButton?.ToggleVisible(false);
                return;
        }

        PlayerControl player = PlayerControl.LocalPlayer;
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
                __instance.ImpostorVentButton?.ToggleVisible(false);
                break;

            case CustomRoles.KB_Normal:
                __instance.SabotageButton?.ToggleVisible(false);
                __instance.AbilityButton?.ToggleVisible(false);
                __instance.ReportButton?.ToggleVisible(false);
                break;
            case CustomRoles.Parasite:
            case CustomRoles.Refugee:
            case CustomRoles.Magician:
                __instance.SabotageButton?.ToggleVisible(true);
                break;
        }

        if (Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState ps) && ps.SubRoles.Contains(CustomRoles.Oblivious))
            __instance.ReportButton?.ToggleVisible(false);

        __instance.KillButton?.ToggleVisible(player.CanUseKillButton());
        __instance.ImpostorVentButton?.ToggleVisible(player.CanUseImpostorVentButton());
        __instance.SabotageButton?.ToggleVisible(player.GetRoleTypes() is RoleTypes.ImpostorGhost or RoleTypes.Impostor or RoleTypes.Phantom or RoleTypes.Shapeshifter);
    }
}

[HarmonyPatch(typeof(VentButton), nameof(VentButton.DoClick))]
internal static class VentButtonDoClickPatch
{
    public static bool Animating;

    public static void Prefix()
    {
        Animating = true;
        LateTask.New(() => { Animating = false; }, 0.6f, log: false);
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Show))]
internal static class MapBehaviourShowPatch
{
    public static bool Prefix(MapBehaviour __instance, ref MapOptions opts)
    {
        if (GameStates.IsMeeting) return true;

        PlayerControl player = PlayerControl.LocalPlayer;

        if (player.GetCustomRole() == CustomRoles.NiceHacker && NiceHacker.PlayerIdList.ContainsKey(player.PlayerId))
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

[HarmonyPatch(typeof(InfectedOverlay), nameof(InfectedOverlay.FixedUpdate))]
internal static class SabotageMapPatch
{
    public static Dictionary<SystemTypes, TextMeshPro> TimerTexts = [];

    public static void Postfix(InfectedOverlay __instance)
    {
        if (SubmergedCompatibility.IsSubmerged()) return;
        
        float perc = __instance.sabSystem.PercentCool;
        int total = __instance.sabSystem.initialCooldown ? 10 : 30;
        if (SabotageSystemTypeRepairDamagePatch.IsCooldownModificationEnabled) total = (int)SabotageSystemTypeRepairDamagePatch.ModifiedCooldownSec;

        int remaining = Math.Clamp(total - (int)Math.Ceiling((1f - perc) * total) + 1, 0, total);

        foreach (MapRoom mr in __instance.rooms)
        {
            if (mr.special == null || mr.special.transform == null) continue;

            SystemTypes room = mr.room;

            if (!TimerTexts.TryGetValue(room, out TextMeshPro timerText))
            {
                TimerTexts[room] = timerText = Object.Instantiate(HudManager.Instance.KillButton.cooldownTimerText, mr.special.transform, true);
                timerText.alignment = TextAlignmentOptions.Center;
                timerText.transform.localPosition = mr.special.transform.localPosition;
                timerText.transform.localPosition = new(0, -0.4f, 0f);
                timerText.overflowMode = TextOverflowModes.Overflow;
                timerText.enableWordWrapping = false;
                timerText.color = Color.white;
                timerText.fontSize = timerText.fontSizeMax = timerText.fontSizeMin = 2.5f;
                timerText.sortingOrder = 100;
            }

            bool isActive = Utils.IsActive(room);
            bool isOtherActive = TimerTexts.Keys.Any(Utils.IsActive);
            bool doorBlock = __instance.DoorsPreventingSabotage;
            timerText.text = $"<b><#ff{(isActive || isOtherActive || doorBlock ? "00" : "ff")}00>{(!isActive && !isOtherActive && !doorBlock ? remaining : isActive && !doorBlock ? "▶" : "⊘")}</color></b>";
            timerText.enabled = remaining > 0 || isActive || isOtherActive || doorBlock;
        }
    }
}

[HarmonyPatch(typeof(MapRoom), nameof(MapRoom.DoorsUpdate))]
internal static class MapRoomDoorsUpdatePatch
{
    public static Dictionary<SystemTypes, TextMeshPro> DoorTimerTexts = [];
    private static readonly int Percent = Shader.PropertyToID("_Percent");

    public static bool Prefix(MapRoom __instance)
    {
        if (!__instance.door || !ShipStatus.Instance || SubmergedCompatibility.IsSubmerged()) return false;

        SystemTypes room = __instance.room;

        float total;
        float timer;

        ISystemType system = ShipStatus.Instance.Systems[SystemTypes.Doors];
        var doorsSystemType = system.TryCast<DoorsSystemType>();
        var autoDoorsSystemType = system.TryCast<AutoDoorsSystemType>();

        if (doorsSystemType != null)
        {
            if (doorsSystemType.initialCooldown > 0f)
            {
                total = 10f;
                timer = doorsSystemType.initialCooldown;
                goto Skip;
            }

            total = 30f;
            timer = doorsSystemType.timers.TryGetValue(room, out float num) ? num : 0f;
            goto Skip;
        }

        if (autoDoorsSystemType != null)
        {
            if (autoDoorsSystemType.initialCooldown > 0.0)
            {
                total = 10f;
                timer = autoDoorsSystemType.initialCooldown;
                goto Skip;
            }

            foreach (OpenableDoor door in ShipStatus.Instance.AllDoors)
            {
                if (door.Room == room)
                {
                    var autoOpenDoor = door.TryCast<AutoOpenDoor>();

                    if (autoOpenDoor != null)
                    {
                        total = 30f;
                        timer = autoOpenDoor.CooldownTimer;
                        goto Skip;
                    }
                }
            }
        }

        total = 0f;
        timer = 0f;

        Skip:

        __instance.door.material.SetFloat(Percent, __instance.Parent.CanUseDoors ? timer / total : 1f);

        if (!DoorTimerTexts.TryGetValue(room, out TextMeshPro doorTimerText))
        {
            DoorTimerTexts[room] = doorTimerText = Object.Instantiate(HudManager.Instance.KillButton.cooldownTimerText, __instance.door.transform, true);
            doorTimerText.alignment = TextAlignmentOptions.Center;
            doorTimerText.transform.localPosition = __instance.door.transform.localPosition;
            doorTimerText.transform.localPosition = new(0, -0.4f, 0f);
            doorTimerText.overflowMode = TextOverflowModes.Overflow;
            doorTimerText.enableWordWrapping = false;
            doorTimerText.color = Color.white;
            doorTimerText.fontSize = doorTimerText.fontSizeMax = doorTimerText.fontSizeMin = 2.5f;
            doorTimerText.sortingOrder = 100;
        }

        var remaining = (int)Math.Ceiling(timer);
        bool canUseDoors = __instance.Parent.CanUseDoors;
        doorTimerText.text = $"<b><#ff{(!canUseDoors ? "00" : "a5")}00a5>{(canUseDoors ? remaining : "⊘")}</color></b>";
        doorTimerText.enabled = remaining > 0 || !canUseDoors;

        return false;
    }
}

[HarmonyPatch(typeof(CrewmateGhostRole), nameof(CrewmateGhostRole.SpawnTaskHeader))]
[HarmonyPatch(typeof(ImpostorGhostRole), nameof(ImpostorGhostRole.SpawnTaskHeader))]
[HarmonyPatch(typeof(ImpostorRole), nameof(ImpostorRole.SpawnTaskHeader))]
internal static class SpawnTaskHeaderPatch
{
    public static bool Prefix()
    {
        return !GameStates.InGame;
    }
}

[HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
internal static class TaskPanelBehaviourPatch
{
    public static void Postfix(TaskPanelBehaviour __instance, [HarmonyArgument(0)] string taskList)
    {
        PlayerControl player = PlayerControl.LocalPlayer;

        if (taskList == "None" || GameStates.IsLobby || player == null) return;

        if (MeetingStates.FirstMeeting)
        {
            NetworkedPlayerInfo data = PlayerControl.LocalPlayer.Data;
            if (data && data.Role) taskList = taskList.Replace($"\n{data.Role.NiceName} {FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.RoleHint)}\n{data.Role.BlurbMed}", string.Empty);
        }

        CustomRoles role = player.GetCustomRole();

        if (!role.IsVanilla() && role != CustomRoles.NotAssigned)
        {
            string roleInfo = player.GetRoleInfo();
            var roleWithInfo = $"<size=80%>{role.ToColoredString()}:\r\n{roleInfo}</size>";

            if (Options.CurrentGameMode != CustomGameMode.Standard)
            {
                string[] splitted = roleInfo.Split(' ');

                roleWithInfo = $"{GetString($"{Options.CurrentGameMode}")}\r\n" + (splitted.Length <= 3
                    ? $"<size=60%>{roleInfo}</size>\r\n"
                    : $"<size=60%>{string.Join(' ', splitted[..3])}\r\n{string.Join(' ', splitted[3..])}</size>\r\n");
            }
            else if (roleInfo.RemoveHtmlTags().Length > 35)
            {
                string[] split = roleInfo.Split(' ');
                int half = split.Length / 2;
                roleWithInfo = $"<size=80%>{role.ToColoredString()}:\r\n{string.Join(' ', split[..half])}\r\n{string.Join(' ', split[half..])}</size>";
            }

            string finalText = Utils.ColorString(player.GetRoleColor(), roleWithInfo);

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.Standard:

                    List<CustomRoles> subRoles = player.GetCustomSubRoles();

                    if (subRoles.Count > 0)
                    {
                        const int max = 3;
                        IEnumerable<string> s = subRoles.Take(max).Select(x => Utils.ColorString(Utils.GetRoleColor(x), $"\r\n\r\n{x.ToColoredString()}:\r\n{GetString($"{x}Info")}"));
                        finalText += s.Aggregate("<size=65%>", (current, next) => current + next) + "</size>";
                        int chunk = subRoles.Any(x => GetString(x.ToString()).Contains(' ')) ? 3 : 4;
                        if (subRoles.Count > max) finalText += $"\r\n<size=65%>....\r\n({subRoles.Skip(max).Chunk(chunk).Select(x => x.Join(r => r.ToColoredString())).Join(delimiter: ",\r\n")})</size>";
                    }

                    if (!Utils.HasTasks(player.Data, false) && taskList.Count(s => s == '\n') >= 2)
                        taskList = taskList.Insert(0, $"<size=55%>{Utils.ColorString(Utils.GetRoleColor(role).ShadeColor(0.2f), GetString("FakeTask"))}</size>\r\n");

                    finalText += $"\r\n\r\n<size=65%>{taskList}</size>";
                    if (MeetingStates.FirstMeeting) finalText += $"\r\n\r\n</color><size=60%>{GetString("PressF1ShowMainRoleDes")}";

                    break;

                case CustomGameMode.SoloKombat:

                    PlayerControl lpc = PlayerControl.LocalPlayer;

                    finalText += "\r\n<size=90%>";
                    finalText += $"\r\n{GetString("PVP.ATK")}: {SoloPVP.PlayerATK[lpc.PlayerId]:N1}";
                    finalText += $"\r\n{GetString("PVP.DF")}: {SoloPVP.PlayerDF[lpc.PlayerId]:N1}";
                    finalText += $"\r\n{GetString("PVP.RCO")}: {SoloPVP.PlayerHPReco[lpc.PlayerId]:N1}";
                    finalText += "\r\n</size>";

                    finalText += Main.PlayerStates.Keys.OrderBy(SoloPVP.GetRankFromScore).Aggregate("<size=70%>", (s, x) => $"{s}\r\n{SoloPVP.GetRankFromScore(x)}. {x.ColoredPlayerName()} - {string.Format(GetString("KillCount").TrimStart(' '), SoloPVP.KBScore.GetValueOrDefault(x, 0))}");

                    finalText += "</size>";
                    break;

                case CustomGameMode.FFA:

                    finalText += Main.PlayerStates.Keys.OrderBy(FreeForAll.GetRankFromScore).Aggregate("<size=70%>", (s, x) => $"{s}\r\n{FreeForAll.GetRankFromScore(x)}. {x.ColoredPlayerName()} -{string.Format(GetString("KillCount"), FreeForAll.KillCount.GetValueOrDefault(x, 0))}");

                    finalText += "</size>";
                    break;

                case CustomGameMode.MoveAndStop:

                    Dictionary<byte, string> SummaryText3 = [];

                    foreach (byte id in Main.PlayerStates.Keys.ToArray())
                    {
                        string name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
                        var summary = $"{Utils.GetProgressText(id)}  {Utils.ColorString(Main.PlayerColors[id], name)}";
                        if (Utils.GetProgressText(id).Trim() == string.Empty) continue;

                        SummaryText3[id] = summary;
                    }

                    if (player.IsAlive()) finalText += $"<size=70%>\r\n{taskList}\r\n</size>";

                    List<(int, byte)> list3 = [];
                    foreach (byte id in Main.PlayerStates.Keys) list3.Add((MoveAndStop.GetRankFromScore(id), id));

                    list3.Sort();
                    list3 = [.. list3.OrderBy(x => !Utils.GetPlayerById(x.Item2).IsAlive())];

                    foreach ((int, byte) id in list3.Where(x => SummaryText3.ContainsKey(x.Item2)).ToArray())
                    {
                        bool alive = Utils.GetPlayerById(id.Item2).IsAlive();
                        finalText += $"{(!alive ? "<#777777>" : string.Empty)}<size=1.6>\r\n{(alive ? SummaryText3[id.Item2].Replace("<size=2>", "<size=1.6>") : SummaryText3[id.Item2].RemoveHtmlTags())}{(!alive ? $"  <#ff0000>{GetString("Dead")}</color>" : string.Empty)}</size>";
                    }

                    break;

                case CustomGameMode.HotPotato:

                    List<string> SummaryText4 = [];
                    SummaryText4.AddRange(from pc in Main.AllPlayerControls let alive = pc.IsAlive() select $"{(!alive ? "<size=80%><#777777>" : "<size=80%>")}{HotPotato.GetIndicator(pc.PlayerId)}{pc.PlayerId.ColoredPlayerName()}{(!alive ? $"</color>  <#ff0000>{GetString("Dead")}</color></size>" : "</size>")}");

                    finalText += $"\r\n\r\n{string.Join('\n', SummaryText4)}";

                    break;

                case CustomGameMode.HideAndSeek when AmongUsClient.Instance.AmHost:

                    finalText += $"\r\n\r\n{CustomHnS.GetTaskBarText()}";

                    break;

                case CustomGameMode.Speedrun:

                    if (player.IsAlive()) finalText += $"<size=70%>\r\n{taskList}\r\n</size>";
                    finalText += $"\r\n<size=90%>{Speedrun.GetTaskBarText()}</size>";

                    break;

                case CustomGameMode.NaturalDisasters:

                    finalText += Main.AllPlayerControls
                        .Select(x => (pc: x, alive: x.IsAlive(), time: NaturalDisasters.SurvivalTime(x.PlayerId)))
                        .OrderByDescending(x => x.alive)
                        .ThenByDescending(x => x.time)
                        .Aggregate("<size=70%>", (s, x) => $"{s}\r\n{x.pc.PlayerId.ColoredPlayerName()} - {(x.alive ? $"<#00ff00>{GetString("Alive")}</color>" : $"{GetString("Dead")}: {string.Format(GetString("SurvivalTime"), x.time)}")}");

                    finalText += "</size>";
                    break;

                case CustomGameMode.RoomRush:

                    if (!RoomRush.PointsSystem)
                    {
                        finalText += Main.AllPlayerControls
                            .Select(x => (pc: x, alive: x.IsAlive(), time: RoomRush.GetSurvivalTime(x.PlayerId)))
                            .OrderByDescending(x => x.alive)
                            .ThenByDescending(x => x.time)
                            .Aggregate("<size=70%>", (s, x) => $"{s}\r\n{x.pc.PlayerId.ColoredPlayerName()} - {(x.alive ? $"<#00ff00>{GetString("Alive")}</color>" : $"{GetString("Dead")}: {string.Format(GetString("SurvivalTime"), x.time)}")}");
                    }
                    else
                    {
                        finalText += Main.AllPlayerControls
                            .Select(x => (pc: x, points_string: RoomRush.GetPoints(x.PlayerId), points_int: int.TryParse(RoomRush.GetPoints(x.PlayerId).Split('/')[0], out int points) ? points : 0))
                            .OrderByDescending(x => x.points_int)
                            .Aggregate("<size=70%>", (s, x) => $"{s}\r\n{x.pc.PlayerId.ColoredPlayerName()} - {x.points_string}");
                    }

                    finalText += "</size>";
                    break;

                case CustomGameMode.Quiz when AmongUsClient.Instance.AmHost:

                    finalText += "\r\n\r\n\r\n<size=70%>";
                    finalText += Quiz.GetTaskBarText();
                    finalText += "</size>";
                    break;

                case CustomGameMode.TheMindGame when AmongUsClient.Instance.AmHost:

                    finalText += "\r\n\r\n\r\n<size=70%>";
                    finalText += TheMindGame.GetTaskBarText();
                    finalText += "</size>";
                    break;

                case CustomGameMode.BedWars when AmongUsClient.Instance.AmHost:

                    finalText += "\r\n\r\n\r\n<size=70%>";
                    finalText += BedWars.GetHudText();
                    finalText += "</size>";
                    break;
            }

            __instance.taskText.text = finalText;
        }

        if (RepairSender.Enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            __instance.taskText.text = RepairSender.GetText();
    }
}

[HarmonyPatch(typeof(DialogueBox), nameof(DialogueBox.Show))]
internal static class DialogueBoxShowPatch
{
    public static bool Prefix(DialogueBox __instance, [HarmonyArgument(0)] string dialogue)
    {
        __instance.target.text = dialogue;

        if (Minigame.Instance) Minigame.Instance.Close();
        if (Minigame.Instance) Minigame.Instance.Close();

        __instance.gameObject.SetActive(true);
        return false;
    }
}

internal static class RepairSender
{
    public static bool Enabled;
    private static bool TypingAmount;

    private static int SystemType;
    private static int Amount;

    public static void Input(int num)
    {
        if (!TypingAmount)
        {
            SystemType *= 10;
            SystemType += num;
        }
        else
        {
            Amount *= 10;
            Amount += num;
        }
    }

    public static void InputEnter()
    {
        if (!TypingAmount)
            TypingAmount = true;
        else
            Send();
    }

    private static void Send()
    {
        ShipStatus.Instance.RpcUpdateSystem((SystemTypes)SystemType, (byte)Amount);
        Reset();
    }

    public static void Reset()
    {
        TypingAmount = false;
        SystemType = 0;
        Amount = 0;
    }

    public static string GetText()
    {
        return SystemType + "(" + (SystemTypes)SystemType + ")\r\n" + Amount;
    }
}
