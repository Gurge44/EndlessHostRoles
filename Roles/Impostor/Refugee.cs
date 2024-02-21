using AmongUs.GameOptions;
using TOHE.Roles.Neutral;

namespace TOHE.Roles.Impostor
{
    internal class Refugee : RoleBase
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

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Amnesiac.RefugeeKillCD.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(true);
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !killer.Is(CustomRoleTypes.Impostor);
        }
    }
}
