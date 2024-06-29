using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Impostor
{
    internal class Parasite : RoleBase
    {
        public static bool On;

        public static OptionItem ParasiteCD;
        public static OptionItem ShapeshiftCooldown;
        public static OptionItem ShapeshiftDuration;

        public static float SSCD;
        public static float SSDur;

        private float Duration;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(4900, TabGroup.ImpostorRoles, CustomRoles.Parasite);
            ParasiteCD = new FloatOptionItem(4910, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Parasite])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = new FloatOptionItem(4911, "ShapeshiftCooldown", new(0f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Parasite])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDuration = new FloatOptionItem(4912, "ShapeshiftDuration", new(0f, 180f, 1f), 15f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Parasite])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
            Duration = float.NaN;
        }

        public override void Init()
        {
            On = false;
            SSCD = ShapeshiftCooldown.GetFloat();
            SSDur = ShapeshiftDuration.GetFloat();
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = ParasiteCD.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(true);
        }

        public override void OnPet(PlayerControl pc)
        {
            PlayerControl target = Main.AllAlivePlayerControls.Where(x => !x.Is(Team.Impostor)).Shuffle().FirstOrDefault();
            if (target != null)
            {
                Duration = SSDur;
                pc.RpcShapeshift(target, !Options.DisableAllShapeshiftAnimations.GetBool());
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (Duration > 0)
            {
                Duration -= Time.fixedDeltaTime;
            }

            if (!float.IsNaN(Duration) && Duration <= 0)
            {
                pc.RpcShapeshift(pc, !Options.DisableAllShapeshiftAnimations.GetBool());
            }
        }
    }
}