﻿using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;

// ReSharper disable PossibleMultipleEnumeration

namespace EHR.Neutral
{
    internal class Bargainer : RoleBase
    {
        public enum Item
        {
            None,
            EnergyDrink,
            LensOfTruth,
            BandAid
        }

        public static bool On;

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        private static OptionItem StartingMoney;
        private static readonly Dictionary<MoneyGainingAction, (OptionItem Enabled, OptionItem Amount)> MoneySettings = [];
        private static readonly Dictionary<Item, (OptionItem Enabled, OptionItem Cost)> ItemSettings = [];
        private static OptionItem ShieldDuration;
        private static OptionItem ShieldTime;
        private static OptionItem ReducedKillCooldown;
        private static OptionItem AlignmentVisible;
        private static OptionItem AlignmentVisibleDuration;

        private static Dictionary<MoneyGainingAction, int> Gains = [];
        private static Dictionary<Item, int> Costs = [];

        private static readonly Dictionary<Item, string> Icons = new()
        {
            [Item.EnergyDrink] = Utils.ColorString(Color.magenta, "\u2668"),
            [Item.LensOfTruth] = "\u2600",
            [Item.BandAid] = Utils.ColorString(Color.green, "♥")
        };

        public List<(Item Item, long ActivateTimeStamp, int Duration, byte Target)> ActiveItems = [];

        private byte BargainerId;
        private bool InShop;
        private int Money;
        private IEnumerable<Item> OrderedItems;
        private Item SelectedItem;

        private static int AlignmentVisibleValue => (AlignmentVisibleOptions)AlignmentVisible.GetValue() switch
        {
            AlignmentVisibleOptions.Forever => int.MaxValue,
            AlignmentVisibleOptions.UntilNextMeeting => int.MaxValue,
            AlignmentVisibleOptions.UntilNextReveal => int.MaxValue,
            AlignmentVisibleOptions.ForSpecifiedTime => AlignmentVisibleDuration.GetInt(),

            _ => 0
        };

        private static int ShieldDurationValue => (ShieldDurationOptions)ShieldDuration.GetValue() switch
        {
            ShieldDurationOptions.UntilNextMeeting => int.MaxValue,
            ShieldDurationOptions.ForSpecifiedTime => ShieldTime.GetInt(),

            _ => 0
        };

        private static IEnumerable<Vector2> ShopLocations
        {
            get
            {
                var mapName = Main.CurrentMap.ToString();
                var devices = DisableDevice.DevicePos.SkipWhile(x => !x.Key.StartsWith(mapName)).TakeWhile(x => x.Key.StartsWith(mapName));
                return devices.Select(x => x.Value);
            }
        }

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            int id = 14870;
            const TabGroup tab = TabGroup.NeutralRoles;

            SetupRoleOptions(id++, tab, CustomRoles.Bargainer);

            KillCooldown = new FloatOptionItem(++id, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(++id, "CanVent", true, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);
            HasImpostorVision = new BooleanOptionItem(++id, "ImpostorVision", true, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);
            StartingMoney = new IntegerOptionItem(++id, "Bargainer.StartingMoney", new(0, 100, 5), 0, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);

            foreach (var action in Enum.GetValues<MoneyGainingAction>())
            {
                var boolOpt = new BooleanOptionItem(++id, $"Bargainer.{action}.Enabled", true, tab)
                    .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);
                var intOpt = new IntegerOptionItem(++id, $"Bargainer.{action}.Amount", new(0, 100, 5), GetDefaultValue(), tab)
                    .SetParent(boolOpt);

                MoneySettings[action] = (boolOpt, intOpt);
                continue;

                int GetDefaultValue() => action switch
                {
                    MoneyGainingAction.Kill => 30,
                    MoneyGainingAction.Sabotage => 10,
                    MoneyGainingAction.SurviveMeeting => 20,

                    _ => 0
                };
            }

            foreach (var item in Enum.GetValues<Item>())
            {
                if (item == Item.None) continue;

                var boolOpt = new BooleanOptionItem(++id, $"Bargainer.{item}.Enabled", true, tab)
                    .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);
                var intOpt = new IntegerOptionItem(++id, $"Bargainer.{item}.Cost", new(0, 200, 5), GetDefaultValue(), tab)
                    .SetParent(boolOpt);
                SetupExtraSettings();

                ItemSettings[item] = (boolOpt, intOpt);
                continue;

                int GetDefaultValue() => item switch
                {
                    Item.EnergyDrink => 40,
                    Item.LensOfTruth => 60,
                    Item.BandAid => 20,

                    _ => 0
                };

