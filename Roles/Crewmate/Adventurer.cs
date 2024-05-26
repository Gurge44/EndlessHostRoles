using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Patches;
using Hazel;
using UnityEngine;

namespace EHR.Roles.Crewmate
{
    internal class Adventurer : RoleBase
    {
        public enum Resource
        {
            TaskCompletion,
            Random,
            DeadBody,
            ShapeshiftSkin,
            LightsFix,
            Grouping
        }

        public enum Weapon
        {
            Gun,
            Shield,
            Lantern,
            Portal,
            Wrench,
            Proxy,
            Prediction,
            RNG
        }

        public static bool On;

        public static readonly Dictionary<Resource, (char Icon, Color Color)> ResourceDisplayData = new()
        {
            { Resource.TaskCompletion, ('\u2756', Color.blue) },
            { Resource.Random, ('Θ', Color.cyan) },
            { Resource.DeadBody, ('\u2673', Color.magenta) },
            { Resource.ShapeshiftSkin, ('⁂', Color.gray) },
            { Resource.LightsFix, ('\u2600', Color.yellow) },
            { Resource.Grouping, ('\u25cb', Color.green) }
        };

        private static readonly Dictionary<Weapon, List<(Resource Resource, int Count)>> Ingredients = new()
        {
            { Weapon.Gun, [(Resource.DeadBody, 1), (Resource.TaskCompletion, 1)] },
            { Weapon.Shield, [(Resource.TaskCompletion, 3)] },
            { Weapon.Lantern, [(Resource.LightsFix, 1), (Resource.TaskCompletion, 1)] },
            { Weapon.Portal, [(Resource.Random, 2)] },
            { Weapon.Wrench, [(Resource.LightsFix, 1), (Resource.TaskCompletion, 2)] },
            { Weapon.Proxy, [(Resource.Grouping, 2), (Resource.TaskCompletion, 1)] },
            { Weapon.Prediction, [(Resource.DeadBody, 1), (Resource.ShapeshiftSkin, 2), (Resource.TaskCompletion, 1)] },
            { Weapon.RNG, [(Resource.Random, 2), (Resource.TaskCompletion, 2)] }
        };

        private static OptionItem IncreasedVisionDuration;
        private static readonly Dictionary<Weapon, OptionItem> WeaponEnabledSettings = [];

        private static List<Weapon> EnabledWeapons = [];

        private readonly Dictionary<Resource, int> ResourceCounts = [];
        public List<Weapon> ActiveWeapons;

        private PlayerControl AdventurerPC;
        public bool InCraftingMode;
        private long LastGroupingResourceTimeStamp;

        private long LastRandomResourceTimeStamp;

        public List<Weapon> OrderedWeapons;
        private Dictionary<Resource, Vector2> ResourceLocations;
        public HashSet<byte> RevealedPlayers;
        private Weapon SelectedWeaponToCraft;
        public HashSet<byte> ShieldedPlayers;
        public override bool IsEnable => On;

        static void HideObject(Resource resource) => CustomNetObject.AllObjects.Values.FirstOrDefault(x => x is AdventurerItem a && a.Resource == resource)?.Despawn();

        static OptionItem CreateWeaponEnabledSetting(int id, Weapon weapon) => BooleanOptionItem.Create(id, $"AdventurerWeaponEnabled.{weapon}", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adventurer]);

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(11330, TabGroup.CrewmateRoles, CustomRoles.Adventurer);

