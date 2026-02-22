using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace EHR.Patches;

public static class TextBoxPatch
{
    private static TextMeshPro PlaceHolderText;
    private static TextMeshPro CommandInfoText;
    private static TextMeshPro AdditionalInfoText;

    public static bool IsInvalidCommand;

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    [HarmonyPrefix]
    public static bool AllowAllCharacters(TextBoxTMP __instance, [HarmonyArgument(0)] string input, [HarmonyArgument(1)] string inputCompo = "")
    {
        if (Translator.GetUserTrueLang() == SupportedLangs.Russian || !__instance.gameObject.HasParentInHierarchy("ChatScreenRoot/ChatScreenContainer")) return true;

        var flag = false;
        __instance.AdjustCaretPosition(input.Length - __instance.text.Length);
        var ch = ' ';
        __instance.tempTxt.Clear();

        for (var index = 0; index < input.Length; ++index)
        {
            char upperInvariant = input[index];

            if (ch == ' ' && upperInvariant == ' ')
                __instance.AdjustCaretPosition(-1);
            else
            {
                switch (upperInvariant)
                {
                    case '\r':
                    case '\n':
                        flag = true;
                        break;
                    case '\b':
                        __instance.tempTxt.Length = Math.Max(__instance.tempTxt.Length - 1, 0);
                        __instance.AdjustCaretPosition(-1);
                        break;
                }

                if (__instance.ForceUppercase)
                    upperInvariant = char.ToUpperInvariant(upperInvariant);

                if (upperInvariant is '\r' or '\n' or '\b')
                    __instance.AdjustCaretPosition(-1);
                else
                {
                    __instance.tempTxt.Append(upperInvariant);
                    ch = upperInvariant;
                }
            }
        }

        if (!__instance.tempTxt.ToString().Equals(TranslationController.Instance.GetString(StringNames.EnterName), StringComparison.OrdinalIgnoreCase) && __instance.characterLimit > 0)
        {
            int length = __instance.tempTxt.Length;
            __instance.tempTxt.Length = Math.Min(__instance.tempTxt.Length, __instance.characterLimit);
            __instance.AdjustCaretPosition(-(length - __instance.tempTxt.Length));
        }

        input = __instance.tempTxt.ToString();

        if (!input.Equals(__instance.text) || !inputCompo.Equals(__instance.compoText))
        {
            __instance.text = input;
            __instance.compoText = inputCompo;
            string str = __instance.text;
            string compoText = __instance.compoText;

            if (__instance.Hidden)
            {
                str = "";
                for (var index = 0; index < __instance.text.Length; ++index) str += "*";
            }

            __instance.outputText.text = str + compoText;
            __instance.outputText.ForceMeshUpdate(true, true);
            if (__instance.keyboard != null) __instance.keyboard.text = __instance.text;
            __instance.OnChange.Invoke();

            if (__instance.tempTxt.Length == __instance.characterLimit && __instance.SendOnFullChars)
            {
                __instance.OnEnter.Invoke();
                __instance.LoseFocus();
            }
        }

        if (flag && !Input.GetKey(KeyCode.LeftShift)) __instance.OnEnter.Invoke();
        __instance.SetPipePosition();
        return false;
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    [HarmonyPostfix]
    public static void ShowCommandHelp(TextBoxTMP __instance)
    {
        try
        {
            if (!HudManager.InstanceExists || !__instance.gameObject.HasParentInHierarchy("ChatScreenRoot/ChatScreenContainer")) return;

            if (!Main.EnableCommandHelper.Value)
            {
                Destroy();
                return;
            }

            string input = __instance.outputText.text.Trim().Replace("\b", "");

            bool startsWithCmd = input.StartsWith("/cmd ");
            if (startsWithCmd) input = "/" + input[5..];

            if (!input.StartsWith('/') || input.Length < 2)
            {
                Destroy();
                IsInvalidCommand = false;
                return;
            }

            Command command = null;
            double highestMatchRate = 0;
            string inputCheck = input.Split(' ')[0];
            var exactMatch = false;
            bool english = TranslationController.Instance.currentLanguage.languageID == SupportedLangs.English;

            foreach (Command cmd in Command.AllCommands)
            {
                string[] commandForms = english ? [.. cmd.CommandForms.TakeWhile(x => x.All(char.IsAscii))] : cmd.CommandForms;

                foreach (string form in commandForms)
                {
                    if (english && !form.All(char.IsAscii)) continue;

                    string check = "/" + form;
                    if (check.Length < inputCheck.Length) continue;

                    if (check == inputCheck)
                    {
                        highestMatchRate = 1;
                        command = cmd;
                        exactMatch = true;
                        break;
                    }

                    var matchNum = 0;

                    for (var i = 0; i < inputCheck.Length; i++)
                    {
                        if (i >= check.Length) break;

                        if (inputCheck[i].Equals(check[i]))
                            matchNum++;
                        else
                            break;
                    }

                    double matchRate = (double)matchNum / inputCheck.Length;

                    if (matchRate > highestMatchRate)
                    {
                        highestMatchRate = matchRate;
                        command = cmd;
                    }
                }

                if (exactMatch) break;
            }

            if (command == null || highestMatchRate < 1)
            {
                Destroy();
                IsInvalidCommand = true;
                Color textColor = input is "/c" or "/cm" or "/cmd" ? Palette.Orange : Color.red;
                __instance.compoText.Color(textColor);
                __instance.outputText.color = textColor;
                return;
            }

            IsInvalidCommand = false;
            HudManager hud = HudManager.Instance;

            if (!PlaceHolderText)
            {
                PlaceHolderText = Object.Instantiate(__instance.outputText, __instance.outputText.transform.parent);
                PlaceHolderText.name = "PlaceHolderText";
                PlaceHolderText.color = new(0.7f, 0.7f, 0.7f, 0.7f);
                PlaceHolderText.transform.localPosition = __instance.outputText.transform.localPosition;
            }

            if (!CommandInfoText)
            {
                CommandInfoText = Object.Instantiate(hud.KillButton.cooldownTimerText, hud.transform.parent, true);
                CommandInfoText.name = "CommandInfoText";
                CommandInfoText.alignment = TextAlignmentOptions.Left;
                CommandInfoText.verticalAlignment = VerticalAlignmentOptions.Top;
                CommandInfoText.transform.localPosition = new(-3.2f, -2.35f, 0f);
                CommandInfoText.overflowMode = TextOverflowModes.Overflow;
                CommandInfoText.enableWordWrapping = false;
                CommandInfoText.color = Color.white;
                CommandInfoText.fontSize = CommandInfoText.fontSizeMax = CommandInfoText.fontSizeMin = 1.8f;
                CommandInfoText.sortingOrder = 1000;
                CommandInfoText.transform.SetAsLastSibling();
            }

            if (!AdditionalInfoText)
            {
                AdditionalInfoText = Object.Instantiate(hud.KillButton.cooldownTimerText, hud.transform.parent, true);
                AdditionalInfoText.name = "AdditionalInfoText";
                AdditionalInfoText.alignment = TextAlignmentOptions.Left;
                AdditionalInfoText.verticalAlignment = VerticalAlignmentOptions.Top;
                AdditionalInfoText.transform.localPosition = new(-5f, 0f, 0f);
                AdditionalInfoText.overflowMode = TextOverflowModes.Overflow;
                AdditionalInfoText.enableWordWrapping = false;
                AdditionalInfoText.color = Color.white;
                AdditionalInfoText.fontSize = AdditionalInfoText.fontSizeMax = AdditionalInfoText.fontSizeMin = 1.8f;
                AdditionalInfoText.sortingOrder = 1000;
                AdditionalInfoText.transform.SetAsLastSibling();
            }

            string inputForm = input.TrimStart('/');
            string text = "/" + (startsWithCmd ? "cmd " : string.Empty) + (exactMatch ? inputForm : command.CommandForms.TakeWhile(x => x.All(char.IsAscii) && x.StartsWith(inputForm)).MaxBy(x => x.Length));
            var info = $"<b>{command.Description}</b>";

            if (!command.CanUseCommand(PlayerControl.LocalPlayer))
                info = $"<#ff5555>{info}</color>  <#ff0000>╳ {Translator.GetString("Message.CommandUnavailableShort")}</color>";

            var additionalInfo = string.Empty;

            if (exactMatch && command.Arguments.Length > 0)
            {
                bool poll = command.CommandForms.Contains("poll");
                int spaces = poll ? input.SkipWhile(x => x != '?').Count(x => x == ' ') + 1 : input.Count(x => x == ' ');

                var preText = $"{text} {command.Arguments}";
                if (!poll) text += " " + (spaces >= command.ArgsDescriptions.Length ? string.Empty : string.Join(' ', command.Arguments.Split(' ')[spaces..]));

                string[] split = preText.Split(' ');
                string[] args = split[(startsWithCmd && split.Length > 2 ? 2 : 1)..];

                for (var i = 0; i < args.Length; i++)
                {
                    int length = command.ArgsDescriptions.Length;
                    if (length <= i) break;

                    int skip = poll ? input.TakeWhile(x => x != '?').Count(x => x == ' ') - 1 : 0;
                    string arg = poll ? i == 0 ? string.Join(' ', args[..++skip]) : args[spaces - 1 < i ? skip + i + spaces : skip + i] : spaces > length && i == length - 1 ? string.Join(' ', args[i..^length]) : args[spaces > i ? i : i + spaces];

                    string argName = command.Arguments.Split(' ')[i];
                    bool current = spaces - 1 == i || spaces > length && i == length - 1, invalid = IsInvalidArg(), valid = IsValidArg();

                    info += "\n" + (invalid, current, valid) switch
                    {
                        (true, true, false) => "<#ffa500>\u27a1    ",
                        (true, false, false) => "<#ff0000>        ",
                        (false, true, true) => "<#00ffa5>\u27a1 \u2713 ",
                        (false, false, true) => "<#00ffa5>\u2713</color> <#00ffff>     ",
                        (false, true, false) => "<#ffff44>\u27a1    ",
                        _ => "        "
                    };

                    info += $"   - <b>{arg}</b>{GetExtraArgInfo()}: {command.ArgsDescriptions[i]}";
                    if (current || invalid || valid) info += "</color>";

                    if (additionalInfo.Length == 0 && argName.Replace('[', '{').Replace(']', '}') is "{id}" or "{id1}" or "{id2}")
                    {
                        Dictionary<byte, string> allIds = Main.EnumeratePlayerControls().ToDictionary(x => x.PlayerId, x => x.PlayerId.ColoredPlayerName());
                        additionalInfo = $"<b><u>{Translator.GetString("PlayerIdList").TrimEnd(' ')}</u></b>\n{string.Join('\n', allIds.Select(x => $"<b>{x.Key}</b> \uffeb <b>{x.Value}</b>"))}";
                        OptionShower.CurrentPage = 0;
                    }

                    continue;

                    bool IsInvalidArg() =>
                        arg != argName && argName switch
                        {
                            "{uuid}" => arg.Length > 16,
                            "{id}" or "{id1}" or "{id2}" => !byte.TryParse(arg, out byte id) || Main.EnumeratePlayerControls().All(x => x.PlayerId != id),
                            "{ids}" => arg.Split(',').Any(x => !byte.TryParse(x, out _)),
                            "{number}" or "{level}" or "{duration}" or "{number1}" or "{number2}" => !int.TryParse(arg, out int num) || num < 0,
                            "{team}" => arg is not "crew" and not "imp",
                            "{role}" => !ChatCommands.GetRoleByName(arg, out _),
                            "{addon}" => !ChatCommands.GetRoleByName(arg, out CustomRoles role) || !role.IsAdditionRole(),
                            "{letter}" => arg.Length != 1 || !char.IsLetter(arg[0]),
                            "{chance}" => !int.TryParse(arg, out int chance) || chance < 0 || chance > 100 || chance % 5 != 0,
                            "{color}" => arg.Length != 6 || !arg.All(x => char.IsDigit(x) || x is >= 'a' and <= 'f' or >= 'A' and <= 'F') || !ColorUtility.TryParseHtmlString($"#{arg}", out _),
                            _ => false
                        };

                    bool IsValidArg() =>
                        argName switch
                        {
                            "{sourcepreset}" or "{targetpreset}" => int.TryParse(arg, out int preset) && preset is >= 1 and <= 10,
                            "{id}" or "{id1}" or "{id2}" => byte.TryParse(arg, out byte id) && Main.EnumeratePlayerControls().Any(x => x.PlayerId == id),
                            "{ids}" => arg.Split(',').All(x => byte.TryParse(x, out _)),
                            "{team}" => arg is "crew" or "imp",
                            "{role}" or "[role]" => ChatCommands.GetRoleByName(arg, out _),
                            "{addon}" => ChatCommands.GetRoleByName(arg, out CustomRoles role) && role.IsAdditionRole(),
                            "{chance}" => int.TryParse(arg, out int chance) && chance is >= 0 and <= 100 && chance % 5 == 0,
                            "{color}" => arg.Length == 6 && arg.All(x => char.IsDigit(x) || x is >= 'a' and <= 'f' or >= 'A' and <= 'F') && ColorUtility.TryParseHtmlString($"#{arg}", out _),
                            _ => false
                        };

                    string GetExtraArgInfo() =>
                        !IsValidArg()
                            ? string.Empty
                            : argName switch
                            {
                                "{id}" or "{id1}" or "{id2}" => $" ({byte.Parse(arg).ColoredPlayerName()})",
                                "{ids}" => $" ({string.Join(", ", arg.Split(',').Select(x => byte.Parse(x).ColoredPlayerName()))})",
                                "{role}" or "{addon}" or "[role]" when ChatCommands.GetRoleByName(arg, out CustomRoles role) => $" ({role.ToColoredString()})",
                                "{color}" when ColorUtility.TryParseHtmlString($"#{arg}", out Color color) => $" ({Utils.ColorString(color, "COLOR")})",
                                _ => string.Empty
                            };
                }
            }

            additionalInfo += startsWithCmd && AmongUsClient.Instance.AmHost ? $"\n\n<#00a5ff>ⓘ <b>{Translator.GetString("HostMayOmitCmdPrefix")}</b></color>" : string.Empty;

            PlaceHolderText.text = text;
            CommandInfoText.text = info;
            AdditionalInfoText.text = additionalInfo;

            PlaceHolderText.enabled = true;
            CommandInfoText.enabled = true;
            AdditionalInfoText.enabled = true;
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
            Destroy();
        }

        return;

        void Destroy()
        {
            if (PlaceHolderText) PlaceHolderText.enabled = false;
            if (CommandInfoText) CommandInfoText.enabled = false;

            if (AdditionalInfoText)
            {
                bool showLobbyCode = HudManager.Instance?.Chat?.IsOpenOrOpening == true && GameStates.IsLobby && Options.GetSuffixMode() == SuffixModes.Streaming && !Options.HideGameSettings.GetBool() && !DataManager.Settings.Gameplay.StreamerMode;
                AdditionalInfoText.enabled = showLobbyCode;
                if (showLobbyCode) AdditionalInfoText.text = $"\n\n{Translator.GetString("LobbyCode")}:\n<size=250%><b>{GameCode.IntToGameName(AmongUsClient.Instance.GameId)}</b></size>";
            }
        }
    }

    public static void OnTabPress(ChatController __instance)
    {
        if (!PlaceHolderText || PlaceHolderText.text == "") return;

        __instance.freeChatField.textArea.SetText(PlaceHolderText.text);
        __instance.freeChatField.textArea.compoText = "";

        if (AdditionalInfoText && AdditionalInfoText.text != "")
            OptionShower.CurrentPage = 0;
    }

    public static void CheckChatOpen()
    {
        try
        {
            bool open = HudManager.InstanceExists && (HudManager.Instance?.Chat?.IsOpenOrOpening ?? false);
            PlaceHolderText?.gameObject.SetActive(open);
            CommandInfoText?.gameObject.SetActive(open);
            AdditionalInfoText?.gameObject.SetActive(open);
        }
        catch { }
    }

    public static void OnMeetingStart()
    {
        PlaceHolderText?.transform.SetAsLastSibling();
        CommandInfoText?.transform.SetAsLastSibling();
        AdditionalInfoText?.transform.SetAsLastSibling();
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Update))]
    [HarmonyPrefix]
    public static bool UpdatePatch(TextBoxTMP __instance)
    {
        if (!__instance.gameObject.HasParentInHierarchy("ChatScreenRoot/ChatScreenContainer")) return true;

        if (!__instance.enabled || !__instance.hasFocus) return false;
        __instance.MoveCaret();
        string inputString = Input.inputString;

        if (inputString.Length > 0 || __instance.compoText != Input.compositionString)
        {
            if (__instance.text is null or "Enter Name") __instance.text = "";
            int startIndex = Math.Clamp(__instance.caretPos, 0, __instance.text.Length);
            __instance.SetText(__instance.text.Insert(startIndex, inputString), Input.compositionString);
        }

        if (!__instance.Pipe || !__instance.hasFocus) return false;
        __instance.pipeBlinkTimer += Time.deltaTime * 2f;
        __instance.Pipe.enabled = (int)__instance.pipeBlinkTimer % 2 == 0;

        return false;
    }

    // Originally by KARPED1EM. Reference: https://github.com/KARPED1EM/TownOfNext/blob/TONX/TONX/Patches/TextBoxPatch.cs
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    [HarmonyPrefix]
    public static void ModifyCharacterLimit(TextBoxTMP __instance)
    {
        if (!__instance.gameObject.HasParentInHierarchy("ChatScreenRoot/ChatScreenContainer")) return;
        __instance.characterLimit = 1200;
    }
}