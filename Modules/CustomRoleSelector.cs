using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.Impostor;
using EHR.Neutral;

namespace EHR.Modules;

internal static class CustomRoleSelector
{
    public static Dictionary<byte, CustomRoles> RoleResult;
    public static List<CustomRoles> AddonRolesList = [];
    public static int AddScientistNum;
    public static int AddEngineerNum;
    public static int AddShapeshifterNum;
    public static int AddNoisemakerNum;
    public static int AddTrackerNum;
    public static int AddPhantomNum;
    public static int AddViperNum;
    public static int AddDetectiveNum;

    public static readonly Dictionary<CustomGameMode, CustomRoles> GameModeRoles = new()
    {
        { CustomGameMode.SoloPVP, CustomRoles.Challenger },
        { CustomGameMode.FFA, CustomRoles.Killer },
        { CustomGameMode.StopAndGo, CustomRoles.Tasker },
        { CustomGameMode.HotPotato, CustomRoles.Potato },
        { CustomGameMode.Speedrun, CustomRoles.Runner },
        { CustomGameMode.CaptureTheFlag, CustomRoles.CTFPlayer },
        { CustomGameMode.NaturalDisasters, CustomRoles.NDPlayer },
        { CustomGameMode.RoomRush, CustomRoles.RRPlayer },
        { CustomGameMode.KingOfTheZones, CustomRoles.KOTZPlayer },
        { CustomGameMode.Quiz, CustomRoles.QuizPlayer },
        { CustomGameMode.TheMindGame, CustomRoles.TMGPlayer },
        { CustomGameMode.BedWars, CustomRoles.BedWarsPlayer },
        { CustomGameMode.Deathrace, CustomRoles.Racer },
        { CustomGameMode.Mingle, CustomRoles.MinglePlayer },
        { CustomGameMode.Snowdown, CustomRoles.SnowdownPlayer }
    };

    public static void SelectCustomRoles()
    {
        RoleResult = [];

        if (Main.GM.Value && Main.AllPlayerControls.Length == 1) return;

        if (Options.CurrentGameMode != CustomGameMode.Standard)
        {
            if (GameModeRoles.TryGetValue(Options.CurrentGameMode, out CustomRoles role))
            {
                AssignRoleToEveryone(role);
                return;
            }

            bool hns = Options.CurrentGameMode == CustomGameMode.HideAndSeek;

            if (hns)
            {
                CustomHnS.AssignRoles();
                RoleResult = CustomHnS.PlayerRoles.ToDictionary(x => x.Key, x => x.Value.Role);
                return;
            }
        }

        var rd = IRandom.Instance;
        int playerCount = Main.AllAlivePlayerControls.Length;

        int optImpNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors);

        var readyRoleNum = 0;
        var readyImpNum = 0;
        var readyNonNeutralKillingNum = 0;
        var readyNeutralKillingNum = 0;
        var readyMadmateNum = 0;
        var readyCovenNum = 0;
        var readyCrewmateNum = 0;

        List<CustomRoles> finalRolesList = [];

        Dictionary<RoleAssignType, List<RoleAssignInfo>> roles = [];
        Enum.GetValues<RoleAssignType>().Do(x => roles[x] = []);

