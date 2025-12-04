using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Impostor;

internal class Changeling : RoleBase
{
    private const int Id = 643510;
    public static readonly Dictionary<byte, bool> ChangedRole = [];

    private static OptionItem CanPickPartnerRole;
    public static OptionItem CanKillBeforeRoleChange;
    private static OptionItem AvailableRoles;

    private static readonly string[] AvailableRolesMode =
    [
        "CL.All", // 0
        "CL.Enabled" // 1
    ];

    private static List<CustomRoles> Roles = [];
    public static bool On;
    private byte ChangelingId;

    public CustomRoles CurrentRole;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Changeling);
        CanPickPartnerRole = new BooleanOptionItem(Id + 10, "CanPickPartnerRole", true, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
        CanKillBeforeRoleChange = new BooleanOptionItem(Id + 11, "CanKillBeforeRoleChange", true, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
        AvailableRoles = new StringOptionItem(Id + 12, "AvailableRoles", AvailableRolesMode, 1, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
    }

    public static List<CustomRoles> GetAvailableRoles(bool check = false)
    {
        try
        {
            CustomRoles[] allRoles = Enum.GetValues<CustomRoles>();

            IEnumerable<CustomRoles> result = AvailableRoles.GetValue() switch
            {
                0 => allRoles,
                1 => allRoles.Where(x => x.GetMode() != 0),
                _ => allRoles
            };

            if (!CanPickPartnerRole.GetBool() && !check) result = result.Where(x => !CustomRoleSelector.RoleResult.ContainsValue(x));

            List<CustomRoles> rolesList = result.ToList();
            rolesList.Remove(CustomRoles.Changeling);
            rolesList.Remove(CustomRoles.Loner);
            rolesList.RemoveAll(x => !x.IsImpostor() || x.IsVanilla() || x.IsAdditionRole());
            return rolesList;
        }
        catch { return []; }
    }

    public override void Add(byte playerId)
    {
        On = true;
        ChangelingId = playerId;
        ChangedRole[playerId] = false;

        if (Roles.Count == 0)
        {
            Logger.Error("No roles for Changeling", "Changeling");
            Utils.GetPlayerById(playerId).RpcSetCustomRole(CustomRoles.ImpostorEHR);
            return;
        }

        CurrentRole = Roles[0];
        Utils.SendRPC(CustomRPC.SyncChangeling, playerId, (int)CurrentRole);
    }

    public override void Init()
    {
        On = false;
        ChangedRole.Clear();
        Roles = GetAvailableRoles();
    }

    private void SelectNextRole(PlayerControl pc)
    {
        int currentIndex = Roles.IndexOf(CurrentRole);
        CurrentRole = currentIndex == Roles.Count - 1 ? Roles[0] : Roles[currentIndex + 1];
        Utils.SendRPC(CustomRPC.SyncChangeling, pc.PlayerId, (int)CurrentRole);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return CanKillBeforeRoleChange.GetBool();
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        SelectNextRole(pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        SelectNextRole(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        LateTask.New(() =>
        {
            pc.RpcSetCustomRole(CurrentRole);
            pc.RpcChangeRoleBasis(CurrentRole);
            ChangedRole[pc.PlayerId] = true;
            pc.RpcResetAbilityCooldown();
        }, 0.3f, log: false);

        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = 0.1f;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        return seer.PlayerId != target.PlayerId || ChangelingId != seer.PlayerId || (seer.IsModdedClient() && !hud) || meeting ? string.Empty : string.Format(Translator.GetString("ChangelingCurrentRole"), CurrentRole.ToColoredString());
    }
}