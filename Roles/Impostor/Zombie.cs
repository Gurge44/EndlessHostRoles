using AmongUs.GameOptions;
using System;

namespace TOHE.Roles.Impostor
{
    internal class Zombie : RoleBase
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
            Main.AllPlayerKillCooldown[id] = Options.ZombieKillCooldown.GetFloat();
            Main.AllPlayerSpeed[id] = Math.Clamp(Main.AllPlayerSpeed[id] - Options.ZombieSpeedReduce.GetFloat(), 0.1f, 3f);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0.2f);
        }
    }
}
