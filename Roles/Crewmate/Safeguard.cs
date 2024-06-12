using System;
using UnityEngine;

namespace EHR.Crewmate
{
    public class Safeguard : RoleBase
    {
        public static bool On;

        private static OptionItem ShieldDuration;
        private static OptionItem MinTasks;
        private float Timer;
        public override bool IsEnable => On;

        private bool Shielded => Timer > 0;

        public static void SetupCustomOption()
        {
            const TabGroup tab = TabGroup.CrewmateRoles;
            const CustomRoles role = CustomRoles.Safeguard;
            int id = 645000;

            Options.SetupRoleOptions(id++, tab, role);
            ShieldDuration = new FloatOptionItem(++id, "AidDur", new(0.5f, 60f, 0.5f), 5f, tab)
                .SetParent(Options.CustomRoleSpawnChances[role])
                .SetValueFormat(OptionFormat.Seconds);
            MinTasks = new IntegerOptionItem(++id, "MinTasksToActivateAbility", new(1, 10, 1), 3, tab)
                .SetParent(Options.CustomRoleSpawnChances[role]);
            Options.OverrideTasksData.Create(++id, tab, role);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            Timer = 0;
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            if (!pc.IsAlive()) return;
            if (completedTaskCount + 1 >= MinTasks.GetValue())
            {
                Timer += ShieldDuration.GetFloat();
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive()) return;
            if (Shielded)
            {
                Timer -= Time.fixedDeltaTime;
                if (Timer <= 0)
                {
                    Timer = 0;
                }
            }
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !Shielded;
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || !seer.Is(CustomRoles.Safeguard) || isMeeting || (seer.IsModClient() && !isHUD) || !Shielded) return string.Empty;
            return seer.IsHost() ? string.Format(Translator.GetString("SafeguardSuffixTimer"), (int)Math.Ceiling(Timer)) : Translator.GetString("SafeguardSuffix");
        }
    }
}