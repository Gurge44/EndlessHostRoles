using AmongUs.GameOptions;

namespace EHR.Neutral
{
    internal class Sunnyboy : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void SetupCustomOption() { }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ScientistCooldown = 0f;
            AURoleOptions.ScientistBatteryCharge = 60f;
        }
    }
}