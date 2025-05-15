using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR;

public static class TheMindGame
{
    private static Dictionary<byte, int> Points = [];
    private static Dictionary<byte, int> SuperPoints = [];
    private static Dictionary<PlayerControl, int> DefaultColorIds = [];
    private static Dictionary<byte, Group> Groups = [];
    private static Dictionary<Group, List<byte>> GroupPlayers = []; // TODO: Reset ALL these at the start of the game
    private static List<SystemTypes> AllRooms = [];
    private static Dictionary<Group, SystemTypes> GroupRooms = [];
    private static Dictionary<byte, int> Pick = [];
    private static HashSet<byte> AmReady = [];
    private static Dictionary<byte, Dictionary<Item, int>> ItemIds = [];
    private static Dictionary<byte, List<Item>> PlayerItems = [];
    private static long ProceedingInCountdownEndTS;
    private static long LastTimeWarning;
    private static bool ShowSuffixOtherThanPoints;
    private static int AuctionValue;
    private static byte WinningBriefcaseHolderId;
    private static List<byte> Round4PlacesFromFirst;
    private static List<byte> Round4PlacesFromLast;
    private static List<byte> EjectedPlayers;
    private static int Round = 1;

    // Settings
    private static bool PlayersCanSeeOthersPoints = true;
    private static int NumGroupsForRound1 = 5; // 1-7
    private static int TimeForEachPickInRound1 = 15;
    private static int NumPointsToAdvanceInRound1 = 10;
    private static int MinPlayersInRound2 = 10;
    private static int SuperPointsToNormalPointsMultiplier = 3;
    private static int TimeForEachPickInRound2 = 25;
    private static int TimeForItemPurchasingInRound2 = 40;
    private static int FoolNumPointsLost = 5;
    private static Dictionary<Item, int> ItemCosts = []; // !!!!
    private static int TimeForEachPickInRound3 = 30;
    private static int MaxPlayersForRound4 = 5;

    private static bool Stop => GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded;

    public static void SetupCustomOption() { }
    public static string GetStatistics(byte id) { }
    public static int GetPoints(byte id) { }
    public static string GetTaskBarText() { }
    public static string GetSuffix(PlayerControl seer, PlayerControl target) { }

    public static IEnumerator OnGameStart()
    {
        yield return null;

        Round = 1;

        AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).Distinct().Shuffle();
        AllRooms.Remove(SystemTypes.Hallway);
        AllRooms.Remove(SystemTypes.Outside);
        AllRooms.Remove(SystemTypes.Ventilation);
        AllRooms.RemoveAll(x => x.ToString().Contains("Decontamination"));

        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        Points = aapc.ToDictionary(x => x.PlayerId, _ => 0);
        SuperPoints = aapc.ToDictionary(x => x.PlayerId, _ => 0);
        DefaultColorIds = aapc.ToDictionary(x => x, x => x.Data.DefaultOutfit.ColorId);