            IncreasedVisionDuration = IntegerOptionItem.Create(11332, "AdventurerIncreasedVisionDuration", new(1, 60, 1), 30, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adventurer])
                .SetValueFormat(OptionFormat.Seconds);

            foreach (var weapon in Enum.GetValues<Weapon>())
            {
                WeaponEnabledSettings[weapon] = CreateWeaponEnabledSetting(11333 + (int)weapon, weapon);
            }
        }

        public override void Add(byte playerId)
        {
            On = true;
            AdventurerPC = Utils.GetPlayerById(playerId);

            InCraftingMode = false;
            SelectedWeaponToCraft = new();

            OrderedWeapons = [];
            ActiveWeapons = [];
            ShieldedPlayers = [];
            RevealedPlayers = [];

            LastRandomResourceTimeStamp = Utils.TimeStamp + 8;
            LastGroupingResourceTimeStamp = Utils.TimeStamp + 20;
            ResourceLocations = [];

            foreach (var resource in Enum.GetValues<Resource>())
            {
                ResourceCounts[resource] = 0;
            }
        }

        public override void Init()
        {
            On = false;

            EnabledWeapons = WeaponEnabledSettings
                .Where(x => x.Value.GetBool())
                .Select(x => x.Key)
                .ToList();
        }

        public override void OnExitVent(PlayerControl pc, Vent vent)
        {
            InCraftingMode = !InCraftingMode;
            Utils.SendRPC(CustomRPC.SyncAdventurer, pc.PlayerId, 1, InCraftingMode);

            switch (InCraftingMode)
            {
                case true:
                    OrderedWeapons = [.. EnabledWeapons.OrderBy(x => !Ingredients[x].All(r => r.Count <= ResourceCounts[r.Resource]))];
                    SelectedWeaponToCraft = OrderedWeapons.FirstOrDefault();
                    Utils.SendRPC(CustomRPC.SyncAdventurer, pc.PlayerId, 4, (int)SelectedWeaponToCraft);
                    break;
                case false when Ingredients[SelectedWeaponToCraft].All(x => x.Count <= ResourceCounts[x.Resource]):
                    var weapon = SelectedWeaponToCraft == Weapon.RNG ? EnabledWeapons.RandomElement() : SelectedWeaponToCraft;
                    ActiveWeapons.Add(weapon);
                    pc.Notify(string.Format(Translator.GetString("AdventurerWeaponCrafted"), Translator.GetString($"AdventurerGun.{weapon}")));
                    foreach ((Resource resource, int count) in Ingredients[weapon])
                    {
                        ResourceCounts[resource] -= count;
                        Utils.SendRPC(CustomRPC.SyncAdventurer, pc.PlayerId, 2, (int)resource, count);
                    }

                    break;
            }

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override void OnPet(PlayerControl pc)
        {
            switch (InCraftingMode)
            {
                case true:
                    SelectedWeaponToCraft = OrderedWeapons[(OrderedWeapons.IndexOf(SelectedWeaponToCraft) + 1) % OrderedWeapons.Count];
                    Utils.SendRPC(CustomRPC.SyncAdventurer, pc.PlayerId, 4, (int)SelectedWeaponToCraft);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    break;
                case false when ActiveWeapons.Count > 0:
                    var target = ExternalRpcPetPatch.SelectKillButtonTarget(pc);
                    switch (ActiveWeapons[0])
                    {
                        case Weapon.Gun when target != null:
                            pc.RpcCheckAndMurder(target);
                            RemoveAndNotify();
                            break;
                        case Weapon.Shield when target != null:
                            ShieldedPlayers.Add(target.PlayerId);
                            RemoveAndNotify();
                            break;
                        case Weapon.Portal:
                            var e = Main.AllAlivePlayerControls.Where(x => x.PlayerId != pc.PlayerId && !x.inVent && !x.inMovingPlat && !x.onLadder);
                            var filtered = e as PlayerControl[] ?? e.ToArray();
                            if (filtered.Length == 0) return;
                            var other = filtered.RandomElement();
                            var pos = other.Pos();
                            other.TP(pc);
                            pc.TP(pos);
                            RemoveAndNotify();
                            break;
                        case Weapon.Lantern:
                            Utils.MarkEveryoneDirtySettings();
                            _ = new LateTask(() => { ActiveWeapons.Remove(Weapon.Lantern); }, IncreasedVisionDuration.GetInt(), log: false);
                            break;
                        case Weapon.Wrench:
                            if (Utils.IsActive(SystemTypes.Electrical))
                            {
                                var SwitchSystem = ShipStatus.Instance?.Systems?[SystemTypes.Electrical]?.TryCast<SwitchSystem>();
                                if (SwitchSystem != null)
                                {
                                    SwitchSystem.ActualSwitches = 0;
                                    SwitchSystem.ExpectedSwitches = 0;
                                }
                            }
                            else if (Utils.IsActive(SystemTypes.Reactor))
                            {
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 16);
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 17);
                            }
                            else if (Utils.IsActive(SystemTypes.Laboratory))
                            {
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 67);
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 66);
                            }
                            else if (Utils.IsActive(SystemTypes.LifeSupp))
                            {
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 67);
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 66);
                            }
                            else if (Utils.IsActive(SystemTypes.Comms))
                            {
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 16);
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 17);
                            }

                            RemoveAndNotify();
                            break;
                        case Weapon.Prediction:
                            var closest = Main.AllAlivePlayerControls.Where(x => x.PlayerId != pc.PlayerId).MinBy(x => Vector2.Distance(pc.Pos(), x.Pos()));
                            RevealedPlayers.Add(closest.PlayerId);
                            RemoveAndNotify(notifyTarget: closest);
                            break;
                    }

                    break;

                    void RemoveAndNotify(PlayerControl notifyTarget = null)
                    {
                        ActiveWeapons.RemoveAt(0);
                        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: notifyTarget ?? pc);
                    }
            }
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            ResourceCounts[Resource.TaskCompletion]++;
            Utils.SendRPC(CustomRPC.SyncAdventurer, pc.PlayerId, 3, (int)Resource.TaskCompletion);
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad) return;
            long now = Utils.TimeStamp;

            if (!GameStates.IsInTask)
            {
                LastGroupingResourceTimeStamp = now;
                LastRandomResourceTimeStamp = now;
                return;
            }

            if (LastRandomResourceTimeStamp + 20 <= now && pc.PlayerId != AdventurerPC.PlayerId && IRandom.Instance.Next(50) == 0)
            {
                if (ResourceLocations.TryGetValue(Resource.Random, out var location))
                {
                    LocateArrow.Remove(AdventurerPC.PlayerId, location);
                    HideObject(Resource.Random);
                }

                var pos = pc.Pos();
                LocateArrow.Add(AdventurerPC.PlayerId, pos);
                ResourceLocations[Resource.Random] = pos;
                _ = new AdventurerItem(pos, Resource.Random, [AdventurerPC.PlayerId]);
                LastRandomResourceTimeStamp = now;
                Utils.NotifyRoles(SpecifySeer: AdventurerPC, SpecifyTarget: AdventurerPC);
            }

            if (LastGroupingResourceTimeStamp + 20 <= now && Main.AllAlivePlayerControls.Count(x => x.PlayerId != pc.PlayerId && Vector2.Distance(x.Pos(), pc.Pos()) < 2f) >= 2)
            {
                if (ResourceLocations.TryGetValue(Resource.Grouping, out var location))
                {
                    LocateArrow.Remove(AdventurerPC.PlayerId, location);
                    HideObject(Resource.Grouping);
                }

                var pos = pc.Pos();
                LocateArrow.Add(AdventurerPC.PlayerId, pos);
                ResourceLocations[Resource.Grouping] = pos;
                _ = new AdventurerItem(pos, Resource.Grouping, [AdventurerPC.PlayerId]);
                LastGroupingResourceTimeStamp = now;
                Utils.NotifyRoles(SpecifySeer: AdventurerPC, SpecifyTarget: AdventurerPC);
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            foreach ((Resource resource, Vector2 location) in ResourceLocations)
            {
                if (Vector2.Distance(pc.Pos(), location) < 2f)
                {
                    ResourceCounts[resource]++;
                    Utils.SendRPC(CustomRPC.SyncAdventurer, pc.PlayerId, 3, (int)resource);
                    ResourceLocations.Remove(resource);
                    HideObject(resource);
                    LocateArrow.Remove(pc.PlayerId, location);

                    var displayData = ResourceDisplayData[resource];
                    pc.Notify(string.Format(Translator.GetString("AdventurerFound"), Utils.ColorString(displayData.Color, $"{displayData.Icon}")));
                    break;
                }
            }
        }

        public override void AfterMeetingTasks()
        {
            ActiveWeapons.Remove(Weapon.Proxy);
        }

        public void OnLightsFix()
        {
            ResourceCounts[Resource.LightsFix]++;
            Utils.SendRPC(CustomRPC.SyncAdventurer, AdventurerPC.PlayerId, 3, (int)Resource.LightsFix);
        }

        public static void OnAnyoneShapeshiftLoop(Adventurer av, PlayerControl shapeshifter)
        {
            var pos = shapeshifter.Pos();
            av.ResourceLocations[Resource.ShapeshiftSkin] = pos;
            HideObject(Resource.ShapeshiftSkin);
            _ = new AdventurerItem(pos, Resource.ShapeshiftSkin, [av.AdventurerPC.PlayerId]);
        }

        public static void OnAnyoneDead(PlayerControl target)
        {
            foreach (var state in Main.PlayerStates.Values)
            {
                if (state.Role is Adventurer { IsEnable: true } av)
                {
                    var pos = target.Pos();
                    av.ResourceLocations[Resource.DeadBody] = pos;
                    HideObject(Resource.DeadBody);
                    _ = new AdventurerItem(pos, Resource.DeadBody, [av.AdventurerPC.PlayerId]);
                }
            }
        }

        public static bool OnAnyoneCheckMurder(PlayerControl target)
        {
            bool any = false;
            foreach (var s in Main.PlayerStates.Values)
            {
                if (s.Role is Adventurer { IsEnable: true } av && av.ShieldedPlayers.Contains(target.PlayerId))
                {
                    any = true;
                    av.ShieldedPlayers.Remove(target.PlayerId);
                    break;
                }
            }

            return !any;
        }

        public override bool KnowRole(PlayerControl seer, PlayerControl target)
        {
            return Main.PlayerStates[seer.PlayerId].Role is Adventurer { IsEnable: true } av && av.RevealedPlayers.Contains(target.PlayerId);
        }

        public void ReceiveRPC(MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    InCraftingMode = reader.ReadBoolean();
                    break;
                case 2:
                    ResourceCounts[(Resource)reader.ReadPackedInt32()] -= reader.ReadInt32();
                    break;
                case 3:
                    ResourceCounts[(Resource)reader.ReadPackedInt32()]++;
                    break;
                case 4:
                    SelectedWeaponToCraft = (Weapon)reader.ReadPackedInt32();
                    break;
            }
        }

        public override string GetSuffix(PlayerControl pc, PlayerControl tar, bool hud = false, bool isForMeeting = false)
        {
            if (pc.IsModClient() && !hud) return string.Empty;
            if (Main.PlayerStates[pc.PlayerId].Role is not Adventurer { IsEnable: true } av) return string.Empty;
            if (pc.PlayerId != tar.PlayerId) return string.Empty;

            IEnumerable<string> resources =
                from resource in Enum.GetValues<Resource>()
                let displayData = ResourceDisplayData[resource]
                select $"{Utils.ColorString(displayData.Color, $"{displayData.Icon}")}{av.ResourceCounts[resource]}";

            string finalText = string.Join(' ', resources);
            if (isForMeeting) return finalText;

            finalText += $"\n{LocateArrow.GetArrows(pc)}\n";

            finalText += "<size=80%>";
            finalText += av.InCraftingMode
                ? string.Format(
                    Translator.GetString("AdventurerIngredientsDisplay"),
                    Translator.GetString($"AdventurerGun.{av.SelectedWeaponToCraft}"),
                    string.Join(' ', Ingredients[av.SelectedWeaponToCraft]
                        .Select(x => $"{Utils.ColorString(ResourceDisplayData[x.Resource].Color, $"{ResourceDisplayData[x.Resource].Icon}")}" +
                                     $"{Utils.ColorString(x.Count > av.ResourceCounts[x.Resource] ? Color.red : Color.white, $"{x.Count}")}")))
                : Translator.GetString("AdventurerVentToEnterCrafting");
            finalText += "</size>";

            return finalText;
        }
    }
}