using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR.Patches;

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
class TextBoxTMPSetTextPatch
{
    private static TextMeshPro PlaceHolderText;
    private static TextMeshPro CommandInfoText;

    public static bool Prefix(TextBoxTMP __instance, [HarmonyArgument(0)] string input, [HarmonyArgument(1)] string inputCompo = "")
    {
        bool flag = false;
        char ch = ' ';
        __instance.tempTxt.Clear();

        foreach (var str in input)
        {
            char upperInvariant = str;
            if (ch != ' ' || upperInvariant != ' ')
            {
                switch (upperInvariant)
                {
                    case '\r' or '\n':
                        flag = true;
                        break;
                    case '\b':
                        __instance.tempTxt.Length = Math.Max(__instance.tempTxt.Length - 1, 0);
                        break;
                }

                if (__instance.ForceUppercase) upperInvariant = char.ToUpperInvariant(upperInvariant);
                if (upperInvariant is not '\b' and not '\n' and not '\r')
                {
                    __instance.tempTxt.Append(upperInvariant);
                    ch = upperInvariant;
                }
            }
        }

        if (!__instance.tempTxt.ToString().Equals(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.EnterName), StringComparison.OrdinalIgnoreCase) && __instance.characterLimit > 0)
            __instance.tempTxt.Length = Math.Min(__instance.tempTxt.Length, __instance.characterLimit);
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
                for (int index = 0; index < __instance.text.Length; ++index)
                    str += "*";
            }

            __instance.outputText.text = str + compoText;
            __instance.outputText.ForceMeshUpdate(true, true);
            if (__instance.keyboard != null) __instance.keyboard.text = __instance.text;
            __instance.OnChange.Invoke();
        }

        if (flag) __instance.OnEnter.Invoke();
        __instance.Pipe.transform.localPosition = __instance.outputText.CursorPos();

        return false;
    }

    public static void Postfix(TextBoxTMP __instance, [HarmonyArgument(0)] string input)
    {
        input = input.Trim();
        if (!input.StartsWith('/') || input.Length < 2)
        {
            PlaceHolderText?.gameObject.SetActive(false);
            CommandInfoText?.gameObject.SetActive(false);
            return;
        }

        Command command = null;
        double highestMatchRate = 0;
        string inputCheck = input.Split(' ')[0];
        bool exactMatch = false;
        foreach (var cmd in ChatCommands.AllCommands)
        {
            foreach (var form in cmd.CommandForms)
            {
                var check = "/" + form;
                if (check.Length < inputCheck.Length) continue;

                if (check == inputCheck)
                {
                    command = cmd;
                    exactMatch = true;
                    break;
                }

                int matchNum = 0;
                for (int i = 0; i < inputCheck.Length; i++)
                {
                    if (i >= check.Length) break;
                    if (inputCheck[i] == check[i]) matchNum++;
                    else break;
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

        if (command == null) return;

        if (PlaceHolderText == null)
        {
            PlaceHolderText = Object.Instantiate(__instance.outputText, __instance.outputText.transform.parent);
            PlaceHolderText.name = "PlaceHolderText";
            PlaceHolderText.color = new(0.7f, 0.7f, 0.7f, 0.7f);
            PlaceHolderText.transform.localPosition = __instance.outputText.transform.localPosition;
        }

        if (CommandInfoText == null)
        {
            var hud = DestroyableSingleton<HudManager>.Instance;
            CommandInfoText = Object.Instantiate(hud.KillButton.cooldownTimerText, hud.transform, true);
            CommandInfoText.name = "CommandInfoText";
            CommandInfoText.alignment = TextAlignmentOptions.Left;
            CommandInfoText.verticalAlignment = VerticalAlignmentOptions.Top;
            CommandInfoText.transform.localPosition = new(-3.2f, -2.35f, 0f);
            CommandInfoText.overflowMode = TextOverflowModes.Overflow;
            CommandInfoText.enableWordWrapping = false;
            CommandInfoText.color = Color.white;
            CommandInfoText.fontSize = CommandInfoText.fontSizeMax = CommandInfoText.fontSizeMin = 1.8f;
        }

        var text = "/" + (exactMatch ? input.TrimStart('/') : command.CommandForms.MaxBy(x => x.Length));
        var info = $"<b>{command.Description}</b>";

        if (exactMatch && command.Arguments.Length > 0)
        {
            int spaces = input.Count(x => x == ' ');
            var preText = $"{text} {command.Arguments}";
            text += " " + command.Arguments.Split(' ').Skip(spaces).Join(delimiter: " ");

            var args = preText.Split(' ')[1..];
            for (int i = 0; i < args.Length; i++)
            {
                if (command.ArgsDescriptions.Length <= i) break;
                bool current = spaces - 1 == i;
                if (current) info += "<#ffff44>";
                info += $"\n       - <b>{args[i]}</b>: {command.ArgsDescriptions[spaces + i]}";
                if (current) info += "</color>";
            }
        }

        PlaceHolderText.text = text;
        CommandInfoText.text = info;
    }

    public static void OnTabPress(ChatController __instance)
    {
        if (PlaceHolderText == null || PlaceHolderText.text == "") return;
        __instance.freeChatField.textArea.SetText(PlaceHolderText.text);
        __instance.freeChatField.textArea.compoText = "";
    }

    public static void Update()
    {
        PlaceHolderText?.gameObject.SetActive(HudManager.Instance.Chat.IsOpenOrOpening);
        CommandInfoText?.gameObject.SetActive(HudManager.Instance.Chat.IsOpenOrOpening);
    }
}

/* Originally by KARPED1EM. Reference: https://github.com/KARPED1EM/TownOfNext/blob/TONX/TONX/Patches/TextBoxPatch.cs */
[HarmonyPatch(typeof(TextBoxTMP))]
public class TextBoxPatch
{
    [HarmonyPatch(nameof(TextBoxTMP.SetText)), HarmonyPrefix]
    public static void ModifyCharacterLimit(TextBoxTMP __instance)
    {
        __instance.characterLimit = 1200;
    }
}