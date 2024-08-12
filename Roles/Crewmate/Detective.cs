using System;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Crewmate
{
    internal class Detective : RoleBase
    {
        public static OptionItem DetectiveCanknowKiller;
        public static OptionItem DetectiveCanknowDeathReason;
        public static OptionItem DetectiveCanknowAddons;
        public static OptionItem DetectiveCanknowKillTime;

        public static Dictionary<byte, string> DetectiveNotify = [];

        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(6600, TabGroup.CrewmateRoles, CustomRoles.Detective);
            DetectiveCanknowKiller = new BooleanOptionItem(6610, "DetectiveCanknowKiller", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detective]);
            DetectiveCanknowDeathReason = new BooleanOptionItem(6611, "DetectiveCanknowDeathReason", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detective]);
            DetectiveCanknowAddons = new BooleanOptionItem(6612, "DetectiveCanknowAddons", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detective]);
            DetectiveCanknowKillTime = new BooleanOptionItem(6613, "DetectiveCanknowKillTime", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detective]);
        }

        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }

        public static void OnReportDeadBody(PlayerControl player, PlayerControl tpc)
        {
            string msg = string.Format(Translator.GetString("DetectiveNoticeVictim"), tpc.GetRealName(), tpc.GetCustomRole().ToColoredString());

            if (DetectiveCanknowKiller.GetBool())
            {
                var realKiller = tpc.GetRealKiller();
                if (realKiller == null) msg += "；" + Translator.GetString("DetectiveNoticeKillerNotFound");
                else msg += "；" + string.Format(Translator.GetString("DetectiveNoticeKiller"), realKiller.GetCustomRole().ToColoredString());
            }

            if (DetectiveCanknowDeathReason.GetBool())
            {
                msg += "；" + string.Format(Translator.GetString("DetectiveNoticeDeathReason"), Translator.GetString($"DeathReason.{Main.PlayerStates[tpc.PlayerId].deathReason}"));
            }

            if (DetectiveCanknowAddons.GetBool())
            {
                msg += "；" + string.Format(Translator.GetString("DetectiveNoticeAddons"), string.Join(", ", tpc.GetCustomSubRoles().Select(x => x.ToColoredString())));
            }

            if (DetectiveCanknowKillTime.GetBool())
            {
                var deathTimeStamp = Main.PlayerStates[tpc.PlayerId].RealKiller.TimeStamp;
                var now = DateTime.Now;
                var timeSpanSeconds = (now - deathTimeStamp).TotalSeconds;
                msg += "；" + string.Format(Translator.GetString("DetectiveNoticeKillTime"), (int)timeSpanSeconds);
            }

            DetectiveNotify.Add(player.PlayerId, msg);
        }
    }
}