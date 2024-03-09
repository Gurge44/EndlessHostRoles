using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public class CopyCat : RoleBase
{
    private const int Id = 666420;
    public static List<byte> playerIdList = [];

    public float CurrentKillCooldown = DefaultKillCooldown;

    public static OptionItem KillCooldown;
    public static OptionItem CanKill;
    public static OptionItem CopyCrewVar;
    public static OptionItem MiscopyLimitOpt;
    public static OptionItem UsePet;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.CopyCat);
        KillCooldown = FloatOptionItem.Create(Id + 10, "CopyCatCopyCooldown", new(0f, 60f, 1f), 15f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat])
            .SetValueFormat(OptionFormat.Seconds);
        CanKill = BooleanOptionItem.Create(Id + 11, "CopyCatCanKill", false, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat]);
        CopyCrewVar = BooleanOptionItem.Create(Id + 13, "CopyCrewVar", true, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat]);
        MiscopyLimitOpt = IntegerOptionItem.Create(Id + 12, "CopyCatMiscopyLimit", new(0, 14, 1), 2, TabGroup.OtherRoles, false).SetParent(CanKill)
            .SetValueFormat(OptionFormat.Times);
        UsePet = CreatePetUseSetting(Id + 14, CustomRoles.CopyCat);
    }

    public override void Init()
    {
        playerIdList = [];
        CurrentKillCooldown = DefaultKillCooldown;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        CurrentKillCooldown = KillCooldown.GetFloat();
        playerId.SetAbilityUseLimit(MiscopyLimitOpt.GetInt());

        if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Utils.GetPlayerById(id).IsAlive() ? CurrentKillCooldown : 0f;

    public static void ResetRole()
    {
        foreach (var player in playerIdList)
        {
            var pc = Utils.GetPlayerById(player);
            var role = pc.GetCustomRole();
            ////////////           /*remove the settings for current role*/             /////////////////////
            switch (role)
            {
                case CustomRoles.Cleanser:
                    Cleanser.DidVote.Remove(pc.PlayerId);
                    break;
                case CustomRoles.Merchant:
                    Merchant.addonsSold.Remove(player);
                    Merchant.bribedKiller.Remove(player);
                    break;
                case CustomRoles.Paranoia:
                    Main.ParaUsedButtonCount.Remove(player);
                    break;
                case CustomRoles.Snitch:
                    Snitch.IsExposed.Remove(player);
                    Snitch.IsComplete.Remove(player);
                    break;
                case CustomRoles.Mayor:
                    Main.MayorUsedButtonCount.Remove(player);
                    break;
            }

            pc.RpcSetCustomRole(CustomRoles.CopyCat);
            (Main.PlayerStates[player].Role as CopyCat)?.SetKillCooldown(player);
        }
    }

    public static bool BlacklList(CustomRoles role) => role is
        CustomRoles.CopyCat or
        //bcoz of vent cd
        CustomRoles.Grenadier or
        CustomRoles.Lighter or
        CustomRoles.SecurityGuard or
        CustomRoles.Ventguard or
        CustomRoles.DovesOfNeace or
        CustomRoles.Veteran or
        CustomRoles.Addict or
        CustomRoles.Alchemist or
        CustomRoles.Chameleon or
        //bcoz im lazy
        CustomRoles.Escort or
        CustomRoles.DonutDelivery or
        CustomRoles.Gaulois or
        CustomRoles.NiceSwapper or
        CustomRoles.Analyzer or
        //bcoz of arrows
        CustomRoles.Mortician or
        CustomRoles.Bloodhound or
        CustomRoles.Tracefinder or
        CustomRoles.Spiritualist or
        CustomRoles.Tracker;

    public override bool OnCheckMurder(PlayerControl pc, PlayerControl tpc)
    {
        CustomRoles role = tpc.GetCustomRole();
        if (BlacklList(role))
        {
            pc.Notify(GetString("CopyCatCanNotCopy"));
            SetKillCooldown(pc.PlayerId);
            return false;
        }

        if (CopyCrewVar.GetBool())
        {
            role = role switch
            {
                CustomRoles.Eraser => CustomRoles.Cleanser,
                CustomRoles.Visionary => CustomRoles.Oracle,
                CustomRoles.Workaholic => CustomRoles.Snitch,
                CustomRoles.Sunnyboy => CustomRoles.Doctor,
                CustomRoles.Vindicator or CustomRoles.Pickpocket => CustomRoles.Mayor,
                CustomRoles.Councillor => CustomRoles.Judge,
                CustomRoles.EvilGuesser or CustomRoles.Doomsayer or CustomRoles.Ritualist => CustomRoles.NiceGuesser,
                _ => role,
            };
        }

        if (role.IsCrewmate() && tpc.GetCustomSubRoles().All(x => x != CustomRoles.Rascal))
        {
            ////////////           /*add the settings for new role*/            ////////////
            /* anything that is assigned in onGameStartedPatch.cs comes here */

            pc.RpcSetCustomRole(role);
            pc.SetAbilityUseLimit(tpc.GetAbilityUseLimit());

            pc.SetKillCooldown();
            pc.Notify(string.Format(GetString("CopyCatRoleChange"), Utils.GetRoleName(role)));
            return false;
        }

        if (CanKill.GetBool())
        {
            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                SetKillCooldown(pc.PlayerId);
                return true;
            }

            pc.Suicide();
            return false;
        }

        pc.Notify(GetString("CopyCatCanNotCopy"));
        SetKillCooldown(pc.PlayerId);
        return false;
    }
}