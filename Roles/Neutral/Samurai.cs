using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class Samurai : RoleBase
    {
        public static bool On;

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        private static OptionItem NearbyDuration;
        private static OptionItem SuccessKCD;
        private static OptionItem KillDelay;

        private (byte Id, long TimeStamp) Target;
        private Dictionary<byte, long> Delays;

        public static void SetupCustomOption()
        {
            const int id = 16880;
            SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.Samurai);
            KillCooldown = FloatOptionItem.Create(id + 2, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai]);
            HasImpostorVision = BooleanOptionItem.Create(id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai]);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            Target = (byte.MaxValue, 0);
            Delays = [];

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override bool IsEnable => On;
        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseKillButton(PlayerControl pc) => Target.Id == byte.MaxValue && pc.IsAlive();

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (Target.Id != byte.MaxValue) return false;

            Target = (target.PlayerId, Utils.TimeStamp);
            return false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (Target.Id == byte.MaxValue || !GameStates.IsInTask || !pc.IsAlive()) return;

            long now = Utils.TimeStamp;

            foreach (var kvp in Delays)
            {
                var player = Utils.GetPlayerById(kvp.Key);
                if (player == null || !player.IsAlive()) continue;

                if (kvp.Value + KillDelay.GetInt() <= now)
                {
                    player.Suicide(realKiller: pc);
                }
            }

            var target = Utils.GetPlayerById(Target.Id);
            if (target == null) return;

            if (Vector2.Distance(target.Pos(), pc.Pos()) > 1.5f)
            {
                Target = (byte.MaxValue, 0);
                return;
            }

            if (Target.TimeStamp + NearbyDuration.GetInt() <= now)
            {
                Delays[Target.Id] = now;
                Target = (byte.MaxValue, 0);
            }
        }
    }
}
