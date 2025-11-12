using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral;

internal class Chemist : RoleBase
{
    public static bool On;
    public static List<Chemist> Instances = [];

    private static OptionItem KillCooldown;
    private static OptionItem HasImpostorVision;

    private static OptionItem AirGainedPerSecond;
    private static OptionItem WaterGainedPerSecond;
    private static OptionItem CoalGainedPerVent;
    private static OptionItem IronOreGainedPerVent;

    private static Dictionary<Item, OptionItem> FinalProductUsageAmounts = [];

    private static OptionItem AcidPlayersDie;
    private static OptionItem AcidPlayersDieAfterTime;
    private static OptionItem BlindDuration;
    private static OptionItem GrenadeExplodeDelay;
    private static OptionItem GrenadeExplodeRadius;

    private static readonly Dictionary<MapNames, SystemTypes> HottestRoom = new()
    {
        [MapNames.Skeld] = SystemTypes.Reactor,
        [MapNames.MiraHQ] = SystemTypes.Reactor,
        [MapNames.Polus] = SystemTypes.Dropship,
        [MapNames.Dleks] = SystemTypes.Reactor,
        [MapNames.Airship] = SystemTypes.GapRoom,
        [MapNames.Fungle] = SystemTypes.Reactor,
        [(MapNames)6] = (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast
    };

    private static readonly Dictionary<Factory, Dictionary<string, (List<(int Count, Item Item)> Ingredients, List<(int Count, Item Item)> Results)>> Processes = new()
    {
        [Factory.None] = [],
        [Factory.ChemicalPlant] = new()
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
        [Factory.AdvancedChemicalPlant] = new()
        {
            ["Cracking of Naphtha to Mineral Oil"] = ([(60, Item.Naphtha), (20, Item.ThermalWater), (20, Item.CarbonMonoxide)], [(100, Item.BaseMineralOil)]),
            ["Coal Cracking Fischer Tropsch Process"] = ([(5, Item.Coal), (50, Item.Steam), (50, Item.OxygenGas)], [(100, Item.SynthesisGas), (20, Item.CarbonDioxide), (30, Item.HydrogenSulfideGas)]),
            ["Explosives"] = ([(1, Item.Sulfur), (1, Item.Coal), (10, Item.Water)], [(3, Item.Explosive)])
        },
        [Factory.SteamCracker] = new()
        {
            ["Steam Cracking Mineral Oil to Synthesis Gas"] = ([(100, Item.BaseMineralOil), (100, Item.Steam)], [(200, Item.SynthesisGas)])
        },
        [Factory.BlastFurnace] = new()
        {
            ["Iron Ore Smelting"] = ([(24, Item.IronOre)], [(24, Item.IronIngot)])
        },
        [Factory.InductionFurnace] = new()
        {
            ["Iron Melting"] = ([(12, Item.IronIngot)], [(120, Item.MoltenIron)])
        },
        [Factory.CastingMachine] = new()
        {
            ["Iron Plate Casting"] = ([(40, Item.MoltenIron)], [(4, Item.IronPlate)])
        },
        [Factory.Electrolyzer] = new()
        {
            ["Purified Water Electrolysis"] = ([(100, Item.PurifiedWater)], [(40, Item.OxygenGas), (60, Item.HydrogenGas)])
        },
        [Factory.CoolingTower] = new()
        {
            ["Steam Cooling"] = ([(100, Item.Steam)], [(100, Item.PurifiedWater)])
        },
        [Factory.WaterTreatmentPlant] = new()
        {
            ["Water Purification"] = ([(150, Item.Water)], [(100, Item.PurifiedWater)]),
            ["Water Boiling"] = ([(100, Item.Water), (1, Item.Coal)], [(60, Item.Steam)])
        },
        [Factory.Liquifier] = new()
        {
            ["Coal Liquefaction"] = ([(1, Item.Coal)], [(50, Item.CarbonDioxide)])
        },
        [Factory.AssemblingMachine] = new()
        {
            ["Grenade"] = ([(5, Item.IronPlate), (10, Item.Coal)], [(2, Item.Grenade)])
        }
    };

    private static Dictionary<SystemTypes, Factory> FactoryLocations = [];

    private Dictionary<byte, (HashSet<byte> OtherAcidPlayers, long TimeStamp)> AcidPlayers;
    private HashSet<byte> BombedBodies;
    public PlayerControl ChemistPC;
    private Factory CurrentFactory;
    private Dictionary<byte, long> Grenades;
    public bool IsBlinding;
    private Dictionary<Item, int> ItemCounts;
    private long LastUpdate;
    private string SelectedProcess;
    private List<string> SortedAvailableProcesses;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 17670;
        const TabGroup tab = TabGroup.NeutralRoles;

        SetupRoleOptions(id++, tab, CustomRoles.Chemist);

        KillCooldown = new FloatOptionItem(++id, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
            .SetValueFormat(OptionFormat.Seconds);

        HasImpostorVision = new BooleanOptionItem(++id, "ImpostorVision", true, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist]);

        AirGainedPerSecond = new IntegerOptionItem(++id, "Chemist.AirGainedPerSecond", new(5, 100, 5), 10, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
            .SetValueFormat(OptionFormat.Times);

        WaterGainedPerSecond = new IntegerOptionItem(++id, "Chemist.WaterGainedPerSecond", new(5, 100, 5), 10, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
            .SetValueFormat(OptionFormat.Times);

        CoalGainedPerVent = new IntegerOptionItem(++id, "Chemist.CoalGainedPerVent", new(1, 10, 1), 1, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
            .SetValueFormat(OptionFormat.Times);

        IronOreGainedPerVent = new IntegerOptionItem(++id, "Chemist.IronOreGainedPerVent", new(1, 50, 1), 4, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
            .SetValueFormat(OptionFormat.Times);

        FinalProductUsageAmounts = Enum.GetValues<Item>()
            .Where(x => GetItemType(x) == ItemType.FinalProduct)
            .ToDictionary(x => x, x => new IntegerOptionItem(++id, $"Chemist.Item.{x}.Usage", new(1, 100, 1), GetDefaultValue(x), tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
                .SetValueFormat(OptionFormat.Times));

        AcidPlayersDie = new StringOptionItem(++id, "Chemist.AcidPlayersDie", Enum.GetNames<AcidPlayersDieOptions>(), 1, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist]);

        AcidPlayersDieAfterTime = new IntegerOptionItem(++id, "Chemist.AcidPlayersDieAfterTime", new(1, 60, 1), 30, tab)
            .SetParent(AcidPlayersDie)
            .SetValueFormat(OptionFormat.Seconds);

        BlindDuration = new IntegerOptionItem(++id, "Chemist.BlindDuration", new(1, 60, 1), 60, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
            .SetValueFormat(OptionFormat.Seconds);

        GrenadeExplodeDelay = new IntegerOptionItem(++id, "Chemist.GrenadeExplodeDelay", new(1, 60, 1), 5, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
            .SetValueFormat(OptionFormat.Seconds);

        GrenadeExplodeRadius = new FloatOptionItem(++id, "Chemist.GrenadeExplodeRadius", new(0.25f, 10f, 0.25f), 8f, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chemist])
            .SetValueFormat(OptionFormat.Multiplier);

        return;

        static int GetDefaultValue(Item item) =>
            item switch
            {
                Item.Explosive => 1,
                Item.Grenade => 1,
                Item.SulfuricAcid => 15,
                Item.MethylamineGas => 50,
                _ => 0
            };
    }

    public override void Init()
    {
        On = false;
        Instances = [];

        FactoryLocations = [];

        LateTask.New(() =>
        {
            FactoryLocations = ShipStatus.Instance.AllRooms
                .Select(x => x.RoomId)
                .Where(x => x != SystemTypes.Outside && !x.ToString().Contains("Decontamination"))
                .Distinct()
                .Zip(Enum.GetValues<Factory>()[1..])
                .ToDictionary(x => x.First, x => x.Second);
        }, 20f, log: false);
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);

        ChemistPC = Utils.GetPlayerById(playerId);
        LastUpdate = Utils.TimeStamp + 8;
        ItemCounts = [];
        CurrentFactory = Factory.None;
        SelectedProcess = string.Empty;
        SortedAvailableProcesses = [];

        AcidPlayers = [];
        IsBlinding = false;
        BombedBodies = [];
        Grenades = [];

        ItemCounts = Enum.GetValues<Item>().ToDictionary(x => x, _ => 0);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool()) AURoleOptions.PhantomCooldown = 1f;
    }

    private static ItemType GetItemType(Item item)
    {
        return item switch
        {
            Item.Air or Item.Coal or Item.IronOre or Item.Water or Item.ThermalWater => ItemType.BasicResource,
            Item.Explosive or Item.Grenade or Item.SulfuricAcid or Item.MethylamineGas => ItemType.FinalProduct,
            _ => ItemType.IntermediateProduct
        };
    }

    private static string GetChemicalForm(Item item)
    {
        return item switch
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
    }

    private static Color GetItemColor(Item item)
    {
        return item switch
        {
            Item.Air => Palette.HalfWhite,
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
            Item.Steam => Palette.White_75Alpha,
            Item.Sulfur => Color.yellow,
            Item.SulfurDioxideGas => Color.yellow,
            Item.SulfuricAcid => Color.yellow,
            Item.SynthesisGas => Color.magenta,
            Item.ThermalWater => Palette.Orange,
            Item.Water => Palette.LightBlue,

            _ => Color.white
        };
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurder(killer, target)) return false;

        int need = FinalProductUsageAmounts[Item.SulfuricAcid].GetInt();
        int need2 = FinalProductUsageAmounts[Item.Grenade].GetInt();
        bool canUseAcid = ItemCounts[Item.SulfuricAcid] >= need && !AcidPlayers.ContainsKey(target.PlayerId) && !AcidPlayers.Any(x => x.Value.OtherAcidPlayers.Contains(target.PlayerId));
        bool canUseGrenade = ItemCounts[Item.Grenade] >= need2;

        if (!canUseAcid && !canUseGrenade) return true;

        return killer.CheckDoubleTrigger(target, () =>
        {
            if (canUseAcid)
            {
                AcidPlayers[target.PlayerId] = ([], Utils.TimeStamp);
                Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 4, target.PlayerId);
                ItemCounts[Item.SulfuricAcid] -= need;
                Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, (int)Item.SulfuricAcid, -need);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            }
            else if (canUseGrenade)
            {
                ItemCounts[Item.Grenade] -= need2;
                Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, (int)Item.Grenade, -need2);
                Grenades[target.PlayerId] = Utils.TimeStamp;
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            }
        });
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        int need = FinalProductUsageAmounts[Item.Explosive].GetInt();

