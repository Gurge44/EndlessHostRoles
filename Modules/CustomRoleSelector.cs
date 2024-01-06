using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Neutral;

namespace TOHE.Modules;

internal class CustomRoleSelector
{
    public static Dictionary<PlayerControl, CustomRoles> RoleResult;
    public static IReadOnlyList<CustomRoles> AllRoles => RoleResult.Values.ToList();

    public static void SelectCustomRoles()
    {
        RoleResult = [];
        var rd = IRandom.Instance;
        int playerCount = Main.AllAlivePlayerControls.Length;
        int optImpNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors);
        int optNonNeutralKillingNum = 0;
        int optNeutralKillingNum = 0;
        //int optCovenNum = 0;

        if (Options.NonNeutralKillingRolesMaxPlayer.GetInt() > 0 && Options.NonNeutralKillingRolesMaxPlayer.GetInt() >= Options.NonNeutralKillingRolesMinPlayer.GetInt())
        {
            optNonNeutralKillingNum = rd.Next(Options.NonNeutralKillingRolesMinPlayer.GetInt(), Options.NonNeutralKillingRolesMaxPlayer.GetInt() + 1);
        }
        if (Options.NeutralKillingRolesMaxPlayer.GetInt() > 0 && Options.NeutralKillingRolesMaxPlayer.GetInt() >= Options.NeutralKillingRolesMinPlayer.GetInt())
        {
            optNeutralKillingNum = rd.Next(Options.NeutralKillingRolesMinPlayer.GetInt(), Options.NeutralKillingRolesMaxPlayer.GetInt() + 1);
        }
        // if (Options.CovenRolesMaxPlayer.GetInt() > 0 && Options.CovenRolesMaxPlayer.GetInt() >= Options.CovenRolesMinPlayer.GetInt())
        // {
        //    optCovenNum = rd.Next(Options.CovenRolesMinPlayer.GetInt(), Options.CovenRolesMaxPlayer.GetInt() + 1);
        // }

        int readyRoleNum = 0;
        int readyNonNeutralKillingNum = 0;
        int readyNeutralKillingNum = 0;

        List<CustomRoles> rolesToAssign = [];
        List<CustomRoles> roleList = [];
        List<CustomRoles> roleOnList = [];

        List<CustomRoles> ImpOnList = [];
        List<CustomRoles> ImpRateList = [];

        List<CustomRoles> NonNeutralKillingOnList = [];
        List<CustomRoles> NonNeutralKillingRateList = [];

        List<CustomRoles> NeutralKillingOnList = [];
        List<CustomRoles> NeutralKillingRateList = [];

