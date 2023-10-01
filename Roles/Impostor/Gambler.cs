using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

// _ = new LateTask(() => { if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data); }, delay, "Bait Self Report");

namespace TOHE.Roles.Impostor
{
    internal class Gambler
    {
        private static readonly int Id = 640700;
        public static List<byte> playerIdList = new();

        public static OptionItem KillCooldown;
        public static OptionItem TimeLimit;
        public static OptionItem Delay;

        public static byte EffectID = byte.MaxValue;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Gambler, 1);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            TimeLimit = IntegerOptionItem.Create(Id + 12, "GamblerTimeLimit", new(1, 60, 1), 20, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            Delay = IntegerOptionItem.Create(Id + 13, "GamblerDelay", new(0, 30, 1), 7, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            playerIdList = new();
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static bool IsEnable => playerIdList.Any();

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;



            return true;
        }

        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!IsEnable) return;
            if (!GameStates.IsInTask) return;

        }

        public static void OnReportDeadBody()
        {

        }
    }
}
