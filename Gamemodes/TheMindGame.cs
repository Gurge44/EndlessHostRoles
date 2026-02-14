using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Roles;
using Hazel;
using InnerNet;
using UnityEngine;

namespace EHR.Gamemodes;

public static class TheMindGame
{
    private static Dictionary<byte, int> Points = [];
    private static Dictionary<byte, int> SuperPoints = [];
    private static Dictionary<PlayerControl, int> DefaultColorIds = [];
    private static Dictionary<byte, Group> Groups = [];
    private static Dictionary<Group, List<byte>> GroupPlayers = [];
    private static List<SystemTypes> AllRooms = [];
    private static Dictionary<Group, SystemTypes> GroupRooms = [];
    private static Dictionary<byte, int> Pick = [];
    private static HashSet<byte> AmReady = [];
    private static Dictionary<byte, Dictionary<Item, int>> ItemIds = [];
    private static Dictionary<byte, List<Item>> PlayerItems = [];
    private static HashSet<byte> HiddenPoints = [];
    private static HashSet<byte> CannotPurchase = [];
    private static HashSet<byte> DoubleModifierActive = [];
    private static long ProceedingInCountdownEndTS;
    private static long LastTimeWarning;
    private static bool ShowSuffixOtherThanPoints;
    private static int AuctionValue;
    private static byte WinningBriefcaseHolderId;
    private static byte WinningBriefcaseLastHolderId;
    private static List<byte> Round4PlacesFromFirst;
    private static List<byte> Round4PlacesFromLast;
    private static List<byte> EjectedPlayers;
    private static bool PreventGameEnd;
    private static int Round;

    // Settings
    private static bool PlayersCanSeeOthersPoints = true;
    private static int NumGroupsForRound1 = 5;
    private static int TimeForEachPickInRound1 = 15;
    private static int NumPointsToAdvanceInRound1 = 10;
    private static int MinPlayersInRound2 = 10;
    private static int SuperPointsToNormalPointsMultiplier = 3;
    private static int TimeForEachPickInRound2 = 25;
    private static int TimeForItemPurchasingInRound2 = 40;
    private static int FoolNumPointsLost = 5;
    private static Dictionary<Item, int> ItemCosts = [];
    private static int TimeForEachPickInRound3 = 30;
    private static int MaxPlayersForRound4 = 5;
    private static int MindDetectiveFailChance = 10;

    private static OptionItem PlayersCanSeeOthersPointsOption;
    private static OptionItem NumGroupsForRound1Option;
    private static OptionItem TimeForEachPickInRound1Option;
    private static OptionItem NumPointsToAdvanceInRound1Option;
    private static OptionItem MinPlayersInRound2Option;
    private static OptionItem SuperPointsToNormalPointsMultiplierOption;
    private static OptionItem TimeForEachPickInRound2Option;
    private static OptionItem TimeForItemPurchasingInRound2Option;
    private static OptionItem FoolNumPointsLostOption;
    private static OptionItem TimeForEachPickInRound3Option;
    private static OptionItem MaxPlayersForRound4Option;
    private static OptionItem MindDetectiveFailChanceOption;
    private static readonly Dictionary<Item, OptionItem> ItemCostsOptions = [];

    private static bool Stop => GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded;

    public static void SetupCustomOption()
    {
        var id = 69_221_001;
        var color = Color.yellow;
        const CustomGameMode gameMode = CustomGameMode.TheMindGame;
        const TabGroup tab = TabGroup.GameSettings;

        PlayersCanSeeOthersPointsOption = new BooleanOptionItem(id++, "TMG.Setting.PlayersCanSeeOthersPoints", true, tab)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode);

        NumGroupsForRound1Option = new IntegerOptionItem(id++, "TMG.Setting.NumGroupsForRound1", new IntegerValueRule(1, 7, 1), 5, tab)
            .SetColor(color)
            .SetGameMode(gameMode);

