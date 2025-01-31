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
        Tags[friendcode] = tag;
        SaveTagsToFile();
    }
    
    public static void DeleteTag(string friendcode)
    {
        Tags.Remove(friendcode);
        SaveTagsToFile();
    }

    private static void SaveTagsToFile()
    {
        try
        {
            File.WriteAllText(TagFile, string.Join("\n", Tags.Select(x => $"{x.Key}={x.Value}")));
        }
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
                .ToDictionary(x => x[0], x => x[1]);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}