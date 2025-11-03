using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace EHR.Impostor;

public class Bard : RoleBase
{
    public static int BardCreations;
    public static bool On;
    public override bool IsEnable => On;

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
        BardCreations = 0;
    }

    public override void SetupCustomOption() { }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Options.AdjustedDefaultKillCooldown / Math.Max(1, 2 * BardCreations);
    }

    public static void OnMeetingHudDestroy(ref string name)
    {
        try
        {
            BardCreations++;

            string json = ModUpdater.Get("https://official-joke-api.appspot.com/random_joke");
            var joke = JsonUtility.FromJson<Joke>(json);
            name = $"{joke.setup}\n{joke.punchline}";

            name += "\n\t\t——" + Translator.GetString("ByBard");
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
            name = Translator.GetString("ByBardGetFailed");
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class Joke
    {
        public string type;
        public string setup;
        public string punchline;
        public int id;
    }
}