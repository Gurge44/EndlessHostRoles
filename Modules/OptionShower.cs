using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public static class OptionShower
{
    private const int MaxLinesPerPage = 50;
    public static int CurrentPage;
    public static List<string> Pages = [];

    private static long LastUpdate;

    static OptionShower() { }

    public static string GetTextNoFresh()
    {
        if (Pages.Count < 3 || (CurrentPage == 0 && LastUpdate != Utils.TimeStamp))
        {
            GetText();
            LastUpdate = Utils.TimeStamp;
        }

        return $"{Pages[CurrentPage]}{GetString("PressTabToNextPage")}({CurrentPage + 1}/{Pages.Count})";
    }

    public static string GetText()
    {
        StringBuilder sb = new();

        Pages =
        [
            GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split('\n').SkipLast(8).Join(delimiter: "\n") + "\n\n"
        ];

        sb.Append($"{Options.GameMode.GetName()}: {Options.GameMode.GetString()}\n\n");

        if (Options.HideGameSettings.GetBool() && !AmongUsClient.Instance.AmHost)
            sb.Append($"<color=#ff0000>{GetString("Message.HideGameSettings")}</color>");
        else
        {
            if (CustomGameMode.Standard.IsActiveOrIntegrated())
            {
                sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.GM)}>{Utils.GetRoleName(CustomRoles.GM)}:</color> {(Main.GM.Value ? GetString("RoleRate") : GetString("RoleOff"))}\n\n");
                sb.Append(GetString("ActiveRolesList")).Append('\n');
                var count = 4;

                foreach (KeyValuePair<CustomRoles, StringOptionItem> kvp in Options.CustomRoleSpawnChances)
                {
                    if (kvp.Value.GameMode is CustomGameMode.Standard or CustomGameMode.All && kvp.Value.GetBool())
                    {
                        sb.Append($"{Utils.ColorString(Utils.GetRoleColor(kvp.Key), Utils.GetRoleName(kvp.Key))}: {kvp.Value.GetString()}  ×{kvp.Key.GetCount()}\n");
                        count++;

                        if (count > MaxLinesPerPage)
                        {
                            count = 0;
                            Pages.Add(sb + "\n\n");
                            sb.Clear().Append(GetString("ActiveRolesList")).Append('\n');
                        }
                    }
                }

                Pages.Add(sb + "\n\n");
                sb.Clear();
            }

            Pages.Add("");
            sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.GM)}>{Utils.GetRoleName(CustomRoles.GM)}:</color> {(Main.GM.Value ? GetString("RoleRate") : GetString("RoleOff"))}\n\n");

            if (!CustomGameMode.HideAndSeek.IsActiveOrIntegrated())
            {
                foreach (KeyValuePair<CustomRoles, StringOptionItem> kvp in Options.CustomRoleSpawnChances)
                {
                    if (!kvp.Key.IsEnable() || kvp.Value.IsCurrentlyHidden()) continue;

                    sb.Append('\n');
                    sb.Append($"{Utils.ColorString(Utils.GetRoleColor(kvp.Key), Utils.GetRoleName(kvp.Key))}: {kvp.Value.GetString()}  ×{kvp.Key.GetCount()}\n");
                    ShowChildren(kvp.Value, ref sb, Utils.GetRoleColor(kvp.Key).ShadeColor(-0.5f), 1);
                }
            }

            foreach (OptionItem opt in OptionItem.AllOptions)
            {
                if (opt.Id is >= 90000 and (< 600000 or > 700000) && !opt.IsCurrentlyHidden() && opt.Parent == null && !opt.IsText)
                {
                    if (opt.IsHeader) sb.Append('\n');

                    sb.Append($"{opt.GetName()}: {opt.GetString()}\n");
                    if (opt.GetBool()) ShowChildren(opt, ref sb, Color.white, 1);
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

        return $"{Pages[CurrentPage]}{GetString("PressTabToNextPage")}({CurrentPage + 1}/{Pages.Count})";
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