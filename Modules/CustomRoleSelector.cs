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

    public static int AddScientistNum;
    public static int AddEngineerNum;
    public static int AddShapeshifterNum;
    public static int AddNoisemakerNum;
    public static int AddTrackerNum;
    public static int AddPhantomNum;

    public static List<CustomRoles> AddonRolesList = [];

    public static void SelectCustomRoles()
    {
        RoleResult = [];

        if (Main.GM.Value && Main.AllPlayerControls.Length == 1) return;

        if (Options.CurrentGameMode != CustomGameMode.Standard)
        {
            Dictionary<CustomGameMode, CustomRoles> gameModeRoles = new()
            {
                { CustomGameMode.SoloKombat, CustomRoles.KB_Normal },
                { CustomGameMode.FFA, CustomRoles.Killer },
                { CustomGameMode.MoveAndStop, CustomRoles.Tasker },
                { CustomGameMode.HotPotato, CustomRoles.Potato },
                { CustomGameMode.Speedrun, CustomRoles.Runner },
                { CustomGameMode.CaptureTheFlag, CustomRoles.CTFPlayer },
                { CustomGameMode.NaturalDisasters, CustomRoles.NDPlayer },
                { CustomGameMode.RoomRush, CustomRoles.RRPlayer }
            };

            if (gameModeRoles.TryGetValue(Options.CurrentGameMode, out var role))
            {
                AssignRoleToEveryone(role);
                return;
            }

            bool hns = Options.CurrentGameMode == CustomGameMode.HideAndSeek;

            if (Options.CurrentGameMode == CustomGameMode.AllInOne)
            {
                var prioritizedGameMode = AllInOneGameMode.GetPrioritizedGameModeForRoles();

                if (gameModeRoles.TryGetValue(prioritizedGameMode, out var allInOneRole))
                {
                    AssignRoleToEveryone(allInOneRole);
                    return;
                }

                hns = prioritizedGameMode == CustomGameMode.HideAndSeek;
            }

            if (hns)
            {
                HnSManager.AssignRoles();
                RoleResult = HnSManager.PlayerRoles.ToDictionary(x => x.Key, x => x.Value.Role);
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

        List<CustomRoles> FinalRolesList = [];

        Dictionary<RoleAssignType, List<RoleAssignInfo>> Roles = [];
        Enum.GetValues<RoleAssignType>().Do(x => Roles[x] = []);

        foreach (byte id in Main.SetRoles.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetRoles.Remove(id);

        (bool Spawning, bool OneIsImp) LoversData = (Lovers.LegacyLovers.GetBool(), rd.Next(100) < Lovers.LovingImpostorSpawnChance.GetInt());
        LoversData.Spawning &= rd.Next(100) < Options.CustomAdtRoleSpawnRate[CustomRoles.Lovers].GetInt();

        foreach (CustomRoles role in Enum.GetValues<CustomRoles>())
        {
            int chance = role.GetMode();
            if (role.IsVanilla() || chance == 0 || role.IsAdditionRole() || (role.OnlySpawnsWithPets() && !Options.UsePets.GetBool()) || (role != CustomRoles.Randomizer && role.IsCrewmate() && Options.AprilFoolsMode.GetBool()) || HnSManager.AllHnSRoles.Contains(role)) continue;

            switch (role)
            {
                case CustomRoles.Doctor when Options.EveryoneSeesDeathReasons.GetBool():
                case CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor when !LoversData.Spawning:
                case CustomRoles.Commander when optImpNum <= 1 && Commander.CannotSpawnAsSoloImp.GetBool():
                case CustomRoles.Changeling when Changeling.GetAvailableRoles(true).Count == 0:
                case CustomRoles.Camouflager when Camouflager.DoesntSpawnOnFungle.GetBool() && Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.DarkHide when Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.Pelican when Roles[RoleAssignType.Impostor].Any(x => x.Role == CustomRoles.Duellist):
                case CustomRoles.Duellist when Roles[RoleAssignType.NeutralKilling].Any(x => x.Role == CustomRoles.Pelican):
                case CustomRoles.VengefulRomantic:
                case CustomRoles.RuthlessRomantic:
                case CustomRoles.Deathknight:
                case CustomRoles.Convict:
                case CustomRoles.Refugee:
                case CustomRoles.CovenLeader:
                case CustomRoles.GM:
                case CustomRoles.NotAssigned:
                    continue;
            }

            int count = role.GetCount();

            if (role == CustomRoles.Randomizer && Options.AprilFoolsMode.GetBool())
            {
                chance = 100;
                count = 15;
            }

            RoleAssignInfo info = new(role, chance, count);

            if (role.IsCoven()) Roles[RoleAssignType.Coven].Add(info);
            else if (role.IsMadmate()) Roles[RoleAssignType.Madmate].Add(info);
            else if (role.IsImpostor() && role != CustomRoles.DoubleAgent) Roles[RoleAssignType.Impostor].Add(info);
            else if (role.IsNK()) Roles[RoleAssignType.NeutralKilling].Add(info);
            else if (role.IsNonNK()) Roles[RoleAssignType.NonKillingNeutral].Add(info);
            else Roles[RoleAssignType.Crewmate].Add(info);
        }

        LoversData.OneIsImp &= Roles[RoleAssignType.Impostor].Count(x => x.SpawnChance == 100) < optImpNum;

        if (LoversData.Spawning)
        {
            if (LoversData.OneIsImp)
            {
                Roles[RoleAssignType.Crewmate].Add(new(CustomRoles.LovingCrewmate, 100, 1));
                Roles[RoleAssignType.Impostor].Add(new(CustomRoles.LovingImpostor, 100, 1));
            }
            else Roles[RoleAssignType.Crewmate].Add(new(CustomRoles.LovingCrewmate, 100, 2));
        }

        var covenLimits = Options.FactionMinMaxSettings[Team.Coven];
        var numCovens = IRandom.Instance.Next(covenLimits.MinSetting.GetInt(), covenLimits.MaxSetting.GetInt() + 1);

        if (numCovens > 0)
        {
            FinalRolesList.Add(CustomRoles.CovenLeader);
            readyCovenNum++;
            readyRoleNum++;
        }

        var neutralLimits = Options.FactionMinMaxSettings[Team.Neutral];
        var numNeutrals = IRandom.Instance.Next(neutralLimits.MinSetting.GetInt(), neutralLimits.MaxSetting.GetInt() + 1);

        if (Roles[RoleAssignType.Impostor].Count == 0 && numNeutrals == 0 && !Main.SetRoles.Values.Any(x => x.IsImpostor() || x.IsNK()))
        {
            Roles[RoleAssignType.Impostor].Add(new(CustomRoles.ImpostorEHR, 100, optImpNum));
            Logger.Warn("Adding Vanilla Impostor", "CustomRoleSelector");
        }

        if (Roles[RoleAssignType.Crewmate].Count == 0 && numNeutrals == 0 && !Main.SetRoles.Values.Any(x => x.IsCrewmate()))
        {
            Roles[RoleAssignType.Crewmate].Add(new(CustomRoles.CrewmateEHR, 100, playerCount));
            Logger.Warn("Adding Vanilla Crewmates", "CustomRoleSelector");
        }

        Logger.Info($"Number of Impostors: {optImpNum}", "FactionLimits");
        Logger.Info($"Number of Neutrals: {neutralLimits.MinSetting.GetInt()} - {neutralLimits.MaxSetting.GetInt()} => {numNeutrals}", "FactionLimits");

        Logger.Msg("=====================================================", "AllActiveRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "ImpRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CrewRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Madmate].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "MadmateRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Coven].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CovenRoles");
        Logger.Msg("=====================================================", "AllActiveRoles");

        Dictionary<RoleOptionType, int> subCategoryLimits = Options.RoleSubCategoryLimits
            .Where(x => x.Key.GetTabFromOptionType() == TabGroup.NeutralRoles || x.Value[0].GetBool())
            .ToDictionary(x => x.Key, x => IRandom.Instance.Next(x.Value[1].GetInt(), x.Value[2].GetInt() + 1));

        try
        {
            var impLimits = subCategoryLimits.Where(x => x.Key.GetTabFromOptionType() == TabGroup.ImpostorRoles).ToDictionary(x => x.Key, x => x.Value);

            if (impLimits.Count > 0 && impLimits.Sum(x => x.Value) < optImpNum)
            {
                // ReSharper disable once AccessToModifiedClosure
                impLimits.Keys.Do(x => subCategoryLimits[x] = Options.RoleSubCategoryLimits[x][2].GetInt());
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        if (subCategoryLimits.Count > 0) Logger.Info($"Sub-Category Limits: {string.Join(", ", subCategoryLimits.Select(x => $"{x.Key}: {x.Value}"))}", "SubCategoryLimits");

        int nkLimit = subCategoryLimits[RoleOptionType.Neutral_Killing];
        int nnkLimit = subCategoryLimits[RoleOptionType.Neutral_Evil] + subCategoryLimits[RoleOptionType.Neutral_Benign];

        Logger.Info($"Number of Neutral Killing roles to select: {nkLimit}", "NeutralKillingLimit");
        Logger.Info($"Number of Non-Killing Neutral roles to select: {nnkLimit}", "NonKillingNeutralLimit");

        var allRoles = Roles.ToDictionary(x => x.Key, x => x.Value.ToList());

        Roles.Keys.ToArray().Do(type => ApplySubCategoryLimits(type, subCategoryLimits));

        Logger.Msg("===================================================", "PreSelectedRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => x.Role.ToString())), "PreSelectedImpostorRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => x.Role.ToString())), "PreSelectedNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => x.Role.ToString())), "PreSelectedNNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => x.Role.ToString())), "PreSelectedCrewRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Madmate].Select(x => x.Role.ToString())), "PreSelectedMadmateRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Coven].Select(x => x.Role.ToString())), "PreSelectedCovenRoles");
        Logger.Msg("===================================================", "PreSelectedRoles");

        try
        {
            int attempts = 0;
            List<RoleAssignType> types = [RoleAssignType.NeutralKilling, RoleAssignType.NonKillingNeutral];

            while (Roles[RoleAssignType.NeutralKilling].Count + Roles[RoleAssignType.NonKillingNeutral].Count > numNeutrals)
            {
                if (attempts++ > 100) break;

                if (types.FindFirst(x => Roles[x].Count == 0, out var nullType)) types.Remove(nullType);
                if (types.Count == 0) break;
                RoleAssignType type = types.RandomElement();

                var toRemove = Roles[type].RandomElement();
                Roles[type].Remove(toRemove);

                Logger.Info($"Removed {toRemove.Role} from {type}", "CustomRoleSelector");
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        int nnkNum = Roles[RoleAssignType.NonKillingNeutral].Count;
        int nkNum = Roles[RoleAssignType.NeutralKilling].Count;

        int madmateNum = IRandom.Instance.Next(Options.MinMadmateRoles.GetInt(), Options.MaxMadmateRoles.GetInt() + 1);

        Logger.Msg("======================================================", "SelectedRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => x.Role.ToString())), "SelectedImpostorRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => x.Role.ToString())), "SelectedNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => x.Role.ToString())), "SelectedNNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => x.Role.ToString())), "SelectedCrewRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Madmate].Select(x => x.Role.ToString())), "SelectedMadmateRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Coven].Select(x => x.Role.ToString())), "SelectedCovenRoles");
        Logger.Msg("======================================================", "SelectedRoles");

        List<PlayerControl> AllPlayers = Main.AllAlivePlayerControls.ToList();

        // Players on the EAC banned list will be assigned as GM when opening rooms
        if (BanManager.CheckEACList(PlayerControl.LocalPlayer.FriendCode, PlayerControl.LocalPlayer.GetClient().GetHashedPuid()))
        {
            Main.GM.Value = true;
            RoleResult[PlayerControl.LocalPlayer.PlayerId] = CustomRoles.GM;
            AllPlayers.Remove(PlayerControl.LocalPlayer);
        }

        if (Main.GM.Value)
        {
            Logger.Warn("Host: GM", "CustomRoleSelector");
            AllPlayers.RemoveAll(x => x.IsHost());
            RoleResult[PlayerControl.LocalPlayer.PlayerId] = CustomRoles.GM;
        }

        AllPlayers.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));
        RoleResult.AddRange(ChatCommands.Spectators.ToDictionary(x => x, _ => CustomRoles.GM));

        // Pre-Assigned Roles By Host Are Selected First
        foreach ((byte id, CustomRoles role) in Main.SetRoles.AddRange(ChatCommands.DraftResult, false))
        {
            PlayerControl pc = AllPlayers.FirstOrDefault(x => x.PlayerId == id);
            if (pc == null) continue;

            RoleResult[pc.PlayerId] = role;
            AllPlayers.Remove(pc);

            if (role.IsCoven())
            {
                Roles[RoleAssignType.Coven].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyCovenNum++;
            }
            else if (role.IsMadmate())
            {
                Roles[RoleAssignType.Madmate].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyMadmateNum++;
            }
            else if (role.IsImpostor())
            {
                Roles[RoleAssignType.Impostor].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyImpNum++;
            }
            else if (role.IsNK())
            {
                Roles[RoleAssignType.NeutralKilling].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyNeutralKillingNum++;
            }
            else if (role.IsNonNK())
            {
                Roles[RoleAssignType.NonKillingNeutral].DoIf(x => x.Role == role, x => x.AssignedCount++);
                readyNonNeutralKillingNum++;
            }
            else
                Roles[RoleAssignType.Crewmate].DoIf(x => x.Role == role, x => x.AssignedCount++);

            readyRoleNum++;

            Logger.Warn($"Pre-Set Role Assigned: {pc.GetRealName()} => {role}", "CustomRoleSelector");
        }

        Roles.Values.Do(l => l.DoIf(x => x.AssignedCount >= x.MaxCount, x => l.Remove(x), false));

        RoleAssignInfo[] Imps;
        RoleAssignInfo[] NNKs = [];
        RoleAssignInfo[] NKs = [];
        RoleAssignInfo[] Mads = [];
        RoleAssignInfo[] Coven = [];
        RoleAssignInfo[] Crews = [];

        // Impostor Roles
        {
            List<CustomRoles> AlwaysImpRoles = [];
            List<CustomRoles> ChanceImpRoles = [];

            for (var i = 0; i < Roles[RoleAssignType.Impostor].Count; i++)
            {
                RoleAssignInfo item = Roles[RoleAssignType.Impostor][i];

                if (item.SpawnChance == 100)
                    for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        AlwaysImpRoles.Add(item.Role);
                else
                {
                    for (var j = 0; j < item.SpawnChance / 5; j++)
                        for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            ChanceImpRoles.Add(item.Role);
                }
            }

            RoleAssignInfo[] ImpRoleCounts = AlwaysImpRoles.Distinct().Select(GetAssignInfo).Concat(ChanceImpRoles.Distinct().Select(GetAssignInfo)).ToArray();
            Imps = ImpRoleCounts;

            // Assign roles set to ALWAYS
            if (readyImpNum < optImpNum)
            {
                while (AlwaysImpRoles.Count > 0)
                {
                    CustomRoles selected = AlwaysImpRoles.RandomElement();
                    RoleAssignInfo info = ImpRoleCounts.FirstOrDefault(x => x.Role == selected);
                    AlwaysImpRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyImpNum++;

                    Imps = ImpRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyImpNum >= optImpNum) break;
                }
            }

            // Assign other roles when needed
            if (readyRoleNum < playerCount && readyImpNum < optImpNum)
            {
                while (ChanceImpRoles.Count > 0)
                {
                    CustomRoles selected = ChanceImpRoles.RandomElement();
                    RoleAssignInfo info = ImpRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (var i = 0; i < info.SpawnChance / 5; i++) ChanceImpRoles.Remove(selected);

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyImpNum++;

                    Imps = ImpRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                        while (ChanceImpRoles.Contains(selected))
                            ChanceImpRoles.Remove(selected);

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyImpNum >= optImpNum) break;
                }
            }
        }

        // Neutral Roles
        {
            // Neutral Non-Killing Roles
            {
                List<CustomRoles> AlwaysNNKRoles = [];
                List<CustomRoles> ChanceNNKRoles = [];

                for (var i = 0; i < Roles[RoleAssignType.NonKillingNeutral].Count; i++)
                {
                    RoleAssignInfo item = Roles[RoleAssignType.NonKillingNeutral][i];

                    if (item.SpawnChance == 100)
                        for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                            AlwaysNNKRoles.Add(item.Role);
                    else
                    {
                        for (var j = 0; j < item.SpawnChance / 5; j++)
                            for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                                ChanceNNKRoles.Add(item.Role);
                    }
                }

                RoleAssignInfo[] NNKRoleCounts = AlwaysNNKRoles.Distinct().Select(GetAssignInfo).Concat(ChanceNNKRoles.Distinct().Select(GetAssignInfo)).ToArray();
                NNKs = NNKRoleCounts;

                // Assign roles set to ALWAYS
                if (readyNonNeutralKillingNum < nnkNum)
                {
                    while (AlwaysNNKRoles.Count > 0 && nnkNum > 0)
                    {
                        CustomRoles selected = AlwaysNNKRoles.RandomElement();
                        RoleAssignInfo info = NNKRoleCounts.FirstOrDefault(x => x.Role == selected);
                        AlwaysNNKRoles.Remove(selected);
                        if (info.AssignedCount >= info.MaxCount) continue;

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNonNeutralKillingNum++;

                        NNKs = NNKRoleCounts;

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNonNeutralKillingNum >= nnkNum) break;
                    }
                }

                // Assign other roles when needed
                if (readyRoleNum < playerCount && readyNonNeutralKillingNum < nnkNum)
                {
                    while (ChanceNNKRoles.Count > 0 && nnkNum > 0)
                    {
                        CustomRoles selected = ChanceNNKRoles.RandomElement();
                        RoleAssignInfo info = NNKRoleCounts.FirstOrDefault(x => x.Role == selected);
                        for (var i = 0; i < info.SpawnChance / 5; i++) ChanceNNKRoles.Remove(selected);

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNonNeutralKillingNum++;

                        NNKs = NNKRoleCounts;

                        if (info.AssignedCount >= info.MaxCount)
                            while (ChanceNNKRoles.Contains(selected))
                                ChanceNNKRoles.Remove(selected);

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNonNeutralKillingNum >= nnkNum) break;
                    }
                }
            }

            // Neutral Killing Roles
            {
                List<CustomRoles> AlwaysNKRoles = [];
                List<CustomRoles> ChanceNKRoles = [];

                for (var i = 0; i < Roles[RoleAssignType.NeutralKilling].Count; i++)
                {
                    RoleAssignInfo item = Roles[RoleAssignType.NeutralKilling][i];

                    if (item.SpawnChance == 100)
                        for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                            AlwaysNKRoles.Add(item.Role);
                    else
                    {
                        for (var j = 0; j < item.SpawnChance / 5; j++)
                            for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                                ChanceNKRoles.Add(item.Role);
                    }
                }

                RoleAssignInfo[] NKRoleCounts = AlwaysNKRoles.Distinct().Select(GetAssignInfo).Concat(ChanceNKRoles.Distinct().Select(GetAssignInfo)).ToArray();
                NKs = NKRoleCounts;

                // Assign roles set to ALWAYS
                if (readyNeutralKillingNum < nkNum)
                {
                    while (AlwaysNKRoles.Count > 0 && nkNum > 0)
                    {
                        CustomRoles selected = AlwaysNKRoles.RandomElement();
                        RoleAssignInfo info = NKRoleCounts.FirstOrDefault(x => x.Role == selected);
                        AlwaysNKRoles.Remove(selected);
                        if (info.AssignedCount >= info.MaxCount) continue;

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNeutralKillingNum++;

                        NKs = NKRoleCounts;

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNeutralKillingNum >= nkNum) break;
                    }
                }

                // Assign other roles when needed
                if (readyRoleNum < playerCount && readyNeutralKillingNum < nkNum)
                {
                    while (ChanceNKRoles.Count > 0 && nkNum > 0)
                    {
                        CustomRoles selected = ChanceNKRoles.RandomElement();
                        RoleAssignInfo info = NKRoleCounts.FirstOrDefault(x => x.Role == selected);
                        for (var i = 0; i < info.SpawnChance / 5; i++) ChanceNKRoles.Remove(selected);

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNeutralKillingNum++;

                        NKs = NKRoleCounts;

                        if (info.AssignedCount >= info.MaxCount)
                            while (ChanceNKRoles.Contains(selected))
                                ChanceNKRoles.Remove(selected);

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNeutralKillingNum >= nkNum) break;
                    }
                }
            }
        }

        // Madmate Roles
        {
            List<CustomRoles> AlwaysMadmateRoles = [];
            List<CustomRoles> ChanceMadmateRoles = [];

            for (var i = 0; i < Roles[RoleAssignType.Madmate].Count; i++)
            {
                RoleAssignInfo item = Roles[RoleAssignType.Madmate][i];

                if (item.SpawnChance == 100)
                    for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        AlwaysMadmateRoles.Add(item.Role);
                else
                {
                    for (var j = 0; j < item.SpawnChance / 5; j++)
                        for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            ChanceMadmateRoles.Add(item.Role);
                }
            }

            RoleAssignInfo[] MadRoleCounts = AlwaysMadmateRoles.Distinct().Select(GetAssignInfo).Concat(ChanceMadmateRoles.Distinct().Select(GetAssignInfo)).ToArray();
            Mads = MadRoleCounts;

            // Assign roles set to ALWAYS
            if (readyRoleNum < playerCount && readyMadmateNum < madmateNum)
            {
                while (AlwaysMadmateRoles.Count > 0)
                {
                    CustomRoles selected = AlwaysMadmateRoles.RandomElement();
                    RoleAssignInfo info = MadRoleCounts.FirstOrDefault(x => x.Role == selected);
                    AlwaysMadmateRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyMadmateNum++;

                    Mads = MadRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyMadmateNum >= madmateNum) break;
                }
            }

            // Assign other roles when needed
            if (readyRoleNum < playerCount && readyMadmateNum < madmateNum)
            {
                while (ChanceMadmateRoles.Count > 0)
                {
                    CustomRoles selected = ChanceMadmateRoles.RandomElement();
                    RoleAssignInfo info = MadRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (var i = 0; i < info.SpawnChance / 5; i++) ChanceMadmateRoles.Remove(selected);

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyMadmateNum++;

                    Mads = MadRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                        while (ChanceMadmateRoles.Contains(selected))
                            ChanceMadmateRoles.Remove(selected);

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyMadmateNum >= madmateNum) break;
                }
            }
        }

        // Coven Roles
        {
            List<CustomRoles> AlwaysCovenRoles = [];
            List<CustomRoles> ChanceCovenRoles = [];

            for (var i = 0; i < Roles[RoleAssignType.Coven].Count; i++)
            {
                RoleAssignInfo item = Roles[RoleAssignType.Coven][i];

                if (item.SpawnChance == 100)
                    for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        AlwaysCovenRoles.Add(item.Role);
                else
                {
                    for (var j = 0; j < item.SpawnChance / 5; j++)
                        for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            ChanceCovenRoles.Add(item.Role);
                }
            }

            RoleAssignInfo[] CovenRoleCounts = AlwaysCovenRoles.Distinct().Select(GetAssignInfo).Concat(ChanceCovenRoles.Distinct().Select(GetAssignInfo)).ToArray();
            Coven = CovenRoleCounts;

            // Assign roles set to ALWAYS
            if (readyCovenNum < numCovens)
            {
                while (AlwaysCovenRoles.Count > 0)
                {
                    CustomRoles selected = AlwaysCovenRoles.RandomElement();
                    RoleAssignInfo info = CovenRoleCounts.FirstOrDefault(x => x.Role == selected);
                    AlwaysCovenRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyCovenNum++;

                    Coven = CovenRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyCovenNum >= numCovens) break;
                }
            }

            // Assign other roles when needed
            if (readyRoleNum < playerCount && readyCovenNum < numCovens)
            {
                while (ChanceCovenRoles.Count > 0)
                {
                    CustomRoles selected = ChanceCovenRoles.RandomElement();
                    RoleAssignInfo info = CovenRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (var i = 0; i < info.SpawnChance / 5; i++) ChanceCovenRoles.Remove(selected);

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyCovenNum++;

                    Coven = CovenRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                        while (ChanceCovenRoles.Contains(selected))
                            ChanceCovenRoles.Remove(selected);

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyCovenNum >= numCovens) break;
                }
            }
        }

        // Crewmate Roles
        {
            int attempts = 0;

            Crew:

            if (attempts++ > 10) goto EndOfAssign;

            List<CustomRoles> AlwaysCrewRoles = [];
            List<CustomRoles> ChanceCrewRoles = [];

            for (var i = 0; i < Roles[RoleAssignType.Crewmate].Count; i++)
            {
                RoleAssignInfo item = Roles[RoleAssignType.Crewmate][i];

                if (item.SpawnChance == 100)
                    for (var j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        AlwaysCrewRoles.Add(item.Role);
                else
                {
                    for (var j = 0; j < item.SpawnChance / 5; j++)
                        for (var k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            ChanceCrewRoles.Add(item.Role);
                }
            }

            RoleAssignInfo[] CrewRoleCounts = AlwaysCrewRoles.Distinct().Select(GetAssignInfo).Concat(ChanceCrewRoles.Distinct().Select(GetAssignInfo)).ToArray();
            Crews = CrewRoleCounts;

            // Assign roles set to ALWAYS
            if (readyRoleNum < playerCount)
            {
                while (AlwaysCrewRoles.Count > 0)
                {
                    CustomRoles selected = AlwaysCrewRoles.RandomElement();
                    RoleAssignInfo info = CrewRoleCounts.FirstOrDefault(x => x.Role == selected);
                    AlwaysCrewRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;

                    Crews = CrewRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                }
            }

            // Assign other roles when needed
            if (readyRoleNum < playerCount)
            {
                while (ChanceCrewRoles.Count > 0)
                {
                    CustomRoles selected = ChanceCrewRoles.RandomElement();
                    RoleAssignInfo info = CrewRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (var i = 0; i < info.SpawnChance / 5; i++) ChanceCrewRoles.Remove(selected);

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;

                    Crews = CrewRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                        while (ChanceCrewRoles.Contains(selected))
                            ChanceCrewRoles.Remove(selected);

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                }
            }

            if (readyRoleNum < playerCount && subCategoryLimits.Count > 0)
            {
                const RoleAssignType redoType = RoleAssignType.Crewmate;
                Roles[redoType] = allRoles[redoType];

                subCategoryLimits = Options.RoleSubCategoryLimits
                    .Where(x => x.Key.GetTabFromOptionType() == TabGroup.CrewmateRoles && x.Value[0].GetBool())
                    .ToDictionary(x => x.Key, x => x.Value[2].GetInt());

                ApplySubCategoryLimits(redoType, subCategoryLimits);
                Roles[redoType].DoIf(x => x.AssignedCount >= x.MaxCount, x => Roles[redoType].Remove(x), false);
                goto Crew;
            }
        }

        EndOfAssign:

        if (Imps.Length > 0) Logger.Info(string.Join(", ", Imps.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "ImpRoleResult");
        if (NNKs.Length > 0) Logger.Info(string.Join(", ", NNKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NNKRoleResult");
        if (NKs.Length > 0) Logger.Info(string.Join(", ", NKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NKRoleResult");
        if (Crews.Length > 0) Logger.Info(string.Join(", ", Crews.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "CrewRoleResult");
        if (Mads.Length > 0) Logger.Info(string.Join(", ", Mads.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "MadRoleResult");
        if (Coven.Length > 0) Logger.Info(string.Join(", ", Coven.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "CovenRoleResult");

        if (rd.Next(0, 100) < Jester.SunnyboyChance.GetInt() && FinalRolesList.Remove(CustomRoles.Jester)) FinalRolesList.Add(CustomRoles.Sunnyboy);
        if (rd.Next(0, 100) < Sans.BardChance.GetInt() && FinalRolesList.Remove(CustomRoles.Sans)) FinalRolesList.Add(CustomRoles.Bard);
        if (rd.Next(0, 100) < Bomber.NukerChance.GetInt() && FinalRolesList.Remove(CustomRoles.Bomber)) FinalRolesList.Add(CustomRoles.Nuker);

        RoleResult.AddRange(AllPlayers.Zip(FinalRolesList.Shuffle()).ToDictionary(x => x.First.PlayerId, x => x.Second), overrideExistingKeys: false);
        Logger.Info(string.Join(", ", RoleResult.Values.Select(x => x.ToString())), "RoleResults");

        if (RoleResult.Count < AllPlayers.Count) Logger.Error("Role assignment error: There are players who have not been assigned a role", "CustomRoleSelector");

        return;

        void AssignRoleToEveryone(CustomRoles role)
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if ((Main.GM.Value && pc.IsHost()) || ChatCommands.Spectators.Contains(pc.PlayerId))
                {
                    RoleResult[pc.PlayerId] = CustomRoles.GM;
                    continue;
                }

                RoleResult[pc.PlayerId] = role;
            }
        }

        RoleAssignInfo GetAssignInfo(CustomRoles role)
        {
            return Roles.Values.FirstOrDefault(x => x.Any(y => y.Role == role))?.FirstOrDefault(x => x.Role == role);
        }

        void ApplySubCategoryLimits(RoleAssignType type, Dictionary<RoleOptionType, int> dictionary) =>
            Roles[type] = Roles[type]
                .Shuffle()
                .OrderBy(x => x.SpawnChance != 100)
                .DistinctBy(x => x.Role)
                .Select(x => (
                    Info: x,
                    Limit: dictionary.TryGetValue(x.OptionType, out var limit)
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
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }
    }

    public static void SelectAddonRoles()
    {
        if (!CustomGameMode.Standard.IsActiveOrIntegrated()) return;

        foreach (byte id in Main.SetAddOns.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetAddOns.Remove(id);

        AddonRolesList = [];

        foreach (CustomRoles role in Enum.GetValues<CustomRoles>())
        {
            if (!role.IsAdditionRole() || role.IsGhostRole()) continue;

            switch (role)
            {
                case CustomRoles.Autopsy when Options.EveryoneSeesDeathReasons.GetBool():
                case CustomRoles.Mare or CustomRoles.Glow or CustomRoles.Sleep when Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.Madmate when Options.MadmateSpawnMode.GetInt() != 0:
                case CustomRoles.Lovers or CustomRoles.LastImpostor or CustomRoles.Workhorse or CustomRoles.Undead:
                case CustomRoles.Nimble or CustomRoles.Physicist or CustomRoles.Bloodlust or CustomRoles.Finder or CustomRoles.Noisy: // Assigned at a different function due to role base change
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

        public int SpawnChance => spawnChance;

        public int MaxCount => maxCount;

        public int AssignedCount { get; set; }

        public RoleOptionType OptionType { get; } = role.GetRoleOptionType();
    }
}