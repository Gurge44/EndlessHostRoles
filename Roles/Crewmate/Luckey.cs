namespace TOHE.Roles.Crewmate
{
    internal class Luckey : RoleBase
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

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            var rd = IRandom.Instance;
            if (rd.Next(0, 100) < Options.LuckeyProbability.GetInt())
            {
                killer.SetKillCooldown(15f);
                return false;
            }

            return true;
        }
    }
}