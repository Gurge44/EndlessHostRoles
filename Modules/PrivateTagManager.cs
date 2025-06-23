using System;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Modules;

public static class PrivateTagManager
{
    private const string TagFile = "./EHR_DATA/tags.txt";
    public static Dictionary<string, string> Tags = [];

    public static void AddTag(string friendcode, string tag)
    {
        LoadTagsFromFile();
        Tags[friendcode] = tag;
        SaveTagsToFile();
    }

    public static void DeleteTag(string friendcode)
    {
        LoadTagsFromFile();
        if (!Tags.Remove(friendcode)) return;
        SaveTagsToFile();
    }

    private static void SaveTagsToFile()
    {
        try { File.WriteAllText(TagFile, string.Join("\n", Tags.Select(x => $"{x.Key}={x.Value}"))); }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void LoadTagsFromFile()
    {
        try
        {
            if (!File.Exists(TagFile))
                return;

            Tags = File.ReadAllLines(TagFile)
                .Select(x => x.Split('='))
                .Where(x => x.Length == 2)
                .ToDictionary(x => x[0], x => x[1]);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}