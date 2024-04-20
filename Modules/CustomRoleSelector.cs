using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using HarmonyLib;

namespace EHR.Modules;

public static class ShuffleListExtension
{
    /// <summary>
    /// Shuffles all elements in a collection randomly
    /// </summary>
    /// <typeparam name="T">The type of the collection</typeparam>
    /// <param name="collection">The collection to be shuffled</param>
    /// <param name="random">An instance of a randomizer algorithm</param>
    /// <returns>The shuffled collection</returns>
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> collection, IRandom random)
    {
        var list = collection.ToList();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }

        return list;
    }
}

internal static class CustomRoleSelector
{
    public static Dictionary<PlayerControl, CustomRoles> RoleResult;

    public static int AddScientistNum;
    public static int AddEngineerNum;
    public static int AddShapeshifterNum;

    public static List<CustomRoles> AddonRolesList = [];
    public static IReadOnlyList<CustomRoles> AllRoles => [.. RoleResult.Values];

    private static void GetNeutralCounts(int NKmaxOpt, int NKminOpt, int NNKmaxOpt, int NNKminOpt, ref int ResultNKnum, ref int ResultNNKnum)
    {
        var rd = IRandom.Instance;

        if (NNKmaxOpt > 0 && NNKmaxOpt >= NNKminOpt)
        {
            ResultNNKnum = rd.Next(NNKminOpt, NNKmaxOpt + 1);
        }

        if (NKmaxOpt > 0 && NKmaxOpt >= NKminOpt)
        {
            ResultNKnum = rd.Next(NKminOpt, NKmaxOpt + 1);
        }
    }

    public static void SelectCustomRoles()
    {
        RoleResult = [];

        if (Main.GM.Value && Main.AllPlayerControls.Length == 1) return;

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                AssignRoleToEveryone(CustomRoles.KB_Normal);
                return;
            case CustomGameMode.FFA:
                AssignRoleToEveryone(CustomRoles.Killer);
                return;
            case CustomGameMode.MoveAndStop:
                AssignRoleToEveryone(CustomRoles.Tasker);
                return;
            case CustomGameMode.HotPotato:
                AssignRoleToEveryone(CustomRoles.Potato);
                return;
            case CustomGameMode.HideAndSeek:
                CustomHideAndSeekManager.AssignRoles(ref RoleResult);
                return;
        }

