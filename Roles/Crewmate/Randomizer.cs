using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Crewmate
{
    internal static class Randomizer
    {
        private static int Id => 643490;
        private static List<byte> PlayerIdList = [];

        private static OptionItem EffectFrequency;
        private static OptionItem EffectDurMin;
        private static OptionItem EffectDurMax;

        enum Effect
        {
            ShieldRandomPlayer,
            ShieldAll,
            Death,
            TPEveryoneToVents,
            PullEveryone,
            Twist,
            SuperSpeedForRandomPlayer,
            SuperSpeedForAll,
            FreezeRandomPlayer,
            FreezeAll,
            Sabotage,
            SuperVisionForRandomPlayer,
            SuperVisionForAll,
            BlindnessForRandomPlayer,
            BlindnessForAll,
            AllKCDsReset,
            AllKCDsTo0,
            Meeting,
            Rift,
            Snipe,
            TimeBomb,
            Tornado,
            RevertToBaseRole,
            InvertControls,
            AddonAssign,
            AddonRemove,
            HandcuffRandomPlayer,
            HandcuffAll,
            DonutForAll,
            AllDoorsOpen,
            AllDoorsClose,
            SetDoorsRandomly,
            Patrol, // Sentinel ability
            PuppetedEffect,
            GhostPlayer, // Lightning ability
            Camouflage,
            Deathpact,
            DevourRandomPlayer,
            Duel,
            ManipulateRandomPlayer, // Mastermind ability
            AgitaterBomb,
            BubbleRandomPlayer,
        }

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Randomizer);
            EffectFrequency = IntegerOptionItem.Create(Id + 2, "RandomizerEffectFrequency", new(1, 90, 1), 10, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
                .SetValueFormat(OptionFormat.Seconds);
            EffectDurMin = IntegerOptionItem.Create(Id + 3, "RandomizerEffectDurMin", new(1, 90, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
                .SetValueFormat(OptionFormat.Seconds);
            EffectDurMax = IntegerOptionItem.Create(Id + 4, "RandomizerEffectDurMax", new(1, 90, 1), 15, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            PlayerIdList = [];
        }

        public static void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
        }
    }
}
