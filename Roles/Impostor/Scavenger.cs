using EHR.Modules;
using EHR.Neutral;
using UnityEngine;
using static EHR.Options;

namespace EHR.Impostor
{
    internal class Scavenger : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(4000, TabGroup.ImpostorRoles, CustomRoles.Scavenger);
            ScavengerKillCooldown = new FloatOptionItem(4010, "KillCooldown", new(0f, 180f, 2.5f), 40f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Scavenger])
                .SetValueFormat(OptionFormat.Seconds);
            ScavengerKillDuration = new FloatOptionItem(4011, "ScavengerKillDuration", new(0f, 90f, 0.5f), 5f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Scavenger])
                .SetValueFormat(OptionFormat.Seconds);
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
            Main.AllPlayerKillCooldown[id] = ScavengerKillCooldown.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!target.Is(CustomRoles.Pestilence))
            {
                float dur = ScavengerKillDuration.GetFloat();
                killer.Notify("....", dur);
                killer.SetKillCooldown(dur + 0.5f);
                LateTask.New(() =>
                {
                    if (Vector2.Distance(killer.Pos(), target.Pos()) > 2f) return;
                    target.TP(Pelican.GetBlackRoomPS());
                    target.Suicide(PlayerState.DeathReason.Kill, killer);
                    if (target.Is(CustomRoles.Pelican)) Pelican.OnPelicanDied(target.PlayerId);
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