        var rd = IRandom.Instance;
        int playerCount = Main.AllAlivePlayerControls.Length;
        int optImpNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors);
        int optNonNeutralKillingNum = 0;
        int optNeutralKillingNum = 0;

        GetNeutralCounts(Options.NeutralKillingRolesMaxPlayer.GetInt(), Options.NeutralKillingRolesMinPlayer.GetInt(), Options.NonNeutralKillingRolesMaxPlayer.GetInt(), Options.NonNeutralKillingRolesMinPlayer.GetInt(), ref optNeutralKillingNum, ref optNonNeutralKillingNum);

        int readyRoleNum = 0;
        int readyImpNum = 0;
        int readyNonNeutralKillingNum = 0;
        int readyNeutralKillingNum = 0;

        List<CustomRoles> FinalRolesList = [];

        Dictionary<RoleAssignType, List<RoleAssignInfo>> Roles = [];

        Roles[RoleAssignType.Impostor] = [];
        Roles[RoleAssignType.NeutralKilling] = [];
        Roles[RoleAssignType.NonKillingNeutral] = [];
        Roles[RoleAssignType.Crewmate] = [];

        foreach (var id in Main.SetRoles.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetRoles.Remove(id);

        foreach (var role in EnumHelper.GetAllValues<CustomRoles>())
        {
            int chance = role.GetMode();
            if (role.IsVanilla() || chance == 0 || role.IsAdditionRole() || (role.OnlySpawnsWithPets() && !Options.UsePets.GetBool()) || (role != CustomRoles.Randomizer && role.IsCrewmate() && Options.AprilFoolsMode.GetBool()) || (Options.CurrentGameMode == CustomGameMode.HideAndSeek && CustomHideAndSeekManager.HideAndSeekRoles.ContainsKey(role))) continue;
            switch (role)
            {
                case CustomRoles.Commander when optImpNum <= 1 && Commander.CannotSpawnAsSoloImp.GetBool():
                case CustomRoles.Changeling when Changeling.GetAvailableRoles(check: true).Count == 0:
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

        if (Roles[RoleAssignType.Impostor].Count == 0 && !Main.SetRoles.Values.Any(x => x.IsImpostor()))
        {
            Roles[RoleAssignType.Impostor].Add(new(CustomRoles.ImpostorEHR, 100, optImpNum));
            Logger.Warn("Adding Vanilla Impostor", "CustomRoleSelector");
        }

        Logger.Info($"Number of NKs: {optNeutralKillingNum}, Number of NNKs: {optNonNeutralKillingNum}", "NeutralNum");
        Logger.Msg("=====================================================", "AllActiveRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "ImpRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CrewRoles");
        Logger.Msg("=====================================================", "AllActiveRoles");

        IEnumerable<RoleAssignInfo> TempAlwaysImpRoles = Roles[RoleAssignType.Impostor].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysNKRoles = Roles[RoleAssignType.NeutralKilling].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysNNKRoles = Roles[RoleAssignType.NonKillingNeutral].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysCrewRoles = Roles[RoleAssignType.Crewmate].Where(x => x.SpawnChance == 100);

        // DistinctBy - Removes duplicate roles if there are any
        // Shuffle - Shuffles all roles in the list into a randomized order
        // Take - Takes the first x roles of the list ... x is the maximum number of roles we could need of that team

        Roles[RoleAssignType.Impostor] = Roles[RoleAssignType.Impostor].Shuffle(rd).Take(optImpNum).ToList();
        Roles[RoleAssignType.NeutralKilling] = Roles[RoleAssignType.NeutralKilling].Shuffle(rd).Take(optNeutralKillingNum).ToList();
        Roles[RoleAssignType.NonKillingNeutral] = Roles[RoleAssignType.NonKillingNeutral].Shuffle(rd).Take(optNonNeutralKillingNum).ToList();
        Roles[RoleAssignType.Crewmate] = Roles[RoleAssignType.Crewmate].Shuffle(rd).Take(playerCount).ToList();

        Roles[RoleAssignType.Impostor].AddRange(TempAlwaysImpRoles);
        Roles[RoleAssignType.NeutralKilling].AddRange(TempAlwaysNKRoles);
        Roles[RoleAssignType.NonKillingNeutral].AddRange(TempAlwaysNNKRoles);
        Roles[RoleAssignType.Crewmate].AddRange(TempAlwaysCrewRoles);

        Roles[RoleAssignType.Impostor] = Roles[RoleAssignType.Impostor].DistinctBy(x => x.Role).ToList();
        Roles[RoleAssignType.NeutralKilling] = Roles[RoleAssignType.NeutralKilling].DistinctBy(x => x.Role).ToList();
        Roles[RoleAssignType.NonKillingNeutral] = Roles[RoleAssignType.NonKillingNeutral].DistinctBy(x => x.Role).ToList();
        Roles[RoleAssignType.Crewmate] = Roles[RoleAssignType.Crewmate].DistinctBy(x => x.Role).ToList();

        Logger.Msg("======================================================", "SelectedRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => x.Role.ToString())), "SelectedImpostorRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => x.Role.ToString())), "SelectedNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => x.Role.ToString())), "SelectedNNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => x.Role.ToString())), "SelectedCrewRoles");
        Logger.Msg("======================================================", "SelectedRoles");

        var AllPlayers = Main.AllAlivePlayerControls.ToList();

        // Players on the EAC banned list will be assigned as GM when opening rooms
        if (BanManager.CheckEACList(PlayerControl.LocalPlayer.FriendCode, PlayerControl.LocalPlayer.GetClient().GetHashedPuid()))
        {
            Main.GM.Value = true;
            RoleResult[PlayerControl.LocalPlayer] = CustomRoles.GM;
            AllPlayers.Remove(PlayerControl.LocalPlayer);
        }

        if (Main.GM.Value) Logger.Warn("Host: GM", "CustomRoleSelector");

        // Pre-Assigned Roles By Host Are Selected First
        foreach (var item in Main.SetRoles)
        {
            PlayerControl pc = AllPlayers.FirstOrDefault(x => x.PlayerId == item.Key);
            if (pc == null) continue;

            RoleResult[pc] = item.Value;
            AllPlayers.Remove(pc);

            if (item.Value.IsImpostor())
            {
                Roles[RoleAssignType.Impostor].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyImpNum++;
            }
            else if (item.Value.IsNK())
            {
                Roles[RoleAssignType.NeutralKilling].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyNeutralKillingNum++;
            }
            else if (item.Value.IsNonNK())
            {
                Roles[RoleAssignType.NonKillingNeutral].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyNonNeutralKillingNum++;
            }

            readyRoleNum++;

            Logger.Warn($"Pre-Set Role Assigned: {pc.GetRealName()} => {item.Value}", "CustomRoleSelector");
        }

        RoleAssignInfo[] Imps;
        RoleAssignInfo[] NNKs = [];
        RoleAssignInfo[] NKs = [];
        RoleAssignInfo[] Crews = [];

        // Impostor Roles
        {
            List<CustomRoles> AlwaysImpRoles = [];
            List<CustomRoles> ChanceImpRoles = [];
            for (int i = 0; i < Roles[RoleAssignType.Impostor].Count; i++)
            {
                RoleAssignInfo item = Roles[RoleAssignType.Impostor][i];
                if (item.SpawnChance == 100)
                {
                    for (int j = 0; j < item.MaxCount - item.AssignedCount; j++)
                    {
                        AlwaysImpRoles.Add(item.Role);
                    }
                }
                else
                {
                    for (int j = 0; j < item.SpawnChance / 5; j++)
                    {
                        for (int k = 0; k < item.MaxCount - item.AssignedCount; k++)
                        {
                            ChanceImpRoles.Add(item.Role);
                        }
                    }
                }
            }

            RoleAssignInfo[] ImpRoleCounts = AlwaysImpRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceImpRoles.Distinct().Select(GetAssignInfo).ToArray());
            Imps = ImpRoleCounts;

            // Assign roles set to ALWAYS
            if (readyImpNum < optImpNum)
            {
                while (AlwaysImpRoles.Count > 0)
                {
                    var selected = AlwaysImpRoles[rd.Next(0, AlwaysImpRoles.Count)];
                    var info = ImpRoleCounts.FirstOrDefault(x => x.Role == selected);
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
                    var selected = ChanceImpRoles[rd.Next(0, ChanceImpRoles.Count)];
                    var info = ImpRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (int i = 0; i < info.SpawnChance / 5; i++) ChanceImpRoles.Remove(selected);

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
                for (int i = 0; i < Roles[RoleAssignType.NonKillingNeutral].Count; i++)
                {
                    RoleAssignInfo item = Roles[RoleAssignType.NonKillingNeutral][i];
                    if (item.SpawnChance == 100)
                    {
                        for (int j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        {
                            AlwaysNNKRoles.Add(item.Role);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < item.SpawnChance / 5; j++)
                        {
                            for (int k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            {
                                ChanceNNKRoles.Add(item.Role);
                            }
                        }
                    }
                }

                RoleAssignInfo[] NNKRoleCounts = AlwaysNNKRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceNNKRoles.Distinct().Select(GetAssignInfo).ToArray());
                NNKs = NNKRoleCounts;

                // Assign roles set to ALWAYS
                if (readyNonNeutralKillingNum < optNonNeutralKillingNum)
                {
                    while (AlwaysNNKRoles.Count > 0 && optNonNeutralKillingNum > 0)
                    {
                        var selected = AlwaysNNKRoles[rd.Next(0, AlwaysNNKRoles.Count)];
                        var info = NNKRoleCounts.FirstOrDefault(x => x.Role == selected);
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
                        var selected = ChanceNNKRoles[rd.Next(0, ChanceNNKRoles.Count)];
                        var info = NNKRoleCounts.FirstOrDefault(x => x.Role == selected);
                        for (int i = 0; i < info.SpawnChance / 5; i++) ChanceNNKRoles.Remove(selected);

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
                for (int i = 0; i < Roles[RoleAssignType.NeutralKilling].Count; i++)
                {
                    RoleAssignInfo item = Roles[RoleAssignType.NeutralKilling][i];
                    if (item.SpawnChance == 100)
                    {
                        for (int j = 0; j < item.MaxCount - item.AssignedCount; j++)
                        {
                            AlwaysNKRoles.Add(item.Role);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < item.SpawnChance / 5; j++)
                        {
                            for (int k = 0; k < item.MaxCount - item.AssignedCount; k++)
                            {
                                ChanceNKRoles.Add(item.Role);
                            }
                        }
                    }
                }

                RoleAssignInfo[] NKRoleCounts = AlwaysNKRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceNKRoles.Distinct().Select(GetAssignInfo).ToArray());
                NKs = NKRoleCounts;

                // Assign roles set to ALWAYS
                if (readyNeutralKillingNum < optNeutralKillingNum)
                {
                    while (AlwaysNKRoles.Count > 0 && optNeutralKillingNum > 0)
                    {
                        var selected = AlwaysNKRoles[rd.Next(0, AlwaysNKRoles.Count)];
                        var info = NKRoleCounts.FirstOrDefault(x => x.Role == selected);
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
                        var selected = ChanceNKRoles[rd.Next(0, ChanceNKRoles.Count)];
                        var info = NKRoleCounts.FirstOrDefault(x => x.Role == selected);
                        for (int i = 0; i < info.SpawnChance / 5; i++) ChanceNKRoles.Remove(selected);

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
            for (int i = 0; i < Roles[RoleAssignType.Crewmate].Count; i++)
            {
                RoleAssignInfo item = Roles[RoleAssignType.Crewmate][i];
                if (item.SpawnChance == 100)
                {
                    for (int j = 0; j < item.MaxCount - item.AssignedCount; j++)
                    {
                        AlwaysCrewRoles.Add(item.Role);
                    }
                }
                else
                {
                    for (int j = 0; j < item.SpawnChance / 5; j++)
                    {
                        for (int k = 0; k < item.MaxCount - item.AssignedCount; k++)
                        {
                            ChanceCrewRoles.Add(item.Role);
                        }
                    }
                }
            }

            RoleAssignInfo[] CrewRoleCounts = AlwaysCrewRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceCrewRoles.Distinct().Select(GetAssignInfo).ToArray());
            Crews = CrewRoleCounts;

            // Assign roles set to ALWAYS
            if (readyRoleNum < playerCount)
            {
                while (AlwaysCrewRoles.Count > 0)
                {
                    var selected = AlwaysCrewRoles[rd.Next(0, AlwaysCrewRoles.Count)];
                    var info = CrewRoleCounts.FirstOrDefault(x => x.Role == selected);
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
                    var selected = ChanceCrewRoles[rd.Next(0, ChanceCrewRoles.Count)];
                    var info = CrewRoleCounts.FirstOrDefault(x => x.Role == selected);
                    for (int i = 0; i < info.SpawnChance / 5; i++) ChanceCrewRoles.Remove(selected);

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
        if (rd.Next(0, 100) < Options.NukerChance.GetInt() && FinalRolesList.Remove(CustomRoles.Bomber)) FinalRolesList.Add(CustomRoles.Nuker);

        Logger.Info(string.Join(", ", FinalRolesList.Select(x => x.ToString())), "RoleResults");

        while (AllPlayers.Count > 0 && FinalRolesList.Count > 0)
        {
            var roleId = rd.Next(0, FinalRolesList.Count);

            CustomRoles assignedRole = FinalRolesList[roleId];

            RoleResult[AllPlayers[0]] = assignedRole;
            Logger.Info($"Role assigned：{AllPlayers[0].GetRealName()} => {assignedRole}", "CustomRoleSelector");

            AllPlayers.RemoveAt(0);
            FinalRolesList.RemoveAt(roleId);
        }

        if (AllPlayers.Count > 0)
            Logger.Error("Role assignment error: There are players who have not been assigned a role", "CustomRoleSelector");
        if (FinalRolesList.Count > 0)
            Logger.Error("Team assignment error: There is an unassigned team", "CustomRoleSelector");

        return;

        void AssignRoleToEveryone(CustomRoles role)
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (Main.GM.Value && pc.PlayerId == 0) continue;
                RoleResult[pc] = role;
            }
        }

        RoleAssignInfo GetAssignInfo(CustomRoles role) => Roles.Values.FirstOrDefault(x => x.Any(y => y.Role == role))?.FirstOrDefault(x => x.Role == role);
    }

    public static void CalculateVanillaRoleCount()
    {
        // Calculate the number of base roles
        AddEngineerNum = 0;
        AddScientistNum = 0;
        AddShapeshifterNum = 0;
        foreach (var role in AllRoles)
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
            }
        }
    }

    public static void SelectAddonRoles()
    {
        if (Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop) return;

        foreach (var id in Main.SetAddOns.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetAddOns.Remove(id);

        AddonRolesList = [];
        foreach (var role in EnumHelper.GetAllValues<CustomRoles>())
        {
            if (!role.IsAdditionRole() || role.IsGhostRole()) continue;
            switch (role)
            {
                case CustomRoles.Mare when Main.CurrentMap == MapNames.Fungle:
                case CustomRoles.Madmate when Options.MadmateSpawnMode.GetInt() != 0:
                case CustomRoles.Lovers or CustomRoles.LastImpostor or CustomRoles.Workhorse or CustomRoles.Undead:
                case CustomRoles.Nimble or CustomRoles.Physicist or CustomRoles.Bloodlust: // Assigned at a different function due to role base change
                    continue;
            }

            AddonRolesList.Add(role);
        }
    }

    enum RoleAssignType
    {
        Impostor,
        NeutralKilling,
        NonKillingNeutral,
        Crewmate
    }

    public class RoleAssignInfo(CustomRoles role, int spawnChance, int maxCount, int assignedCount = 0)
    {
        public CustomRoles Role
        {
            get => role;
            set => role = value;
        }

        public int SpawnChance
        {
            get => spawnChance;
            set => spawnChance = value;
        }

        public int MaxCount
        {
            get => maxCount;
            set => maxCount = value;
        }

        public int AssignedCount
        {
            get => assignedCount;
            set => assignedCount = value;
        }
    }
}