        List<CustomRoles> roleRateList = [];

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
                    if (pc.PlayerId == 0 || pc.IsModClient() || pc.PlayerId == PlayerControl.LocalPlayer.PlayerId || pc.GetClientId() == Main.HostClientId) RoleResult.Add(pc, CustomRoles.DonutDelivery);
                    else RoleResult.Add(pc, CustomRoles.Tasker);
                }
                return;
        }

        foreach (var id in Main.SetRoles.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) Main.SetRoles.Remove(id);

        Dictionary<CustomRoles, (int ASSIGNED, int COUNT)> RoleCounts = [];

        System.Collections.IList list = Enum.GetValues(typeof(CustomRoles));
        for (int i1 = 0; i1 < list.Count; i1++)
        {
            object cr = list[i1];
            CustomRoles role = (CustomRoles)Enum.Parse(typeof(CustomRoles), cr.ToString());
            if (role.IsVanilla() || role.IsAdditionRole() || (CustomRolesHelper.OnlySpawnsWithPetsRoleList.Contains(role) && !Options.UsePets.GetBool()) || (!Main.UseVersionProtocol.Value && !role.IsAbleToHostPublic())) continue;
            switch (role)
            {
                case CustomRoles.DarkHide when (MapNames)Main.NormalOptions.MapId == MapNames.Fungle:
                case CustomRoles.Pelican when roleList.Contains(CustomRoles.Duellist):
                case CustomRoles.Duellist when roleList.Contains(CustomRoles.Pelican):
                case CustomRoles.GM:
                case CustomRoles.NotAssigned:
                    continue;
            }
            var roleCount = role.GetCount();
            RoleCounts[role] = (0, roleCount);
            for (int i = 0; i < roleCount; i++)
                roleList.Add(role);
        }

        Logger.Info(string.Join(", ", RoleCounts.Select(x => $"{x.Key}: {x.Value.COUNT}")), "RoleCounts");

        // Career setting: priority
        for (int i2 = 0; i2 < roleList.Count; i2++)
        {
            CustomRoles role = roleList[i2];
            if (role.GetMode() == 100)
            {
                if (role.IsImpostor()) ImpOnList.Add(role);
                else if (role.IsNonNK()) NonNeutralKillingOnList.Add(role);
                else if (role.IsNK()) NeutralKillingOnList.Add(role);
                else roleOnList.Add(role);
            }
        }
        // Add pre-set roles by host as priority roles
        foreach (var role in Main.SetRoles.Values.ToArray())
        {
            if (role.IsImpostor() && !ImpOnList.Contains(role)) ImpOnList.Add(role);
            else if (role.IsNonNK() && !NonNeutralKillingOnList.Contains(role)) NonNeutralKillingOnList.Add(role);
            else if (role.IsNK() && !NeutralKillingOnList.Contains(role)) NeutralKillingOnList.Add(role);
            else if (!roleOnList.Contains(role)) roleOnList.Add(role);
        }
        // Career settings are: enabled (percentage is 1-99%)
        for (int i3 = 0; i3 < roleList.Count; i3++)
        {
            CustomRoles role = roleList[i3];
            int chance = role.GetMode();
            if (chance is < 100 and > 0)
            {
                chance /= 5;
                if (role.IsImpostor())
                {
                    for (int i = 0; i < chance; i++)
                    {
                        ImpRateList.Add(role);
                    }
                }
                else if (role.IsNonNK())
                {
                    for (int i = 0; i < chance; i++)
                    {
                        NonNeutralKillingRateList.Add(role);
                    }
                }
                else if (role.IsNK())
                {
                    for (int i = 0; i < chance; i++)
                    {
                        NeutralKillingRateList.Add(role);
                    }
                }
                else
                {
                    for (int i = 0; i < chance; i++)
                    {
                        roleRateList.Add(role);
                    }
                }
            }
        }

        if (ImpRateList.Count == 0 && ImpOnList.Count == 0 && Main.SetRoles.Count == 0 && Main.DevRole.Count == 0)
        {
            for (int i = 0; i < optImpNum; i++)
            {
                ImpOnList.Add(CustomRoles.ImpostorTOHE);
                Logger.Warn("No Impostor roles are enabled, adding a vanilla impostor to the game", "CustomRoleSelector");
            }
        }

        LogRoleChancePool();

        void LogRoleChancePool()
        {
            Logger.Info(string.Join(", ", ImpRateList.Select(p => p.ToString())), "ImpChancePool");
            Logger.Info(string.Join(", ", NeutralKillingRateList.Select(p => p.ToString())), "NKChancePool");
            Logger.Info(string.Join(", ", NonNeutralKillingRateList.Select(p => p.ToString())), "NNKChancePool");
            Logger.Info(string.Join(", ", roleRateList.Select(p => p.ToString())), "CrewChancePool");

            Logger.Info(string.Join(", ", RoleCounts.Select(p => $"{p.Key}: {p.Value.ASSIGNED}/{p.Value.COUNT}")), "AssignedCount");
        }

        // Assign roles set to ALWAYS (impostors)
        while (ImpOnList.Count > 0)
        {
            var select = ImpOnList[rd.Next(0, ImpOnList.Count)];
            ImpOnList.Remove(select);
            rolesToAssign.Add(select);
            readyRoleNum++;
            Logger.Info(select.ToString() + " joins the impostor role waiting list (priority)", "CustomRoleSelector");
            if (readyRoleNum >= playerCount) goto EndOfAssign;
            if (readyRoleNum >= optImpNum) break;
        }
        // The priority profession is not enough to allocate, start to allocate the enabled roles (impostors)
        if (readyRoleNum < playerCount && readyRoleNum < optImpNum)
        {
            while (ImpRateList.Count > 0)
            {
                var select = ImpRateList[rd.Next(0, ImpRateList.Count)];
                var (ASSIGNED, COUNT) = RoleCounts[select];
                rolesToAssign.Add(select);
                readyRoleNum++;
                Logger.Info(select.ToString() + " added to the impostor role waiting list", "CustomRoleSelector");
                RoleCounts[select] = (ASSIGNED + 1, COUNT);
                if (ASSIGNED + 1 >= COUNT) while (ImpRateList.Contains(select)) ImpRateList.Remove(select);
                if (readyRoleNum >= playerCount) goto EndOfAssign;
                if (readyRoleNum >= optImpNum) break;
            }
        }

        // Select NonNeutralKilling "Always"
        while (NonNeutralKillingOnList.Count > 0 && optNonNeutralKillingNum > 0)
        {
            var select = NonNeutralKillingOnList[rd.Next(0, NonNeutralKillingOnList.Count)];
            NonNeutralKillingOnList.Remove(select);
            rolesToAssign.Add(select);
            readyRoleNum++;
            readyNonNeutralKillingNum += select.GetCount();
            Logger.Info(select.ToString() + " added to neutral role candidate list (priority)", "CustomRoleSelector");
            if (readyRoleNum >= playerCount) goto EndOfAssign;
            if (readyNonNeutralKillingNum >= optNonNeutralKillingNum) break;
        }

        // Select NonNeutralKilling "Random"
        if (readyRoleNum < playerCount && readyNonNeutralKillingNum < optNonNeutralKillingNum)
        {
            while (NonNeutralKillingRateList.Count > 0 && optNonNeutralKillingNum > 0)
            {
                var select = NonNeutralKillingRateList[rd.Next(0, NonNeutralKillingRateList.Count)];
                var (ASSIGNED, COUNT) = RoleCounts[select];
                rolesToAssign.Add(select);
                readyRoleNum++;
                readyNonNeutralKillingNum += select.GetCount();
                Logger.Info(select.ToString() + " added to neutral role waiting list", "CustomRoleSelector");
                RoleCounts[select] = (ASSIGNED + 1, COUNT);
                if (ASSIGNED + 1 >= COUNT) while (NonNeutralKillingRateList.Contains(select)) NonNeutralKillingRateList.Remove(select);
                if (readyRoleNum >= playerCount) goto EndOfAssign;
                if (readyNonNeutralKillingNum >= optNonNeutralKillingNum) break;
            }
        }

        // Select NeutralKilling "Always"
        while (NeutralKillingOnList.Count > 0 && optNeutralKillingNum > 0)
        {
            var select = NeutralKillingOnList[rd.Next(0, NeutralKillingOnList.Count)];
            NeutralKillingOnList.Remove(select);
            rolesToAssign.Add(select);
            readyRoleNum++;
            readyNeutralKillingNum += select.GetCount();
            Logger.Info(select.ToString() + " added to neutral role candidate list (priority)", "CustomRoleSelector");
            if (readyRoleNum >= playerCount) goto EndOfAssign;
            if (readyNeutralKillingNum >= optNeutralKillingNum) break;
        }

        // Select NeutralKilling "Random"
        if (readyRoleNum < playerCount && readyNeutralKillingNum < optNeutralKillingNum)
        {
            while (NeutralKillingRateList.Count > 0 && optNeutralKillingNum > 0)
            {
                var select = NeutralKillingRateList[rd.Next(0, NeutralKillingRateList.Count)];
                var (ASSIGNED, COUNT) = RoleCounts[select];
                rolesToAssign.Add(select);
                readyRoleNum++;
                readyNeutralKillingNum += select.GetCount();
                Logger.Info(select.ToString() + " added to neutral role waiting list", "CustomRoleSelector");
                RoleCounts[select] = (ASSIGNED + 1, COUNT);
                if (ASSIGNED + 1 >= COUNT) while (NeutralKillingRateList.Contains(select)) NeutralKillingRateList.Remove(select);
                if (readyRoleNum >= playerCount) goto EndOfAssign;
                if (readyNeutralKillingNum >= optNeutralKillingNum) break;
            }
        }

        // Assign roles set to ALWAYS
        while (roleOnList.Count > 0)
        {
            var select = roleOnList[rd.Next(0, roleOnList.Count)];
            roleOnList.Remove(select);
            rolesToAssign.Add(select);
            readyRoleNum++;
            Logger.Info(select.ToString() + " joined the crew role waiting list (priority)", "CustomRoleSelector");
            if (readyRoleNum >= playerCount) goto EndOfAssign;
        }
        // There are not enough priority occupations to allocate. Start allocating enabled occupations.
        if (readyRoleNum < playerCount)
        {
            while (roleRateList.Count > 0)
            {
                var select = roleRateList[rd.Next(0, roleRateList.Count)];
                var (ASSIGNED, COUNT) = RoleCounts[select];
                rolesToAssign.Add(select);
                readyRoleNum++;
                Logger.Info(select.ToString() + " joined the crew role waiting list", "CustomRoleSelector");
                RoleCounts[select] = (ASSIGNED + 1, COUNT);
                if (ASSIGNED + 1 >= COUNT) while (roleRateList.Contains(select)) roleRateList.Remove(select);
                if (readyRoleNum >= playerCount) goto EndOfAssign;
            }
        }

    EndOfAssign:

        LogRoleChancePool();

        if (rd.Next(0, 100) < Options.SunnyboyChance.GetInt() && rolesToAssign.Remove(CustomRoles.Jester)) rolesToAssign.Add(CustomRoles.Sunnyboy);
        if (rd.Next(0, 100) < Sans.BardChance.GetInt() && rolesToAssign.Remove(CustomRoles.Sans)) rolesToAssign.Add(CustomRoles.Bard);
        if (rd.Next(0, 100) < Options.NukerChance.GetInt() && rolesToAssign.Remove(CustomRoles.Bomber)) rolesToAssign.Add(CustomRoles.Nuker);


        if (Romantic.IsEnable)
        {
            if (rolesToAssign.Contains(CustomRoles.Romantic) && rolesToAssign.Contains(CustomRoles.Lovers))
                rolesToAssign.Remove(CustomRoles.Lovers);
        }

        // Players on the EAC banned list will be assigned as basic ass crewmates when opening rooms
        if (BanManager.CheckEACList(PlayerControl.LocalPlayer.FriendCode, PlayerControl.LocalPlayer.GetClient().GetHashedPuid()))
        {
            if (!rolesToAssign.Contains(CustomRoles.CrewmateTOHE))
                rolesToAssign.Add(CustomRoles.CrewmateTOHE);
            Main.DevRole.Remove(PlayerControl.LocalPlayer.PlayerId);
            Main.DevRole.Add(PlayerControl.LocalPlayer.PlayerId, CustomRoles.CrewmateTOHE);
        }

        // Dev Roles List Edit
        foreach (var dr in Main.DevRole)
        {
            if (dr.Key == PlayerControl.LocalPlayer.PlayerId && Options.EnableGM.GetBool()) continue;
            if (rolesToAssign.Contains(dr.Value))
            {
                rolesToAssign.Remove(dr.Value);
                rolesToAssign.Insert(dr.Key, dr.Value);
                Logger.Info("Occupation list improved priority：" + dr.Value, "Dev Role");
                continue;
            }
            for (int i = 0; i < rolesToAssign.Count; i++)
            {
                var role = rolesToAssign[i];
                if (dr.Value.GetMode() != role.GetMode()) continue;
                if (
                    (dr.Value.IsImpostor() && role.IsImpostor()) ||
                    (dr.Value.IsNonNK() && role.IsNonNK()) ||
                    (dr.Value.IsNK() && role.IsNK()) ||
                    (dr.Value.IsCrewmate() & role.IsCrewmate())
                    )
                {
                    rolesToAssign.RemoveAt(i);
                    rolesToAssign.Insert(dr.Key, dr.Value);
                    Logger.Info($"Coverage occupation list：{i} {role} => {dr.Value}", "Dev Role");
                    break;
                }
            }
        }

        var AllPlayer = Main.AllAlivePlayerControls.ToList();

        foreach (var item in Main.SetRoles)
        {
            if (item.Key == PlayerControl.LocalPlayer.PlayerId && Options.EnableGM.GetBool()) continue;

            rolesToAssign.Remove(item.Value);
            rolesToAssign.Insert(item.Key, item.Value);

            Logger.Warn($"Override {Main.AllPlayerNames[item.Key]}'s role to their role set by host: {Translator.GetString(item.Value.ToString())}", "CustomRoleSelector");
        }

        while (AllPlayer.Count > 0 && rolesToAssign.Count > 0)
        {
            PlayerControl delPc = null;
            for (int i = 0; i < AllPlayer.Count; i++)
            {
                PlayerControl pc = AllPlayer[i];
                foreach (var dr in Main.DevRole.Where(x => pc.PlayerId == x.Key))
                {
                    if (dr.Key == PlayerControl.LocalPlayer.PlayerId && Options.EnableGM.GetBool()) continue;
                    var id = rolesToAssign.IndexOf(dr.Value);
                    if (id == -1) continue;
                    RoleResult.Add(pc, rolesToAssign[id]);
                    Logger.Info($"Role priority allocation：{AllPlayer[0].GetRealName()} => {rolesToAssign[id]}", "CustomRoleSelector");
                    delPc = pc;
                    rolesToAssign.RemoveAt(id);
                    goto EndOfWhile;
                }
                foreach (var item in Main.SetRoles.Where(x => pc.PlayerId == x.Key))
                {
                    if (item.Key == PlayerControl.LocalPlayer.PlayerId && Options.EnableGM.GetBool()) continue;

                    var index = rolesToAssign.IndexOf(item.Value);
                    if (index == -1) continue;

                    RoleResult.Add(pc, rolesToAssign[index]);
                    Logger.Warn($"Assign Overridden Role: {AllPlayer[0].GetRealName()} => {rolesToAssign[index]}", "CustomRoleSelector");

                    delPc = pc;

                    rolesToAssign.RemoveAt(index);

                    goto EndOfWhile;
                }
            }

            var roleId = rd.Next(0, rolesToAssign.Count);

            CustomRoles assignedRole;
            if (Main.SetRoles.TryGetValue(AllPlayer[0].PlayerId, out var preSetRole))
            {
                assignedRole = preSetRole;
                Main.SetRoles.Remove(AllPlayer[0].PlayerId);

            }
            else assignedRole = rolesToAssign[roleId];

            RoleResult.Add(AllPlayer[0], assignedRole);
            Logger.Info($"Role assigned：{AllPlayer[0].GetRealName()} => {rolesToAssign[roleId]}", "CustomRoleSelector");

            AllPlayer.RemoveAt(0);
            rolesToAssign.RemoveAt(roleId);

        EndOfWhile:;
            if (delPc != null)
            {
                AllPlayer.Remove(delPc);
                Main.DevRole.Remove(delPc.PlayerId);
                Main.SetRoles.Remove(delPc.PlayerId);
            }
        }

        if (AllPlayer.Count > 0)
            Logger.Error("Role assignment error: There are players who have not been assigned a role", "CustomRoleSelector");
        if (rolesToAssign.Count > 0)
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
            if (!Main.UseVersionProtocol.Value && !role.IsAbleToHostPublic()) continue;
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