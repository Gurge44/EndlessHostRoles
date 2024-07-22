using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Crewmate
{
    public class Rhapsode : RoleBase
    {
        public static bool On;
        private static List<Rhapsode> Instances = [];

        public static OptionItem AbilityCooldown;
        public static OptionItem AbilityDuration;
        private static OptionItem ExcludeCrewmates;
        private static OptionItem AbilityUseLimit;
        public static OptionItem RhapsodeAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        private bool AbilityActive;
        private long ActivateTimeStamp;
        private long LastUpdate;
        private byte RhapsodeId;

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            int id = 647650;
            Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Rhapsode);
            AbilityCooldown = new IntegerOptionItem(++id, "AbilityCooldown", new(0, 60, 1), 30, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
                .SetValueFormat(OptionFormat.Seconds);
            AbilityDuration = new IntegerOptionItem(++id, "AbilityDuration", new(0, 60, 1), 10, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
                .SetValueFormat(OptionFormat.Seconds);
            ExcludeCrewmates = new BooleanOptionItem(++id, "Rhapsode.ExcludeCrewmates", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode]);
            AbilityUseLimit = new IntegerOptionItem(++id, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
                .SetValueFormat(OptionFormat.Times);
            RhapsodeAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = new FloatOptionItem(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            On = false;
            Instances = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
            Instances.Add(this);
            RhapsodeId = playerId;
            AbilityActive = false;
            ActivateTimeStamp = 0;
            LastUpdate = 0;
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = AbilityCooldown.GetInt();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId) => ActivateAbility(physics.myPlayer);

        public override void OnPet(PlayerControl pc) => ActivateAbility(pc);

        private void ActivateAbility(PlayerControl pc)
        {
            AbilityActive = true;
            ActivateTimeStamp = Utils.TimeStamp;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, AbilityActive);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            long now = Utils.TimeStamp;
            if (now == LastUpdate) return;
            LastUpdate = now;

            if (AbilityActive && Utils.TimeStamp - ActivateTimeStamp >= AbilityDuration.GetInt())
            {
                AbilityActive = false;
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, AbilityActive);
            }

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public static bool CheckAbilityUse(PlayerControl pc)
        {
            if (pc.IsCrewmate() && ExcludeCrewmates.GetBool()) return true;
            return !Instances.Any(x => x.AbilityActive);
        }

        public void ReceiveRPC(Hazel.MessageReader reader)
        {
            AbilityActive = reader.ReadBoolean();
            if (AbilityActive) ActivateTimeStamp = Utils.TimeStamp;
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != RhapsodeId || isHUD || isMeeting || !AbilityActive) return string.Empty;
            return $"\u25b6 ({AbilityDuration.GetInt() - (Utils.TimeStamp - ActivateTimeStamp)}s)";
        }
    }
}