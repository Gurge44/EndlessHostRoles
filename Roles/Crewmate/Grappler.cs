using System.Collections.Generic;
using System.Linq;
using EHR.Modules;

namespace EHR.Crewmate
{
    public class Grappler : RoleBase
    {
        public static bool On;
        private static List<Grappler> Instances = [];

        private static OptionItem AbilityUseLimit;
        public static OptionItem AbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        private byte GrapplerId;
        private bool InUse;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(647250, TabGroup.CrewmateRoles, CustomRoles.Grappler)
                .AutoSetupOption(ref AbilityUseLimit, 0, new IntegerValueRule(0, 20, 1), OptionFormat.Times)
                .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.3f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
                .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
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
            GrapplerId = playerId;
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
        }

        public override void AfterMeetingTasks()
        {
            if (GrapplerId.GetAbilityUseLimit() >= 1f)
            {
                InUse = true;
                GrapplerId.GetPlayer().RpcRemoveAbilityUse();
            }
            else InUse = false;

            Utils.SendRPC(CustomRPC.SyncRoleData, GrapplerId, InUse);
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            return base.GetProgressText(playerId, comms) + (InUse ? "<#00ff00>\u271a</color>" : string.Empty);
        }

        public void ReceiveRPC(Hazel.MessageReader reader)
        {
            InUse = reader.ReadBoolean();
        }

        public static bool OnAnyoneCheckMurder(PlayerControl target)
        {
            if (new[] { SystemTypes.Electrical, SystemTypes.Reactor, SystemTypes.Laboratory, SystemTypes.LifeSupp, SystemTypes.Comms, SystemTypes.HeliSabotage, SystemTypes.MushroomMixupSabotage }.Any(Utils.IsActive)) return true;

            foreach (Grappler instance in Instances)
            {
                if (instance.InUse)
                {
                    target.TP(instance.GrapplerId.GetPlayer());
                    instance.InUse = false;
                    Utils.SendRPC(CustomRPC.SyncRoleData, instance.GrapplerId, instance.InUse);
                    return false;
                }
            }

            return true;
        }
    }
}