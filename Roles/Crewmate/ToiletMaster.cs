using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate
{
    public class ToiletMaster : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        private Dictionary<Vector2, (Toilet NetObject, int Uses)> Toilets = [];
        Dictionary<byte, (Poop Poop, long TimeStamp)> ActivePoops = [];

        private static OptionItem AbilityCooldown;
        private static OptionItem AbilityUses;
        private static OptionItem ToiletDuration;
        private static OptionItem ToiletVisibility;
        private static OptionItem ToiletUseRadius;
        private static OptionItem PoopDuration;
        private static OptionItem BrownPoopSpeedBoost;
        private static OptionItem GreenPoopRadius;
        private static OptionItem RedPoopRadius;

        enum ToiletVisibilityOptions
        {
            Instant,
            Delayed,
            AfterMeeting,
            Invisible
        }

        public static void SetupCustomOption()
        {
            int id = 644700;
            const TabGroup tab = TabGroup.CrewmateRoles;
            SetupRoleOptions(id++, tab, CustomRoles.ToiletMaster);
            AbilityCooldown = IntegerOptionItem.Create(++id, "AbilityCooldown", new(0, 60, 1), 5, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Seconds);
            AbilityUses = IntegerOptionItem.Create(++id, "AbilityUseLimit", new(0, 10, 1), 3, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);
            ToiletDuration = IntegerOptionItem.Create(++id, "TM.ToiletDuration", new(0, 60, 1), 10, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Seconds);
            ToiletVisibility = StringOptionItem.Create(++id, "TM.ToiletVisibility", Enum.GetNames<ToiletVisibilityOptions>(), 0, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);
            ToiletUseRadius = FloatOptionItem.Create(++id, "TM.ToiletUseRadius", new(0f, 5f, 0.25f), 1f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Multiplier);
            PoopDuration = IntegerOptionItem.Create(++id, "TM.PoopDuration", new(0, 60, 1), 10, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Seconds);
            BrownPoopSpeedBoost = FloatOptionItem.Create(++id, "TM.BrownPoopSpeedBoost", new(0f, 3f, 0.05f), 0.5f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Multiplier);
            GreenPoopRadius = FloatOptionItem.Create(++id, "TM.GreenPoopRadius", new(0f, 5f, 0.25f), 1f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Multiplier);
            RedPoopRadius = FloatOptionItem.Create(++id, "TM.RedPoopRadius", new(0f, 5f, 0.25f), 1f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Add(byte playerId)
        {
            On = true;
            Toilets = [];
            ActivePoops = [];
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnPet(PlayerControl pc)
        {
            var pos = pc.Pos();
            Toilets[pos] = (new(pos, [pc.PlayerId]), 0);
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || !pc.IsAlive() || !GameStates.IsInTask) return;

            try
            {
                var pos = pc.Pos();
                var toilet = Toilets.First(x => Vector2.Distance(x.Key, pos) <= ToiletUseRadius.GetFloat()).Value;
                toilet.Uses++;

                var poop = Enum.GetValues<Poop>().Shuffle()[0];
                switch (poop)
                {
                    // INCOMPLETE
                }
            }
            catch
            {
                return;
            }
        }

        enum Poop
        {
            Brown,
            Green,
            Red,
            Purple,
            Blue,
        }
    }
}