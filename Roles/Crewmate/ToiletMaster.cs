using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate
{
    public class ToiletMaster : RoleBase
    {
        public static bool On;

        public static OptionItem AbilityCooldown;
        private static OptionItem AbilityUses;
        private static OptionItem ToiletDuration;
        private static OptionItem ToiletVisibility;
        private static OptionItem ToiletUseRadius;
        private static OptionItem ToiletUseTime;
        private static OptionItem BrownPoopSpeedBoost;
        private static OptionItem GreenPoopRadius;
        private static OptionItem RedPoopRadius;
        private static OptionItem RedPoopRoleBlockDuration;
        private static OptionItem PurplePoopNotifyOnKillAttempt;
        public static OptionItem AbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        private static readonly Dictionary<Poop, OptionItem> PoopDurationSettings = [];

        private Dictionary<byte, (Poop Poop, long TimeStamp, object Data)> ActivePoops = [];
        private Dictionary<byte, long> PlayersUsingToilet = [];

        private Dictionary<Vector2, (Toilet NetObject, int Uses)> Toilets = [];
        static ToiletVisibilityOptions ToiletVisible => (ToiletVisibilityOptions)ToiletVisibility.GetValue();
        public override bool IsEnable => On;

        private static ParallelQuery<PlayerControl> EmptyParallelQuery => Enumerable.Empty<PlayerControl>().AsParallel();

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
            ToiletUseTime = IntegerOptionItem.Create(++id, "TM.ToiletUseTime", new(0, 60, 1), 5, tab)
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

        public override void Init()
        {
            On = false;
            Toilets = [];
            ActivePoops = [];
            PlayersUsingToilet = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(AbilityUses.GetFloat());
        }

        public override void OnPet(PlayerControl pc)
        {
            var pos = pc.Pos();
            ParallelQuery<PlayerControl> hideList = ToiletVisible switch
            {
                ToiletVisibilityOptions.Instant => EmptyParallelQuery,
                _ => Main.AllPlayerControls.Remove(pc)
            };
            Toilets[pos] = (new(pos, hideList), 0);

            if (ToiletVisible == ToiletVisibilityOptions.Delayed)
                _ = new LateTask(() => Toilets[pos] = (new(pos, EmptyParallelQuery), Toilets[pos].Uses), 5f, log: false);
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || !pc.IsAlive() || !GameStates.IsInTask) return;

            if (ActivePoops.TryGetValue(pc.PlayerId, out var activePoop) && activePoop.TimeStamp + PoopDurationSettings[activePoop.Poop].GetInt() <= Utils.TimeStamp)
            {
                ActivePoops.Remove(pc.PlayerId);
                switch (activePoop.Poop)
                {
                    case Poop.Brown:
                        Main.AllPlayerSpeed[pc.PlayerId] -= BrownPoopSpeedBoost.GetFloat();
                        break;
                    case Poop.Red:
                        var defaultSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        ((List<PlayerControl>)activePoop.Data).Do(x =>
                        {
                            Main.AllPlayerSpeed[x.PlayerId] = defaultSpeed;
                            x.MarkDirtySettings();
                        });
                        break;
                }
            }

            try
            {
                var pos = pc.Pos();
                var toilet = Toilets.First(x => Vector2.Distance(x.Key, pos) <= ToiletUseRadius.GetFloat()).Value;

                if (toilet.Uses >= AbilityUses.GetInt()) return;
                if (!PlayersUsingToilet.TryGetValue(pc.PlayerId, out var ts))
                {
                    PlayersUsingToilet[pc.PlayerId] = Utils.TimeStamp;
                    return;
                }

                if (ts + ToiletUseTime.GetInt() > Utils.TimeStamp) return;
                toilet.Uses++;

                var poop = Enum.GetValues<Poop>().RandomElement();
                if (poop != Poop.Green) pc.Notify(Utils.ColorString(GetPoopColor(poop), Translator.GetString($"TM.{poop}PoopNotify")));
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
                            x.MarkDirtySettings();
                            affectedPlayers.Add(x);
                        });
                        ActivePoops[pc.PlayerId] = (poop, Utils.TimeStamp, affectedPlayers);
                        break;
                    case Poop.Blue:
                    case Poop.Purple:
                        ActivePoops[pc.PlayerId] = (poop, Utils.TimeStamp, null);
                        break;
                }

                Logger.Info($"{pc.GetNameWithRole()} used a toilet => {poop} poop", "ToiletMaster");
            }
            catch
            {
                PlayersUsingToilet.Remove(pc.PlayerId);
            }
        }

        public override void AfterMeetingTasks()
        {
            if (ToiletVisible == ToiletVisibilityOptions.AfterMeeting)
                Toilets.Values.Do(x => x.NetObject = new(Toilets.GetKeyByValue((x.NetObject, x.Uses)), EmptyParallelQuery));
        }

        static Color GetPoopColor(Poop poop)
        {
            return poop switch
            {
                Poop.Brown => Palette.Brown,
                Poop.Green => Color.green,
                Poop.Red => Color.red,
                Poop.Purple => Palette.Purple,
                Poop.Blue => Color.blue,
                _ => Color.white
            };
        }

        public static bool OnAnyoneCheckMurderStart(PlayerControl killer, PlayerControl target)
        {
            foreach (var state in Main.PlayerStates.Values)
            {
                if (state.Role is ToiletMaster tm && tm.ActivePoops.TryGetValue(killer.PlayerId, out var poop) && poop.Poop == Poop.Blue)
                {
                    killer.RpcCheckAndMurder(target);
                    return true;
                }
            }

            return false;
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