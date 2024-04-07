using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class Bargainer : RoleBase
    {
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

        enum AlignmentVisibleOptions
        {
            Forever,
            UntilNextMeeting,
            UntilNextReveal,
            ForSpecifiedTime
        }

        private static int AlignmentVisibleValue => (AlignmentVisibleOptions)AlignmentVisible.GetValue() switch
        {
            AlignmentVisibleOptions.Forever => int.MaxValue,
            AlignmentVisibleOptions.UntilNextMeeting => int.MaxValue,
            AlignmentVisibleOptions.UntilNextReveal => int.MaxValue,
            AlignmentVisibleOptions.ForSpecifiedTime => AlignmentVisibleDuration.GetInt(),

            _ => 0
        };

        enum ShieldDurationOptions
        {
            UntilNextMeeting,
            ForSpecifiedTime
        }

        private static int ShieldDurationValue => (ShieldDurationOptions)ShieldDuration.GetValue() switch
        {
            ShieldDurationOptions.UntilNextMeeting => int.MaxValue,
            ShieldDurationOptions.ForSpecifiedTime => ShieldTime.GetInt(),

            _ => 0
        };

        private static Dictionary<MoneyGainingAction, int> Gains = [];
        private static Dictionary<Item, int> Costs = [];

        private static readonly Dictionary<Item, string> Icons = new()
        {
            [Item.EnergyDrink] = Utils.ColorString(Color.magenta, "\u2668"),
            [Item.LensOfTruth] = "\u2600",
            [Item.BandAid] = Utils.ColorString(Color.green, "♥")
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

        enum MoneyGainingAction
        {
            Kill,
            Sabotage,
            SurviveMeeting
        }

        public enum Item
        {
            None,
            EnergyDrink,
            LensOfTruth,
            BandAid
        }

        private byte BargainerId;
        private int Money;
        private bool InShop;
        private Item SelectedItem;
        private IEnumerable<Item> OrderedItems;

        public List<(Item Item, long ActivateTimeStamp, int Duration, byte Target)> ActiveItems = [];

        public static void SetupCustomOption()
        {
            int id = 14870;
            const TabGroup tab = TabGroup.NeutralRoles;

            SetupRoleOptions(id++, tab, CustomRoles.Bargainer);

            KillCooldown = FloatOptionItem.Create(++id, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(++id, "CanVent", true, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);
            HasImpostorVision = BooleanOptionItem.Create(++id, "ImpostorVision", true, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);
            StartingMoney = IntegerOptionItem.Create(++id, "Bargainer.StartingMoney", new(0, 100, 5), 0, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);

            foreach (var action in EnumHelper.GetAllValues<MoneyGainingAction>())
            {
                var boolOpt = BooleanOptionItem.Create(++id, $"Bargainer.{action}.Enabled", true, tab)
                    .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);
                var intOpt = IntegerOptionItem.Create(++id, $"Bargainer.{action}.Amount", new(0, 100, 5), GetDefaultValue(), tab)
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

            foreach (var item in EnumHelper.GetAllValues<Item>())
            {
                if (item == Item.None) continue;

                var boolOpt = BooleanOptionItem.Create(++id, $"Bargainer.{item}.Enabled", true, tab)
                    .SetParent(CustomRoleSpawnChances[CustomRoles.Bargainer]);
                var intOpt = IntegerOptionItem.Create(++id, $"Bargainer.{item}.Cost", new(0, 200, 5), GetDefaultValue(), tab)
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
                            ShieldDuration = StringOptionItem.Create(++id, $"Bargainer.{item}.DurationSwitch", EnumHelper.GetAllNames<ShieldDurationOptions>(), 0, tab)
                                .SetParent(boolOpt);
                            ShieldTime = IntegerOptionItem.Create(++id, $"Bargainer.{item}.Duration", new(0, 60, 1), 20, tab)
                                .SetParent(ShieldDuration)
                                .SetValueFormat(OptionFormat.Seconds);
                            break;
                        case Item.EnergyDrink:
                            ReducedKillCooldown = FloatOptionItem.Create(++id, $"Bargainer.{item}.ReducedKCD", new(0f, 180f, 0.5f), 17.5f, tab)
                                .SetParent(boolOpt)
                                .SetValueFormat(OptionFormat.Seconds);
                            break;
                        case Item.LensOfTruth:
                            AlignmentVisible = StringOptionItem.Create(++id, $"Bargainer.{item}.DurationSwitch", EnumHelper.GetAllNames<AlignmentVisibleOptions>(), (int)AlignmentVisibleOptions.UntilNextReveal, tab)
                                .SetParent(boolOpt);
                            AlignmentVisibleDuration = IntegerOptionItem.Create(++id, $"Bargainer.{item}.Duration", new(1, 30, 1), 10, tab)
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

        public override bool IsEnable => On;
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
                InShop = false;
                ActiveItems.Clear();
                Money = 0;
                return;
            }

            var wasInShop = InShop;
            InShop = ShopLocations.Any(x => Vector2.Distance(pc.Pos(), x) < DisableDevice.UsableDistance());

            switch (wasInShop)
            {
                case true when !InShop && ActiveItems.All(x => x.Item != SelectedItem) && Costs.TryGetValue(SelectedItem, out var cost) && cost <= Money:
                {
                    (int duration, byte target) = GetData(SelectedItem);
                    ActiveItems.Add((SelectedItem, Utils.TimeStamp, duration, target));
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
                        Item.LensOfTruth => (AlignmentVisibleValue, Main.AllAlivePlayerControls[IRandom.Instance.Next(Main.AllAlivePlayerControls.Length)].PlayerId),
                        Item.BandAid => (ShieldDurationValue, byte.MaxValue),

                        _ => (0, byte.MaxValue)
                    };
                }
                case false when InShop:
                {
                    OrderedItems = Costs.OrderByDescending(x => x.Value <= Money).Select(x => x.Key).Append(Item.None);
                    SelectedItem = OrderedItems.FirstOrDefault();
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    break;
                }
            }

            ActiveItems.RemoveAll(x => x.Item == Item.None);

            foreach (var item in ActiveItems.Where(x => x.Duration != int.MaxValue && x.ActivateTimeStamp + x.Duration < Utils.TimeStamp).ToArray())
            {
                ActiveItems.Remove(item);
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

            foreach (var index in indexesToRemove) ActiveItems.RemoveAt(index);
        }

        public static bool KnowRole(PlayerControl seer, PlayerControl target)
        {
            return Main.PlayerStates[seer.PlayerId].Role is Bargainer bg && bg.ActiveItems.Any(x => x.Target == target.PlayerId);
        }

        public static string GetSuffix(PlayerControl seer)
        {
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
    }
}