                void SetupExtraSettings()
                {
                    switch (item)
                    {
                        case Item.BandAid:
                            ShieldDuration = new StringOptionItem(++id, $"Bargainer.{item}.DurationSwitch", Enum.GetNames<ShieldDurationOptions>(), 0, tab)
                                .SetParent(boolOpt);
                            ShieldTime = new IntegerOptionItem(++id, $"Bargainer.{item}.Duration", new(0, 60, 1), 20, tab)
                                .SetParent(ShieldDuration)
                                .SetValueFormat(OptionFormat.Seconds);
                            break;
                        case Item.EnergyDrink:
                            ReducedKillCooldown = new FloatOptionItem(++id, $"Bargainer.{item}.ReducedKCD", new(0f, 180f, 0.5f), 17.5f, tab)
                                .SetParent(boolOpt)
                                .SetValueFormat(OptionFormat.Seconds);
                            break;
                        case Item.LensOfTruth:
                            AlignmentVisible = new StringOptionItem(++id, $"Bargainer.{item}.DurationSwitch", Enum.GetNames<AlignmentVisibleOptions>(), (int)AlignmentVisibleOptions.UntilNextReveal, tab)
                                .SetParent(boolOpt);
                            AlignmentVisibleDuration = new IntegerOptionItem(++id, $"Bargainer.{item}.Duration", new(1, 30, 1), 10, tab)
                                .SetParent(AlignmentVisible)
                                .SetValueFormat(OptionFormat.Seconds);
                            break;
                    }
                }
            }
        }

        public override void Init()
        {
            On = false;

            Gains = [];
            foreach (var kvp in MoneySettings)
            {
                if (kvp.Value.Enabled.GetBool())
                {
                    Gains[kvp.Key] = kvp.Value.Amount.GetInt();
                }
            }

            Costs = [];
            foreach (var kvp in ItemSettings)
            {
                if (kvp.Value.Enabled.GetBool())
                {
                    Costs[kvp.Key] = kvp.Value.Cost.GetInt();
                }
            }
        }

        public override void Add(byte playerId)
        {
            On = true;

            BargainerId = playerId;
            Money = StartingMoney.GetInt();
            Update();

            InShop = false;
            SelectedItem = Item.None;
            ActiveItems = [];

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = ActiveItems.Any(x => x.Item == Item.EnergyDrink) ? ReducedKillCooldown.GetFloat() : KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseSabotage(PlayerControl pc) => pc.IsAlive();

        void Update() => BargainerId.SetAbilityUseLimit(Money);

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            if (Gains.TryGetValue(MoneyGainingAction.Kill, out int gain))
            {
                Money += gain;
                Update();
            }
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            if (InShop)
            {
                var list = OrderedItems.ToList();
                SelectedItem = list[(list.IndexOf(SelectedItem) + 1) % list.Count];
                Utils.SendRPC(CustomRPC.SyncBargainer, pc.PlayerId, 2, (int)SelectedItem);
                return false;
            }

            if (Gains.TryGetValue(MoneyGainingAction.Sabotage, out var gain))
            {
                Money += gain;
                Update();
            }

            return true;
        }

        public override void AfterMeetingTasks()
        {
            if (Gains.TryGetValue(MoneyGainingAction.SurviveMeeting, out var gain))
            {
                Money += gain;
                Update();
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || !pc.IsAlive())
            {
                if (InShop || ActiveItems.Count > 0 || Money != 0)
                {
                    Utils.SendRPC(CustomRPC.SyncBargainer, pc.PlayerId, 1, false);
                    Utils.SendRPC(CustomRPC.SyncBargainer, pc.PlayerId, 5);

                    InShop = false;
                    ActiveItems.Clear();
                    Money = 0;
                    Update();
                }

                return;
            }

            var wasInShop = InShop;
            InShop = ShopLocations.Any(x => Vector2.Distance(pc.Pos(), x) < DisableDevice.UsableDistance);
            Utils.SendRPC(CustomRPC.SyncBargainer, pc.PlayerId, 1, InShop);

            switch (wasInShop)
            {
                case true when !InShop && ActiveItems.All(x => x.Item != SelectedItem) && Costs.TryGetValue(SelectedItem, out var cost) && cost <= Money:
                {
                    (int duration, byte target) = GetData(SelectedItem);
                    ActiveItems.Add((SelectedItem, Utils.TimeStamp, duration, target));
                    Utils.SendRPC(CustomRPC.SyncBargainer, pc.PlayerId, 3, (int)SelectedItem, Utils.TimeStamp, duration, target);
                    Money -= cost;
                    Update();

                    pc.Notify(string.Format(Translator.GetString("Bargainer.Notify.PurchasedItem"), Translator.GetString($"Bargainer.{SelectedItem}")));

                    switch (SelectedItem)
                    {
                        case Item.EnergyDrink:
                            pc.ResetKillCooldown();
                            pc.SyncSettings();
                            if (Main.KillTimers[pc.PlayerId] > ReducedKillCooldown.GetFloat()) pc.SetKillCooldown(ReducedKillCooldown.GetFloat());
                            break;
                        case Item.LensOfTruth:
                            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: Utils.GetPlayerById(target));
                            break;
                    }

                    break;

                    static (int Duration, byte Target) GetData(Item item) => item switch
                    {
                        Item.EnergyDrink => (int.MaxValue, byte.MaxValue),
                        Item.LensOfTruth => (AlignmentVisibleValue, Main.AllAlivePlayerControls.RandomElement().PlayerId),
                        Item.BandAid => (ShieldDurationValue, byte.MaxValue),

                        _ => (0, byte.MaxValue)
                    };
                }
                case false when InShop:
                {
                    Func<KeyValuePair<Item, int>, bool> canBuy = x => x.Value <= Money;
                    var orderedItems = Costs.OrderByDescending(canBuy);
                    var unAffordableItems = orderedItems.SkipWhile(canBuy).Select(x => x.Key).Prepend(Item.None);
                    var affordableItems = orderedItems.TakeWhile(canBuy).Select(x => x.Key);
                    OrderedItems = affordableItems.Concat(unAffordableItems);
                    SelectedItem = OrderedItems.FirstOrDefault();
                    Utils.SendRPC(CustomRPC.SyncBargainer, pc.PlayerId, 2, (int)SelectedItem);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    break;
                }
            }

            var rc = ActiveItems.RemoveAll(x => x.Item == Item.None);
            if (rc > 0) Utils.SendRPC(CustomRPC.SyncBargainer, pc.PlayerId, 6);

            var array = ActiveItems.Where(x => x.Duration != int.MaxValue && x.ActivateTimeStamp + x.Duration < Utils.TimeStamp).ToArray();
            for (int i = 0; i < array.Length; i++)
            {
                var item = array[i];
                ActiveItems.Remove(item);
                Utils.SendRPC(CustomRPC.SyncBargainer, pc.PlayerId, 4, i);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: Utils.GetPlayerById(item.Target) ?? pc);
            }
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return ActiveItems.All(x => x.Item != Item.BandAid);
        }

        public override void OnReportDeadBody()
        {
            List<int> indexesToRemove = [];

            for (int i = 0; i < ActiveItems.Count; i++)
            {
                switch (ActiveItems[i].Item)
                {
                    case Item.LensOfTruth:
                        if ((AlignmentVisibleOptions)AlignmentVisible.GetValue() is AlignmentVisibleOptions.ForSpecifiedTime or AlignmentVisibleOptions.UntilNextMeeting)
                            indexesToRemove.Add(i);
                        break;
                    default:
                        indexesToRemove.Add(i);
                        break;
                }
            }

            foreach (var index in indexesToRemove)
            {
                ActiveItems.RemoveAt(index);
                Utils.SendRPC(CustomRPC.SyncBargainer, BargainerId, 4, index);
            }

            Utils.GetPlayerById(BargainerId).ResetKillCooldown();
        }

        public override bool KnowRole(PlayerControl seer, PlayerControl target)
        {
            return Main.PlayerStates[seer.PlayerId].Role is Bargainer bg && bg.ActiveItems.Any(x => x.Target == target.PlayerId);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            if (Main.PlayerStates[playerId].Role is not Bargainer bg) return;

            switch (reader.ReadPackedInt32())
            {
                case 1:
                    bg.InShop = reader.ReadBoolean();
                    break;
                case 2:
                    bg.SelectedItem = (Item)reader.ReadPackedInt32();
                    break;
                case 3:
                    bg.ActiveItems.Add(((Item)reader.ReadPackedInt32(), long.Parse(reader.ReadString()), reader.ReadPackedInt32(), reader.ReadByte()));
                    break;
                case 4:
                    bg.ActiveItems.RemoveAt(reader.ReadPackedInt32());
                    break;
                case 5:
                    bg.ActiveItems.Clear();
                    break;
                case 6:
                    bg.ActiveItems.RemoveAll(x => x.Item == Item.None);
                    break;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
        {
            if (seer.PlayerId != target.PlayerId) return string.Empty;
            if (Main.PlayerStates[seer.PlayerId].Role is not Bargainer bg) return string.Empty;

            string result = string.Empty;

            if (bg.InShop)
            {
                result += string.Format(
                    Translator.GetString("Bargainer.Suffix.InShop"),
                    Translator.GetString($"Bargainer.{bg.SelectedItem}"),
                    Utils.ColorString(bg.Money >= Costs[bg.SelectedItem] ? Color.white : Color.red, $"{Costs[bg.SelectedItem]}"));
                result += "\n";
            }

            if (seer.IsModClient()) result += "<size=150%>";
            result += string.Join(' ', bg.ActiveItems.Select(x =>
            {
                var timeLeft = x.Duration - (Utils.TimeStamp - x.ActivateTimeStamp) + 1;
                var icon = Icons[x.Item];
                if (x.Item == Item.LensOfTruth && x.Target != byte.MaxValue) icon = Utils.ColorString(Main.PlayerColors[x.Target], icon);
                return seer.IsModClient() && timeLeft < 10 ? $"{icon} ({timeLeft}s)" : icon;
            }));
            if (seer.IsModClient()) result += "</size>";

            return result;
        }

        enum AlignmentVisibleOptions
        {
            Forever,
            UntilNextMeeting,
            UntilNextReveal,
            ForSpecifiedTime
        }

        enum ShieldDurationOptions
        {
            UntilNextMeeting,
            ForSpecifiedTime
        }

        enum MoneyGainingAction
        {
            Kill,
            Sabotage,
            SurviveMeeting
        }
    }
}