        {
            IEnumerable<IEnumerable<PlayerControl>> groups = aapc.Partition(NumGroupsForRound1);
            RandomSpawn.SpawnMap map = RandomSpawn.SpawnMap.GetSpawnMap();
            GroupRooms = [];
            Group group = default(Group);

            foreach (IEnumerable<PlayerControl> players in groups)
            {
                (SystemTypes room, Vector2 location) = map.Positions.ExceptBy(GroupRooms.Values, x => x.Key).RandomElement();
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

        Main.Instance.StartCoroutine(PreventMovingOutOfGroupRooms());

        Utils.SetChatVisibleForAll();

        yield return NotifyEveryone("TMG.Tutorial.Basics", 4);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Notify.Round", 2, 1);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Tutorial.Round1", 10, TimeForEachPickInRound1, NumPointsToAdvanceInRound1, MinPlayersInRound2);
        if (Stop) yield break;

        Main.AllAlivePlayerControls.Do(x => x.RpcChangeRoleBasis(CustomRoles.PhantomEHR));

        while (true)
        {
            Pick = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, _ => IRandom.Instance.Next(1, 4));
            ProceedingInCountdownEndTS = Utils.TimeStamp + TimeForEachPickInRound1;
            float timer = TimeForEachPickInRound1;

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
                }
            }

            ShowSuffixOtherThanPoints = false;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                Group group = Groups[pc.PlayerId];
                int pick = Pick[pc.PlayerId];
                bool sameAsSomeoneElse = GroupPlayers[group].FindFirst(x => x != pc.PlayerId && Pick[x] == pick, out byte otherId);

                if (!sameAsSomeoneElse) Points[pc.PlayerId] += pick;

                pc.Notify(string.Format(Translator.GetString(sameAsSomeoneElse ? "TMG.Notify.SamePick" : "TMG.Notify.Pick"), pick, sameAsSomeoneElse ? otherId.ColoredPlayerName() : string.Empty));
            }

            yield return new WaitForSeconds(3f);
            if (Stop) yield break;
            NameNotifyManager.Reset();

            int playersAdvancing = Points.Count(x => x.Value >= NumPointsToAdvanceInRound1);
            yield return NotifyEveryone("TMG.Notify.Round1NumPlayersAdvancing", 3, playersAdvancing, Main.AllAlivePlayerControls.Length, MinPlayersInRound2);
            if (Stop) yield break;

            if (playersAdvancing >= MinPlayersInRound2) break;
        }

        foreach (List<byte> playerIds in GroupPlayers.Values)
        {
            var points = playerIds.ToDictionary(x => x, x => Points[x]);
            int lowestScore = points.Values.Min();
            playerIds.Intersect(Main.AllAlivePlayerControls.Select(x => x.PlayerId)).Join(points, x => x, x => x.Key, (id, kvp) => (id, points: kvp.Value)).DoIf(x => x.points == lowestScore, x => x.id.GetPlayer().Suicide());
        }

        Round = 2;

        DefaultColorIds.DoIf(x => x.Key != null && x.Value is >= byte.MinValue and <= byte.MaxValue, x => x.Key.RpcSetColor((byte)x.Value));

        yield return NotifyEveryone("TMG.Notify.Round", 2, 2);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Tutorial.Round2", 9, SuperPointsToNormalPointsMultiplier);
        if (Stop) yield break;

        for (int i = 0; i < 3; i++)
        {
            AuctionValue = IRandom.Instance.Next(1, 11);
            Pick = Main.AllAlivePlayerControls.IntersectBy(Points.Keys, x => x.PlayerId).ToDictionary(x => x.PlayerId, x => IRandom.Instance.Next(Points[x.PlayerId] + 1));
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
                }
            }

            ShowSuffixOtherThanPoints = false;

            Main.AllAlivePlayerControls.Do(x => Points[x.PlayerId] -= Pick[x.PlayerId]);
            int highestBid = Pick.Values.Max();
            List<byte> auctionWinners = Pick.Where(x => x.Value == highestBid).Select(x => x.Key).ToList();
            auctionWinners.ForEach(x => SuperPoints[x] += AuctionValue);

            yield return NotifyEveryone("TMG.Notify.AuctionEnd", 4, highestBid, string.Join(" & ", auctionWinners.ConvertAll(x => x.ColoredPlayerName())), AuctionValue);
            if (Stop) yield break;
        }

        int lowestSuperPoints = SuperPoints.Values.Min();
        Main.AllAlivePlayerControls.Join(SuperPoints, x => x.PlayerId, x => x.Key, (pc, kvp) => (pc, points: kvp.Value)).DoIf(x => x.points == lowestSuperPoints, x => x.pc.Suicide());

        yield return NotifyEveryone("TMG.Notify.ItemPurchasingBegins", 6, TimeForItemPurchasingInRound2);
        if (Stop) yield break;

        Item[] items = Enum.GetValues<Item>();
        int[] itemIds = items.Select(x => (int)x).ToArray();

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
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
            }

            if (Main.AllAlivePlayerControls.Length == AmReady.Count)
            {
                ProceedingInCountdownEndTS = Utils.TimeStamp;
                break;
            }
        }

        Main.AllAlivePlayerControls.ExceptBy(AmReady, x => x.PlayerId).Do(x => x.RpcChangeRoleBasis(CustomRoles.TMGPlayer));
        ShowSuffixOtherThanPoints = false;

        yield return NotifyEveryone("TMG.Notify.ItemPurchasingEnd", 3);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Notify.Round", 2, 3);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Tutorial.Round3", 6, Main.AllAlivePlayerControls.Length, MaxPlayersForRound4);
        if (Stop) yield break;

        Main.AllAlivePlayerControls.Do(x => x.RpcChangeRoleBasis(CustomRoles.PhantomEHR));

        while (true)
        {
            Pick = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, _ => IRandom.Instance.Next(1, Main.AllAlivePlayerControls.Length + 1));
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
                }
            }

            ShowSuffixOtherThanPoints = false;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                int pick = Pick[pc.PlayerId];
                bool sameAsSomeoneElse = Main.AllAlivePlayerControls.Without(pc).FindFirst(x => Pick[x.PlayerId] == pick, out PlayerControl otherPc);

                if (!sameAsSomeoneElse) Points[pc.PlayerId] += pick;

                pc.Notify(string.Format(Translator.GetString(sameAsSomeoneElse ? "TMG.Notify.SamePick" : "TMG.Notify.Pick"), pick, sameAsSomeoneElse ? otherPc.PlayerId.ColoredPlayerName() : string.Empty));
            }

            yield return new WaitForSeconds(3f);
            if (Stop) yield break;
            NameNotifyManager.Reset();

            int lowestScore = Points.Values.Min();
            Main.AllAlivePlayerControls.Join(Points, x => x.PlayerId, x => x.Key, (pc, kvp) => (pc, points: kvp.Value)).DoIf(x => x.points == lowestScore, x => x.pc.Suicide());

            yield return NotifyEveryone("TMG.Notify.Round3NumPlayersLeft", 3, Main.AllAlivePlayerControls.Length, MaxPlayersForRound4);
            if (Stop) yield break;

            if (Main.AllAlivePlayerControls.Length <= MaxPlayersForRound4) break;
        }

        Round = 4;

        yield return NotifyEveryone("TMG.Notify.Round", 2, 4);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Tutorial.Round4", 20);
        if (Stop) yield break;

        while (true)
        {
            PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true);
            yield return new WaitForSeconds(8f);
            if (!GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded) yield break;

            WinningBriefcaseHolderId = Main.AllAlivePlayerControls.RandomElement().PlayerId;

            Utils.SendMessage("\n", WinningBriefcaseHolderId, Translator.GetString("TMG.Message.YouHoldTheWinningBriefcase"));
            Main.AllAlivePlayerControls.Select(x => x.PlayerId).Without(WinningBriefcaseHolderId).Do(x => Utils.SendMessage("\n", x, Translator.GetString("TMG.Message.YouHoldAnEmptyBriefcase")));

            while (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)
            {
                yield return new WaitForSeconds(1f);
                if (!GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded) yield break;
            }

            yield return new WaitForSeconds(1f);

            while (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)
            {
                yield return new WaitForSeconds(1f);
                if (!GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded) yield break;
            }

            yield return new WaitForSeconds(1f);

            while (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)
            {
                yield return new WaitForSeconds(1f);
                if (!GameStates.InGame || GameStates.IsLobby || GameStates.IsEnded) yield break;
            }

            yield return new WaitForSeconds(1f);

            aapc = Main.AllAlivePlayerControls;

            if (aapc.Length <= 2)
            {
                if (aapc.Length >= 1)
                {
                    WinningBriefcaseHolderId = aapc.RandomElement().PlayerId;
                    Round4PlacesFromLast.Add(WinningBriefcaseHolderId);

                    if (aapc.Length == 2)
                        Round4PlacesFromFirst.Add(aapc.Select(x => x.PlayerId).Without(WinningBriefcaseHolderId).Single());

                    yield return NotifyEveryone("TMG.Notify.Round4EndLastHolder", 3, WinningBriefcaseHolderId.ColoredPlayerName());
                }

                break;
            }
        }

        EjectedPlayers.ToValidPlayers().FindAll(x => !x.IsAlive()).ForEach(x => x.RpcRevive());

        yield return NotifyEveryone("TMG.Notify.Round4End", 3);
        if (Stop) yield break;

        List<byte> ranking = [];
        ranking.AddRange(Round4PlacesFromFirst);
        Round4PlacesFromLast.Reverse();
        ranking.AddRange(Round4PlacesFromLast);

        string join = string.Join('\n', ranking.Select((x, i) => $"{i + 1}. {x.ColoredPlayerName()}"));
        NameNotifyManager.Reset();
        Main.AllAlivePlayerControls.NotifyPlayers(join, 100f);
        ProceedingInCountdownEndTS = Utils.TimeStamp + 5;
        yield return new WaitForSeconds(5f);
        if (Stop) yield break;

        Dictionary<byte, int> round4Points = ranking.ToDictionary(x => x, x => (ranking.Count - ranking.IndexOf(x)) * 10);

        join = string.Join('\n', round4Points.Select(x => $"{x.Key.ColoredPlayerName()}:  +{x.Value}"));
        yield return NotifyEveryone("TMG.Notify.Round4PointGain", 7, join);
        if (Stop) yield break;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            int superPoints = SuperPoints[pc.PlayerId];

            for (int i = 0; i < superPoints; i++)
                Points[pc.PlayerId] += SuperPointsToNormalPointsMultiplier;
        }

        yield return NotifyEveryone("TMG.Notify.SuperPointConversion", 4, SuperPointsToNormalPointsMultiplier);
        if (Stop) yield break;

        yield return NotifyEveryone("TMG.Notify.TheWinnerIs", 2);

        int highestPoints = Points.Values.Max();
        HashSet<byte> winners = Points.Where(x => x.Value == highestPoints).Select(x => x.Key).ToHashSet();
        CustomWinnerHolder.WinnerIds = winners;
    }

    private static IEnumerator NotifyEveryone(string key, int time, params object[] args)
    {
        NameNotifyManager.Reset();
        Main.AllAlivePlayerControls.NotifyPlayers(string.Format(Translator.GetString(key), args), 100f);
        ProceedingInCountdownEndTS = Utils.TimeStamp + time;
        yield return new WaitForSeconds(time);
        NameNotifyManager.Reset();
    }

    private static IEnumerator PreventMovingOutOfGroupRooms()
    {
        while (Round == 1 && !GameStates.IsEnded && GameStates.IsInTask && !ExileController.Instance)
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                Group group = Groups[pc.PlayerId];
                SystemTypes groupRoom = GroupRooms[group];
                PlainShipRoom room = pc.GetPlainShipRoom();

                if (room == null || room.RoomId != groupRoom)
                    pc.TP(RandomSpawn.SpawnMap.GetSpawnMap().Positions[groupRoom]);
            }

            yield return new WaitForSeconds(2f);
        }
    }

    public static string GetEjectionMessage(NetworkedPlayerInfo exiled)
    {
        if (exiled == null) return string.Empty;

        EjectedPlayers.Add(exiled.PlayerId);

        if (WinningBriefcaseHolderId == exiled.PlayerId)
        {
            Round4PlacesFromFirst.Add(exiled.PlayerId);
            return string.Format(Translator.GetString("TMG.EjectionText.HadWinningBriefcase"), exiled.PlayerId.ColoredPlayerName(), Round4PlacesFromFirst.IndexOf(exiled.PlayerId) + 1);
        }

        Round4PlacesFromLast.Add(exiled.PlayerId);
        return string.Format(Translator.GetString("TMG.EjectionText.HadEmptyBriefcase"), exiled.PlayerId.ColoredPlayerName());
    }

    public static void OnChat(PlayerControl pc, string text)
    {
        // TODO: Item purchasing
        // TODO: Item using, including the stealing of briefcases
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
            case 2 when AuctionValue == 0 && AmReady.Add(player.PlayerId):
            {
                player.RpcChangeRoleBasis(CustomRoles.TMGPlayer);
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
                if (Pick[player.PlayerId] > Main.AllAlivePlayerControls.Length) Pick[player.PlayerId] = 1;
                break;
            }
        }

        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
    }

    private static string GetHexColor(this Group group)
    {
        return group switch
        {
            Group.Red => "#ff0000",
            Group.Yellow => "#ffff00",
            Group.Blue => "#00ffff",
            Group.Green => "#00ff00",
            Group.Tan => "#A88E8E",
            Group.Rose => "#FAB8EB",
            Group.Orange => "#ff8800",
            _ => "#ffffff"
        };
    }

    private static Color GetColor(this Group group)
    {
        return group switch
        {
            Group.Red => Color.red,
            Group.Yellow => Color.yellow,
            Group.Blue => Color.cyan,
            Group.Green => Color.green,
            Group.Tan => Palette.Brown,
            Group.Rose => Color.magenta,
            Group.Orange => Palette.Orange,
            _ => Color.white
        };
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

    public static bool CheckForGameEnd(out GameOverReason reason) { }

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
    public static bool On;

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