        TimeForEachPickInRound1Option = new IntegerOptionItem(id++, "TMG.Setting.TimeForEachPickInRound1", new IntegerValueRule(1, 60, 1), 8, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        NumPointsToAdvanceInRound1Option = new IntegerOptionItem(id++, "TMG.Setting.NumPointsToAdvanceInRound1", new IntegerValueRule(1, 100, 1), 10, tab)
            .SetColor(color)
            .SetGameMode(gameMode);

        MinPlayersInRound2Option = new IntegerOptionItem(id++, "TMG.Setting.MinPlayersInRound2", new IntegerValueRule(1, 15, 1), 10, tab)
            .SetValueFormat(OptionFormat.Players)
            .SetColor(color)
            .SetGameMode(gameMode);

        SuperPointsToNormalPointsMultiplierOption = new IntegerOptionItem(id++, "TMG.Setting.SuperPointsToNormalPointsMultiplier", new IntegerValueRule(1, 10, 1), 3, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        TimeForEachPickInRound2Option = new IntegerOptionItem(id++, "TMG.Setting.TimeForEachPickInRound2", new IntegerValueRule(1, 60, 1), 25, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        TimeForItemPurchasingInRound2Option = new IntegerOptionItem(id++, "TMG.Setting.TimeForItemPurchasingInRound2", new IntegerValueRule(1, 60, 1), 40, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        FoolNumPointsLostOption = new IntegerOptionItem(id++, "TMG.Setting.FoolNumPointsLost", new IntegerValueRule(1, 100, 1), 5, tab)
            .SetColor(color)
            .SetGameMode(gameMode);

        TimeForEachPickInRound3Option = new IntegerOptionItem(id++, "TMG.Setting.TimeForEachPickInRound3", new IntegerValueRule(1, 60, 1), 20, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        MaxPlayersForRound4Option = new IntegerOptionItem(id++, "TMG.Setting.MaxPlayersForRound4", new IntegerValueRule(1, 15, 1), 5, tab)
            .SetValueFormat(OptionFormat.Players)
            .SetColor(color)
            .SetGameMode(gameMode);

        MindDetectiveFailChanceOption = new IntegerOptionItem(id++, "TMG.Setting.MindDetectiveFailChance", new IntegerValueRule(0, 100, 1), 5, tab)
            .SetValueFormat(OptionFormat.Percent)
            .SetColor(color)
            .SetGameMode(gameMode);

        foreach (Item item in Enum.GetValues<Item>())
        {
            int defaultValue = item switch
            {
                Item.BlindingGas => 5,
                Item.MerchantDise => 2,
                Item.Lasso => 6,
                Item.DoubleModifier => 4,
                Item.Fool => 3,
                Item.MindDetective => 2,
                _ => 5
            };

            var option = new IntegerOptionItem(id++, $"TMG.Setting.ItemCost.{item}", new IntegerValueRule(1, 100, 1), defaultValue, tab)
                .SetHeader((int)item == 0)
                .SetColor(color)
                .SetGameMode(gameMode);

            ItemCostsOptions[item] = option;
        }
    }

    public static string GetStatistics(byte id)
    {
        return string.Format(Translator.GetString("TMG.EndScreen.Points"), GetPoints(id));
    }

    public static int GetPoints(byte id)
    {
        return Points.GetValueOrDefault(id, 0);
    }

    public static string GetTaskBarText()
    {
        long now = Utils.TimeStamp;
        return ProceedingInCountdownEndTS < now ? string.Empty : string.Format(Translator.GetString("TMG.Message.TimeLeft"), ProceedingInCountdownEndTS - now);
    }

    public static string GetSuffix(PlayerControl seer, PlayerControl target)
    {
        if (!target.IsAlive() || !Main.IntroDestroyed || Round is 0 or 5) return string.Empty;

        var sb = new StringBuilder("<#ffffff>");

        bool self = seer.PlayerId == target.PlayerId;
        int points = Points[target.PlayerId];
        int superPoints = SuperPoints[target.PlayerId];

        if (!self)
        {
            sb.Append($"ID {target.PlayerId}");
            sb.Append('\n');
        }

        if ((!self || !HiddenPoints.Contains(seer.PlayerId)) && (self || PlayersCanSeeOthersPoints))
            sb.Append(string.Format(Translator.GetString("TMG.Suffix.Points"), points, superPoints));

        if (!self) return sb.ToString();

        if (ShowSuffixOtherThanPoints)
        {
            switch (Round)
            {
                case 1:
                case 3:
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append(string.Format(Translator.GetString("TMG.Suffix.YourPick"), Pick[seer.PlayerId]));
                    sb.Append('\n');
                    sb.Append(Translator.GetString("TMG.Suffix.ChangePickHint"));
                    break;
                case 2 when AuctionValue != 0:
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append(string.Format(Translator.GetString("TMG.Suffix.AuctionValue"), AuctionValue));
                    sb.Append('\n');
                    sb.Append(string.Format(Translator.GetString("TMG.Suffix.YourBid"), Pick[seer.PlayerId]));
                    sb.Append('\n');
                    sb.Append(Translator.GetString("TMG.Suffix.ChangeBidHint"));
                    break;
                case 2:
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append("<size=80%>");
                    sb.Append(string.Join('\n', Enum.GetValues<Item>().Select(x => $"{Translator.GetString($"TMG.Item.{x}")} (ID {ItemIds[seer.PlayerId][x]}) ({string.Format(Translator.GetString("TMG.Suffix.ItemCost"), ItemCosts[x])}) - {Translator.GetString($"TMG.ItemDescription.{x}")}")));
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append(Translator.GetString("TMG.Suffix.BuyItemHint"));
                    sb.Append("</size>");
                    break;
            }
        }

        List<Item> items = PlayerItems[seer.PlayerId];

        if (items.Count > 0)
        {
            sb.Append('\n');
            sb.Append('\n');
            sb.Append(Translator.GetString("TMG.Suffix.YourItems"));
            sb.Append('\n');
            sb.Append(string.Join(", ", items.GroupBy(x => x).Select(x => $"{x.Count()}x {Translator.GetString($"TMG.Item.{x.Key}")} (ID {ItemIds[seer.PlayerId][x.Key]})")));
            sb.Append('\n');
            sb.Append('\n');
            sb.Append(Translator.GetString("TMG.Suffix.UseItemHint"));
        }

        return sb.ToString().Trim();
    }

    public static IEnumerator OnGameStart()
    {
        Round = 0;
        ShowSuffixOtherThanPoints = false;

        yield return null;

        var currentMap = Main.CurrentMap;
        AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).Distinct().Shuffle();
        AllRooms.Remove(SystemTypes.Hallway);
        AllRooms.Remove(SystemTypes.Outside);
        AllRooms.Remove(SystemTypes.Ventilation);
        if (currentMap is MapNames.Skeld or MapNames.Dleks or MapNames.Polus) AllRooms.Remove(SystemTypes.LifeSupp);
        if (currentMap is MapNames.Skeld or MapNames.Dleks) AllRooms.Remove(SystemTypes.UpperEngine);
        if (currentMap is MapNames.Skeld or MapNames.Dleks) AllRooms.Remove(SystemTypes.LowerEngine);
        if (currentMap is MapNames.MiraHQ) AllRooms.Remove(SystemTypes.Storage);
        if (currentMap is MapNames.MiraHQ) AllRooms.Remove(SystemTypes.MedBay);
        if (currentMap is MapNames.MiraHQ) AllRooms.Remove(SystemTypes.Admin);
        if (currentMap is MapNames.MiraHQ or MapNames.Polus or MapNames.Airship) AllRooms.Remove(SystemTypes.Comms);
        if (currentMap is MapNames.Polus) AllRooms.Remove(SystemTypes.Security);
        if (currentMap is MapNames.Polus) AllRooms.Remove(SystemTypes.BoilerRoom);
        if (currentMap is MapNames.Fungle) AllRooms.Remove(SystemTypes.SleepingQuarters);
        if (currentMap is MapNames.Fungle) AllRooms.Remove(SystemTypes.FishingDock);
        if (currentMap is MapNames.Fungle) AllRooms.Remove(SystemTypes.Greenhouse);
        AllRooms.RemoveAll(x => x.ToString().Contains("Decontamination"));
        if (SubmergedCompatibility.IsSubmerged()) AllRooms.RemoveAll(x => (byte)x > 135);

        var aapc = Main.AllAlivePlayerControls;
        Points = aapc.ToDictionary(x => x.PlayerId, _ => 0);
        SuperPoints = aapc.ToDictionary(x => x.PlayerId, _ => 0);
        DefaultColorIds = aapc.ToDictionary(x => x, x => x.Data.DefaultOutfit.ColorId);
        PlayerItems = aapc.ToDictionary(x => x.PlayerId, _ => new List<Item>());

        Groups = [];
        GroupPlayers = [];
        GroupRooms = [];
        Pick = [];
        AmReady = [];
        ItemIds = [];
        HiddenPoints = [];
        CannotPurchase = [];
        DoubleModifierActive = [];
        EjectedPlayers = [];
        Round4PlacesFromFirst = [];
        Round4PlacesFromLast = [];
        WinningBriefcaseHolderId = byte.MaxValue;
        WinningBriefcaseLastHolderId = byte.MaxValue;
        PreventGameEnd = false;
        AuctionValue = 0;

        ItemCosts = ItemCostsOptions.ToDictionary(x => x.Key, x => x.Value.GetInt());

        PlayersCanSeeOthersPoints = PlayersCanSeeOthersPointsOption.GetBool();
        NumGroupsForRound1 = NumGroupsForRound1Option.GetInt();
        TimeForEachPickInRound1 = TimeForEachPickInRound1Option.GetInt();
        NumPointsToAdvanceInRound1 = NumPointsToAdvanceInRound1Option.GetInt();
        MinPlayersInRound2 = MinPlayersInRound2Option.GetInt();
        SuperPointsToNormalPointsMultiplier = SuperPointsToNormalPointsMultiplierOption.GetInt();
        TimeForEachPickInRound2 = TimeForEachPickInRound2Option.GetInt();
        TimeForItemPurchasingInRound2 = TimeForItemPurchasingInRound2Option.GetInt();
        FoolNumPointsLost = FoolNumPointsLostOption.GetInt();
        TimeForEachPickInRound3 = TimeForEachPickInRound3Option.GetInt();
        MaxPlayersForRound4 = MaxPlayersForRound4Option.GetInt();
        MindDetectiveFailChance = MindDetectiveFailChanceOption.GetInt();

        if (MinPlayersInRound2 > aapc.Count)
            MinPlayersInRound2 = aapc.Count;

        {
            IEnumerable<IEnumerable<PlayerControl>> groups = aapc.Partition(NumGroupsForRound1);
            RandomSpawn.SpawnMap map = RandomSpawn.SpawnMap.GetSpawnMap();
            GroupRooms = [];
            Group group = default(Group);

            foreach (IEnumerable<PlayerControl> players in groups)
            {
                (SystemTypes room, Vector2 location) = map.Positions.IntersectBy(AllRooms, x => x.Key).ExceptBy(GroupRooms.Values, x => x.Key).RandomElement();
                GroupRooms[group] = room;
                List<byte> ids = [];

                foreach (PlayerControl pc in players)
                {
                    Groups[pc.PlayerId] = group;
                    pc.RpcSetColor(group.GetColorId());
                    pc.TP(location);
                    ids.Add(pc.PlayerId);
                }

                GroupPlayers[group] = ids;
                group++;
            }
        }

        Round = 1;

        Main.Instance.StartCoroutine(PreventMovingOutOfGroupRooms());

        Utils.SetChatVisibleForAll();

        yield return NotifyEveryone("TMG.Tutorial.Basics", 6);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Notify.Round", 2, 1);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Tutorial.Round1", 12, TimeForEachPickInRound1, NumPointsToAdvanceInRound1, Main.AllAlivePlayerControls.Count - MinPlayersInRound2);
        if (Stop) yield break;

        Main.EnumerateAlivePlayerControls().Do(x => x.RpcChangeRoleBasis(CustomRoles.PhantomEHR));

        while (true)
        {
            Pick = aapc.ToDictionary(x => x.PlayerId, _ => IRandom.Instance.Next(1, 4));
            ProceedingInCountdownEndTS = Utils.TimeStamp + TimeForEachPickInRound1;
            float timer = TimeForEachPickInRound1;

            ShowSuffixOtherThanPoints = true;
            Utils.NotifyRoles();

            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (Stop) yield break;
                yield return null;

                if ((int)timer % 10 == 0 && LastTimeWarning + 2 < Utils.TimeStamp && timer > 3f)
                {
                    LastTimeWarning = Utils.TimeStamp;
                    Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.TimeLeft"), (int)timer), title: Translator.GetString("TMG.Message.TimeLeftTitle"));
                    LateTask.New(() => Utils.NotifyRoles(SpecifyTarget: Main.EnumerateAlivePlayerControls().MinBy(x => x.PlayerId)), 1f, log: false);
                }
            }

            ShowSuffixOtherThanPoints = false;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                Group group = Groups[pc.PlayerId];
                int pick = Pick[pc.PlayerId];
                bool sameAsSomeoneElse = GroupPlayers[group].FindFirst(x => x != pc.PlayerId && Pick.TryGetValue(x, out int p) && p == pick, out byte otherId);

                if (!sameAsSomeoneElse) Points[pc.PlayerId] += pick;

                pc.Notify(string.Format(Translator.GetString(sameAsSomeoneElse ? "TMG.Notify.SamePick" : "TMG.Notify.Pick"), pick, sameAsSomeoneElse ? otherId.ColoredPlayerName() : string.Empty));
            }

            yield return new WaitForSecondsRealtime(3f);
            if (Stop) yield break;
            NameNotifyManager.Reset();

            if (Points.Values.Any(x => x >= NumPointsToAdvanceInRound1)) break;
        }

        aapc = Main.AllAlivePlayerControls;
        aapc.Join(Points, x => x.PlayerId, x => x.Key, (pc, kvp) => (pc, points: kvp.Value)).OrderBy(x => x.points).SkipLast(MinPlayersInRound2).Do(x => x.pc.Suicide());

        Round = 2;

        DefaultColorIds.DoIf(x => x.Key != null && x.Value is >= byte.MinValue and <= byte.MaxValue, x => x.Key.RpcSetColor((byte)x.Value));

        yield return NotifyEveryone("TMG.Notify.Round", 2, 2);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Tutorial.Round2", 10, SuperPointsToNormalPointsMultiplier);
        if (Stop) yield break;

        for (int i = 0; i < 3; i++)
        {
            AuctionValue = IRandom.Instance.Next(1, 11);
            Pick = Main.EnumerateAlivePlayerControls().IntersectBy(Points.Keys, x => x.PlayerId).ToDictionary(x => x.PlayerId, x => IRandom.Instance.Next(Points[x.PlayerId] + 1));
            ProceedingInCountdownEndTS = Utils.TimeStamp + TimeForEachPickInRound2;
            float timer = TimeForEachPickInRound2;

            ShowSuffixOtherThanPoints = true;
            Utils.NotifyRoles();

            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (Stop) yield break;
                yield return null;

                if ((int)timer % 10 == 0 && LastTimeWarning + 2 < Utils.TimeStamp)
                {
                    LastTimeWarning = Utils.TimeStamp;
                    Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.TimeLeft"), (int)timer), title: Translator.GetString("TMG.Message.TimeLeftTitle"));
                    LateTask.New(() => Utils.NotifyRoles(SpecifyTarget: Main.EnumerateAlivePlayerControls().MinBy(x => x.PlayerId)), 1f, log: false);
                }
            }

            ShowSuffixOtherThanPoints = false;

            Main.EnumerateAlivePlayerControls().Do(x => Points[x.PlayerId] -= Pick[x.PlayerId]);
            int highestBid = Pick.Values.Max();
            List<byte> auctionWinners = Pick.Where(x => x.Value == highestBid).Select(x => x.Key).ToList();
            auctionWinners.ForEach(x => SuperPoints[x] += AuctionValue);

            yield return NotifyEveryone("TMG.Notify.AuctionEnd", 4, highestBid, string.Join(" & ", auctionWinners.ConvertAll(x => x.ColoredPlayerName())), AuctionValue);
            if (Stop) yield break;
        }

