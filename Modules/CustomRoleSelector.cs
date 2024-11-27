using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.Impostor;
using EHR.Neutral;

namespace EHR.Modules
{
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

        private static void GetNeutralCounts(int NKmaxOpt, int NKminOpt, int NNKmaxOpt, int NNKminOpt, ref int ResultNKnum, ref int ResultNNKnum)
        {
            var rd = IRandom.Instance;
            if (NNKmaxOpt > 0 && NNKmaxOpt >= NNKminOpt) ResultNNKnum = rd.Next(NNKminOpt, NNKmaxOpt + 1);
            if (NKmaxOpt > 0 && NKmaxOpt >= NKminOpt) ResultNKnum = rd.Next(NKminOpt, NKmaxOpt + 1);
        }

        public static void SelectCustomRoles()
        {
            RoleResult = [];

            if (Main.GM.Value && Main.AllPlayerControls.Length == 1) return;

            // switch (Options.CurrentGameMode)
            // {
            //     case CustomGameMode.SoloKombat:
            //         AssignRoleToEveryone(CustomRoles.KB_Normal);
            //         return;
            //     case CustomGameMode.FFA:
            //         AssignRoleToEveryone(CustomRoles.Killer);
            //         return;
            //     case CustomGameMode.MoveAndStop:
            //         AssignRoleToEveryone(CustomRoles.Tasker);
            //         return;
            //     case CustomGameMode.HotPotato:
            //         AssignRoleToEveryone(CustomRoles.Potato);
            //         return;
            //     case CustomGameMode.Speedrun:
            //         AssignRoleToEveryone(CustomRoles.Runner);
            //         return;
            //     case CustomGameMode.CaptureTheFlag:
            //         AssignRoleToEveryone(CustomRoles.CTFPlayer);
            //         return;
            //     case CustomGameMode.NaturalDisasters:
            //         AssignRoleToEveryone(CustomRoles.NDPlayer);
            //         return;
            //     case CustomGameMode.RoomRush:
            //         AssignRoleToEveryone(CustomRoles.RRPlayer);
            //         return;
            //     case CustomGameMode.HideAndSeek:
            //         HnSManager.AssignRoles();
            //         RoleResult = HnSManager.PlayerRoles.ToDictionary(x => x.Key, x => x.Value.Role);
            //         return;
            // }

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
            var optNonNeutralKillingNum = 0;
            var optNeutralKillingNum = 0;

            GetNeutralCounts(Options.NeutralKillingRolesMaxPlayer.GetInt(), Options.NeutralKillingRolesMinPlayer.GetInt(), Options.NonNeutralKillingRolesMaxPlayer.GetInt(), Options.NonNeutralKillingRolesMinPlayer.GetInt(), ref optNeutralKillingNum, ref optNonNeutralKillingNum);

            var readyRoleNum = 0;
            var readyImpNum = 0;
            var readyNonNeutralKillingNum = 0;
            var readyNeutralKillingNum = 0;

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

                if (role.IsImpostor()) Roles[RoleAssignType.Impostor].Add(info);
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
                else
                    Roles[RoleAssignType.Crewmate].Add(new(CustomRoles.LovingCrewmate, 100, 2));
            }

            if (Roles[RoleAssignType.Impostor].Count == 0 && optNeutralKillingNum == 0 && optNonNeutralKillingNum == 0 && !Main.SetRoles.Values.Any(x => x.IsImpostor() || x.IsNK()))
            {
                Roles[RoleAssignType.Impostor].Add(new(CustomRoles.ImpostorEHR, 100, optImpNum));
                Logger.Warn("Adding Vanilla Impostor", "CustomRoleSelector");
            }

            if (Roles[RoleAssignType.Crewmate].Count == 0 && optNeutralKillingNum == 0 && optNonNeutralKillingNum == 0 && !Main.SetRoles.Values.Any(x => x.IsCrewmate()))
            {
                Roles[RoleAssignType.Crewmate].Add(new(CustomRoles.CrewmateEHR, 100, playerCount));
                Logger.Warn("Adding Vanilla Crewmates", "CustomRoleSelector");
            }

