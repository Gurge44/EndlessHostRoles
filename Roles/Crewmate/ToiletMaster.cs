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

        private static OptionItem AbilityCooldown;
        private static OptionItem AbilityUses;
        private static OptionItem ToiletDuration;
        private static OptionItem ToiletVisibility;
        private static OptionItem ToiletUseRadius;
        private static OptionItem PoopDuration;
        private static OptionItem BrownPoopSpeedBoost;
        private static OptionItem GreenPoopRadius;
        private static OptionItem RedPoopRadius;
        private static OptionItem RedPoopRoleBlockDuration;
        private static OptionItem RedPoopFreezeDuration;
        private static OptionItem PurplePoopNotifyOnKillAttempt;
        public static OptionItem AbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        private static Dictionary<Poop, OptionItem> PoopDurationSettings = [];
        private Dictionary<byte, (Poop Poop, long TimeStamp, object Data)> ActivePoops = [];
        Dictionary<byte, long> PlayersUsingToilet = [];

        private Dictionary<Vector2, (Toilet NetObject, int Uses)> Toilets = [];
        public override bool IsEnable => On;

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
            RedPoopRoleBlockDuration = IntegerOptionItem.Create(++id, "TM.RedPoopRoleBlockDuration", new(0, 60, 1), 10, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Seconds);
            RedPoopFreezeDuration = IntegerOptionItem.Create(++id, "TM.RedPoopFreezeDuration", new(0, 60, 1), 10, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Seconds);
            PurplePoopNotifyOnKillAttempt = BooleanOptionItem.Create(++id, "TM.PurplePoopNotifyOnKillAttempt", false, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);
            AbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.6f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.1f, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);
            Enum.GetValues<Poop>().Do(poop =>
            {
                PoopDurationSettings[poop] = IntegerOptionItem.Create(++id, $"TM.{poop}PoopDuration", new(0, 60, 1), 10, tab)
                    .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                    .SetValueFormat(OptionFormat.Seconds);
            });
        }

        public override void Add(byte playerId)
        {
            On = true;
            Toilets = [];
            ActivePoops = [];
            PlayersUsingToilet = [];
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
                if (toilet.Uses >= AbilityUses.GetInt() || PlayersUsingToilet.ContainsKey(pc.PlayerId)) return;
                toilet.Uses++;

                var poop = Enum.GetValues<Poop>().RandomElement();
                switch (poop)
                {
                    case Poop.Brown:
                        Main.AllPlayerSpeed[pc.PlayerId] += BrownPoopSpeedBoost.GetFloat();
                        ActivePoops[pc.PlayerId] = (poop, Utils.TimeStamp, null);
                        break;
                    case Poop.Green:
                        var radius = GreenPoopRadius.GetFloat();
                        var isKillerNearby = Main.AllAlivePlayerControls.Any(x => x.PlayerId != pc.PlayerId && Vector2.Distance(x.Pos(), pos) <= radius);
                        var color = isKillerNearby ? Color.red : Color.green;
                        var str = Translator.GetString(isKillerNearby ? "TM.GreenPoopKiller" : "TM.GreenPoop");
                        pc.Notify(Utils.ColorString(color, str));
                        break;
                    case Poop.Red:
                        var duration = RedPoopRoleBlockDuration.GetInt();
                        List<PlayerControl> affectedPlayers = [];
                        Utils.GetPlayersInRadius(RedPoopRadius.GetFloat(), pos).Remove(pc).Do(x =>
                        {
                            x.BlockRole(duration);
                            Main.AllPlayerSpeed[x.PlayerId] = Main.MinSpeed;
                            affectedPlayers.Add(x);
                        });
                        ActivePoops[pc.PlayerId] = (poop, Utils.TimeStamp, affectedPlayers);
                        break;
                }

                Logger.Info($"{pc.GetNameWithRole()} used a toilet => {poop} poop", "ToiletMaster");
            }
            catch
            {
                PlayersUsingToilet.Remove(pc.PlayerId);
            }
        }

        enum ToiletVisibilityOptions
        {
            Instant,
            Delayed,
            AfterMeeting,
            Invisible
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