        if (ItemCounts[Item.Explosive] >= need)
        {
            ItemCounts[Item.Explosive] -= need;
            Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, (int)Item.Explosive, -need);
            BombedBodies.Add(target.PlayerId);
        }
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (!GameStates.IsInTask || !pc.IsAlive()) return;

        Vector2 pos = pc.Pos();

        if (AcidPlayers.TryGetValue(pc.PlayerId, out (HashSet<byte> OtherAcidPlayers, long TimeStamp) acidPlayers))
        {
            Main.AllAlivePlayerControls
                .ExceptBy(acidPlayers.OtherAcidPlayers, x => x.PlayerId)
                .Where(x => x.PlayerId != pc.PlayerId && x.PlayerId != ChemistPC.PlayerId && Vector2.Distance(x.Pos(), pos) < 2.5f)
                .Do(x => acidPlayers.OtherAcidPlayers.Add(x.PlayerId));
        }

        if (Grenades.TryGetValue(pc.PlayerId, out long ts) && ts + GrenadeExplodeDelay.GetInt() <= Utils.TimeStamp)
        {
            Grenades.Remove(pc.PlayerId);

            float radius = GrenadeExplodeRadius.GetFloat();

            Main.AllAlivePlayerControls
                .Where(x => x.PlayerId != ChemistPC.PlayerId && Vector2.Distance(x.Pos(), pos) < radius && ChemistPC.RpcCheckAndMurder(x, true))
                .Do(x => x.Suicide(realKiller: ChemistPC));
        }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (IsBlinding) return;

        int need = FinalProductUsageAmounts[Item.MethylamineGas].GetInt();

        if (ItemCounts[Item.MethylamineGas] >= need)
        {
            ItemCounts[Item.MethylamineGas] -= need;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, (int)Item.MethylamineGas, -need);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

            IsBlinding = true;
            Utils.MarkEveryoneDirtySettings();

            LateTask.New(() =>
            {
                if (IsBlinding)
                {
                    IsBlinding = false;
                    Utils.MarkEveryoneDirtySettings();
                }
            }, BlindDuration.GetInt(), log: false);
        }
    }

    public override void OnExitVent(PlayerControl pc, Vent vent)
    {
        ItemCounts[Item.Coal] += CoalGainedPerVent.GetInt();
        ItemCounts[Item.IronOre] += IronOreGainedPerVent.GetInt();

        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, (int)Item.Coal, CoalGainedPerVent.GetInt());
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, (int)Item.IronOre, IronOreGainedPerVent.GetInt());
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
    {
        if (BombedBodies.Contains(target.PlayerId))
        {
            if (ChemistPC.RpcCheckAndMurder(reporter, true)) reporter.Suicide(realKiller: ChemistPC);

            return false;
        }

        return true;
    }

    public override void OnReportDeadBody()
    {
        CheckAndKillAcidPlayers(true);
        IsBlinding = false;
        BombedBodies.Clear();
    }

    private void CheckAndKillAcidPlayers(bool force = false)
    {
        foreach (KeyValuePair<byte, (HashSet<byte> OtherAcidPlayers, long TimeStamp)> kvp in AcidPlayers)
        {
            if (force || kvp.Value.TimeStamp + AcidPlayersDieAfterTime.GetInt() <= Utils.TimeStamp)
            {
                PlayerControl srcPlayer = Utils.GetPlayerById(kvp.Key);

                if (srcPlayer != null && srcPlayer.IsAlive() && ChemistPC.RpcCheckAndMurder(srcPlayer, true))
                    srcPlayer.Suicide(realKiller: ChemistPC);

                foreach (byte id in kvp.Value.OtherAcidPlayers)
                {
                    PlayerControl player = Utils.GetPlayerById(id);
                    if (player == null || !player.IsAlive() || !ChemistPC.RpcCheckAndMurder(player, true)) continue;

                    player.Suicide(realKiller: ChemistPC);
                }
            }
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || !pc.IsAlive() || LastUpdate >= Utils.TimeStamp) return;

        LastUpdate = Utils.TimeStamp;

        if (ItemCounts[Item.Air] < 900)
        {
            ItemCounts[Item.Air] += AirGainedPerSecond.GetInt();
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, (int)Item.Air, AirGainedPerSecond.GetInt());
        }

        if (ItemCounts[Item.Water] < 900)
        {
            ItemCounts[Item.Water] += WaterGainedPerSecond.GetInt();
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, (int)Item.Water, WaterGainedPerSecond.GetInt());
        }

        Factory beforeFactory = CurrentFactory;
        PlainShipRoom room = pc.GetPlainShipRoom();

        if (ItemCounts[Item.ThermalWater] < 50 && room != null && room.RoomId == HottestRoom[Main.CurrentMap])
        {
            ItemCounts[Item.ThermalWater]++;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, (int)Item.ThermalWater, 1);
        }

        if (room != null)
        {
            CurrentFactory = FactoryLocations.GetValueOrDefault(room.RoomId);
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 3, (int)CurrentFactory);

            if (CurrentFactory != beforeFactory)
            {
                SortedAvailableProcesses = Processes[CurrentFactory]
                    .OrderByDescending(x => x.Value.Ingredients.TrueForAll(y => ItemCounts[y.Item] >= y.Count))
                    .Select(x => x.Key)
                    .ToList();

                SelectedProcess = SortedAvailableProcesses.FirstOrDefault() ?? string.Empty;
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 2, SelectedProcess);
            }
        }

        if ((AcidPlayersDieOptions)AcidPlayersDie.GetValue() == AcidPlayersDieOptions.AfterTime)
            CheckAndKillAcidPlayers();

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        if (SortedAvailableProcesses.Count == 0) return false;

        Cycle(pc);

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (SortedAvailableProcesses.Count == 0) return false;

        Cycle(pc);

        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (SortedAvailableProcesses.Count == 0) return false;

        Cycle(shapeshifter);

        return false;
    }

    private void Cycle(PlayerControl pc)
    {
        int index = SortedAvailableProcesses.IndexOf(SelectedProcess);
        SelectedProcess = SortedAvailableProcesses[(index + 1) % SortedAvailableProcesses.Count];
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 2, SelectedProcess);

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (SelectedProcess == string.Empty) return;

        (List<(int Count, Item Item)> Ingredients, List<(int Count, Item Item)> Results) = Processes[CurrentFactory][SelectedProcess];

        if (!Ingredients.TrueForAll(x => ItemCounts[x.Item] >= x.Count)) return;

        Ingredients.ForEach(x =>
        {
            ItemCounts[x.Item] -= x.Count;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, (int)x.Item, -x.Count);
        });

        Results.ForEach(x =>
        {
            ItemCounts[x.Item] += x.Count;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, (int)x.Item, x.Count);
        });

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        
        if (pc.AmOwner && Results.Exists(x => x.Item == Item.SulfuricAcid))
            Achievements.Type.HeyRabek.Complete();
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                var item = (Item)reader.ReadPackedInt32();
                ItemCounts.TryAdd(item, 0);
                ItemCounts[item] += reader.ReadPackedInt32();
                break;
            case 2:
                SelectedProcess = reader.ReadString();
                break;
            case 3:
                CurrentFactory = (Factory)reader.ReadPackedInt32();
                break;
            case 4:
                AcidPlayers[reader.ReadByte()] = ([], Utils.TimeStamp);
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || ChemistPC.PlayerId != seer.PlayerId) return string.Empty;

        bool self = seer.PlayerId == target.PlayerId;
        if (self && seer.IsModdedClient() && !hud) return string.Empty;

        StringBuilder sb = new StringBuilder().Append("<size=80%>");

        if (self)
        {
            Dictionary<ItemType, Dictionary<Item, int>> grouped = ItemCounts
                .Where(x => x.Value > 0)
                .GroupBy(x => GetItemType(x.Key))
                .OrderBy(x => (int)x.Key)
                .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Key, y => y.Value));

            foreach ((ItemType type, Dictionary<Item, int> items) in grouped)
            {
                if (items.Count == 0) continue;

                sb.Append($"{type.ToString()[0]}: ");

                foreach ((Item item, int count) in items) sb.Append(Utils.ColorString(GetItemColor(item), $"{Utils.ColorString(FinalProductUsageAmounts.TryGetValue(item, out OptionItem opt) && opt.GetInt() <= count ? Color.green : Color.white, $"{count}")} {GetChemicalForm(item)}") + ", ");

                sb.Length -= 2;
                sb.AppendLine();
            }

            if (SelectedProcess == string.Empty) return sb.ToString().TrimEnd();

            (List<(int Count, Item Item)> Ingredients, List<(int Count, Item Item)> Results) = Processes[CurrentFactory][SelectedProcess];

            Func<(int Count, Item Item), string> selector = x => $"{x.Count} {Utils.ColorString(GetItemColor(x.Item), $"{GetChemicalForm(x.Item)}")}";
            sb.Append(string.Join(", ", Ingredients.Select(selector)));
            sb.Append(Ingredients.Count + Results.Count > 2 ? "\n\u2192  " : " → ");
            sb.Append(string.Join(", ", Results.Select(selector)));
        }

        if ((AcidPlayersDieOptions)AcidPlayersDie.GetValue() == AcidPlayersDieOptions.AfterTime)
        {
            int time = AcidPlayersDieAfterTime.GetInt();
            long now = Utils.TimeStamp;

            foreach (KeyValuePair<byte, (HashSet<byte> OtherAcidPlayers, long TimeStamp)> kvp in AcidPlayers)
            {
                if (kvp.Key == target.PlayerId || kvp.Value.OtherAcidPlayers.Contains(target.PlayerId))
                {
                    sb.Append(Utils.ColorString(Color.yellow, $"\u26a0 {time - (now - kvp.Value.TimeStamp):N0}"));
                    break;
                }
            }
        }

        return sb.Append("</size>").ToString();
    }

    public static string GetProcessesInfo()
    {
        var sb = new StringBuilder();

        foreach ((Factory factory, Dictionary<string, (List<(int Count, Item Item)> Ingredients, List<(int Count, Item Item)> Results)> processes) in Processes)
        {
            string factoryName = factory.ToString();
            factoryName = string.Concat(factoryName.Select(c => char.IsUpper(c) ? " " + c : c.ToString())).Trim();

            sb.Append("<b>");
            sb.Append("<u>");
            sb.Append(factoryName);
            if (FactoryLocations.ContainsValue(factory)) sb.Append($" ({Translator.GetString(FactoryLocations.GetKeyByValue(factory).ToString())})");
            sb.Append(':');
            sb.Append("</u>");
            sb.Append("</b>");

            sb.AppendLine();
            sb.AppendLine();

            foreach ((string process, (List<(int Count, Item Item)> ingredients, List<(int Count, Item Item)> results)) in processes)
            {
                sb.Append("<i>");
                sb.Append(process);
                sb.Append(':');
                sb.Append("</i>");
                sb.AppendLine();

                foreach ((int count, Item item) in ingredients)
                {
                    sb.Append($"{count} {Utils.ColorString(GetItemColor(item), $"{GetChemicalForm(item)}")}");
                    sb.AppendLine();
                }

                sb.Append('➡');
                if (results.Count > 1) sb.AppendLine();

                foreach ((int count, Item item) in results)
                {
                    sb.Append($"{count} {Utils.ColorString(GetItemColor(item), $"{GetChemicalForm(item)}")}");
                    sb.AppendLine();
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private enum AcidPlayersDieOptions
    {
        AfterMeeting,
        AfterTime
    }

    private enum Item
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

    private enum ItemType
    {
        BasicResource,
        IntermediateProduct,
        FinalProduct
    }

    private enum Factory
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
}