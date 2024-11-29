using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate
{
    public class Ankylosaurus : RoleBase
    {
        public static bool On;

        public override bool IsEnable => On;

        private static OptionItem Speed;
        private static OptionItem Vision;
        private static OptionItem Lives;

        private int LivesLeft;

        public override void SetupCustomOption()
        {
            StartSetup(647100)
                .AutoSetupOption(ref Speed, 0.8f, new FloatValueRule(0.05f, 3f, 0.05f), OptionFormat.Multiplier)
                .AutoSetupOption(ref Vision, 0.25f, new FloatValueRule(0f, 1.3f, 0.05f), OptionFormat.Multiplier)
                .AutoSetupOption(ref Lives, 3, new IntegerValueRule(1, 30, 1), OptionFormat.Health);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            LivesLeft = Lives.GetInt();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            Main.AllPlayerSpeed[playerId] = Speed.GetFloat();

            var vision = Vision.GetFloat();
            opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            Utils.SendRPC(CustomRPC.SyncRoleData, target.PlayerId, LivesLeft - 1);
            return LivesLeft-- <= 1;
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            return base.GetProgressText(playerId, comms) + $" ({LivesLeft} \u2665)";
        }

        public void ReceiveRPC(MessageReader reader)
        {
            LivesLeft = reader.ReadPackedInt32();
        }
    }
}