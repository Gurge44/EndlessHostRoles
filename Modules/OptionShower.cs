using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public static class OptionShower
{
    private const int MaxLinesPerPage = 50;
    public static int CurrentPage;
    public static List<string> Pages = [];
    public static string LastText = string.Empty;
    private static bool Running;
    private static bool InQueue;

    static OptionShower() { }

    public static string GetTextNoFresh()
    {
        try
        {
            var text = $"{Pages[CurrentPage]}{GetString("PressTabToNextPage")}({CurrentPage + 1}/{Pages.Count})";
            LastText = text;
            return text;
        }
        catch (Exception e)
        {
            if (!OnGameJoinedPatch.JoiningGame) Utils.ThrowException(e);
            return LastText;
        }
    }

    public static IEnumerator GetText()
    {
        if (Running)
        {
            if (InQueue) yield break;
            InQueue = true;
            while (Running) yield return null;
            InQueue = false;
        }
        
        Running = true;
        
        StringBuilder sb = new();

        Pages =
        [
            string.Join('\n', GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split('\n')[..^8]) + "\n\n"
        ];

        sb.Append($"{Options.GameMode.GetName()}: {Options.GameMode.GetString()}\n\n");

        if (Options.HideGameSettings.GetBool() && !AmongUsClient.Instance.AmHost)
            sb.Append($"<color=#ff0000>{GetString("Message.HideGameSettings")}</color>");
        else
        {
            if (Options.CurrentGameMode == CustomGameMode.Standard)
            {
                sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.GM)}>{Utils.GetRoleName(CustomRoles.GM)}</color>: {(Main.GM.Value ? GetString("RoleRate") : GetString("RoleOff"))}\n\n");
                sb.Append(GetString("ActiveRolesList")).Append('\n');
                var count = 4;

                foreach (KeyValuePair<CustomRoles, StringOptionItem> kvp in Options.CustomRoleSpawnChances)
                {
                    if (kvp.Value.GameMode is CustomGameMode.Standard or CustomGameMode.All && kvp.Value.GetBool())
                    {
                        sb.Append($"{Utils.ColorString(Utils.GetRoleColor(kvp.Key), Utils.GetRoleName(kvp.Key))}: {kvp.Value.GetString()}  x{kvp.Key.GetCount()}\n");
                        count++;

                        if (count > MaxLinesPerPage)
                        {
                            count = 0;
                            Pages.Add(sb + "\n\n");
                            sb.Clear().Append(GetString("ActiveRolesList")).Append('\n');

                            yield return null;
                        }
                    }
                }

                Pages.Add(sb + "\n\n");
                sb.Clear();
            }

            Pages.Add("");
            sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.GM)}>{Utils.GetRoleName(CustomRoles.GM)}</color>: {(Main.GM.Value ? GetString("RoleRate") : GetString("RoleOff"))}\n\n");

            var index = 0;

            if (Options.CurrentGameMode != CustomGameMode.HideAndSeek)
            {
                foreach (KeyValuePair<CustomRoles, StringOptionItem> kvp in Options.CustomRoleSpawnChances)
                {
                    if (!kvp.Key.IsEnable() || kvp.Value.IsCurrentlyHidden()) continue;

                    sb.Append('\n');
                    sb.Append($"{Utils.ColorString(Utils.GetRoleColor(kvp.Key), Utils.GetRoleName(kvp.Key))}: {kvp.Value.GetString()}  ×{kvp.Key.GetCount()}\n");
                    ShowChildren(kvp.Value, ref sb, Utils.GetRoleColor(kvp.Key).ShadeColor(-0.5f), 1);

                    if (index++ >= 2)
                    {
                        yield return null;
                        index = 0;
                    }
                }
            }

            foreach (OptionItem opt in OptionItem.AllOptions)
            {
                if (opt.Id is >= 90000 and (< 600000 or > 700000) && !opt.IsCurrentlyHidden() && opt.Parent == null && !opt.IsText)
                {
                    if (opt.IsHeader) sb.Append('\n');

                    sb.Append($"{opt.GetName()}: {opt.GetString()}\n");
                    if (opt.GetBool()) ShowChildren(opt, ref sb, Color.white, 1);

                    if (opt.IsHeader) yield return null;
                }
            }
        }

        string[] tmp = sb.ToString().Split("\n\n");

        foreach (string str in tmp)
        {
            if (Pages[^1].Count(c => c == '\n') + 1 + str.Count(c => c == '\n') + 1 > MaxLinesPerPage - 3)
                Pages.Add(str + "\n\n");
            else
                Pages[^1] += str + "\n\n";
        }

        if (CurrentPage >= Pages.Count) CurrentPage = Pages.Count - 1;

        Running = false;
    }

    public static void Next()
    {
        CurrentPage++;
        if (CurrentPage >= Pages.Count) CurrentPage = 0;
    }

    private static void ShowChildren(OptionItem option, ref StringBuilder sb, Color color, int deep = 0)
    {
        foreach (var opt in option.Children.Select((v, i) => new { Value = v, Index = i + 1 }))
        {
            if (opt.Value.Name == "Maximum") continue;

            sb.Append(string.Concat(Enumerable.Repeat(Utils.ColorString(color, "┃"), deep - 1)));
            sb.Append(Utils.ColorString(color, opt.Index == option.Children.Count ? "┗ " : "┣ "));
            sb.Append($"{opt.Value.GetName()}: {opt.Value.GetString()}\n");
            if (opt.Value.GetBool()) ShowChildren(opt.Value, ref sb, color, deep + 1);
        }
    }

}
