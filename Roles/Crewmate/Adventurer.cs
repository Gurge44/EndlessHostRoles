using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Patches;
using Hazel;
using UnityEngine;

namespace EHR.Crewmate;

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

    private int Count;
    private bool InCraftingMode;
    private long LastGroupingResourceTimeStamp;

    private long LastRandomResourceTimeStamp;

    private List<Weapon> OrderedWeapons;
    private Dictionary<Resource, Vector2> ResourceLocations;
    private HashSet<byte> RevealedPlayers;
    private Weapon SelectedWeaponToCraft;
    private HashSet<byte> ShieldedPlayers;
    public override bool IsEnable => On;

    private static void HideObject(Resource resource)
    {
        CustomNetObject.AllObjects.FirstOrDefault(x => x is AdventurerItem a && a.Resource == resource)?.Despawn();
    }

    private static OptionItem CreateWeaponEnabledSetting(int id, Weapon weapon)
    {
        return new BooleanOptionItem(id, $"AdventurerWeaponEnabled.{weapon}", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adventurer]);
    }

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(11330, TabGroup.CrewmateRoles, CustomRoles.Adventurer);

        IncreasedVisionDuration = new IntegerOptionItem(11332, "AdventurerIncreasedVisionDuration", new(1, 60, 1), 30, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adventurer])
            .SetValueFormat(OptionFormat.Seconds);

        foreach (Weapon weapon in Enum.GetValues<Weapon>()) WeaponEnabledSettings[weapon] = CreateWeaponEnabledSetting(11333 + (int)weapon, weapon);
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

        LastRandomResourceTimeStamp = Utils.TimeStamp + 30;
        LastGroupingResourceTimeStamp = Utils.TimeStamp + 30;
        ResourceLocations = [];

        foreach (Resource resource in Enum.GetValues<Resource>()) ResourceCounts[resource] = 0;
    }

    public override void Init()
    {
        On = false;

        EnabledWeapons = WeaponEnabledSettings
            .Where(x => x.Value.GetBool())
            .Select(x => x.Key)
            .ToList();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = 0.1f;
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void OnExitVent(PlayerControl pc, Vent vent)
    {
        InCraftingMode = !InCraftingMode;
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, InCraftingMode);

        switch (InCraftingMode)
        {
            case true:
            {
                OrderedWeapons = [.. EnabledWeapons.OrderBy(x => !Ingredients[x].All(r => r.Count <= ResourceCounts[r.Resource]))];
                SelectedWeaponToCraft = OrderedWeapons.FirstOrDefault();
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 4, (int)SelectedWeaponToCraft);
                break;
            }
            case false when Ingredients[SelectedWeaponToCraft].All(x => x.Count <= ResourceCounts[x.Resource]):
            {
                Weapon weapon = SelectedWeaponToCraft == Weapon.RNG ? EnabledWeapons.RandomElement() : SelectedWeaponToCraft;
                ActiveWeapons.Add(weapon);
                pc.Notify(string.Format(Translator.GetString("AdventurerWeaponCrafted"), Translator.GetString($"AdventurerGun.{weapon}")));

                foreach ((Resource resource, int count) in Ingredients[weapon])
                {
                    ResourceCounts[resource] -= count;
                    Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 2, (int)resource, count);
                }

                if (pc.AmOwner)
                    Achievements.Type.HowDoICraftThisAgain.Complete();

                break;
            }
        }

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        switch (InCraftingMode)
        {
            case true:
                SelectedWeaponToCraft = OrderedWeapons[(OrderedWeapons.IndexOf(SelectedWeaponToCraft) + 1) % OrderedWeapons.Count];
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 4, (int)SelectedWeaponToCraft);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                break;
            case false when ActiveWeapons.Count > 0:
                PlayerControl target = ExternalRpcPetPatch.SelectKillButtonTarget(pc);

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
                        IEnumerable<PlayerControl> e = Main.AllAlivePlayerControls.Where(x => x.PlayerId != pc.PlayerId && !x.inVent && !x.inMovingPlat && !x.onLadder);
                        PlayerControl[] filtered = e as PlayerControl[] ?? e.ToArray();
                        if (filtered.Length == 0) return;

                        PlayerControl other = filtered.RandomElement();
                        Vector2 pos = other.Pos();
                        other.TP(pc);
                        pc.TP(pos);
                        RemoveAndNotify();
                        break;
                    case Weapon.Lantern:
                        Utils.MarkEveryoneDirtySettings();
                        LateTask.New(() => ActiveWeapons.Remove(Weapon.Lantern), IncreasedVisionDuration.GetInt(), log: false);
                        break;
                    case Weapon.Wrench:
                        if (Utils.IsActive(SystemTypes.Electrical))
                        {
                            var switchSystem = ShipStatus.Instance?.Systems?[SystemTypes.Electrical]?.CastFast<SwitchSystem>();

                            if (switchSystem != null)
                            {
                                switchSystem.ActualSwitches = 0;
                                switchSystem.ExpectedSwitches = 0;
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
                        PlayerControl closest = Main.AllAlivePlayerControls.Where(x => x.PlayerId != pc.PlayerId).MinBy(x => Vector2.Distance(pc.Pos(), x.Pos()));
                        RevealedPlayers.Add(closest.PlayerId);
                        RemoveAndNotify(closest);
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
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 3, (int)Resource.TaskCompletion);
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
            if (ResourceLocations.TryGetValue(Resource.Random, out Vector2 location))
            {
                LocateArrow.Remove(AdventurerPC.PlayerId, location);
                HideObject(Resource.Random);
            }

            Vector2 pos = pc.Pos();
            LocateArrow.Add(AdventurerPC.PlayerId, pos);
            ResourceLocations[Resource.Random] = pos;
            _ = new AdventurerItem(pos, Resource.Random, [AdventurerPC.PlayerId]);
            LastRandomResourceTimeStamp = now;
            Utils.NotifyRoles(SpecifySeer: AdventurerPC, SpecifyTarget: AdventurerPC);
        }

        if (LastGroupingResourceTimeStamp + 20 <= now && Main.AllAlivePlayerControls.Count(x => x.PlayerId != pc.PlayerId && Vector2.Distance(x.Pos(), pc.Pos()) < 2f) >= 2)
        {
            if (ResourceLocations.TryGetValue(Resource.Grouping, out Vector2 location))
            {
                LocateArrow.Remove(AdventurerPC.PlayerId, location);
                HideObject(Resource.Grouping);
            }

            Vector2 pos = pc.Pos();
            LocateArrow.Add(AdventurerPC.PlayerId, pos);
            ResourceLocations[Resource.Grouping] = pos;
            _ = new AdventurerItem(pos, Resource.Grouping, [AdventurerPC.PlayerId]);
            LastGroupingResourceTimeStamp = now;
            Utils.NotifyRoles(SpecifySeer: AdventurerPC, SpecifyTarget: AdventurerPC);
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (Count++ < 10) return;

        Count = 0;

        foreach ((Resource resource, Vector2 location) in ResourceLocations)
        {
            if (Vector2.Distance(pc.Pos(), location) < 2f)
            {
                ResourceCounts[resource]++;
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 3, (int)resource);
                ResourceLocations.Remove(resource);
                HideObject(resource);
                LocateArrow.Remove(pc.PlayerId, location);

                (char Icon, Color Color) displayData = ResourceDisplayData[resource];
                pc.Notify(string.Format(Translator.GetString("AdventurerFound"), Utils.ColorString(displayData.Color, $"{displayData.Icon}")));
                break;
            }
        }
    }

    public override void AfterMeetingTasks()
    {
        long now = Utils.TimeStamp;
        LastGroupingResourceTimeStamp = now;
        LastRandomResourceTimeStamp = now;
        ActiveWeapons.Remove(Weapon.Proxy);
    }

    public void OnLightsFix()
    {
        ResourceCounts[Resource.LightsFix]++;
        Utils.SendRPC(CustomRPC.SyncRoleData, AdventurerPC.PlayerId, 3, (int)Resource.LightsFix);
    }

    public static void OnAnyoneShapeshiftLoop(Adventurer av, PlayerControl shapeshifter)
    {
        Vector2 pos = shapeshifter.Pos();
        av.ResourceLocations[Resource.ShapeshiftSkin] = pos;
        HideObject(Resource.ShapeshiftSkin);
        _ = new AdventurerItem(pos, Resource.ShapeshiftSkin, [av.AdventurerPC.PlayerId]);
    }

    public static void OnAnyoneDead(PlayerControl target)
    {
        foreach (PlayerState state in Main.PlayerStates.Values)
        {
            if (state.Role is Adventurer { IsEnable: true } av)
            {
                Vector2 pos = target.Pos();
                av.ResourceLocations[Resource.DeadBody] = pos;
                HideObject(Resource.DeadBody);
                _ = new AdventurerItem(pos, Resource.DeadBody, [av.AdventurerPC.PlayerId]);
            }
        }
    }

    public static bool OnAnyoneCheckMurder(PlayerControl target)
    {
        var any = false;

        foreach (PlayerState s in Main.PlayerStates.Values)
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
        if (base.KnowRole(seer, target)) return true;

        return RevealedPlayers.Contains(target.PlayerId);
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

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if ((seer.IsModdedClient() && !hud) || seer.PlayerId != target.PlayerId || seer.PlayerId != AdventurerPC.PlayerId) return string.Empty;

        IEnumerable<string> resources =
            from resource in Enum.GetValues<Resource>()
            let displayData = ResourceDisplayData[resource]
            select $"{Utils.ColorString(displayData.Color, $"{displayData.Icon}")}{ResourceCounts[resource]}";

        string finalText = string.Join(' ', resources);
        if (meeting) return finalText;

        finalText += $"\n{LocateArrow.GetArrows(seer)}\n";

        finalText += "<size=80%>";

        finalText += InCraftingMode
            ? string.Format(
                Translator.GetString("AdventurerIngredientsDisplay"),
                Translator.GetString($"AdventurerGun.{SelectedWeaponToCraft}"),
                string.Join(' ', Ingredients[SelectedWeaponToCraft]
                    .Select(x => $"{Utils.ColorString(ResourceDisplayData[x.Resource].Color, $"{ResourceDisplayData[x.Resource].Icon}")}" +
                                 $"{Utils.ColorString(x.Count > ResourceCounts[x.Resource] ? Color.red : Color.white, $"{x.Count}")}")))
            : Translator.GetString("AdventurerVentToEnterCrafting");

        finalText += "</size>";

        return finalText;
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}