        int lowestSuperPoints = SuperPoints.Values.Min();
        Main.EnumerateAlivePlayerControls().Join(SuperPoints, x => x.PlayerId, x => x.Key, (pc, kvp) => (pc, points: kvp.Value)).DoIf(x => x.points == lowestSuperPoints, x => x.pc.Suicide());

        yield return NotifyEveryone("TMG.Notify.ItemPurchasingBegins", 6, TimeForItemPurchasingInRound2);
        if (Stop) yield break;

        Item[] items = Enum.GetValues<Item>();
        int[] itemIds = items.Select(x => (int)x).ToArray();

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            ItemIds[pc.PlayerId] = items.Zip(itemIds.Shuffle()).ToDictionary(x => x.First, x => x.Second);

        AuctionValue = 0;
        ShowSuffixOtherThanPoints = true;
        Utils.NotifyRoles();

        ProceedingInCountdownEndTS = Utils.TimeStamp + TimeForItemPurchasingInRound2;
        float timer2 = TimeForItemPurchasingInRound2;

        while (timer2 > 0f)
        {
            timer2 -= Time.deltaTime;
            if (Stop) yield break;
            yield return null;

            if ((int)timer2 % 10 == 0 && LastTimeWarning + 2 < Utils.TimeStamp)
            {
                LastTimeWarning = Utils.TimeStamp;
                Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.TimeLeft"), (int)timer2), title: Translator.GetString("TMG.Message.TimeLeftTitle"));
                LateTask.New(() => Utils.NotifyRoles(SpecifyTarget: Main.EnumerateAlivePlayerControls().MinBy(x => x.PlayerId)), 1f, log: false);
            }

            if (Main.AllAlivePlayerControls.Count == AmReady.Count)
            {
                ProceedingInCountdownEndTS = Utils.TimeStamp;
                break;
            }
        }

