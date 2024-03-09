using System;
using AmongUs.GameOptions;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    internal class Zombie : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(16400, TabGroup.OtherRoles, CustomRoles.Zombie);
            ZombieKillCooldown = FloatOptionItem.Create(16410, "KillCooldown", new(0f, 180f, 2.5f), 5f, TabGroup.OtherRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
                .SetValueFormat(OptionFormat.Seconds);
            ZombieSpeedReduce = FloatOptionItem.Create(16411, "ZombieSpeedReduce", new(0.0f, 1.0f, 0.1f), 0.1f, TabGroup.OtherRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
                .SetValueFormat(OptionFormat.Multiplier);
        }

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
            Main.AllPlayerKillCooldown[id] = ZombieKillCooldown.GetFloat();
            Main.AllPlayerSpeed[id] = Math.Clamp(Main.AllPlayerSpeed[id] - ZombieSpeedReduce.GetFloat(), 0.1f, 3f);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0.2f);
        }
    }
}