            Logger.Info($"Number of NKs: {optNeutralKillingNum}, Number of NNKs: {optNonNeutralKillingNum}", "NeutralNum");
            Logger.Msg("=====================================================", "AllActiveRoles");
            Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "ImpRoles");
            Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NKRoles");
            Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NNKRoles");
            Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CrewRoles");
            Logger.Msg("=====================================================", "AllActiveRoles");

            Dictionary<RoleOptionType, int> subCategoryLimits = Options.RoleSubCategoryLimits
                .Where(x => x.Value[0].GetBool())
                .ToDictionary(x => x.Key, x => IRandom.Instance.Next(x.Value[1].GetInt(), x.Value[2].GetInt() + 1));

            if (subCategoryLimits.Count > 0) Logger.Info($"Sub-Category Limits: {string.Join(", ", subCategoryLimits.Select(x => $"{x.Key}: {x.Value}"))}", "SubCategoryLimits");

            foreach (RoleAssignType type in Roles.Keys.ToArray())
            {
                Roles[type] = Roles[type]
                    .Shuffle()
                    .OrderBy(x => x.SpawnChance != 100)
                    .DistinctBy(x => x.Role)
                    .Select(x => (
                        Info: x,
                        Limit: subCategoryLimits.TryGetValue(x.OptionType, out var limit)
                            ? (Exists: true, Value: limit)
                            : (Exists: false, Value: 0)))
                    .GroupBy(x => x.Info.OptionType)
                    .Select(x => (Grouping: x, x.FirstOrDefault().Limit))
                    .SelectMany(x => x.Limit.Exists ? x.Grouping.Take(x.Limit.Value) : x.Grouping)
                    .OrderByDescending(x => x.Limit is { Exists: true, Value: > 0 })
                    .Select(x => x.Info)
                    .Take(type switch
                    {
                        RoleAssignType.Impostor => optImpNum,
                        RoleAssignType.NeutralKilling => optNeutralKillingNum,
                        RoleAssignType.NonKillingNeutral => optNonNeutralKillingNum,
                        RoleAssignType.Crewmate => playerCount,
                        _ => 0
                    })
                    .ToList();
            }

            Logger.Msg("======================================================", "SelectedRoles");
            Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => x.Role.ToString())), "SelectedImpostorRoles");
            Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => x.Role.ToString())), "SelectedNKRoles");
            Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => x.Role.ToString())), "SelectedNNKRoles");
            Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => x.Role.ToString())), "SelectedCrewRoles");
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

            // Pre-Assigned Roles By Host Are Selected First
            foreach ((byte id, CustomRoles role) in Main.SetRoles.AddRange(ChatCommands.DraftResult, false))
            {
                PlayerControl pc = AllPlayers.FirstOrDefault(x => x.PlayerId == id);
                if (pc == null) continue;

                RoleResult[pc.PlayerId] = role;
                AllPlayers.Remove(pc);

                if (role.IsImpostor())
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
                    if (readyNonNeutralKillingNum < optNonNeutralKillingNum)
                    {
                        while (AlwaysNNKRoles.Count > 0 && optNonNeutralKillingNum > 0)
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

                            if (readyNonNeutralKillingNum >= optNonNeutralKillingNum) break;
                        }
                    }

                    // Assign other roles when needed
                    if (readyRoleNum < playerCount && readyNonNeutralKillingNum < optNonNeutralKillingNum)
                    {
                        while (ChanceNNKRoles.Count > 0 && optNonNeutralKillingNum > 0)
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

                            if (readyNonNeutralKillingNum >= optNonNeutralKillingNum) break;
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
                    if (readyNeutralKillingNum < optNeutralKillingNum)
                    {
                        while (AlwaysNKRoles.Count > 0 && optNeutralKillingNum > 0)
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

                            if (readyNeutralKillingNum >= optNeutralKillingNum) break;
                        }
                    }

                    // Assign other roles when needed
                    if (readyRoleNum < playerCount && readyNeutralKillingNum < optNeutralKillingNum)
                    {
                        while (ChanceNKRoles.Count > 0 && optNeutralKillingNum > 0)
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

                            if (readyNeutralKillingNum >= optNeutralKillingNum) break;
                        }
                    }
                }
            }

            // Crewmate Roles
            {
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
            }

            EndOfAssign:

            if (Imps.Length > 0) Logger.Info(string.Join(", ", Imps.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "ImpRoleResult");

            if (NNKs.Length > 0) Logger.Info(string.Join(", ", NNKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NNKRoleResult");

            if (NKs.Length > 0) Logger.Info(string.Join(", ", NKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NKRoleResult");

            if (Crews.Length > 0) Logger.Info(string.Join(", ", Crews.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "CrewRoleResult");

            if (rd.Next(0, 100) < Jester.SunnyboyChance.GetInt() && FinalRolesList.Remove(CustomRoles.Jester)) FinalRolesList.Add(CustomRoles.Sunnyboy);

            if (rd.Next(0, 100) < Sans.BardChance.GetInt() && FinalRolesList.Remove(CustomRoles.Sans)) FinalRolesList.Add(CustomRoles.Bard);

            if (rd.Next(0, 100) < Bomber.NukerChance.GetInt() && FinalRolesList.Remove(CustomRoles.Bomber)) FinalRolesList.Add(CustomRoles.Nuker);

            Logger.Info(string.Join(", ", FinalRolesList.Select(x => x.ToString())), "RoleResults");

            Dictionary<byte, CustomRoles> preResult = RoleResult.ToDictionary(x => x.Key, x => x.Value);
            RoleResult = AllPlayers.Zip(FinalRolesList.Shuffle()).ToDictionary(x => x.First.PlayerId, x => x.Second);
            RoleResult.AddRange(preResult);

            if (RoleResult.Count < AllPlayers.Count) Logger.Error("Role assignment error: There are players who have not been assigned a role", "CustomRoleSelector");

            return;

            void AssignRoleToEveryone(CustomRoles role)
            {
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (Main.GM.Value && pc.IsHost())
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
            Crewmate
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
}