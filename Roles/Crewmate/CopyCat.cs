using System.Collections.Generic;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class CopyCat : RoleBase
{
    private const int Id = 666420;
    public static List<CopyCat> Instances = [];
    public static List<byte> PlayerIdList = [];
    private static bool Resetting;

    private static OptionItem KillCooldown;
    private static OptionItem CanKill;
    private static OptionItem CopyCrewVar;
    private static OptionItem CopyCrewVarEvenIfDisabled;
    private static OptionItem MiscopyLimitOpt;
    private static OptionItem ResetToCopyCatEachRound;
    public static OptionItem UsePet;

    public PlayerControl CopyCatPC;
    private float CurrentKillCooldown = AdjustedDefaultKillCooldown;
    private float TempLimit;

    public override bool IsEnable => Instances.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.CopyCat);

        KillCooldown = new FloatOptionItem(Id + 10, "CopyCatCopyCooldown", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat])
            .SetValueFormat(OptionFormat.Seconds);

        CanKill = new BooleanOptionItem(Id + 11, "CopyCatCanKill", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat]);

        CopyCrewVar = new BooleanOptionItem(Id + 13, "CopyCrewVar", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat]);
        
        CopyCrewVarEvenIfDisabled = new BooleanOptionItem(Id + 15, "CopyCrewVarEvenIfDisabled", true, TabGroup.CrewmateRoles)
            .SetParent(CopyCrewVar);

        MiscopyLimitOpt = new IntegerOptionItem(Id + 12, "CopyCatMiscopyLimit", new(0, 14, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CanKill)
            .SetValueFormat(OptionFormat.Times);

        ResetToCopyCatEachRound = new BooleanOptionItem(Id + 9, "CopyCatResetToCopyCatEachRound", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CopyCat]);

        UsePet = CreatePetUseSetting(Id + 14, CustomRoles.CopyCat);
    }

    public override void Init()
    {
        if (Resetting) return;
        Instances = [];
        PlayerIdList = [];
        CurrentKillCooldown = AdjustedDefaultKillCooldown;
    }

    public override void Add(byte playerId)
    {
        if (Resetting) return;
        Instances.Add(this);
        PlayerIdList.Add(playerId);
        CopyCatPC = Utils.GetPlayerById(playerId);
        CurrentKillCooldown = KillCooldown.GetFloat();
        int limit = MiscopyLimitOpt.GetInt();
        playerId.SetAbilityUseLimit(limit);
        TempLimit = limit;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CurrentKillCooldown;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    private void ResetRole()
    {
        CopyCatPC.RpcSetCustomRole(CustomRoles.CopyCat);
        CopyCatPC.RpcChangeRoleBasis(CustomRoles.CopyCat);
        Main.PlayerStates[CopyCatPC.PlayerId].Role = this;
        SetKillCooldown(CopyCatPC.PlayerId);
        CopyCatPC.SetAbilityUseLimit(TempLimit);
        CopyCatPC.SyncSettings();
    }

    public static void ResetRoles()
    {
        if (!ResetToCopyCatEachRound.GetBool()) return;
        
        Resetting = true;

        try { Instances.Do(x => x.ResetRole()); }
        finally { Resetting = false; }
    }

    public override bool OnCheckMurder(PlayerControl pc, PlayerControl tpc)
    {
        CustomRoles role = tpc.GetCustomRole();

        if (CopyCrewVar.GetBool())
        {
            var newRole = role switch
            {
                CustomRoles.Swooper or CustomRoles.Wraith => CustomRoles.Chameleon,
                CustomRoles.Stealth or CustomRoles.Nonplus => CustomRoles.Grenadier,
                CustomRoles.TimeThief => CustomRoles.TimeManager,
                CustomRoles.Consigliere or CustomRoles.Ritualist or CustomRoles.PotionMaster => CustomRoles.Investigator,
                CustomRoles.AntiAdminer => new[] {CustomRoles.Sensor, CustomRoles.Telecommunication}.RandomElement(),
                CustomRoles.CursedWolf or CustomRoles.Jinx or CustomRoles.Goddess => CustomRoles.Veteran,
                CustomRoles.EvilTracker => new[] {CustomRoles.Scout, CustomRoles.TrackerEHR}.RandomElement(),
                CustomRoles.Mercenary => CustomRoles.Addict,
                CustomRoles.Miner => CustomRoles.Mole,
                CustomRoles.Escapist or CustomRoles.Enderman => CustomRoles.Tunneler,
                CustomRoles.Twister => CustomRoles.TimeMaster,
                CustomRoles.Disperser => CustomRoles.Transporter,
                CustomRoles.EvilEraser => CustomRoles.NiceEraser,
                CustomRoles.Visionary => CustomRoles.Oracle,
                CustomRoles.Workaholic or CustomRoles.Pawn => CustomRoles.Snitch,
                CustomRoles.Sunnyboy or CustomRoles.Fabricator => new[] {CustomRoles.Doctor, CustomRoles.ScientistEHR}.RandomElement(),
                CustomRoles.Vindicator or CustomRoles.Pickpocket => CustomRoles.Mayor,
                CustomRoles.Councillor or CustomRoles.Magistrate => CustomRoles.Judge,
                CustomRoles.EvilGuesser or CustomRoles.Doomsayer or CustomRoles.NecroGuesser or CustomRoles.Augur => CustomRoles.NiceGuesser,
                CustomRoles.Vengeance => CustomRoles.Adrenaline,
                CustomRoles.Occultist => CustomRoles.Altruist,
                CustomRoles.Dealer or CustomRoles.Catalyst => CustomRoles.Merchant,
                CustomRoles.Romantic => CustomRoles.Aid,
                CustomRoles.Chemist => CustomRoles.Alchemist,
                CustomRoles.Bomber or CustomRoles.Nuker => CustomRoles.Tree,
                CustomRoles.Siren => CustomRoles.Rhapsode,
                CustomRoles.Consort => CustomRoles.Escort,
                CustomRoles.Parasite => CustomRoles.DoubleAgent,
                CustomRoles.BloodKnight or CustomRoles.Wildling => CustomRoles.Safeguard,
                CustomRoles.PlagueBearer => CustomRoles.Socialite,
                CustomRoles.Demon => CustomRoles.Spy,
                CustomRoles.Undertaker or CustomRoles.Vortex => CustomRoles.Vacuum,
                CustomRoles.Kidnapper => new[] {CustomRoles.Autocrat, CustomRoles.Carrier}.RandomElement(),
                CustomRoles.Capitalist => CustomRoles.Helper,
                CustomRoles.Technician or CustomRoles.Saboteur => CustomRoles.Mechanic,
                CustomRoles.Inhibitor => new[] {CustomRoles.Doorjammer, CustomRoles.SecurityGuard}.RandomElement(),
                CustomRoles.Magician or CustomRoles.Curser => CustomRoles.Wizard,
                CustomRoles.Penguin => CustomRoles.Goose,
                CustomRoles.Anonymous => CustomRoles.Paranoid,
                CustomRoles.SoulCatcher or CustomRoles.Swapster => CustomRoles.Transporter,
                CustomRoles.NoteKiller => CustomRoles.Decryptor,
                CustomRoles.Gaslighter => new[] {CustomRoles.Medic, CustomRoles.Monarch}.RandomElement(),
                CustomRoles.Timelord => CustomRoles.Deputy,
                CustomRoles.Amnesiac => CustomRoles.Tracefinder,
                CustomRoles.SerialKiller => CustomRoles.Sheriff,
                CustomRoles.Maverick => CustomRoles.Vigilante,
                CustomRoles.Hookshot or CustomRoles.Ninja => new[] {CustomRoles.Tether, CustomRoles.Transmitter}.RandomElement(),
                CustomRoles.Hypocrite or CustomRoles.Specter => CustomRoles.Speedrunner,
                CustomRoles.Bandit => CustomRoles.Cleanser,
                CustomRoles.Centralizer => CustomRoles.Convener,
                CustomRoles.Weatherman => CustomRoles.Tornado,
                _ => role
            };
            
            if (newRole.GetMode() != 0 || CopyCrewVarEvenIfDisabled.GetBool())
                role = newRole;
        }

        if (tpc.IsCrewmate() && !tpc.Is(CustomRoles.Rascal) && !tpc.Is(CustomRoles.Jailor) && !tpc.IsConverted())
        {
            TempLimit = pc.GetAbilityUseLimit();

            pc.RpcSetCustomRole(role);
            pc.RpcChangeRoleBasis(role);
            pc.SetAbilityUseLimit(tpc.GetAbilityUseLimit());

            pc.Notify(string.Format(GetString("CopyCatRoleChange"), Utils.GetRoleName(role)));
            pc.SyncSettings();

            LateTask.New(() => pc.SetKillCooldown(), 0.2f, log: false);
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