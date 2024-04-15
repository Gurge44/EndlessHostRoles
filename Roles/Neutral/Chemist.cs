using AmongUs.GameOptions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class Chemist : RoleBase
    {
        public static bool On;

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;

        private static OptionItem AirGainedPerSecond;
        private static OptionItem WaterGainedPerSecond;
        private static OptionItem CoalGainedPerTask;
        private static OptionItem IronOreGainedPerTask;

        private static Dictionary<Item, OptionItem> FinalProductUsageAmounts = [];

        private static OverrideTasksData Tasks;

        public static void SetupCustomOption()
        {
            int id = 17670;
            const TabGroup tab = TabGroup.NeutralRoles;

            SetupRoleOptions(id++, tab, CustomRoles.Chemist);

            KillCooldown = FloatOptionItem.Create(++id, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(++id, "CanVent", true, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist]);
            HasImpostorVision = BooleanOptionItem.Create(++id, "ImpostorVision", true, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist]);

            AirGainedPerSecond = IntegerOptionItem.Create(++id, "Chemist.AirGainedPerSecond", new(5, 100, 5), 10, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times);
            WaterGainedPerSecond = IntegerOptionItem.Create(++id, "Chemist.WaterGainedPerSecond", new(5, 100, 5), 10, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times);
            CoalGainedPerTask = IntegerOptionItem.Create(++id, "Chemist.CoalGainedPerTask", new(1, 10, 1), 2, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times);
            IronOreGainedPerTask = IntegerOptionItem.Create(++id, "Chemist.IronOreGainedPerTask", new(1, 50, 1), 8, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times);

            FinalProductUsageAmounts = EnumHelper.GetAllValues<Item>()
                .Where(x => GetItemType(x) == ItemType.FinalProduct)
                .ToDictionary(x => x, x => IntegerOptionItem.Create(++id, $"Chemist.Item.{x}.Usage", new(1, 100, 1), GetDefaultValue(x), tab)
                    .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                    .SetValueFormat(OptionFormat.Times));

            Tasks = OverrideTasksData.Create(++id, tab, CustomRoles.Chemist);
            return;

            static int GetDefaultValue(Item item) => item switch
            {
                Item.Explosive => 1,
                Item.Grenade => 1,
                Item.SulfuricAcid => 30,
                Item.MethylamineGas => 100,
                _ => 0
            };
        }

        public override void Init()
        {
            On = false;

            FactoryLocations = [];
            _ = new LateTask(() =>
            {
                FactoryLocations = ShipStatus.Instance.AllRooms
                    .Zip(EnumHelper.GetAllValues<Factory>())
                    .ToDictionary(x => x.First, x => x.Second);
            }, 10f, log: false);
        }

        public override void Add(byte playerId)
        {
            On = true;
            LastUpdate = Utils.TimeStamp + 8;
            ItemCounts = [];
            CurrentFactory = Factory.None;
            SelectedProcess = string.Empty;
            SortedAvailableProcesses = [];

            foreach (var item in EnumHelper.GetAllValues<Item>())
            {
                ItemCounts[item] = 0;
            }

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override bool IsEnable => On;
        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseSabotage(PlayerControl pc) => pc.IsAlive();

        enum Item
        {
            Air,
            AmmoniaGas,
            BaseMineralOil,
            CarbonDioxide,
            CarbonMonoxide,
            Coal,
            Explosive,
            Grenade,
            HydrogenGas,
            HydrogenSulfideGas,
            IronIngot,
            IronOre,
            IronPlate,
            MethanolGas,
            MethylamineGas,
            MoltenIron,
            Naphtha,
            NitrogenGas,
            OxygenGas,
            PurifiedWater,
            Steam,
            Sulfur,
            SulfurDioxideGas,
            SulfuricAcid,
            SynthesisGas,
            ThermalWater,
            Water
        }

        enum ItemType
        {
            BasicResource,
            IntermediateProduct,
            FinalProduct
        }

        enum Factory
        {
            None,
            ChemicalPlant, // up to 2 ingredients
            AdvancedChemicalPlant, // 3 or more ingredients
            SteamCracker,
            BlastFurnace,
            InductionFurnace,
            CastingMachine,
            Electrolyzer,
            CoolingTower,
            WaterTreatmentPlant,
            Liquifier,
            AssemblingMachine
        }

        private static readonly Dictionary<Factory, Dictionary<string, (List<(int Count, Item Item)> Ingredients, List<(int Count, Item Item)> Results)>> Processes = new()
        {
            [Factory.ChemicalPlant] =
            {
                ["Synthesis Sulfur"] = ([(60, Item.HydrogenSulfideGas), (40, Item.OxygenGas)], [(3, Item.Sulfur)]),
                ["Synthesis Of Naphtha"] = ([(150, Item.SynthesisGas), (50, Item.CarbonMonoxide)], [(100, Item.Naphtha)]),
                ["Reversed Water Gas Shift"] = ([(50, Item.HydrogenGas), (50, Item.CarbonDioxide)], [(50, Item.PurifiedWater), (50, Item.CarbonMonoxide)]),
                ["Synthesis Sulfur Dioxide"] = ([(1, Item.Sulfur), (60, Item.OxygenGas)], [(60, Item.SulfurDioxideGas)]),
                ["Synthesis Sulfuric Acid"] = ([(90, Item.SulfurDioxideGas), (40, Item.PurifiedWater)], [(60, Item.SulfuricAcid)]),
                ["Synthesis to Methanol"] = ([(100, Item.SynthesisGas), (40, Item.CarbonDioxide)], [(20, Item.PurifiedWater), (80, Item.MethanolGas)]),
                ["Synthesis Gas Separation"] = ([(100, Item.SynthesisGas)], [(40, Item.CarbonMonoxide), (60, Item.HydrogenGas)]),
                ["Synthesis Gas Reforming"] = ([(60, Item.CarbonMonoxide), (90, Item.HydrogenGas)], [(100, Item.SynthesisGas)]),
                ["Methylamine Gas"] = ([(50, Item.MethanolGas), (250, Item.AmmoniaGas)], [(200, Item.MethylamineGas), (50, Item.PurifiedWater)]),
                ["Synthesis Methanol"] = ([(100, Item.CarbonDioxide), (100, Item.HydrogenGas)], [(100, Item.MethanolGas)]),
                ["Air Separation"] = ([(100, Item.Air)], [(50, Item.OxygenGas), (50, Item.NitrogenGas)])
            },
            [Factory.AdvancedChemicalPlant] =
            {
                ["Cracking of Naphtha to Mineral Oil"] = ([(60, Item.Naphtha), (20, Item.ThermalWater), (20, Item.CarbonMonoxide)], [(100, Item.BaseMineralOil)]),
                ["Coal Cracking Fischer Tropsch Process"] = ([(5, Item.Coal), (50, Item.Steam), (50, Item.OxygenGas)], [(100, Item.SynthesisGas), (20, Item.CarbonDioxide), (30, Item.HydrogenSulfideGas)]),
                ["Explosives"] = ([(1, Item.Sulfur), (1, Item.Coal), (10, Item.Water)], [(2, Item.Explosive)])
            },
            [Factory.SteamCracker] =
            {
                ["Steam Cracking Mineral Oil to Synthesis Gas"] = ([(100, Item.BaseMineralOil), (100, Item.Steam)], [(200, Item.SynthesisGas)])
            },
            [Factory.BlastFurnace] =
            {
                ["Iron Ore Smelting"] = ([(24, Item.IronOre)], [(24, Item.IronIngot)])
            },
            [Factory.InductionFurnace] =
            {
                ["Iron Melting"] = ([(12, Item.IronIngot)], [(120, Item.MoltenIron)])
            },
            [Factory.CastingMachine] =
            {
                ["Iron Plate Casting"] = ([(40, Item.MoltenIron)], [(4, Item.IronPlate)])
            },
            [Factory.Electrolyzer] =
            {
                ["Purified Water Electrolysis"] = ([(100, Item.PurifiedWater)], [(40, Item.OxygenGas), (60, Item.HydrogenGas)])
            },
            [Factory.CoolingTower] =
            {
                ["Steam Cooling"] = ([(100, Item.Steam)], [(100, Item.PurifiedWater)])
            },
            [Factory.WaterTreatmentPlant] =
            {
                ["Water Purification"] = ([(150, Item.Water)], [(100, Item.PurifiedWater)]),
                ["Water Boiling"] = ([(100, Item.Water), (1, Item.Coal)], [(60, Item.Steam)])
            },
            [Factory.Liquifier] =
            {
                ["Coal Liquefaction"] = ([(1, Item.Coal)], [(50, Item.CarbonDioxide)])
            },
            [Factory.AssemblingMachine] =
            {
                ["Grenade"] = ([(5, Item.IronPlate), (10, Item.Coal)], [(1, Item.Grenade)])
            },
            [Factory.None] = []
        };

        static ItemType GetItemType(Item item) => item switch
        {
            Item.Air or Item.Coal or Item.IronOre or Item.Water => ItemType.BasicResource,
            Item.Explosive or Item.Grenade or Item.SulfuricAcid or Item.MethylamineGas => ItemType.FinalProduct,
            _ => ItemType.IntermediateProduct
        };

        static string GetChemicalForm(Item item) => item switch
        {
            Item.AmmoniaGas => "NH<sub>3</sub>",
            Item.CarbonDioxide => "CO<sub>2</sub>",
            Item.CarbonMonoxide => "CO",
            Item.HydrogenGas => "H<sub>2</sub>",
            Item.HydrogenSulfideGas => "H<sub>2</sub>S",
            Item.MethanolGas => "CH<sub>3</sub>OH",
            Item.MethylamineGas => "CH<sub>3</sub>NH<sub>2</sub>",
            Item.NitrogenGas => "N<sub>2</sub>",
            Item.OxygenGas => "O<sub>2</sub>",
            Item.PurifiedWater => "H<sub>2</sub>O",
            Item.Sulfur => "S",
            Item.SulfurDioxideGas => "SO<sub>2</sub>",
            Item.SulfuricAcid => "H<sub>2</sub>SO<sub>4</sub>",
            Item.SynthesisGas => "H<sub>2</sub>+CO",

            _ => Translator.GetString($"Chemist.Item.{item}") switch
            {
                { } str when str.Count(char.IsUpper) == 1 => str,
                { } str => string.Join(string.Empty, str.Where(char.IsUpper))
            }
        };

        static Color GetItemColor(Item item) => item switch
        {
            Item.Air => Palette.White_75Alpha,
            Item.AmmoniaGas => Color.blue,
            Item.BaseMineralOil => Color.green,
            Item.CarbonDioxide => Palette.Brown,
            Item.CarbonMonoxide => Palette.Purple,
            Item.Coal => Color.black,
            Item.Explosive => Color.red,
            Item.Grenade => Color.red,
            Item.HydrogenGas => Color.white,
            Item.HydrogenSulfideGas => Color.yellow,
            Item.IronIngot => Color.gray,
            Item.IronOre => Color.gray,
            Item.IronPlate => Color.gray,
            Item.MethanolGas => Palette.Brown,
            Item.MethylamineGas => Color.blue,
            Item.MoltenIron => Color.gray,
            Item.Naphtha => Palette.ImpostorRed,
            Item.NitrogenGas => Color.blue,
            Item.OxygenGas => Color.red,
            Item.PurifiedWater => Color.cyan,
            Item.Steam => Palette.HalfWhite,
            Item.Sulfur => Color.yellow,
            Item.SulfurDioxideGas => Color.yellow,
            Item.SulfuricAcid => Color.yellow,
            Item.SynthesisGas => Color.magenta,
            Item.ThermalWater => Palette.Orange,
            Item.Water => Palette.LightBlue,

            _ => Color.white
        };

        private static Dictionary<PlainShipRoom, Factory> FactoryLocations = [];

        private long LastUpdate;
        private Dictionary<Item, int> ItemCounts;
        private Factory CurrentFactory;
        private string SelectedProcess;
        private List<string> SortedAvailableProcesses;

        private Dictionary<byte, HashSet<byte>> AcidPlayers;

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            return base.OnCheckMurder(killer, target);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || !pc.IsAlive() || LastUpdate >= Utils.TimeStamp) return;
            LastUpdate = Utils.TimeStamp;

            ItemCounts[Item.Air] += AirGainedPerSecond.GetInt();
            ItemCounts[Item.Water] += WaterGainedPerSecond.GetInt();

            var beforeFactory = CurrentFactory;
            var room = pc.GetPlainShipRoom();

            CurrentFactory = FactoryLocations.GetValueOrDefault(room);

            if (CurrentFactory != beforeFactory)
            {
                SortedAvailableProcesses = Processes[CurrentFactory]
                    .OrderByDescending(x => x.Value.Ingredients.TrueForAll(y => ItemCounts[y.Item] >= y.Count))
                    .Select(x => x.Key)
                    .ToList();

                SelectedProcess = SortedAvailableProcesses.FirstOrDefault() ?? string.Empty;
            }

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            if (SortedAvailableProcesses.Count == 0) return false;

            var index = SortedAvailableProcesses.IndexOf(SelectedProcess);
            SelectedProcess = SortedAvailableProcesses[(index + 1) % SortedAvailableProcesses.Count];

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            if (SelectedProcess == string.Empty) return;

            (List<(int Count, Item Item)> Ingredients, List<(int Count, Item Item)> Results) = Processes[CurrentFactory][SelectedProcess];

            if (!Ingredients.TrueForAll(x => ItemCounts[x.Item] >= x.Count)) return;

            Ingredients.ForEach(x => ItemCounts[x.Item] -= x.Count);
            Results.ForEach(x => ItemCounts[x.Item] += x.Count);

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            if (!pc.IsAlive()) return;

            ItemCounts[Item.Coal] += CoalGainedPerTask.GetInt();
            ItemCounts[Item.IronOre] += IronOreGainedPerTask.GetInt();

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public static string GetSuffix(PlayerControl seer, PlayerControl target)
        {
            if (Main.PlayerStates[seer.PlayerId].Role is not Chemist cm) return string.Empty;

            if (seer.PlayerId == target.PlayerId)
            {
                var sb = new StringBuilder().Append("<size=80%>");

                var grouped = cm.ItemCounts
                    .Where(x => x.Value > 0)
                    .GroupBy(x => GetItemType(x.Key))
                    .OrderBy(x => (int)x.Key)
                    .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Key, y => y.Value));

                foreach ((ItemType type, Dictionary<Item, int> items) in grouped)
                {
                    sb.Append($"{type.ToString()[0]}: ");

                    foreach ((Item item, int count) in items)
                    {
                        sb.Append(Utils.ColorString(GetItemColor(item), $"{count} {GetChemicalForm(item)}"));
                    }

                    sb.AppendLine();
                }

                if (cm.SelectedProcess == string.Empty) return sb.ToString().TrimEnd();

                (List<(int Count, Item Item)> Ingredients, List<(int Count, Item Item)> Results) = Processes[cm.CurrentFactory][cm.SelectedProcess];

                sb.Append(string.Join(", ", Ingredients.Select(x => $"{x.Count} {GetChemicalForm(x.Item)}")));
                sb.Append('\u2192');
                sb.Append(string.Join(", ", Results.Select(x => $"{x.Count} {GetChemicalForm(x.Item)}")));

                return sb.ToString();
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
