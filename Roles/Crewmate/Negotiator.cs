using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Crewmate
{
    public class Negotiator : RoleBase
    {
        public static bool On;
        private static List<Negotiator> Instances = [];

        private static OptionItem LowVision;
        private static OptionItem LowSpeed;
        private static OptionItem AbilityUseLimit;
        public static OptionItem AbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        private byte NegotiatorId;
        private NegotiationType Penalty;
        private Dictionary<byte, HashSet<NegotiationType>> PermanentPenalties;
        private byte TargetId;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(647950)
                .AutoSetupOption(ref LowVision, 0.4f, new FloatValueRule(0f, 1f, 0.05f), OptionFormat.Multiplier)
                .AutoSetupOption(ref LowSpeed, 0.9f, new FloatValueRule(0.1f, 2f, 0.1f), OptionFormat.Multiplier)
                .AutoSetupOption(ref AbilityUseLimit, 0, new IntegerValueRule(0, 20, 1), OptionFormat.Times)
                .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.4f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
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
            NegotiatorId = playerId;
            TargetId = byte.MaxValue;
            Penalty = default;
            PermanentPenalties = [];
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
        }

        public static void OnAnyoneApplyGameOptions(IGameOptions opt, byte id)
        {
            bool lowVision = false, lowSpeed = false;
            foreach (Negotiator instance in Instances)
            {
                if (instance.PermanentPenalties.TryGetValue(id, out var penalties))
                {
                    lowVision |= penalties.Contains(NegotiationType.LowVision);
                    lowSpeed |= penalties.Contains(NegotiationType.LowSpeed);
                }
            }

            if (lowVision)
            {
                var vision = LowVision.GetFloat();

                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
            }

            if (lowSpeed) Main.AllPlayerSpeed[id] = LowSpeed.GetFloat();
        }

        public override void AfterMeetingTasks()
        {
            if (TargetId != byte.MaxValue)
            {
                var target = TargetId.GetPlayer();
                TargetId = byte.MaxValue;
                if (target == null || !target.IsAlive()) return;
                var negotiator = NegotiatorId.GetPlayer();

                switch (Penalty)
                {
                    case NegotiationType.Suicide:
                        target.Suicide(PlayerState.DeathReason.Negotiation, negotiator);
                        break;
                    case NegotiationType.HarmfulAddon:
                        var addon = Options.GroupedAddons[AddonTypes.Harmful].Shuffle().FirstOrDefault(x => CustomRolesHelper.CheckAddonConflict(x, target));
                        if (addon != default) target.RpcSetCustomRole(addon);
                        break;
                    case NegotiationType.LowVision:
                    case NegotiationType.LowSpeed:
                        if (PermanentPenalties.TryGetValue(target.PlayerId, out var penalties)) penalties.Add(Penalty);
                        else PermanentPenalties[target.PlayerId] = [Penalty];
                        break;
                }

                negotiator?.Notify(string.Format(Translator.GetString("Negotiator.FinishNotify"), Translator.GetString($"Negotiator.Type.{Penalty}")));
            }
        }

        public override bool OnVote(PlayerControl voter, PlayerControl target)
        {
            if (target == null || voter == null || voter.PlayerId == target.PlayerId || TargetId != byte.MaxValue || voter.GetAbilityUseLimit() < 1f || Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;

            TargetId = target.PlayerId;
            Penalty = NegotiationType.Suicide;
            voter.RpcRemoveAbilityUse();

            var message = Enum.GetValues<NegotiationType>().Aggregate(Translator.GetString("Negotiator.TargetMessage"), (s, x) => $"{s}\n{(int)x}) {Translator.GetString($"Negotiator.Type.{x}")}");
            Utils.SendMessage(message, target.PlayerId, Translator.GetString("Negotiator.Title"));

            Main.DontCancelVoteList.Add(voter.PlayerId);
            return true;
        }

        public static void ReceiveCommand(PlayerControl pc, int index)
        {
            foreach (Negotiator instance in Instances)
            {
                if (pc.PlayerId != instance.TargetId || !pc.IsAlive()) continue;
                NegotiationType type = (NegotiationType)index;
                instance.Penalty = type;
                var text = string.Format(Translator.GetString("Negotiator.TargetPickSuccess"), Translator.GetString($"Negotiator.Type.{type}"));
                Utils.SendMessage(text, pc.PlayerId, Translator.GetString("Negotiator.Title"));
            }
        }

        enum NegotiationType
        {
            Suicide,
            HarmfulAddon,
            LowVision,
            LowSpeed
        }
    }
}