        ShowSuffixOtherThanPoints = false;

        yield return NotifyEveryone("TMG.Notify.ItemPurchasingEnd", 3);
        if (Stop) yield break;

        Round = 3;

        yield return NotifyEveryone("TMG.Notify.Round", 2, 3);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Tutorial.Round3", 6, Main.AllAlivePlayerControls.Count, MaxPlayersForRound4);
        if (Stop) yield break;

        while (true)
        {
            Pick = Main.EnumerateAlivePlayerControls().ToDictionary(x => x.PlayerId, _ => IRandom.Instance.Next(1, Main.AllAlivePlayerControls.Count + 1));
            ProceedingInCountdownEndTS = Utils.TimeStamp + TimeForEachPickInRound3;
            float timer = TimeForEachPickInRound3;

            ShowSuffixOtherThanPoints = true;
            Utils.NotifyRoles();

            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (Stop) yield break;
                yield return null;

                if ((int)timer % 10 == 0 && LastTimeWarning + 2 < Utils.TimeStamp)
                {
                    LastTimeWarning = Utils.TimeStamp;
                    Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.TimeLeft"), (int)timer), title: Translator.GetString("TMG.Message.TimeLeftTitle"));
                    LateTask.New(() => Utils.NotifyRoles(SpecifyTarget: Main.EnumerateAlivePlayerControls().MinBy(x => x.PlayerId)), 1f, log: false);
                }
            }

