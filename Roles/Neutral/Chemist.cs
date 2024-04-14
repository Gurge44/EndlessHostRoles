using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private static OverrideTasksData Tasks;

        public static void SetupCustomOption()
        {
            const int id = 17670;
            SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.Chemist);
            KillCooldown = FloatOptionItem.Create(id + 2, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist]);
            HasImpostorVision = BooleanOptionItem.Create(id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist]);

            AirGainedPerSecond = IntegerOptionItem.Create(id + 5, "Chemist.AirGainedPerSecond", new(5, 100, 5), 10, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times);
            WaterGainedPerSecond = IntegerOptionItem.Create(id + 6, "Chemist.WaterGainedPerSecond", new(5, 100, 5), 10, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times);
            CoalGainedPerTask = IntegerOptionItem.Create(id + 7, "Chemist.CoalGainedPerTask", new(1, 10, 1), 2, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times);
            IronOreGainedPerTask = IntegerOptionItem.Create(id + 8, "Chemist.IronOreGainedPerTask", new(1, 50, 1), 8, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times);

            Tasks = OverrideTasksData.Create(id + 9, TabGroup.NeutralRoles, CustomRoles.Chemist);
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
            }
        };

        static bool IsBasicResource(Item item) => item is
            Item.Air or
            Item.Coal or
            Item.IronOre or
            Item.Water;

        static bool IsFinalProduct(Item item) => item is
            Item.Explosive or
            Item.Grenade or
            Item.SulfuricAcid or
            Item.MethylamineGas;

        private static Dictionary<PlainShipRoom, Factory> FactoryLocations = [];

        private long LastUpdate;
        private Dictionary<Item, int> ItemCounts;
        private Factory CurrentFactory;
        private string SelectedProcess;

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || !pc.IsAlive() || LastUpdate == Utils.TimeStamp) return;
            LastUpdate = Utils.TimeStamp;

            ItemCounts[Item.Air] += AirGainedPerSecond.GetInt();
            ItemCounts[Item.Water] += WaterGainedPerSecond.GetInt();

            CurrentFactory = FactoryLocations.GetValueOrDefault(pc.GetPlainShipRoom());

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }
}
