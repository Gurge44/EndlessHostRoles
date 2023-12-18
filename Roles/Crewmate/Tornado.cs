using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    internal class Tornado
    {
        private static int Id => 64420;
        private static readonly List<byte> playerIdList = [];
        public static OptionItem TornadoCooldown;
        private static OptionItem TornadoDuration;
        private static OptionItem TornadoRange;
        private static readonly List<Vector2> Tornados = [];
        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tornado);
            TornadoCooldown = IntegerOptionItem.Create(Id + 2, "TornadoCooldown", new(1, 90, 1), 15, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Seconds);
            TornadoDuration = IntegerOptionItem.Create(Id + 3, "TornadoDuration", new(1, 90, 1), 25, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Seconds);
            TornadoRange = FloatOptionItem.Create(Id + 4, "TornadoRange", new(0.5f, 25f, 0.5f), 3f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
                .SetValueFormat(OptionFormat.Multiplier);
        }
        public static void Init()
        {
            playerIdList.Clear();
            Tornados.Clear();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }
        public static void SpawnTornado(PlayerControl pc)
        {
            if (pc == null || pc.HasAbilityCD()) return;
            pc.AddAbilityCD();

            Tornados.Add(pc.Pos());
        }
        public static void OnFixedUpdate()
        {

        }
        public static void AfterMeetingTasks()
        {
            foreach (var pc in playerIdList.Select(x => GetPlayerById(x)).ToArray())
            {
                pc.AddAbilityCD();
            }
        }
    }
}
