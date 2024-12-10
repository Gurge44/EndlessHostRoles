using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR.Patches;

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.IsCharAllowed))]
static class TextBoxTMPIsCharAllowed
{
    public static bool Prefix(TextBoxTMP __instance, [HarmonyArgument(0)] char i, ref bool __result)
    {
        if (!__instance.gameObject.HasParentInHierarchy("ChatScreenRoot/ChatScreenContainer")) return true;
        __result = i != '\b';
        return false;
    }
}

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
internal static class TextBoxTMPSetTextPatch
{
    private static TextMeshPro PlaceHolderText;
    private static TextMeshPro CommandInfoText;
    private static TextMeshPro AdditionalInfoText;

    public static bool IsInvalidCommand;

    public static void Postfix(TextBoxTMP __instance)
    {
        try
        {
            if (!__instance.gameObject.HasParentInHierarchy("ChatScreenRoot/ChatScreenContainer")) return;

            if (!Main.EnableCommandHelper.Value)
            {
                Destroy();
                return;
            }

            string input = __instance.outputText.text.Trim().Replace("\b", "");

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

            foreach (Command cmd in ChatCommands.AllCommands)
            {
                string[] commandForms = english ? cmd.CommandForms.TakeWhile(x => x.All(char.IsAscii)).ToArray() : cmd.CommandForms;

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
                __instance.compoText.Color(Color.red);
                __instance.outputText.color = Color.red;
                return;
            }

            IsInvalidCommand = false;

            if (PlaceHolderText == null)
            {
                PlaceHolderText = Object.Instantiate(__instance.outputText, __instance.outputText.transform.parent);
                PlaceHolderText.name = "PlaceHolderText";
                PlaceHolderText.color = new(0.7f, 0.7f, 0.7f, 0.7f);
                PlaceHolderText.transform.localPosition = __instance.outputText.transform.localPosition;
            }

            if (CommandInfoText == null)
            {
                HudManager hud = DestroyableSingleton<HudManager>.Instance;
                CommandInfoText = Object.Instantiate(hud.KillButton.cooldownTimerText, hud.transform, true);
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

            if (AdditionalInfoText == null)
            {
                HudManager hud = DestroyableSingleton<HudManager>.Instance;
                AdditionalInfoText = Object.Instantiate(hud.KillButton.cooldownTimerText, hud.transform, true);
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
            string text = "/" + (exactMatch ? inputForm : command.CommandForms.Where(x => x.All(char.IsAscii) && x.StartsWith(inputForm)).MaxBy(x => x.Length));
            var info = $"<b>{command.Description}</b>";

            string additionalInfo = string.Empty;

            if (exactMatch && command.Arguments.Length > 0)
            {
                bool poll = command.CommandForms.Contains("poll");
                bool say = command.CommandForms.Contains("say");
                int spaces = poll ? input.SkipWhile(x => x != '?').Count(x => x == ' ') + 1 : input.Count(x => x == ' ');
                if (say) spaces = Math.Min(spaces, 1);

                var preText = $"{text} {command.Arguments}";
                if (!poll) text += " " + command.Arguments.Split(' ').Skip(spaces).Join(delimiter: " ");

                string[] args = preText.Split(' ')[1..];

                for (var i = 0; i < args.Length; i++)
                {
                    if (command.ArgsDescriptions.Length <= i) break;

                    int skip = poll ? input.TakeWhile(x => x != '?').Count(x => x == ' ') - 1 : 0;
                    string arg = say ? args[0] : poll ? i == 0 ? args[..++skip].Join(delimiter: " ") : args[spaces - 1 < i ? skip + i + spaces : skip + i] : args[spaces > i ? i : i + spaces];

                    string argName = command.Arguments.Split(' ')[i];
                    bool current = spaces - 1 == i, invalid = IsInvalidArg(), valid = IsValidArg();

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
                        var allIds = Main.AllPlayerControls.ToDictionary(x => x.PlayerId.ColoredPlayerName(), x => x.PlayerId);
                        additionalInfo = $"<b><u>{Translator.GetString("PlayerIdList").TrimEnd(' ')}</u></b>\n{string.Join('\n', allIds.Select(x => $"<b>{x.Key}</b> \uffeb <b>{x.Value}</b>"))}";
                        OptionShower.CurrentPage = 0;
                    }

                    continue;

                    bool IsInvalidArg()
                    {
                        return arg != argName && argName switch
                        {
                            "{id}" or "{id1}" or "{id2}" => !byte.TryParse(arg, out byte id) || Main.AllPlayerControls.All(x => x.PlayerId != id),
                            "{number}" or "{level}" or "{duration}" or "{number1}" or "{number2}" => !int.TryParse(arg, out int num) || num < 0,
                            "{team}" => arg is not "crew" and not "imp",
                            "{role}" => !ChatCommands.GetRoleByName(arg, out _),
                            "{addon}" => !ChatCommands.GetRoleByName(arg, out CustomRoles role) || !role.IsAdditionRole(),
                            "{letter}" => arg.Length != 1 || !char.IsLetter(arg[0]),
                            "{chance}" => !int.TryParse(arg, out int chance) || chance < 0 || chance > 100 || chance % 5 != 0,
                            _ => false
                        };
                    }

                    bool IsValidArg()
                    {
                        return argName switch
                        {
                            "{id}" or "{id1}" or "{id2}" => byte.TryParse(arg, out byte id) && Main.AllPlayerControls.Any(x => x.PlayerId == id),
                            "{team}" => arg is "crew" or "imp",
                            "{role}" => ChatCommands.GetRoleByName(arg, out _),
                            "{addon}" => ChatCommands.GetRoleByName(arg, out CustomRoles role) && role.IsAdditionRole(),
                            "{chance}" => int.TryParse(arg, out int chance) && chance is >= 0 and <= 100 && chance % 5 == 0,
                            _ => false
                        };
                    }

                    string GetExtraArgInfo()
                    {
                        return !IsValidArg()
                            ? string.Empty
                            : argName switch
                            {
                                "{id}" or "{id1}" or "{id2}" => $" ({byte.Parse(arg).ColoredPlayerName()})",
                                "{role}" or "{addon}" when ChatCommands.GetRoleByName(arg, out CustomRoles role) => $" ({role.ToColoredString()})",
                                _ => string.Empty
                            };
                    }
                }
            }

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
            if (PlaceHolderText != null) PlaceHolderText.enabled = false;
            if (CommandInfoText != null) CommandInfoText.enabled = false;
            if (AdditionalInfoText != null) AdditionalInfoText.enabled = false;
        }
    }

    public static void OnTabPress(ChatController __instance)
    {
        if (PlaceHolderText == null || PlaceHolderText.text == "") return;

        __instance.freeChatField.textArea.SetText(PlaceHolderText.text);
        __instance.freeChatField.textArea.compoText = "";

        if (AdditionalInfoText != null && AdditionalInfoText.text != "")
            OptionShower.CurrentPage = 0;
    }

    public static void Update()
    {
        try
        {
            bool open = HudManager.Instance?.Chat?.IsOpenOrOpening ?? false;
            PlaceHolderText?.gameObject.SetActive(open);
            CommandInfoText?.gameObject.SetActive(open);
            AdditionalInfoText?.gameObject.SetActive(open);
        }
        catch { }
    }
}

// Originally by KARPED1EM. Reference: https://github.com/KARPED1EM/TownOfNext/blob/TONX/TONX/Patches/TextBoxPatch.cs
[HarmonyPatch(typeof(TextBoxTMP))]
public static class TextBoxPatch
{
    [HarmonyPatch(nameof(TextBoxTMP.SetText))]
    [HarmonyPrefix]
    public static void ModifyCharacterLimit(TextBoxTMP __instance)
    {
        if (!__instance.gameObject.HasParentInHierarchy("ChatScreenRoot/ChatScreenContainer")) return;
        __instance.characterLimit = 1200;
    }
}