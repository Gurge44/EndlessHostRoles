namespace TOHE.Roles.Impostor
{
    internal class Bomber : RoleBase
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

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return base.CanUseKillButton(pc) && Options.BomberCanKill.GetBool();
        }

        public override void SetKillCooldown(byte id)
        {
            if (Options.BomberCanKill.GetBool()) Main.AllPlayerKillCooldown[id] = Options.BomberKillCD.GetFloat();
            else Main.AllPlayerKillCooldown[id] = 300f;
        }
    }
}