            ShowSuffixOtherThanPoints = false;

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                int pick = Pick[pc.PlayerId];
                bool sameAsSomeoneElse = Main.EnumerateAlivePlayerControls().Without(pc).FindFirst(x => Pick.TryGetValue(x.PlayerId, out int p) && p == pick, out PlayerControl otherPc);

                if (!sameAsSomeoneElse)
                    Points[pc.PlayerId] += DoubleModifierActive.Contains(pc.PlayerId) ? pick * 2 : pick;

                if (HiddenPoints.Contains(pc.PlayerId)) continue;
                pc.Notify(string.Format(Translator.GetString(sameAsSomeoneElse ? "TMG.Notify.SamePick" : "TMG.Notify.Pick"), pick, sameAsSomeoneElse ? otherPc.PlayerId.ColoredPlayerName() : string.Empty));
            }

            yield return new WaitForSecondsRealtime(3f);
            if (Stop) yield break;
            NameNotifyManager.Reset();

            int lowestScore = Points.Values.Min();
            Main.EnumerateAlivePlayerControls().Join(Points, x => x.PlayerId, x => x.Key, (pc, kvp) => (pc, points: kvp.Value)).DoIf(x => x.points == lowestScore, x => x.pc.Suicide());

            yield return NotifyEveryone("TMG.Notify.Round3NumPlayersLeft", 3, Main.AllAlivePlayerControls.Count, MaxPlayersForRound4);
            if (Stop) yield break;

            if (Main.AllAlivePlayerControls.Count <= MaxPlayersForRound4) break;
        }

        Round = 4;

        yield return NotifyEveryone("TMG.Notify.Round", 2, 4);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Tutorial.Round4", 20);
        if (Stop) yield break;

        PreventGameEnd = true;

        while (true)
        {
            PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true);
            yield return new WaitForSecondsRealtime(8f);
            if (!GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded) yield break;

            WinningBriefcaseHolderId = Main.EnumerateAlivePlayerControls().RandomElement().PlayerId;

            Utils.SendMessage("\n", WinningBriefcaseHolderId, Translator.GetString("TMG.Message.YouHoldTheWinningBriefcase"), importance: MessageImportance.High);
            Main.EnumerateAlivePlayerControls().Select(x => x.PlayerId).Without(WinningBriefcaseHolderId).Do(x => Utils.SendMessage("\n", x, Translator.GetString("TMG.Message.YouHoldAnEmptyBriefcase"), importance: MessageImportance.High));

            while (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (!GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded) yield break;
            }

            yield return new WaitForSecondsRealtime(1f);

            while (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (!GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded) yield break;
            }

            yield return new WaitForSecondsRealtime(1f);

            while (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (!GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded) yield break;
            }

            yield return new WaitForSecondsRealtime(1f);

            aapc = Main.AllAlivePlayerControls;

            if (aapc.Count <= 2)
            {
                if (aapc.Count >= 1)
                {
                    WinningBriefcaseHolderId = aapc.RandomElement().PlayerId;
                    Round4PlacesFromLast.Add(WinningBriefcaseHolderId);

                    if (aapc.Count == 2)
                        Round4PlacesFromFirst.Add(aapc.Select(x => x.PlayerId).Without(WinningBriefcaseHolderId).Single());

                    yield return NotifyEveryone("TMG.Notify.Round4EndLastHolder", 3, WinningBriefcaseHolderId.ColoredPlayerName());
                }

                break;
            }
        }

        Round = 5;

        HiddenPoints.Clear();
        EjectedPlayers.ToValidPlayers().FindAll(x => !x.IsAlive()).ForEach(x => x.RpcRevive());

        yield return NotifyEveryone("TMG.Notify.Round4End", 3);
        if (Stop) yield break;

        List<byte> ranking = [];
        ranking.AddRange(Round4PlacesFromFirst);
        Round4PlacesFromLast.Reverse();
        ranking.AddRange(Round4PlacesFromLast);

        string join = string.Join('\n', ranking.Select((x, i) => $"{i + 1}. {x.ColoredPlayerName()}"));
        NameNotifyManager.Reset();
        Main.EnumerateAlivePlayerControls().NotifyPlayers(join, 100f);
        ProceedingInCountdownEndTS = Utils.TimeStamp + 5;
        yield return new WaitForSecondsRealtime(5f);
        if (Stop) yield break;

        Dictionary<byte, int> round4Points = ranking.ToDictionary(x => x, x => (ranking.Count - ranking.IndexOf(x)) * 10);
        round4Points.Do(x => Points[x.Key] += x.Value);

        join = string.Join('\n', round4Points.Select(x => $"{x.Key.ColoredPlayerName()}:  +{x.Value}"));
        yield return NotifyEveryone("TMG.Notify.Round4PointGain", 5, join);
        if (Stop) yield break;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            int superPoints = SuperPoints[pc.PlayerId];

            for (int i = 0; i < superPoints; i++)
                Points[pc.PlayerId] += SuperPointsToNormalPointsMultiplier;
        }

        var w = Utils.CreateRPC(CustomRPC.TMGSync);
        w.WritePacked(Points.Count);

        foreach ((byte key, int value) in Points)
        {
            w.Write(key);
            w.WritePacked(value);
        }

        Utils.EndRPC(w);

        SuperPoints.SetAllValues(0);

        yield return NotifyEveryone("TMG.Notify.SuperPointConversion", 4, SuperPointsToNormalPointsMultiplier);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Notify.TheWinnerIs", 2);

        PreventGameEnd = false;
        int highestPoints = Points.Values.Max();
        HashSet<byte> winners = Points.Where(x => x.Value == highestPoints).Select(x => x.Key).ToHashSet();
        CustomWinnerHolder.WinnerIds = winners;
    }

    private static IEnumerator NotifyEveryone(string key, int time, params object[] args)
    {
        NameNotifyManager.Reset();
        string str = Translator.GetString(key);
        Main.EnumerateAlivePlayerControls().NotifyPlayers(args.Length == 0 ? str : string.Format(str, args), 100f);
        ProceedingInCountdownEndTS = Utils.TimeStamp + time;
        yield return new WaitForSecondsRealtime(time);
        NameNotifyManager.Reset();
    }

    private static IEnumerator PreventMovingOutOfGroupRooms()
    {
        while (Round == 1 && !GameStates.IsEnded && GameStates.IsInTask && !ExileController.Instance)
        {
            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                Group group = Groups[pc.PlayerId];
                SystemTypes groupRoom = GroupRooms[group];

                if (!pc.IsInRoom(groupRoom))
                    pc.TP(RandomSpawn.SpawnMap.GetSpawnMap().Positions[groupRoom]);
            }

            yield return new WaitForSecondsRealtime(2f);
        }
    }

    public static string GetEjectionMessage(byte exiledPlayerId)
    {
        EjectedPlayers.Add(exiledPlayerId);

        if (WinningBriefcaseHolderId == exiledPlayerId)
        {
            Round4PlacesFromFirst.Add(exiledPlayerId);
            return string.Format(Translator.GetString("TMG.EjectionText.HadWinningBriefcase"), exiledPlayerId.ColoredPlayerName(), Round4PlacesFromFirst.IndexOf(exiledPlayerId) + 1);
        }

        Round4PlacesFromLast.Add(exiledPlayerId);
        return string.Format(Translator.GetString("TMG.EjectionText.HadEmptyBriefcase"), exiledPlayerId.ColoredPlayerName());
    }

    public static void OnChat(PlayerControl pc, string text)
    {
        try
        {
            if (Round < 2 || !text.Any(x => x is >= '0' and <= '9') || text.Length < 2 || GameStates.IsLobby) return;
            
            Utils.CheckServerCommand(ref text, out bool spamRequired);

            switch (text[0])
            {
                case 'b':
                {
                    if (CannotPurchase.Contains(pc.PlayerId))
                    {
                        Utils.SendMessage(Translator.GetString("TMG.Message.CannotPurchase"), pc.PlayerId, "<#ff0000>X</color>");
                        break;
                    }

                    var item = FindItemFromText();
                    if (!item.HasValue) break;

                    int superPoints = SuperPoints[pc.PlayerId];
                    int cost = ItemCosts[item.Value];
                    string itemName = Translator.GetString($"TMG.Item.{item.Value}");

                    if (cost > superPoints)
                    {
                        Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.NotEnoughSuperPoints"), itemName, cost, superPoints), pc.PlayerId, "<#ff0000>X</color>");
                        break;
                    }

                    if (item.Value == Item.DoubleModifier) DoubleModifierActive.Add(pc.PlayerId);

                    SuperPoints[pc.PlayerId] -= cost;
                    PlayerItems[pc.PlayerId].Add(item.Value);
                    Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.ItemPurchased"), itemName, cost, superPoints), pc.PlayerId, "<#00ff00>✓</color>");

                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    break;
                }
                case 'u':
                {
                    var item = FindItemFromText();
                    if (!item.HasValue || !PlayerItems[pc.PlayerId].Remove(item.Value)) break;

                    switch (item.Value)
                    {
                        case Item.BlindingGas:
                        {
                            byte id = FindTargetIdFromText();
                            if (id == byte.MaxValue) break;
                            HiddenPoints.Add(id);
                            PlayerControl target = id.GetPlayer();
                            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
                            Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.ItemUsed"), Translator.GetString($"TMG.Item.{item.Value}")), pc.PlayerId, "<#00ff00>✓</color>");
                            break;
                        }
                        case Item.MerchantDise:
                        {
                            byte id = FindTargetIdFromText();
                            if (id == byte.MaxValue) break;
                            CannotPurchase.Add(id);
                            Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.ItemUsed"), Translator.GetString($"TMG.Item.{item.Value}")), pc.PlayerId, "<#00ff00>✓</color>");
                            break;
                        }
                        case Item.Lasso:
                        {
                            if (Round != 4 || !GameStates.IsMeeting)
                            {
                                Utils.SendMessage(Translator.GetString("TMG.Message.LassoOnlyInRound4"), pc.PlayerId, "<#ff0000>X</color>");
                                break;
                            }

                            byte id = FindTargetIdFromText();
                            if (id == byte.MaxValue) break;

                            if (!pc.AmOwner && spamRequired)
                                Utils.SendMessage("\n", pc.PlayerId, Translator.GetString("NoSpamAnymoreUseCmd"));

                            if (id == WinningBriefcaseHolderId)
                            {
                                WinningBriefcaseLastHolderId = id;
                                WinningBriefcaseHolderId = pc.PlayerId;
                                LateTask.New(() =>
                                {
                                    Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.LassoedWinningBriefcaseSelf"), id.ColoredPlayerName()), pc.PlayerId, "<#00ff00>✓</color>", importance: MessageImportance.High);
                                    Utils.SendMessage(Translator.GetString("TMG.Message.LassoedWinningBriefcase"), id, "<#ffff00>⚠</color>");
                                }, 0.2f);
                            }
                            else
                                LateTask.New(() => Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.LassoedEmptyBriefcase"), id.ColoredPlayerName()), pc.PlayerId, "<#ffa500>-</color>", importance: MessageImportance.High), 0.2f);

                            break;
                        }
                        case Item.Fool:
                        {
                            byte id = FindTargetIdFromText();
                            if (id == byte.MaxValue) break;
                            Points[id] -= FoolNumPointsLost;
                            break;
                        }
                        case Item.MindDetective:
                        {
                            byte id = FindTargetIdFromText();
                            if (id == byte.MaxValue) break;

                            int pick = Pick[id];

                            if (IRandom.Instance.Next(100) < MindDetectiveFailChance)
                                pick = IRandom.Instance.Next(1, Main.AllAlivePlayerControls.Count + 1);

                            Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.MindDetective"), id.ColoredPlayerName(), pick), pc.PlayerId, "<#00ff00>✓</color>", importance: MessageImportance.High);
                            break;
                        }
                    }

                    break;
                }
                case 's':
                {
                    if (WinningBriefcaseLastHolderId != pc.PlayerId)
                    {
                        if (Round == 4) Utils.SendMessage(Translator.GetString("TMG.Message.StealBackNotLastHolder"), pc.PlayerId, "<#ff0000>X</color>", importance: MessageImportance.High);
                        break;
                    }

                    byte id = FindTargetIdFromText(false);
                    if (id == byte.MaxValue) break;

                    if (id == WinningBriefcaseHolderId)
                    {
                        WinningBriefcaseHolderId = pc.PlayerId;
                        WinningBriefcaseLastHolderId = byte.MaxValue;
                        Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.StealBackWinningBriefcaseSelf"), id.ColoredPlayerName()), pc.PlayerId, "<#00ff00>✓</color>", importance: MessageImportance.High);
                        Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.StealBackWinningBriefcase"), pc.PlayerId.ColoredPlayerName()), id, "<#ffff00>⚠</color>", importance: MessageImportance.High);
                    }
                    else
                    {
                        WinningBriefcaseLastHolderId = byte.MaxValue;
                        Utils.SendMessage(string.Format(Translator.GetString("TMG.Message.StealBackEmptyBriefcase"), id.ColoredPlayerName()), pc.PlayerId, "<#ffa500>-</color>", importance: MessageImportance.High);
                    }

                    break;
                }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        return;

        Item? FindItemFromText()
        {
            for (int i = 1; i < text.Length; i++)
            {
                char letter = text[i];

                switch (letter)
                {
                    case ' ':
                    case < '0':
                    case > '9':
                        continue;
                    default:
                        int id = letter - '0';
                        Dictionary<Item, int> itemIds = ItemIds[pc.PlayerId];
                        if (id >= itemIds.Count) break;
                        return itemIds.GetKeyByValue(id);
                }
            }

            return null;
        }

        byte FindTargetIdFromText(bool space = true)
        {
            int startIndex;

            if (space)
            {
                int lastSpaceIndex = text.LastIndexOf(' ');
                if (lastSpaceIndex == -1 || lastSpaceIndex == text.Length - 1) return byte.MaxValue;
                startIndex = lastSpaceIndex + 1;
            }
            else
                startIndex = 1;

            string idString = text[startIndex..];
            return !byte.TryParse(idString, out byte id) ? byte.MaxValue : id;
        }
    }

    public static void OnVanish(PlayerControl player)
    {
        switch (Round)
        {
            case 1:
            {
                Pick[player.PlayerId]++;
                if (Pick[player.PlayerId] > 3) Pick[player.PlayerId] = 1;
                break;
            }
            case 2 when AuctionValue == 0:
            {
                AmReady.Add(player.PlayerId);
                break;
            }
            case 2:
            {
                Pick[player.PlayerId]++;
                if (Pick[player.PlayerId] > Points[player.PlayerId]) Pick[player.PlayerId] = 0;
                break;
            }
            case 3:
            {
                Pick[player.PlayerId]++;
                if (Pick[player.PlayerId] > Main.AllAlivePlayerControls.Count) Pick[player.PlayerId] = 1;
                break;
            }
        }

        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32(), AmongUsClient.Instance.AmHost)
        {
            case (1, true):
            {
                OnChat(reader.ReadNetObject<PlayerControl>(), reader.ReadString());
                break;
            }
            case (2, false):
            {
                Points = [];
                int count = reader.ReadPackedInt32();

                for (int i = 0; i < count; i++)
                {
                    byte key = reader.ReadByte();
                    int value = reader.ReadPackedInt32();
                    Points[key] = value;
                }

                break;
            }
        }
    }

    private static byte GetColorId(this Group group)
    {
        return group switch
        {
            Group.Red => 0,
            Group.Yellow => 5,
            Group.Blue => 10,
            Group.Green => 11,
            Group.Tan => 16,
            Group.Rose => 13,
            Group.Orange => 4,
            _ => 7
        };
    }

    public static bool CheckForGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;

        if (PreventGameEnd) return false;

        if (CustomWinnerHolder.WinnerIds.Count > 0) return true;

        var aapc = Main.AllAlivePlayerControls;

        switch (aapc.Count)
        {
            case 0:
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                CustomWinnerHolder.WinnerIds = [PlayerControl.LocalPlayer.PlayerId];
                Logger.Warn("Game ended with no players alive", "TMG");
                return true;
            case 1:
                CustomWinnerHolder.WinnerIds = [aapc[0].PlayerId];
                Logger.Warn("Game ended with one player alive", "TMG");
                return true;
        }

        return false;
    }

    enum Item
    {
        BlindingGas,
        MerchantDise,
        Lasso,
        DoubleModifier,
        Fool,
        MindDetective
    }

    private enum Group
    {
        Red,
        Yellow,
        Blue,
        Green,
        Tan,
        Rose,
        Orange
    }
}

public class TMGPlayer : RoleBase
{
    private static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption() { }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        TheMindGame.OnVanish(pc);
        return false;
    }
}