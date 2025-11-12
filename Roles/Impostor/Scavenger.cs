using EHR.Modules;
using EHR.Neutral;
using UnityEngine;
using static EHR.Options;

namespace EHR.Impostor;

internal class Scavenger : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(4000, TabGroup.ImpostorRoles, CustomRoles.Scavenger);

        ScavengerKillCooldown = new FloatOptionItem(4010, "KillCooldown", new(0f, 180f, 0.5f), 35f, TabGroup.ImpostorRoles)
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
            Main.Instance.StartCoroutine(CoRoutine());
            return false;

            System.Collections.IEnumerator CoRoutine()
            {
                while (dur > 0)
                {
                    if (killer == null || target == null || Vector2.Distance(killer.Pos(), target.Pos()) > 2f) yield break;
                    dur -= Time.fixedDeltaTime;
                    yield return null;
                }
                
                target.TP(Pelican.GetBlackRoomPS());
                target.Suicide(PlayerState.DeathReason.Scavenged, killer);
                
                if (target.Is(CustomRoles.Pelican))
                {
                    Pelican.OnPelicanDied(target.PlayerId);
                    if (target.AmOwner) Achievements.Type.YouCopiedMyWholeFlow.Complete();
                }

                killer.SetKillCooldown();
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), Translator.GetString("KilledByScavenger")));
            }
        }

        killer.Suicide(PlayerState.DeathReason.Kill, target);

        if (target.AmOwner)
            Achievements.Type.YoureTooLate.Complete();

        return false;
    }
}