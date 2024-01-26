using AmongUs.GameOptions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Neutral;

namespace TOHE.Modules;

public static class ShuffleListExtension
{
    /// <summary>
    /// Shuffles all elements in a collection randomly
    /// </summary>
    /// <typeparam name="T">The type of the collection</typeparam>
    /// <param name="collection">The collection to be shuffled</param>
    /// <param name="random">An instance of a randomizer algorithm</param>
    /// <returns></returns>
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
internal class CustomRoleSelector
{
    public static Dictionary<PlayerControl, CustomRoles> RoleResult;
    public static IReadOnlyList<CustomRoles> AllRoles => [.. RoleResult.Values];

    enum RoleAssignType
    {
        Impostor,
        NeutralKilling,
        NonKillingNeutral,
        Crewmate
    }

    public class RoleAssignInfo(CustomRoles role, int spawnChance, int maxCount, int assignedCount = 0)
    {
        public CustomRoles Role { get => role; set => role = value; }
        public int SpawnChance { get => spawnChance; set => spawnChance = value; }
        public int MaxCount { get => maxCount; set => maxCount = value; }
        public int AssignedCount { get => assignedCount; set => assignedCount = value; }
    }

    public static void GetNeutralCounts(int NKmaxOpt, int NKminOpt, int NNKmaxOpt, int NNKminOpt, ref int ResultNKnum, ref int ResultNNKnum)
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
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                RoleResult = [];
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    RoleResult.Add(pc, CustomRoles.KB_Normal);
                }
                return;
            case CustomGameMode.FFA:
                RoleResult = [];
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    RoleResult.Add(pc, CustomRoles.Killer);
                }
                return;
            case CustomGameMode.MoveAndStop:
                RoleResult = [];
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (pc.IsModClient()) RoleResult.Add(pc, CustomRoles.DonutDelivery);
                    else RoleResult.Add(pc, CustomRoles.Tasker);
                }
                return;
        }

        RoleResult = [];
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

        Dictionary<RoleAssignType, List<RoleAssignInfo>> AllRoles = [];

        AllRoles[RoleAssignType.Impostor] = [];
        AllRoles[RoleAssignType.NeutralKilling] = [];
        AllRoles[RoleAssignType.NonKillingNeutral] = [];
        AllRoles[RoleAssignType.Crewmate] = [];

        foreach (var id in Main.SetRoles.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetRoles.Remove(id);

        System.Collections.IList list = Enum.GetValues(typeof(CustomRoles));
        for (int i1 = 0; i1 < list.Count; i1++)
        {
            object cr = list[i1];
            CustomRoles role = (CustomRoles)Enum.Parse(typeof(CustomRoles), cr.ToString());
            int chance = role.GetMode();
            if (role.IsVanilla() || chance == 0 || role.IsAdditionRole() || (role.OnlySpawnsWithPets() && !Options.UsePets.GetBool())) continue;
            switch (role)
            {
                case CustomRoles.DarkHide when (MapNames)Main.NormalOptions.MapId == MapNames.Fungle:
                case CustomRoles.Pelican when AllRoles[RoleAssignType.Impostor].Any(x => x.Role == CustomRoles.Duellist):
                case CustomRoles.Duellist when AllRoles[RoleAssignType.NeutralKilling].Any(x => x.Role == CustomRoles.Pelican):
                case CustomRoles.GM:
                case CustomRoles.NotAssigned:
                    continue;
            }

            int count = role.GetCount();
            RoleAssignInfo info = new(role, chance, count);

            if (role.IsImpostor()) AllRoles[RoleAssignType.Impostor].Add(info);
            else if (role.IsNK()) AllRoles[RoleAssignType.NeutralKilling].Add(info);
            else if (role.IsNonNK()) AllRoles[RoleAssignType.NonKillingNeutral].Add(info);
            else AllRoles[RoleAssignType.Crewmate].Add(info);
        }

        if (AllRoles[RoleAssignType.Impostor].Count == 0 && !Main.SetRoles.Values.Any(x => x.IsImpostor()))
        {
            AllRoles[RoleAssignType.Impostor].Add(new(CustomRoles.ImpostorTOHE, 100, 1));
            Logger.Warn("Adding Vanilla Impostor", "CustomRoleSelector");
        }

        Logger.Info($"Number of NKs: {optNeutralKillingNum}, Number of NNKs: {optNonNeutralKillingNum}", "NeutralNum");
        Logger.Msg("=====================================================", "AllActiveRoles");
        Logger.Info(string.Join(", ", AllRoles[RoleAssignType.Impostor].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "ImpRoles");
        Logger.Info(string.Join(", ", AllRoles[RoleAssignType.NeutralKilling].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NKRoles");
        Logger.Info(string.Join(", ", AllRoles[RoleAssignType.NonKillingNeutral].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NNKRoles");
        Logger.Info(string.Join(", ", AllRoles[RoleAssignType.Crewmate].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CrewRoles");
        Logger.Msg("=====================================================", "AllActiveRoles");

        IEnumerable<RoleAssignInfo> TempAlwaysImpRoles = AllRoles[RoleAssignType.Impostor].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysNKRoles = AllRoles[RoleAssignType.NeutralKilling].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysNNKRoles = AllRoles[RoleAssignType.NonKillingNeutral].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysCrewRoles = AllRoles[RoleAssignType.Crewmate].Where(x => x.SpawnChance == 100);

        // DistinctBy - Removes duplicate roles if there are any
        // Shuffle - Shuffles all roles in the list into a randomized order
        // Take - Takes the first x roles of the list ... x is the maximum number of roles we could need of that team

        AllRoles[RoleAssignType.Impostor] = AllRoles[RoleAssignType.Impostor].DistinctBy(x => x.Role).Shuffle(rd).Take(optImpNum).ToList();
        AllRoles[RoleAssignType.NeutralKilling] = AllRoles[RoleAssignType.NeutralKilling].DistinctBy(x => x.Role).Shuffle(rd).Take(optNeutralKillingNum).ToList();
        AllRoles[RoleAssignType.NonKillingNeutral] = AllRoles[RoleAssignType.NonKillingNeutral].DistinctBy(x => x.Role).Shuffle(rd).Take(optNonNeutralKillingNum).ToList();
        AllRoles[RoleAssignType.Crewmate] = AllRoles[RoleAssignType.Crewmate].DistinctBy(x => x.Role).Shuffle(rd).Take(playerCount).ToList();

        AllRoles[RoleAssignType.Impostor].AddRange(TempAlwaysImpRoles);
        AllRoles[RoleAssignType.NeutralKilling].AddRange(TempAlwaysNKRoles);
        AllRoles[RoleAssignType.NonKillingNeutral].AddRange(TempAlwaysNNKRoles);
        AllRoles[RoleAssignType.Crewmate].AddRange(TempAlwaysCrewRoles);

        Logger.Msg("======================================================", "SelectedRoles");
        Logger.Info(string.Join(", ", AllRoles[RoleAssignType.Impostor].Select(x => x.Role.ToString())), "SelectedImpostorRoles");
        Logger.Info(string.Join(", ", AllRoles[RoleAssignType.NeutralKilling].Select(x => x.Role.ToString())), "SelectedNKRoles");
        Logger.Info(string.Join(", ", AllRoles[RoleAssignType.NonKillingNeutral].Select(x => x.Role.ToString())), "SelectedNNKRoles");
        Logger.Info(string.Join(", ", AllRoles[RoleAssignType.Crewmate].Select(x => x.Role.ToString())), "SelectedCrewRoles");
        Logger.Msg("======================================================", "SelectedRoles");

        var AllPlayers = Main.AllAlivePlayerControls.ToList();

        // Players on the EAC banned list will be assigned as GM when opening rooms
        if (BanManager.CheckEACList(PlayerControl.LocalPlayer.FriendCode, PlayerControl.LocalPlayer.GetClient().GetHashedPuid()))
        {
            Options.EnableGM.SetValue(1);
            RoleResult[PlayerControl.LocalPlayer] = CustomRoles.GM;
            AllPlayers.Remove(PlayerControl.LocalPlayer);
        }

        // Pre-Assigned Roles By Host Are Selected First
        foreach (var item in Main.SetRoles)
        {
            PlayerControl pc = AllPlayers.FirstOrDefault(x => x.PlayerId == item.Key);
            if (pc == null) continue;

            RoleResult[pc] = item.Value;
            AllPlayers.Remove(pc);

            if (item.Value.IsImpostor())
            {
                AllRoles[RoleAssignType.Impostor].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyImpNum++;
            }
            else if (item.Value.IsNK())
            {
                AllRoles[RoleAssignType.NeutralKilling].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyNeutralKillingNum++;
            }
            else if (item.Value.IsNonNK())
            {
                AllRoles[RoleAssignType.NonKillingNeutral].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyNonNeutralKillingNum++;
            }

            readyRoleNum++;

            Logger.Warn($"Pre-Set Role Assigned: {pc.GetRealName()} => {item.Value}", "CustomRoleSelector");
        }

        RoleAssignInfo[] Imps = [];
        RoleAssignInfo[] NNKs = [];
        RoleAssignInfo[] NKs = [];
        RoleAssignInfo[] Crews = [];

        RoleAssignInfo GetAssignInfo(CustomRoles role) => AllRoles.Values.FirstOrDefault(x => x.Any(x => x.Role == role)).FirstOrDefault(x => x.Role == role);

        // Impostor Roles
        {
            List<CustomRoles> AlwaysImpRoles = [];
            List<CustomRoles> ChanceImpRoles = [];
            for (int i = 0; i < AllRoles[RoleAssignType.Impostor].Count; i++)
            {
                RoleAssignInfo item = AllRoles[RoleAssignType.Impostor][i];
                if (item.SpawnChance == 100)
                {
                    for (int j = 0; j < item.MaxCount; j++)
                    {
                        AlwaysImpRoles.Add(item.Role);
                    }
                }
                else
                {
                    for (int j = 0; j < item.SpawnChance / 5; j++)
                    {
                        for (int k = 0; k < item.MaxCount; k++)
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

                    if (info.AssignedCount >= info.MaxCount) while (ChanceImpRoles.Contains(selected)) ChanceImpRoles.Remove(selected);

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
                for (int i = 0; i < AllRoles[RoleAssignType.NonKillingNeutral].Count; i++)
                {
                    RoleAssignInfo item = AllRoles[RoleAssignType.NonKillingNeutral][i];
                    if (item.SpawnChance == 100)
                    {
                        for (int j = 0; j < item.MaxCount; j++)
                        {
                            AlwaysNNKRoles.Add(item.Role);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < item.SpawnChance / 5; j++)
                        {
                            for (int k = 0; k < item.MaxCount; k++)
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

                        if (info.AssignedCount >= info.MaxCount) while (ChanceNNKRoles.Contains(selected)) ChanceNNKRoles.Remove(selected);

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNonNeutralKillingNum >= optNonNeutralKillingNum) break;
                    }
                }
            }

            // Neutral Killing Roles
            {
                List<CustomRoles> AlwaysNKRoles = [];
                List<CustomRoles> ChanceNKRoles = [];
                for (int i = 0; i < AllRoles[RoleAssignType.NeutralKilling].Count; i++)
                {
                    RoleAssignInfo item = AllRoles[RoleAssignType.NeutralKilling][i];
                    if (item.SpawnChance == 100)
                    {
                        for (int j = 0; j < item.MaxCount; j++)
                        {
                            AlwaysNKRoles.Add(item.Role);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < item.SpawnChance / 5; j++)
                        {
                            for (int k = 0; k < item.MaxCount; k++)
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

                        if (info.AssignedCount >= info.MaxCount) while (ChanceNKRoles.Contains(selected)) ChanceNKRoles.Remove(selected);

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
            for (int i = 0; i < AllRoles[RoleAssignType.Crewmate].Count; i++)
            {
                RoleAssignInfo item = AllRoles[RoleAssignType.Crewmate][i];
                if (item.SpawnChance == 100)
                {
                    for (int j = 0; j < item.MaxCount; j++)
                    {
                        AlwaysCrewRoles.Add(item.Role);
                    }
                }
                else
                {
                    for (int j = 0; j < item.SpawnChance / 5; j++)
                    {
                        for (int k = 0; k < item.MaxCount; k++)
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

                    if (info.AssignedCount >= info.MaxCount) while (ChanceCrewRoles.Contains(selected)) ChanceCrewRoles.Remove(selected);

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                }
            }
        }

    EndOfAssign:

        if (Imps.Length > 0) Logger.Info(string.Join(", ", Imps.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "ImpRoleResult");
        if (NNKs.Length > 0) Logger.Info(string.Join(", ", NNKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NNKRoleResult");
        if (NKs.Length > 0) Logger.Info(string.Join(", ", NKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NKRoleResult");
        if (Crews.Length > 0) Logger.Info(string.Join(", ", Crews.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "CrewRoleResult");

        if (rd.Next(0, 100) < Options.SunnyboyChance.GetInt() && FinalRolesList.Remove(CustomRoles.Jester)) FinalRolesList.Add(CustomRoles.Sunnyboy);
        if (rd.Next(0, 100) < Sans.BardChance.GetInt() && FinalRolesList.Remove(CustomRoles.Sans)) FinalRolesList.Add(CustomRoles.Bard);
        if (rd.Next(0, 100) < Options.NukerChance.GetInt() && FinalRolesList.Remove(CustomRoles.Bomber)) FinalRolesList.Add(CustomRoles.Nuker);

        if (Romantic.IsEnable)
        {
            if (FinalRolesList.Contains(CustomRoles.Romantic) && FinalRolesList.Contains(CustomRoles.Lovers))
                FinalRolesList.Remove(CustomRoles.Lovers);
        }

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
    }

    public static int addScientistNum;
    public static int addEngineerNum;
    public static int addShapeshifterNum;
    public static void CalculateVanillaRoleCount()
    {
        // Calculate the number of special professions in the original version
        addEngineerNum = 0;
        addScientistNum = 0;
        addShapeshifterNum = 0;
        for (int i = 0; i < AllRoles.Count; i++)
        {
            CustomRoles role = AllRoles[i];
            switch (role.GetVNRole())
            {
                case CustomRoles.Scientist: addScientistNum++; break;
                case CustomRoles.Engineer: addEngineerNum++; break;
                case CustomRoles.Shapeshifter: addShapeshifterNum++; break;
            }
        }
    }

    public static List<CustomRoles> AddonRolesList = [];
    public static void SelectAddonRoles()
    {
        if (Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop) return;

        foreach (var id in Main.SetAddOns.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetAddOns.Remove(id);

        AddonRolesList = [];
        System.Collections.IList list = Enum.GetValues(typeof(CustomRoles));
        for (int i = 0; i < list.Count; i++)
        {
            object cr = list[i];
            CustomRoles role = (CustomRoles)Enum.Parse(typeof(CustomRoles), cr.ToString());
            if (!role.IsAdditionRole()) continue;
            switch (role)
            {
                case CustomRoles.Mare when (MapNames)Main.NormalOptions.MapId == MapNames.Fungle:
                case CustomRoles.Madmate when Options.MadmateSpawnMode.GetInt() != 0:
                case CustomRoles.Lovers or CustomRoles.LastImpostor or CustomRoles.Workhorse:
                case CustomRoles.Nimble or CustomRoles.Physicist: // Assigned at a different function due to role base change
                    continue;
            }
            AddonRolesList.Add(role);
        }
    }
}