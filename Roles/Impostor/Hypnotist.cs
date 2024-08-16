using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Impostor
{
    public class Hypnotist : RoleBase
    {
        public static bool On;
        private static List<Hypnotist> Instances = [];

        public static OptionItem AbilityCooldown;
        public static OptionItem AbilityDuration;
        public static OptionItem AbilityUseLimit;
        public static OptionItem AbilityUseGainWithEachKill;
        private long ActivateTS;
        private int Count;
        private byte HypnotistId;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(647550, TabGroup.ImpostorRoles, CustomRoles.Hypnotist)
                .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
                .AutoSetupOption(ref AbilityDuration, 15, new IntegerValueRule(0, 30, 1), OptionFormat.Seconds)
                .AutoSetupOption(ref AbilityUseLimit, 1, new IntegerValueRule(0, 5, 1), OptionFormat.Times)
                .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
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
            Count = 0;
            ActivateTS = 0;
            HypnotistId = playerId;
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetInt();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public static bool OnAnyoneReport() => Instances.All(x => x.ActivateTS == 0);

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            OnPet(shapeshifter);
            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            Count = 0;
            ActivateTS = Utils.TimeStamp;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            Utils.SendRPC(CustomRPC.SyncRoleData, HypnotistId, ActivateTS);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            bool notify = false;
            int timeLeft = (int)(ActivateTS + AbilityDuration.GetInt() - Utils.TimeStamp);
            if (ActivateTS != 0 && timeLeft <= 0)
            {
                ActivateTS = 0;
                notify = true;
                pc.RpcResetAbilityCooldown();
            }
            else if (ActivateTS != 0 && Count++ >= 30 && timeLeft <= 6)
            {
                Count = 0;
                notify = true;
            }

            if (notify) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public void ReceiveRPC(Hazel.MessageReader reader)
        {
            ActivateTS = long.Parse(reader.ReadString());
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != HypnotistId || isMeeting || (seer.IsModClient() && !isHUD)) return string.Empty;
            int timeLeft = (int)(ActivateTS + AbilityDuration.GetInt() - Utils.TimeStamp);
            return timeLeft <= 5 ? $"\u25a9 ({timeLeft})" : "\u25a9";
        }
    }
}