        foreach (byte id in Main.SetRoles.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetRoles.Remove(id);

        (bool Spawning, bool OneIsImp) loversData = (Lovers.LegacyLovers.GetBool(), rd.Next(100) < Lovers.LovingImpostorSpawnChance.GetInt());
        loversData.Spawning &= rd.Next(100) < Options.CustomAdtRoleSpawnRate[CustomRoles.Lovers].GetInt();

        HashSet<CustomRoles> xorBannedRoles = [];

        foreach ((CustomRoles, CustomRoles) xor in Main.XORRoles)
        {
            bool first = rd.Next(2) == 0;
            xorBannedRoles.Add(first ? xor.Item1 : xor.Item2);
        }

        if (Main.XORRoles.Count > 0) Logger.Info($"Roles banned by XOR combinations: {string.Join(", ", xorBannedRoles)}", "CustomRoleSelector");

        foreach (CustomRoles role in Enum.GetValues<CustomRoles>())
        {
            int chance = role.GetMode();
            if (role.IsVanilla() || chance == 0 || role.IsAdditionRole() || (role.OnlySpawnsWithPets() && !Options.UsePets.GetBool()) || CustomHnS.AllHnSRoles.Contains(role) || xorBannedRoles.Contains(role)) continue;

            switch (role)
            {
                case CustomRoles.Doctor when Options.EveryoneSeesDeathReasons.GetBool():
                case CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor when !loversData.Spawning:
                case CustomRoles.Commander when optImpNum <= 1 && Commander.CannotSpawnAsSoloImp.GetBool():
                case CustomRoles.Changeling when Changeling.GetAvailableRoles(true).Count == 0:
                case CustomRoles.Camouflager when Camouflager.DoesntSpawnOnFungle.GetBool() && Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.Battery when Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.Beacon when Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.Stalker when Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.Pelican when roles[RoleAssignType.Impostor].Any(x => x.Role == CustomRoles.Duellist):
                case CustomRoles.Duellist when roles[RoleAssignType.NeutralKilling].Any(x => x.Role == CustomRoles.Pelican):
                case CustomRoles.VengefulRomantic:
                case CustomRoles.RuthlessRomantic:
                case CustomRoles.Deathknight:
                case CustomRoles.Convict:
                case CustomRoles.Renegade:
                case CustomRoles.CovenMember:
                case CustomRoles.CovenLeader:
                case CustomRoles.Death:
                case CustomRoles.GM:
                case CustomRoles.NotAssigned:
                    continue;
            }

            int count = role.GetCount();

            RoleAssignInfo info = new(role, chance, count);

            if (role.IsCoven()) roles[RoleAssignType.Coven].Add(info);
            else if (role.IsMadmate()) roles[RoleAssignType.Madmate].Add(info);
            else if (role.IsImpostor() && role != CustomRoles.DoubleAgent) roles[RoleAssignType.Impostor].Add(info);
            else if (role.IsNK()) roles[RoleAssignType.NeutralKilling].Add(info);
            else if (role.IsNonNK()) roles[RoleAssignType.NonKillingNeutral].Add(info);
            else roles[RoleAssignType.Crewmate].Add(info);
        }

        if (optImpNum >= 2 && roles[RoleAssignType.Impostor].FindFirst(x => x.Role == CustomRoles.Loner, out var lonerInfo) && lonerInfo.SpawnChance > rd.Next(100))
        {
            finalRolesList.Add(CustomRoles.Loner);
            readyImpNum++;
            readyRoleNum++;
            optImpNum--;
            Logger.Info("Loner selected as Impostor", "CustomRoleSelector");
        }
        else
            roles[RoleAssignType.Impostor].RemoveAll(x => x.Role == CustomRoles.Loner);

        loversData.OneIsImp &= roles[RoleAssignType.Impostor].Count(x => x.SpawnChance == 100) < optImpNum;

        if (loversData.Spawning)
        {
            if (loversData.OneIsImp)
            {
                roles[RoleAssignType.Crewmate].Add(new(CustomRoles.LovingCrewmate, 100, 1));
                roles[RoleAssignType.Impostor].Add(new(CustomRoles.LovingImpostor, 100, 1));
            }
            else roles[RoleAssignType.Crewmate].Add(new(CustomRoles.LovingCrewmate, 100, 2));
        }

        (OptionItem MinSetting, OptionItem MaxSetting) covenLimits = Options.FactionMinMaxSettings[Team.Coven];
        int numCovens;

        try { numCovens = rd.Next(covenLimits.MinSetting.GetInt(), covenLimits.MaxSetting.GetInt() + 1); }
        catch { numCovens = (int)(new[] { covenLimits.MinSetting.GetInt(), covenLimits.MinSetting.GetInt() + 1 }.Average()); }

        if (numCovens > 0 && Options.CovenLeaderSpawns.GetBool() && !Main.SetRoles.ContainsValue(CustomRoles.CovenLeader) && !ChatCommands.DraftResult.ContainsValue(CustomRoles.CovenLeader))
        {
            finalRolesList.Add(CustomRoles.CovenLeader);
            readyCovenNum++;
            readyRoleNum++;
        }

        (OptionItem MinSetting, OptionItem MaxSetting) neutralLimits = Options.FactionMinMaxSettings[Team.Neutral];
        int numNeutrals;

        try { numNeutrals = rd.Next(neutralLimits.MinSetting.GetInt(), neutralLimits.MaxSetting.GetInt() + 1); }
        catch { numNeutrals = (int)(new[] { neutralLimits.MinSetting.GetInt(), neutralLimits.MinSetting.GetInt() + 1 }.Average()); }

        if (roles[RoleAssignType.Impostor].Count == 0 && numNeutrals == 0 && !Main.SetRoles.Values.Any(x => x.IsImpostor() || x.IsNK()))
        {
            roles[RoleAssignType.Impostor].Add(new(CustomRoles.ImpostorEHR, 100, optImpNum));
            Logger.Warn("Adding Vanilla Impostor", "CustomRoleSelector");
        }

        if (roles[RoleAssignType.Crewmate].Count == 0 && numNeutrals == 0 && !Main.SetRoles.Values.Any(x => x.IsCrewmate()))
        {
            roles[RoleAssignType.Crewmate].Add(new(CustomRoles.CrewmateEHR, 100, playerCount - optImpNum));
            Logger.Warn("Adding Vanilla Crewmates", "CustomRoleSelector");
        }

        Logger.Info($"Number of Impostors: {optImpNum}", "FactionLimits");
        Logger.Info($"Number of Neutrals: {neutralLimits.MinSetting.GetInt()} - {neutralLimits.MaxSetting.GetInt()} => {numNeutrals}", "FactionLimits");
        Logger.Info($"Number of Coven members: {covenLimits.MinSetting.GetInt()} - {covenLimits.MaxSetting.GetInt()} => {numCovens}", "FactionLimits");

        Logger.Msg("=====================================================", "AllActiveRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Impostor].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "ImpRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.NeutralKilling].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NKRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.NonKillingNeutral].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NNKRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Crewmate].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CrewRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Madmate].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "MadmateRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Coven].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CovenRoles");
        Logger.Msg("=====================================================", "AllActiveRoles");

        Dictionary<RoleOptionType, int> subCategoryLimits;

        try
        {
            subCategoryLimits = Options.RoleSubCategoryLimits
                .Where(x => x.Key.GetTabFromOptionType() == TabGroup.NeutralRoles || x.Value[0].GetBool())
                .ToDictionary(x => x.Key, x => rd.Next(x.Value[1].GetInt(), x.Value[2].GetInt() + 1));
        }
        catch { subCategoryLimits = []; }

        try
        {
            Dictionary<RoleOptionType, int> impLimits = subCategoryLimits.Where(x => x.Key.GetTabFromOptionType() == TabGroup.ImpostorRoles).ToDictionary(x => x.Key, x => x.Value);

            if (impLimits.Count > 0 && impLimits.Sum(x => x.Value) < optImpNum)
                impLimits.Keys.Do(x => subCategoryLimits[x] = Options.RoleSubCategoryLimits[x][2].GetInt());
        }
        catch (Exception e) { Utils.ThrowException(e); }

        if (subCategoryLimits.Count > 0) Logger.Info($"Sub-Category Limits: {string.Join(", ", subCategoryLimits.Select(x => $"{x.Key}: {x.Value}"))}", "SubCategoryLimits");

        int nkLimit = subCategoryLimits[RoleOptionType.Neutral_Killing];
        int nnkLimit;

        try { nnkLimit = rd.Next(Options.MinNNKs.GetInt(), Options.MaxNNKs.GetInt() + 1); }
        catch { nnkLimit = (int)(new[] { Options.MinNNKs.GetInt(), Options.MaxNNKs.GetInt() + 1 }.Average()); }

        int madmateNum;

        try { madmateNum = rd.Next(Options.MinMadmateRoles.GetInt(), Options.MaxMadmateRoles.GetInt() + 1); }
        catch { madmateNum = (int)(new[] { Options.MinMadmateRoles.GetInt(), Options.MaxMadmateRoles.GetInt() + 1 }.Average()); }

        Logger.Info($"Number of Neutral Killing roles to select: {nkLimit}", "NeutralKillingLimit");
        Logger.Info($"Number of Non-Killing Neutral roles to select: {nnkLimit}", "NonKillingNeutralLimit");
        Logger.Info($"Number of Madmate roles to select: {madmateNum}", "MadmateLimit");

        Dictionary<RoleAssignType, List<RoleAssignInfo>> allRoles = roles.ToDictionary(x => x.Key, x => x.Value.ToList());

        roles.Keys.ToArray().Do(type => ApplySubCategoryLimits(type, subCategoryLimits));

        Logger.Msg("===================================================", "PreSelectedRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Impostor].Select(x => x.Role.ToString())), "PreSelectedImpostorRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.NeutralKilling].Select(x => x.Role.ToString())), "PreSelectedNKRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.NonKillingNeutral].Select(x => x.Role.ToString())), "PreSelectedNNKRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Crewmate].Select(x => x.Role.ToString())), "PreSelectedCrewRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Madmate].Select(x => x.Role.ToString())), "PreSelectedMadmateRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Coven].Select(x => x.Role.ToString())), "PreSelectedCovenRoles");
        Logger.Msg("===================================================", "PreSelectedRoles");

        try
        {
            var attempts = 0;
            List<RoleAssignType> types = [RoleAssignType.NeutralKilling, RoleAssignType.NonKillingNeutral];

            while (roles[RoleAssignType.NeutralKilling].Count + roles[RoleAssignType.NonKillingNeutral].Count > numNeutrals)
            {
                if (attempts++ > 100) break;

                if (types.FindFirst(x => roles[x].Count == 0, out RoleAssignType nullType)) types.Remove(nullType);
                if (types.Count == 0) break;
                RoleAssignType type = types.RandomElement();

                RoleAssignInfo toRemove = roles[type].RandomElement();
                roles[type].Remove(toRemove);

                Logger.Info($"Removed {toRemove.Role} from {type}", "CustomRoleSelector");
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        int nnkNum = roles[RoleAssignType.NonKillingNeutral].Count;
        int nkNum = roles[RoleAssignType.NeutralKilling].Count;

        Logger.Msg("======================================================", "SelectedRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Impostor].Select(x => x.Role.ToString())), "SelectedImpostorRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.NeutralKilling].Select(x => x.Role.ToString())), "SelectedNKRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.NonKillingNeutral].Select(x => x.Role.ToString())), "SelectedNNKRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Crewmate].Select(x => x.Role.ToString())), "SelectedCrewRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Madmate].Select(x => x.Role.ToString())), "SelectedMadmateRoles");
        Logger.Info(string.Join(", ", roles[RoleAssignType.Coven].Select(x => x.Role.ToString())), "SelectedCovenRoles");
        Logger.Msg("======================================================", "SelectedRoles");

        List<PlayerControl> allPlayers = Main.AllAlivePlayerControls.ToList();

        // Players on the EAC banned list will be assigned as GM when opening rooms
        if (BanManager.CheckEACList(PlayerControl.LocalPlayer.FriendCode, PlayerControl.LocalPlayer.GetClient().GetHashedPuid()))
        {
            Main.GM.Value = true;
            RoleResult[PlayerControl.LocalPlayer.PlayerId] = CustomRoles.GM;
            allPlayers.Remove(PlayerControl.LocalPlayer);
        }

        if (Main.GM.Value)
        {
            Logger.Warn("Host: GM", "CustomRoleSelector");
            allPlayers.RemoveAll(x => x.IsHost());
            RoleResult[PlayerControl.LocalPlayer.PlayerId] = CustomRoles.GM;
        }

        allPlayers.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));
        RoleResult.AddRange(ChatCommands.Spectators.ToDictionary(x => x, _ => CustomRoles.GM));

        Dictionary<byte, CustomRoles> preSetRoles = Main.SetRoles.AddRange(ChatCommands.DraftResult, false);

        if (ChatCommands.DraftResult.Count > 0 && ChatCommands.DraftResult.Count + preSetRoles.Count >= allPlayers.Count && preSetRoles.All(x => x.Value.GetCountTypes() is CountTypes.Crew or CountTypes.None or CountTypes.OutOfGame))
        {
            byte removeKey = ChatCommands.DraftResult.Keys.RandomElement();
            ChatCommands.DraftResult.Remove(removeKey);
            preSetRoles.Remove(removeKey);
        }

        // Pre-Assigned Roles By Host Are Selected First
        foreach ((byte id, CustomRoles role) in preSetRoles)
        {
            PlayerControl pc = allPlayers.FirstOrDefault(x => x.PlayerId == id);
            if (pc == null) continue;

            RoleResult[pc.PlayerId] = role;
            allPlayers.Remove(pc);

            if (role.IsCoven())
            {
                roles[RoleAssignType.Coven].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyCovenNum++;
            }
            else if (role.IsMadmate())
            {
                roles[RoleAssignType.Madmate].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyMadmateNum++;
            }
            else if (role.IsImpostor())
            {
                roles[RoleAssignType.Impostor].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyImpNum++;
            }
            else if (role.IsNK())
            {
                roles[RoleAssignType.NeutralKilling].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyNeutralKillingNum++;
            }
            else if (role.IsNonNK())
            {
                roles[RoleAssignType.NonKillingNeutral].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyNonNeutralKillingNum++;
            }
            else
                roles[RoleAssignType.Crewmate].DoIf(x => x.Role == role, x => x.AssignedCount++);

            readyRoleNum++;

            Logger.Warn($"Pre-Set Role Assigned: {pc.GetRealName()} => {role}", "CustomRoleSelector");
        }

        roles.Values.Do(l => l.DoIf(x => x.AssignedCount >= x.MaxCount, x => l.Remove(x), false));

        AssignRoles(RoleAssignType.Impostor, optImpNum, ref readyImpNum, ref readyRoleNum, playerCount, finalRolesList, roles);
        AssignRoles(RoleAssignType.NonKillingNeutral, nnkNum, ref readyNonNeutralKillingNum, ref readyRoleNum, playerCount, finalRolesList, roles);
        AssignRoles(RoleAssignType.NeutralKilling, nkNum, ref readyNeutralKillingNum, ref readyRoleNum, playerCount, finalRolesList, roles);
        AssignRoles(RoleAssignType.Madmate, madmateNum, ref readyMadmateNum, ref readyRoleNum, playerCount, finalRolesList, roles);
        AssignRoles(RoleAssignType.Coven, numCovens, ref readyCovenNum, ref readyRoleNum, playerCount, finalRolesList, roles);
        AssignRoles(RoleAssignType.Crewmate, playerCount - readyRoleNum, ref readyCrewmateNum, ref readyRoleNum, playerCount, finalRolesList, roles);
        
        if (readyRoleNum < playerCount && subCategoryLimits.Count > 0)
        {
            const RoleAssignType redoType = RoleAssignType.Crewmate;
            roles[redoType] = allRoles[redoType];

            subCategoryLimits = Options.RoleSubCategoryLimits
                .Where(x => x.Key.GetTabFromOptionType() == TabGroup.CrewmateRoles && x.Value[0].GetBool())
                .ToDictionary(x => x.Key, x => x.Value[2].GetInt());

            ApplySubCategoryLimits(redoType, subCategoryLimits);
            roles[redoType].DoIf(x => x.AssignedCount >= x.MaxCount, x => roles[redoType].Remove(x), false);
            AssignRoles(RoleAssignType.Crewmate, playerCount - readyRoleNum, ref readyCrewmateNum, ref readyRoleNum, playerCount, finalRolesList, roles);
        }

        if (rd.Next(0, 100) < Jester.SunnyboyChance.GetInt() && finalRolesList.Remove(CustomRoles.Jester)) finalRolesList.Add(CustomRoles.Sunnyboy);
        if (rd.Next(0, 100) < Arrogance.BardChance.GetInt() && finalRolesList.Remove(CustomRoles.Arrogance)) finalRolesList.Add(CustomRoles.Bard);
        if (rd.Next(0, 100) < Bomber.NukerChance.GetInt() && finalRolesList.Remove(CustomRoles.Bomber)) finalRolesList.Add(CustomRoles.Nuker);

        RoleResult.AddRange(allPlayers.Shuffle().Zip(finalRolesList.Shuffle()).ToDictionary(x => x.First.PlayerId, x => x.Second), false);
        Logger.Info(string.Join(", ", RoleResult.Values.Select(x => x.ToString())), "RoleResults");

        if (RoleResult.Count < allPlayers.Count) Logger.Error("Role assignment error: There are players who have not been assigned a role", "CustomRoleSelector");

        return;

        void AssignRoleToEveryone(CustomRoles role)
        {
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if ((Main.GM.Value && pc.IsHost()) || ChatCommands.Spectators.Contains(pc.PlayerId))
                {
                    RoleResult[pc.PlayerId] = CustomRoles.GM;
                    continue;
                }

                RoleResult[pc.PlayerId] = role;
            }
        }

        void ApplySubCategoryLimits(RoleAssignType type, Dictionary<RoleOptionType, int> dictionary) =>
            roles[type] = roles[type]
                .Shuffle()
                .OrderBy(x => x.SpawnChance != 100)
                .DistinctBy(x => x.Role)
                .Select(x => (
                    Info: x,
                    Limit: dictionary.TryGetValue(x.OptionType, out int limit)
                        ? (Exists: true, Value: limit)
                        : (Exists: false, Value: 0)))
                .GroupBy(x => x.Info.OptionType)
                .Select(x => (Grouping: x, x.FirstOrDefault().Limit))
                .SelectMany(x => x.Limit.Exists ? x.Grouping.Take(x.Limit.Value) : x.Grouping)
                .Shuffle()
                .OrderBy(x => x.Info.SpawnChance != 100)
                .ThenByDescending(x => x.Limit is { Exists: true, Value: > 0 })
                .Take(type switch
                {
                    RoleAssignType.Impostor => optImpNum,
                    RoleAssignType.NeutralKilling => nkLimit,
                    RoleAssignType.NonKillingNeutral => nnkLimit,
                    RoleAssignType.Coven => numCovens,
                    RoleAssignType.Madmate => madmateNum,
                    RoleAssignType.Crewmate => playerCount,
                    _ => 0
                })
                .Select(x => x.Info)
                .ToList();
        
        static RoleAssignInfo PickWeighted(List<RoleAssignInfo> pool, IRandom rng)
        {
            int totalWeight = pool.Sum(t => t.SpawnChance);
            if (totalWeight <= 0) return null;

            int roll = rng.Next(totalWeight);
            int cumulative = 0;

            foreach (var info in pool)
            {
                cumulative += info.SpawnChance;
                if (roll < cumulative) return info;
            }

            return null;
        }
        
        void AssignRoles(
            RoleAssignType type,
            int targetCount,
            ref int readyCategoryCount,
            ref int readyRoleNumInner,
            int playerCountInner,
            List<CustomRoles> finalRoles,
            Dictionary<RoleAssignType, List<RoleAssignInfo>> rolesInner
        )
        {
            if (targetCount <= 0) return;

            var list = rolesInner[type];

            // 1️⃣ Assign ALWAYS roles first (SpawnChance == 100)
            for (int i = 0; i < list.Count && readyCategoryCount < targetCount; i++)
            {
                var info = list[i];
                if (info.SpawnChance != 100) continue;

                while (info.AssignedCount < info.MaxCount &&
                       readyCategoryCount < targetCount &&
                       readyRoleNumInner < playerCountInner)
                {
                    finalRoles.Add(info.Role);
                    info.AssignedCount++;
                    readyCategoryCount++;
                    readyRoleNumInner++;
                }
            }

            // 2️⃣ Assign weighted roles
            while (readyCategoryCount < targetCount && readyRoleNumInner < playerCountInner)
            {
                // Build current valid pool
                List<RoleAssignInfo> pool = list.FindAll(info => info.SpawnChance > 0 && info.AssignedCount < info.MaxCount);

                if (pool.Count == 0) break;

                var chosen = PickWeighted(pool, rd);
                if (chosen == null) break;

                finalRoles.Add(chosen.Role);
                chosen.AssignedCount++;
                readyCategoryCount++;
                readyRoleNumInner++;
            }
        }
    }

    public static void CalculateVanillaRoleCount()
    {
        // Calculate the number of base roles
        AddEngineerNum = 0;
        AddScientistNum = 0;
        AddShapeshifterNum = 0;
        AddNoisemakerNum = 0;
        AddTrackerNum = 0;
        AddPhantomNum = 0;
        AddViperNum = 0;
        AddDetectiveNum = 0;

        foreach (CustomRoles role in RoleResult.Values)
        {
            try
            {
                switch (role.GetVNRole())
                {
                    case CustomRoles.Scientist:
                        AddScientistNum++;
                        break;
                    case CustomRoles.Engineer:
                        AddEngineerNum++;
                        break;
                    case CustomRoles.Shapeshifter:
                        AddShapeshifterNum++;
                        break;
                    case CustomRoles.Noisemaker:
                        AddNoisemakerNum++;
                        break;
                    case CustomRoles.Tracker:
                        AddTrackerNum++;
                        break;
                    case CustomRoles.Phantom:
                        AddPhantomNum++;
                        break;
                    case CustomRoles.Viper:
                        AddViperNum++;
                        break;
                    case CustomRoles.Detective:
                        AddDetectiveNum++;
                        break;
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }
    }

    public static void SelectAddonRoles()
    {
        if (Options.CurrentGameMode != CustomGameMode.Standard) return;

        foreach (byte id in Main.SetAddOns.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetAddOns.Remove(id);

        AddonRolesList = [];

        foreach (CustomRoles role in Enum.GetValues<CustomRoles>())
        {
            if (!role.IsAdditionRole() || role.IsGhostRole()) continue;

            switch (role)
            {
                case CustomRoles.Concealer or CustomRoles.Hidden when Options.AnonymousBodies.GetBool():
                case CustomRoles.Autopsy when Options.EveryoneSeesDeathReasons.GetBool():
                case CustomRoles.Gravestone when Options.EveryoneSeesDeadPlayersRoles.GetBool():
                case CustomRoles.Mare or CustomRoles.Glow or CustomRoles.Sleep when Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.Madmate when Options.MadmateSpawnMode.GetInt() != 0:
                case CustomRoles.Lovers or CustomRoles.LastImpostor or CustomRoles.Workhorse or CustomRoles.Undead or CustomRoles.Insane:
                case CustomRoles.Nimble or CustomRoles.Physicist or CustomRoles.Bloodlust or CustomRoles.Finder or CustomRoles.Noisy or CustomRoles.Examiner or CustomRoles.Venom: // Assigned at a different function due to role base change
                    continue;
            }

            AddonRolesList.Add(role);
        }
    }

    private enum RoleAssignType
    {
        Impostor,
        NeutralKilling,
        NonKillingNeutral,
        Crewmate,
        Madmate,
        Coven
    }

    private class RoleAssignInfo(CustomRoles role, int spawnChance, int maxCount)
    {
        public CustomRoles Role => role;
        public int SpawnChance { get; set; } = spawnChance;

        public int MaxCount => maxCount;
        public int AssignedCount { get; set; }
        public RoleOptionType OptionType { get; } = role.GetRoleOptionType();
    }

}
