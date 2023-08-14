using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using AmongUs.Data.Player;
using Assets.InnerNet;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TOHE;

// ##https://github.com/Yumenopai/TownOfHost_Y
public class ModNews
{
    public int Number;
    public int BeforeNumber;
    public string Title;
    public string SubTitle;
    public string ShortTitle;
    public string Text;
    public string Date;

    public Announcement ToAnnouncement()
    {
        var result = new Announcement
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

        return result;
    }
}
[HarmonyPatch]
public class ModNewsHistory
{
    public static List<ModNews> AllModNews = new();

    // 
    public static void Init()
    {
        // When creating new news, you can not delete old news
        {
            // TOHE v2.4.3
            var news = new ModNews
            {
                Number = 100001,
                Title = "TownOfHostEdited v2.5.0",
                SubTitle = "★★★★Another big update, maybe bigger?★★★★",
                ShortTitle = "★TOHE v2.5.0",
                Text = "<size=150%>Welcome to TOHE v2.5.0.</size>\n\n<size=125%>Support for Among Us v2023.7.11 and v2023.7.12</size>\n"

                    + "\n【Base】\n - Base on TOH v4.1.2\r\n"
                    + "\n【Fixes】\n - Various bug fixes\n\r"
                    + "\n【Changes】\n - Hex Master hex icon changed to separate it from Spellcaster\n - Fortune Teller moved to Experimentals due to a planned and unfinished rework\n\r"

                    + "\n【New Features】\n - New role: Twister (role by papercut on Discord)\n\r - New role: Chameleon (from Project: Lotus)\n\r - New role: Morphling\n\r - New role: Inspector (role by ryuk on Discord)\n\r - New role: Medusa\n\r - New add-on: Lazy\n\r - New add-on: Gravestone\n\r - New add-on: Autopsy (from TOHY)\n\r - New add-on: Loyal\n\r - New add-on: Visionary\n\r- New experimental role: Spiritcaller (role by papercut on Discord)\n\r"

                    + "\n【Role Changes】\n - Various changes were made, such as an update to Opportunist\n\r",

                Date = "2023-7-14T00:00:00Z"

            };
            AllModNews.Add(news);
        }   

        {
            // TOHE v2.4.2
            var news = new ModNews
            {
                Number = 100000,
                Title = "TownOfHostEdited v2.4.2",
                SubTitle = "★★★★Ooooh bigger update★★★★",
                ShortTitle = "★TOHE v2.4.2",
                Text = "Added in some new stuff, along with some bug fixes.\r\nAmong Us v2023.3.28 is recommended so the roles work correctly.\n"

                    + "\n【Base】\n - Base on TOH v4.1.2\r\n"
                    + "\n【Fixes】\n - Fixed various black screen bugs (some still exist but should be less common)\r\n - Other various bug fixes (they're hard to keep track of)\r\n"
                    + "\n【Changes】\n - Judge now supports Guesser Mode\r\n - Background image reverted to use the AU v2023.3.28 size due to the recommended Among Us version being v2023.3.28\r\n - Many other unlisted changes\r\n - Mario renamed to Vector due to copyright concerns\r\n"

                    + "\n【New Features】\n - ###Impostors\n - Councillor\r\n - Deathpact (role by papercut on Discord)\r\n - Saboteur (25% chance to replace Inhibitor)\r\n - Consigliere (by Yumeno from TOHY)\r\n - Dazzler (role by papercut on Discord)\r\n - Devourer (role by papercut on Discord)\r\n"
                    + "\n ### Crewmates\n - Addict (role by papercut on Discord)\r\n - Tracefinder\r\n - Deputy\r\n - Merchant (role by papercut on Discord)\r\n - Oracle\r\n - Spiritualist (role by papercut on Discord)\r\n - Retributionist\r\n- Guardian\r\n - Monarch\r\n"
                    + "\n ### Neutrals\n - Maverick\r\n - Cursed Soul\r\n - Vulture (role by ryuk on Discord)\r\n - Jinx\r\n - Pickpocket\r\n - Ritualist\r\n - Traitor\r\n"
                    + "\n ### Add-ons\n - Double Shot (add-on by TommyXL)\r\n - Rascal\r\n"

                    + "\n【Role Changes】\n - Mimic now has a setting to see the roles of dead players, due to how useless this add-on was\r\n - A revealed Workaholic can no longer be guessed\r\n - Doctor has a new setting like Workaholic to be revealed to all (currently exposes evil Doctors, use at your own risk)\r\n - Mayor has a setting for a TOS mechanic to reveal themselves\r\n - Warlock balancing\r\n - Cleaner balancing (resets kill cooldown to value set in Cleaner settings)\r\n - Updated Monarch\r\n- Removed speed boost from Mare\r\n"
                    + "\n【Removals】\n - Removed Flash\r\n - Removed Speed Booster\r\n - Temporarily removed Oblivious",

                Date = "2023-7-5T00:00:00Z"

            };
            AllModNews.Add(news);
        }
    }

    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)), HarmonyPrefix]
    public static bool SetModAnnouncements(PlayerAnnouncementData __instance, [HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> aRange)
    {
        if (AllModNews.Count < 1)
        {
            Init();
            AllModNews.Sort((a1, a2) => { return DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)); });
        }

        List<Announcement> FinalAllNews = new();
        AllModNews.Do(n => FinalAllNews.Add(n.ToAnnouncement()));
        foreach (var news in aRange)
        {
            if (!AllModNews.Any(x => x.Number == news.Number))
                FinalAllNews.Add(news);
        }
        FinalAllNews.Sort((a1, a2) => { return DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)); });

        aRange = new(FinalAllNews.Count);
        for (int i = 0; i < FinalAllNews.Count; i++)
            aRange[i] = FinalAllNews[i];

        return true;
    }
}