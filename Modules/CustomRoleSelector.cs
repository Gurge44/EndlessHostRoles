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
        { CustomGameMode.SoloKombat, CustomRoles.KB_Normal },
        { CustomGameMode.FFA, CustomRoles.Killer },
        { CustomGameMode.MoveAndStop, CustomRoles.Tasker },
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
        { CustomGameMode.Mingle, CustomRoles.MinglePlayer }
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
                case CustomRoles.Refugee:
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
            readyImpNum += 2;
            readyRoleNum++;
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

        if (ChatCommands.DraftResult.Count > 0 && ChatCommands.DraftResult.Count + Main.SetRoles.Count >= allPlayers.Count && preSetRoles.All(x => x.Value.GetCountTypes() is CountTypes.Crew or CountTypes.None or CountTypes.OutOfGame))
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

        RoleAssignInfo[] imps;
        RoleAssignInfo[] nnKs = [];
        RoleAssignInfo[] nKs = [];
        RoleAssignInfo[] mads = [];
        RoleAssignInfo[] coven = [];
        RoleAssignInfo[] crews = [];

        // Impostor Roles
        {
            List<CustomRoles> alwaysImpRoles = [];
            List<CustomRoles> chanceImpRoles = [];

            for (var i = 0; i < roles[RoleAssignType.Impostor].Count; i++)
            {
                RoleAssignInfo item = roles[RoleAssignType.Impostor][i];

                if (item.SpawnChance == 100)
                {
                    for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        alwaysImpRoles.Add(item.Role);
                }
                else
                {
                    for (var j = 0; j < item.SpawnChance / 5; j++)
                    {
                        for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            chanceImpRoles.Add(item.Role);
                    }
                }
            }

            RoleAssignInfo[] impRoleCounts = alwaysImpRoles.Distinct().Select(GetAssignInfo).Concat(chanceImpRoles.Distinct().Select(GetAssignInfo)).ToArray();
            imps = impRoleCounts;

            // Assign roles set to ALWAYS
            if (readyImpNum < optImpNum)
            {
                while (alwaysImpRoles.Count > 0)
                {
                    CustomRoles selected = alwaysImpRoles.RandomElement();
                    RoleAssignInfo info = impRoleCounts.FirstOrDefault(x => x.Role == selected);
                    alwaysImpRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    finalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyImpNum++;

                    imps = impRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyImpNum >= optImpNum) break;
                }
            }

            // Assign other roles when needed
            if (readyRoleNum < playerCount && readyImpNum < optImpNum)
            {
                while (chanceImpRoles.Count > 0)
                {
                    CustomRoles selected = chanceImpRoles.RandomElement();
                    RoleAssignInfo info = impRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (var i = 0; i < info.SpawnChance / 5; i++) chanceImpRoles.Remove(selected);

                    finalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyImpNum++;

                    imps = impRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                    {
                        while (chanceImpRoles.Contains(selected))
                            chanceImpRoles.Remove(selected);
                    }

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyImpNum >= optImpNum) break;
                }
            }
        }

        // Neutral Roles
        {
            // Neutral Non-Killing Roles
            {
                List<CustomRoles> alwaysNNKRoles = [];
                List<CustomRoles> chanceNNKRoles = [];

                for (var i = 0; i < roles[RoleAssignType.NonKillingNeutral].Count; i++)
                {
                    RoleAssignInfo item = roles[RoleAssignType.NonKillingNeutral][i];

                    if (item.SpawnChance == 100)
                    {
                        for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                            alwaysNNKRoles.Add(item.Role);
                    }
                    else
                    {
                        for (var j = 0; j < item.SpawnChance / 5; j++)
                        {
                            for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                                chanceNNKRoles.Add(item.Role);
                        }
                    }
                }

                RoleAssignInfo[] nnkRoleCounts = alwaysNNKRoles.Distinct().Select(GetAssignInfo).Concat(chanceNNKRoles.Distinct().Select(GetAssignInfo)).ToArray();
                nnKs = nnkRoleCounts;

                // Assign roles set to ALWAYS
                if (readyNonNeutralKillingNum < nnkNum)
                {
                    while (alwaysNNKRoles.Count > 0 && nnkNum > 0)
                    {
                        CustomRoles selected = alwaysNNKRoles.RandomElement();
                        RoleAssignInfo info = nnkRoleCounts.FirstOrDefault(x => x.Role == selected);
                        alwaysNNKRoles.Remove(selected);
                        if (info.AssignedCount >= info.MaxCount) continue;

                        finalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNonNeutralKillingNum++;

                        nnKs = nnkRoleCounts;

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNonNeutralKillingNum >= nnkNum) break;
                    }
                }

                // Assign other roles when needed
                if (readyRoleNum < playerCount && readyNonNeutralKillingNum < nnkNum)
                {
                    while (chanceNNKRoles.Count > 0 && nnkNum > 0)
                    {
                        CustomRoles selected = chanceNNKRoles.RandomElement();
                        RoleAssignInfo info = nnkRoleCounts.FirstOrDefault(x => x.Role == selected);
                        for (var i = 0; i < info.SpawnChance / 5; i++) chanceNNKRoles.Remove(selected);

                        finalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNonNeutralKillingNum++;

                        nnKs = nnkRoleCounts;

                        if (info.AssignedCount >= info.MaxCount)
                        {
                            while (chanceNNKRoles.Contains(selected))
                                chanceNNKRoles.Remove(selected);
                        }

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNonNeutralKillingNum >= nnkNum) break;
                    }
                }
            }

            // Neutral Killing Roles
            {
                List<CustomRoles> alwaysNKRoles = [];
                List<CustomRoles> chanceNKRoles = [];

                for (var i = 0; i < roles[RoleAssignType.NeutralKilling].Count; i++)
                {
                    RoleAssignInfo item = roles[RoleAssignType.NeutralKilling][i];

                    if (item.SpawnChance == 100)
                    {
                        for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                            alwaysNKRoles.Add(item.Role);
                    }
                    else
                    {
                        for (var j = 0; j < item.SpawnChance / 5; j++)
                        {
                            for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                                chanceNKRoles.Add(item.Role);
                        }
                    }
                }

                RoleAssignInfo[] nkRoleCounts = alwaysNKRoles.Distinct().Select(GetAssignInfo).Concat(chanceNKRoles.Distinct().Select(GetAssignInfo)).ToArray();
                nKs = nkRoleCounts;

                // Assign roles set to ALWAYS
                if (readyNeutralKillingNum < nkNum)
                {
                    while (alwaysNKRoles.Count > 0 && nkNum > 0)
                    {
                        CustomRoles selected = alwaysNKRoles.RandomElement();
                        RoleAssignInfo info = nkRoleCounts.FirstOrDefault(x => x.Role == selected);
                        alwaysNKRoles.Remove(selected);
                        if (info.AssignedCount >= info.MaxCount) continue;

                        finalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNeutralKillingNum++;

                        nKs = nkRoleCounts;

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNeutralKillingNum >= nkNum) break;
                    }
                }

                // Assign other roles when needed
                if (readyRoleNum < playerCount && readyNeutralKillingNum < nkNum)
                {
                    while (chanceNKRoles.Count > 0 && nkNum > 0)
                    {
                        CustomRoles selected = chanceNKRoles.RandomElement();
                        RoleAssignInfo info = nkRoleCounts.FirstOrDefault(x => x.Role == selected);
                        for (var i = 0; i < info.SpawnChance / 5; i++) chanceNKRoles.Remove(selected);

                        finalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNeutralKillingNum++;

                        nKs = nkRoleCounts;

                        if (info.AssignedCount >= info.MaxCount)
                        {
                            while (chanceNKRoles.Contains(selected))
                                chanceNKRoles.Remove(selected);
                        }

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNeutralKillingNum >= nkNum) break;
                    }
                }
            }
        }

        // Madmate Roles
        {
            List<CustomRoles> alwaysMadmateRoles = [];
            List<CustomRoles> chanceMadmateRoles = [];

            for (var i = 0; i < roles[RoleAssignType.Madmate].Count; i++)
            {
                RoleAssignInfo item = roles[RoleAssignType.Madmate][i];

                if (item.SpawnChance == 100)
                {
                    for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        alwaysMadmateRoles.Add(item.Role);
                }
                else
                {
                    for (var j = 0; j < item.SpawnChance / 5; j++)
                    {
                        for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            chanceMadmateRoles.Add(item.Role);
                    }
                }
            }

            RoleAssignInfo[] madRoleCounts = alwaysMadmateRoles.Distinct().Select(GetAssignInfo).Concat(chanceMadmateRoles.Distinct().Select(GetAssignInfo)).ToArray();
            mads = madRoleCounts;

            // Assign roles set to ALWAYS
            if (readyRoleNum < playerCount && readyMadmateNum < madmateNum)
            {
                while (alwaysMadmateRoles.Count > 0)
                {
                    CustomRoles selected = alwaysMadmateRoles.RandomElement();
                    RoleAssignInfo info = madRoleCounts.FirstOrDefault(x => x.Role == selected);
                    alwaysMadmateRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    finalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyMadmateNum++;

                    mads = madRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyMadmateNum >= madmateNum) break;
                }
            }

            // Assign other roles when needed
            if (readyRoleNum < playerCount && readyMadmateNum < madmateNum)
            {
                while (chanceMadmateRoles.Count > 0)
                {
                    CustomRoles selected = chanceMadmateRoles.RandomElement();
                    RoleAssignInfo info = madRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (var i = 0; i < info.SpawnChance / 5; i++) chanceMadmateRoles.Remove(selected);

                    finalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyMadmateNum++;

                    mads = madRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                    {
                        while (chanceMadmateRoles.Contains(selected))
                            chanceMadmateRoles.Remove(selected);
                    }

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyMadmateNum >= madmateNum) break;
                }
            }
        }

        // Coven Roles
        {
            List<CustomRoles> alwaysCovenRoles = [];
            List<CustomRoles> chanceCovenRoles = [];

            for (var i = 0; i < roles[RoleAssignType.Coven].Count; i++)
            {
                RoleAssignInfo item = roles[RoleAssignType.Coven][i];

                if (item.SpawnChance == 100)
                {
                    for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        alwaysCovenRoles.Add(item.Role);
                }
                else
                {
                    for (var j = 0; j < item.SpawnChance / 5; j++)
                    {
                        for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            chanceCovenRoles.Add(item.Role);
                    }
                }
            }

            RoleAssignInfo[] covenRoleCounts = alwaysCovenRoles.Distinct().Select(GetAssignInfo).Concat(chanceCovenRoles.Distinct().Select(GetAssignInfo)).ToArray();
            coven = covenRoleCounts;

            // Assign roles set to ALWAYS
            if (readyCovenNum < numCovens)
            {
                while (alwaysCovenRoles.Count > 0)
                {
                    CustomRoles selected = alwaysCovenRoles.RandomElement();
                    RoleAssignInfo info = covenRoleCounts.FirstOrDefault(x => x.Role == selected);
                    alwaysCovenRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    finalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyCovenNum++;

                    coven = covenRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyCovenNum >= numCovens) break;
                }
            }

            // Assign other roles when needed
            if (readyRoleNum < playerCount && readyCovenNum < numCovens)
            {
                while (chanceCovenRoles.Count > 0)
                {
                    CustomRoles selected = chanceCovenRoles.RandomElement();
                    RoleAssignInfo info = covenRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (var i = 0; i < info.SpawnChance / 5; i++) chanceCovenRoles.Remove(selected);

                    finalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyCovenNum++;

                    coven = covenRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                    {
                        while (chanceCovenRoles.Contains(selected))
                            chanceCovenRoles.Remove(selected);
                    }

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyCovenNum >= numCovens) break;
                }
            }
        }

        // Crewmate Roles
        {
            var attempts = 0;

            Crew:

            if (attempts++ > 10) goto EndOfAssign;

            List<CustomRoles> alwaysCrewRoles = [];
            List<CustomRoles> chanceCrewRoles = [];

            for (var i = 0; i < roles[RoleAssignType.Crewmate].Count; i++)
            {
                RoleAssignInfo item = roles[RoleAssignType.Crewmate][i];

                if (item.SpawnChance == 100)
                {
                    for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        alwaysCrewRoles.Add(item.Role);
                }
                else
                {
                    for (var j = 0; j < item.SpawnChance / 5; j++)
                    {
                        for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            chanceCrewRoles.Add(item.Role);
                    }
                }
            }

            RoleAssignInfo[] crewRoleCounts = alwaysCrewRoles.Distinct().Select(GetAssignInfo).Concat(chanceCrewRoles.Distinct().Select(GetAssignInfo)).ToArray();
            crews = crewRoleCounts;

            // Assign roles set to ALWAYS
            if (readyRoleNum < playerCount)
            {
                while (alwaysCrewRoles.Count > 0)
                {
                    CustomRoles selected = alwaysCrewRoles.RandomElement();
                    RoleAssignInfo info = crewRoleCounts.FirstOrDefault(x => x.Role == selected);
                    alwaysCrewRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    finalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;

                    crews = crewRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                }
            }

            // Assign other roles when needed
            if (readyRoleNum < playerCount)
            {
                while (chanceCrewRoles.Count > 0)
                {
                    CustomRoles selected = chanceCrewRoles.RandomElement();
                    RoleAssignInfo info = crewRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (var i = 0; i < info.SpawnChance / 5; i++) chanceCrewRoles.Remove(selected);

                    finalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;

                    crews = crewRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                    {
                        while (chanceCrewRoles.Contains(selected))
                            chanceCrewRoles.Remove(selected);
                    }

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                }
            }

            if (readyRoleNum < playerCount && subCategoryLimits.Count > 0)
            {
                const RoleAssignType redoType = RoleAssignType.Crewmate;
                roles[redoType] = allRoles[redoType];

                subCategoryLimits = Options.RoleSubCategoryLimits
                    .Where(x => x.Key.GetTabFromOptionType() == TabGroup.CrewmateRoles && x.Value[0].GetBool())
                    .ToDictionary(x => x.Key, x => x.Value[2].GetInt());

                ApplySubCategoryLimits(redoType, subCategoryLimits);
                roles[redoType].DoIf(x => x.AssignedCount >= x.MaxCount, x => roles[redoType].Remove(x), false);
                goto Crew;
            }
        }

        EndOfAssign:

        if (imps.Length > 0) Logger.Info(string.Join(", ", imps.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "ImpRoleResult");
        if (nnKs.Length > 0) Logger.Info(string.Join(", ", nnKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NNKRoleResult");
        if (nKs.Length > 0) Logger.Info(string.Join(", ", nKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NKRoleResult");
        if (crews.Length > 0) Logger.Info(string.Join(", ", crews.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "CrewRoleResult");
        if (mads.Length > 0) Logger.Info(string.Join(", ", mads.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "MadRoleResult");
        if (coven.Length > 0) Logger.Info(string.Join(", ", coven.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "CovenRoleResult");

        if (rd.Next(0, 100) < Jester.SunnyboyChance.GetInt() && finalRolesList.Remove(CustomRoles.Jester)) finalRolesList.Add(CustomRoles.Sunnyboy);
        if (rd.Next(0, 100) < Arrogance.BardChance.GetInt() && finalRolesList.Remove(CustomRoles.Arrogance)) finalRolesList.Add(CustomRoles.Bard);
        if (rd.Next(0, 100) < Bomber.NukerChance.GetInt() && finalRolesList.Remove(CustomRoles.Bomber)) finalRolesList.Add(CustomRoles.Nuker);

        RoleResult.AddRange(allPlayers.Zip(finalRolesList.Shuffle()).ToDictionary(x => x.First.PlayerId, x => x.Second), false);
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

        RoleAssignInfo GetAssignInfo(CustomRoles role) => roles.Values.FirstOrDefault(x => x.Any(y => y.Role == role))?.FirstOrDefault(x => x.Role == role);

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
                .OrderByDescending(x => x.Limit is { Exists: true, Value: > 0 })
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
                case CustomRoles.Concealer when Options.AnonymousBodies.GetBool():
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