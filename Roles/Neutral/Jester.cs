using AmongUs.GameOptions;

namespace TOHE.Roles.Neutral
{
    internal class Jester : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

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
            AURoleOptions.EngineerCooldown = 0f;
            AURoleOptions.EngineerInVentMaxTime = 0f;
            opt.SetVision(Options.JesterHasImpostorVision.GetBool());
        }
    }
}