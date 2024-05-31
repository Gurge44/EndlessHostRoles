using System.Collections.Generic;
using System.Linq;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Crewmate;

public class CopyCat : RoleBase
{
    private const int Id = 666420;
    public static List<byte> PlayerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem CanKill;
    public static OptionItem CopyCrewVar;
    public static OptionItem MiscopyLimitOpt;
    public static OptionItem UsePet;

    public float CurrentKillCooldown = DefaultKillCooldown;
    private float TempLimit;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.CopyCat);
        KillCooldown = new FloatOptionItem(Id + 10, "CopyCatCopyCooldown", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat])
            .SetValueFormat(OptionFormat.Seconds);
        CanKill = new BooleanOptionItem(Id + 11, "CopyCatCanKill", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat]);
        CopyCrewVar = new BooleanOptionItem(Id + 13, "CopyCrewVar", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat]);
        MiscopyLimitOpt = new IntegerOptionItem(Id + 12, "CopyCatMiscopyLimit", new(0, 14, 1), 2, TabGroup.CrewmateRoles).SetParent(CanKill)
            .SetValueFormat(OptionFormat.Times);
        UsePet = CreatePetUseSetting(Id + 14, CustomRoles.CopyCat);
    }

    public override void Init()
    {
        PlayerIdList = [];
        CurrentKillCooldown = DefaultKillCooldown;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        CurrentKillCooldown = KillCooldown.GetFloat();
        playerId.SetAbilityUseLimit(MiscopyLimitOpt.GetInt());

        if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Utils.GetPlayerById(id).IsAlive() ? CurrentKillCooldown : 0f;

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public static void ResetRole()
    {
        foreach (var player in PlayerIdList)
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
                    Paranoia.ParaUsedButtonCount.Remove(player);
                    break;
                case CustomRoles.Snitch:
                    Snitch.IsExposed.Remove(player);
                    Snitch.IsComplete.Remove(player);
                    break;
                case CustomRoles.Mayor:
                    Mayor.MayorUsedButtonCount.Remove(player);
                    break;
            }

            pc.RpcSetCustomRole(CustomRoles.CopyCat);
            if (Main.PlayerStates[player].Role is CopyCat cc)
            {
                pc.SetAbilityUseLimit(cc.TempLimit);
                cc.SetKillCooldown(player);
            }
        }
    }

    public static bool BlacklList(CustomRoles role) => role is
        CustomRoles.CopyCat or
        // can't copy due to vent cooldown
        CustomRoles.Grenadier or
        CustomRoles.Lighter or
        CustomRoles.SecurityGuard or
        CustomRoles.Ventguard or
        CustomRoles.DovesOfNeace or
        CustomRoles.Veteran or
        CustomRoles.Addict or
        CustomRoles.Chameleon;

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
                _ => role
            };
        }

        if (tpc.IsCrewmate() && tpc.GetCustomSubRoles().All(x => x != CustomRoles.Rascal))
        {
            ////////////           /*add the settings for new role*/            ////////////
            /* anything that is assigned in onGameStartedPatch.cs comes here */

            TempLimit = pc.GetAbilityUseLimit();

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