using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace EHR;

public static class DevManager
{
    private static readonly TagInfo DefaultDevUser = new();

    private static int Attempts;

    private static Dictionary<string, TagInfo> Tags { get; set; } = [];

    public static TagInfo GetDevUser(this string code)
    {
        code = code.Replace(':', '#');
        return Tags.GetValueOrDefault(code, DefaultDevUser);
    }

    private static IEnumerator FetchTags()
    {
        Attempts++;

        if (Attempts > 10)
        {
            Logger.Error($"Failed to fetch tags after {Attempts - 1} attempts.", "DevManager.FetchTags");
            yield break;
        }

        UnityWebRequest request = UnityWebRequest.Get("https://gurge44.pythonanywhere.com/get_all_tags");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Logger.Error($"Error fetching tags: {request.error}", "DevManager.FetchTags");
            yield return new WaitForSeconds(300f);
            yield return FetchTags();
        }
        else
        {
            try
            {
                string json = request.downloadHandler.text.Trim();
                Tags = JsonSerializer.Deserialize<Dictionary<string, TagInfo>>(json);
                Logger.Info($"Tags successfully fetched: {Tags.Count} tags loaded.", "DevManager.FetchTags");
            }
            catch (Exception ex) { Logger.Error($"Error parsing tags JSON: {ex.Message}", "DevManager.FetchTags"); }
        }
    }

    public static void StartFetchingTags()
    {
        Main.Instance.StartCoroutine(FetchTags());
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class TagInfo
    {
        public string tag_name { get; set; }
        public string tag_color { get; set; }
        public bool up { get; set; }
        public string ip { get; set; }

        public bool HasTag()
        {
            return !string.IsNullOrWhiteSpace(tag_name);
        }

        public string GetTag()
        {
            return !HasTag() ? "" : $"<size=1.5><color={tag_color}>{tag_name}</color></size>\r\n";
        }
    }
}