using AmongUs.GameOptions;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class Mole : RoleBase
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

        private static int Id => 64400;
        public static void SetupCustomOption() => SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mole);

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerInVentMaxTime = 1f;
            AURoleOptions.EngineerCooldown = 5f;
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            var pc = physics.myPlayer;
            if (pc == null) return;
            _ = new LateTask(() =>
            {
                var vents = Object.FindObjectsOfType<Vent>();
                var vent = vents[IRandom.Instance.Next(0, vents.Count)];
                physics.RpcBootFromVent(vent.Id); // This boots them from the vent which is randomly selected (and they teleport to it automatically)
            }, 0.5f, "Mole BootFromVent");
        }
    }
}
