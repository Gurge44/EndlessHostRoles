using System.Collections.Generic;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate
{
    public class CopyCat : RoleBase
    {
        private const int Id = 666420;
        public static List<CopyCat> Instances = [];

        private static OptionItem KillCooldown;
        private static OptionItem CanKill;
        private static OptionItem CopyCrewVar;
        private static OptionItem MiscopyLimitOpt;
        public static OptionItem UsePet;

        public PlayerControl CopyCatPC;
        private float CurrentKillCooldown = DefaultKillCooldown;
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
            MiscopyLimitOpt = new IntegerOptionItem(Id + 12, "CopyCatMiscopyLimit", new(0, 14, 1), 2, TabGroup.CrewmateRoles)
                .SetParent(CanKill)
                .SetValueFormat(OptionFormat.Times);
            UsePet = CreatePetUseSetting(Id + 14, CustomRoles.CopyCat);
        }

        public override void Init()
        {
            Instances = [];
            CurrentKillCooldown = DefaultKillCooldown;
        }

        public override void Add(byte playerId)
        {
            Instances.Add(this);
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
            CustomRoles role = CopyCatPC.GetCustomRole();
            // Remove settings for current role
            switch (role)
            {
                case CustomRoles.Cleanser:
                    Cleanser.DidVote.Remove(CopyCatPC.PlayerId);
                    break;
                case CustomRoles.Merchant:
                    Merchant.AddonsSold.Remove(CopyCatPC.PlayerId);
                    Merchant.BribedKiller.Remove(CopyCatPC.PlayerId);
                    break;
                case CustomRoles.Paranoia:
                    Paranoia.ParaUsedButtonCount.Remove(CopyCatPC.PlayerId);
                    break;
                case CustomRoles.Snitch:
                    Snitch.IsExposed.Remove(CopyCatPC.PlayerId);
                    Snitch.IsComplete.Remove(CopyCatPC.PlayerId);
                    break;
                case CustomRoles.Mayor:
                    Mayor.MayorUsedButtonCount.Remove(CopyCatPC.PlayerId);
                    break;
            }

            Main.PlayerStates[CopyCatPC.PlayerId].MainRole = CustomRoles.CopyCat;
            Main.PlayerStates[CopyCatPC.PlayerId].Role = this;
            SetKillCooldown(CopyCatPC.PlayerId);
            CopyCatPC.SetAbilityUseLimit(TempLimit);
            CopyCatPC.RpcChangeRoleBasis(CustomRoles.CopyCat);
            CopyCatPC.SyncSettings();
        }

        public static void ResetRoles()
        {
            Instances.Do(x => x.ResetRole());
        }

        public override bool OnCheckMurder(PlayerControl pc, PlayerControl tpc)
        {
            CustomRoles role = tpc.GetCustomRole();

            if (CopyCrewVar.GetBool())
            {
                role = role switch
                {
                    CustomRoles.Swooper or CustomRoles.Wraith => CustomRoles.Chameleon,
                    CustomRoles.Stealth or CustomRoles.Nonplus => CustomRoles.Grenadier,
                    CustomRoles.TimeThief => CustomRoles.TimeManager,
                    CustomRoles.EvilDiviner or CustomRoles.Ritualist => CustomRoles.Farseer,
                    CustomRoles.AntiAdminer => CustomRoles.Monitor,
                    CustomRoles.CursedWolf or CustomRoles.Jinx => CustomRoles.Veteran,
                    CustomRoles.EvilTracker => CustomRoles.TrackerEHR,
                    CustomRoles.SerialKiller => CustomRoles.Addict,
                    CustomRoles.Miner => CustomRoles.Mole,
                    CustomRoles.Escapee => CustomRoles.Tunneler,
                    CustomRoles.Twister => CustomRoles.TimeMaster,
                    CustomRoles.Disperser => CustomRoles.Transporter,
                    CustomRoles.Eraser => CustomRoles.Cleanser,
                    CustomRoles.Visionary => CustomRoles.Oracle,
                    CustomRoles.Workaholic => CustomRoles.Snitch,
                    CustomRoles.Sunnyboy => CustomRoles.Doctor,
                    CustomRoles.Vindicator or CustomRoles.Pickpocket => CustomRoles.Mayor,
                    CustomRoles.Councillor => CustomRoles.Judge,
                    CustomRoles.EvilGuesser or CustomRoles.Doomsayer => CustomRoles.NiceGuesser,
                    _ => role
                };
            }

            if (tpc.IsCrewmate() && !tpc.GetCustomSubRoles().Contains(CustomRoles.Rascal) && !tpc.IsConverted())
            {
                TempLimit = pc.GetAbilityUseLimit();

                pc.RpcSetCustomRole(role);
                pc.RpcChangeRoleBasis(role);
                pc.SetAbilityUseLimit(tpc.GetAbilityUseLimit());

                pc.SyncSettings();
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
}