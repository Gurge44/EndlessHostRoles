using TOHE.Modules;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE.Roles.Impostor
{
    internal class Scavenger : RoleBase
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
            Main.AllPlayerKillCooldown[id] = Options.ScavengerKillCooldown.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!target.Is(CustomRoles.Pestilence))
            {
                float dur = Options.ScavengerKillDuration.GetFloat();
                killer.Notify("....", dur);
                killer.SetKillCooldown(dur + 0.5f);
                _ = new LateTask(() =>
                {
                    if (Vector2.Distance(killer.Pos(), target.Pos()) > 2f) return;
                    target.TP(Pelican.GetBlackRoomPS());
                    target.Suicide(PlayerState.DeathReason.Kill, killer);
                    killer.SetKillCooldown();
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), Translator.GetString("KilledByScavenger")));
                }, dur, "Scavenger Kill");
                return false;
            }

            killer.Suicide(PlayerState.DeathReason.Kill, target);
            return false;
        }
    }
}
