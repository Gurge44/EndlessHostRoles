using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AmongUs.Data;
using AmongUs.Data.Player;
using Assets.InnerNet;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.Networking;

namespace EHR;

#if !ANDROID
// ReSharper disable once ClassNeverInstantiated.Global
public class ModNews
{
    // ReSharper disable UnassignedField.Global
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public int Number { get; set; }
    public string Date { get; set; }
    public string Title { get; set; }
    public string SubTitle { get; set; }
    public string ShortTitle { get; set; }

    public string Text { get; set; }
    // ReSharper restore UnassignedField.Global
    // ReSharper restore UnusedAutoPropertyAccessor.Global

    public Announcement ToAnnouncement()
    {
        return new()
        {
            Number = Number,
            Title = Title,
            SubTitle = SubTitle,
            ShortTitle = ShortTitle,
            Text = Text,
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Date = Date,
            Id = "ModNews"
        };
    }

    public static List<ModNews> FromJson(string json)
    {
        return JsonSerializer.Deserialize<List<ModNews>>(json);
    }
}

public static class ModNewsFetcher
{
    private const string NewsUrl = "https://gurge44.pythonanywhere.com/modnews";

    public static IEnumerator FetchNews()
    {
        UnityWebRequest request = UnityWebRequest.Get(NewsUrl);
        request.SetRequestHeader("User-Agent", $"{Main.ModName} v{Main.PluginVersion}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Logger.Error("Failed to fetch mod news: " + request.error, "ModNewsFetcher");
            yield break;
        }

        try
        {
            List<ModNews> newsList = ModNews.FromJson(request.downloadHandler.text);
            ModNewsHistory.AllModNews = newsList.OrderByDescending(n => DateTime.Parse(n.Date)).ToList();
            Logger.Info($"Successfully fetched {ModNewsHistory.AllModNews.Count} mod news items.", "ModNewsFetcher");
        }
        catch (Exception ex) { Utils.ThrowException(ex); }
    }
}

[HarmonyPatch]
public static class ModNewsHistory
{
    public static List<ModNews> AllModNews = [];

    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements))]
    [HarmonyPrefix]
    public static void SetModAnnouncements(ref Il2CppReferenceArray<Announcement> aRange)
    {
        if (AllModNews.Count == 0)
        {
            Logger.Warn("No mod news loaded.", "ModNewsHistory");
            return;
        }

        List<Announcement> finalAllNews = AllModNews.ConvertAll(n => n.ToAnnouncement());
        finalAllNews.AddRange(aRange.Where(news => AllModNews.All(x => x.Number != news.Number)));
        finalAllNews.Sort((a1, a2) => DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)));

        aRange = new Il2CppReferenceArray<Announcement>(finalAllNews.Count);

        for (var i = 0; i < finalAllNews.Count; i++)
            aRange[i] = finalAllNews[i];
    }
}
#endif