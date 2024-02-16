using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE.Roles.Crewmate
{
    internal static class Randomizer
    {
        private static int Id => 643490;
        private static List<byte> PlayerIdList = [];

        private static OptionItem EffectFrequencyOpt;
        private static OptionItem EffectDurMin;
        private static OptionItem EffectDurMax;

        private static int EffectFrequency;
        private static int MinimumEffectDuration;
        private static int MaximumEffectDuration;

        public static Dictionary<byte, Dictionary<Effect, (long StartTimeStamp, int Duration)>> CurrentEffects = [];
        private static Dictionary<byte, float> AllPlayerDefaultSpeed = [];

        private static Dictionary<Vector2, Vector2> Rifts = [];
        private static Dictionary<Vector2, (long PlaceTimeStamp, int ExplosionDelay)> Bombs = [];

        private static float TimeSinceLastMeeting = 0;
        private static long LastEffectPick = 0;
        private static Dictionary<byte, long> LastTP = [];

        private static string RNGString => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Randomizer), Translator.GetString("RNGHasSpoken"));
        private static void NotifyAboutRNG(PlayerControl pc) => pc.Notify(RNGString, IRandom.Instance.Next(2, 7));

        public static bool IsEnable => PlayerIdList.Count > 0;

        public enum Effect
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
            SuperVisionForRandomPlayer,
            SuperVisionForAll,
            BlindnessForRandomPlayer,
            BlindnessForAll,
            AllKCDsReset,
            AllKCDsTo0,
            Meeting,
            Rift,
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

        private static bool IsSpeedChangingEffect(this Effect effect) => effect is
            Effect.SuperSpeedForRandomPlayer or
            Effect.SuperSpeedForAll or
            Effect.FreezeRandomPlayer or
            Effect.FreezeAll or
            Effect.InvertControls;

        private static bool IsVisionChangingEffect(this Effect effect) => effect is
            Effect.SuperVisionForRandomPlayer or
            Effect.SuperVisionForAll or
            Effect.BlindnessForRandomPlayer or
            Effect.BlindnessForAll;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Randomizer);
            EffectFrequencyOpt = IntegerOptionItem.Create(Id + 2, "RandomizerEffectFrequency", new(1, 90, 1), 10, TabGroup.CrewmateRoles, false)
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
            var now = Utils.GetTimeStamp();

            PlayerIdList = [];
            CurrentEffects = [];
            AllPlayerDefaultSpeed = [];
            Rifts = [];
            Bombs = [];

            TimeSinceLastMeeting = 0;
            LastEffectPick = 0;
            LastTP = [];
            Main.AllPlayerControls.Do(x => LastTP[x.PlayerId] = now);

            EffectFrequency = EffectFrequencyOpt.GetInt();
            MinimumEffectDuration = EffectDurMin.GetInt();
            MaximumEffectDuration = EffectDurMax.GetInt();
        }

        public static void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            AllPlayerDefaultSpeed = Main.AllPlayerSpeed;
        }

        private static PlayerControl PickRandomPlayer()
        {
            var allPc = Main.AllAlivePlayerControls;
            var pc = allPc[IRandom.Instance.Next(0, allPc.Length)];
            return pc;
        }

        private static void AddEffectForPlayer(PlayerControl pc, Effect effect)
        {
            if (pc == null) return;
            CurrentEffects[pc.PlayerId] ??= [];
            int duration = IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration + 1);
            CurrentEffects[pc.PlayerId].TryAdd(effect, (Utils.GetTimeStamp(), duration));
        }

        private static Effect PickRandomEffect()
        {
            LastEffectPick = Utils.GetTimeStamp();
            var allEffects = EnumHelper.GetAllValues<Effect>();
            var effect = allEffects[IRandom.Instance.Next(0, allEffects.Length)];
            return effect;
        }

        private static void Apply(this Effect effect, PlayerControl randomizer)
        {
            bool notifyEveryone = true;

            try
            {
                switch (effect)
                {
                    case Effect.ShieldRandomPlayer:
                        {
                            var pc = PickRandomPlayer();
                            AddEffectForPlayer(pc, effect);
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.ShieldAll:
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                AddEffectForPlayer(pc, effect);
                                NotifyAboutRNG(pc);
                                notifyEveryone = false;
                            }
                        }
                        break;
                    case Effect.Death:
                        {
                            var pc = PickRandomPlayer();
                            pc.Suicide(PlayerState.DeathReason.RNG, randomizer);
                            NotifyAboutRNG(pc);
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.TPEveryoneToVents:
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                pc.TPtoRndVent();
                                NotifyAboutRNG(pc);
                                notifyEveryone = false;
                            }
                        }
                        break;
                    case Effect.PullEveryone:
                        Utils.TPAll(randomizer.Pos());
                        break;
                    case Effect.Twist:
                        {
                            List<byte> changePositionPlayers = [];
                            var rd = IRandom.Instance;
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                if (changePositionPlayers.Contains(pc.PlayerId) || Pelican.IsEaten(pc.PlayerId) || pc.onLadder || pc.inVent || GameStates.IsMeeting) continue;

                                var filtered = Main.AllAlivePlayerControls.Where(a => !a.inVent && !Pelican.IsEaten(a.PlayerId) && !a.onLadder && a.PlayerId != pc.PlayerId && !changePositionPlayers.Contains(a.PlayerId)).ToArray();
                                if (filtered.Length == 0) break;

                                var target = filtered[rd.Next(0, filtered.Length)];

                                changePositionPlayers.Add(target.PlayerId);
                                changePositionPlayers.Add(pc.PlayerId);

                                pc.RPCPlayCustomSound("Teleport");

                                var originPs = target.Pos();
                                target.TP(pc.Pos());
                                pc.TP(originPs);

                                target.Notify(RNGString);
                                NotifyAboutRNG(pc);
                            }
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.SuperSpeedForRandomPlayer:
                        {
                            var pc = PickRandomPlayer();
                            RevertSpeedChangesForPlayer(pc, false);
                            AddEffectForPlayer(pc, effect);
                            Main.AllPlayerSpeed[pc.PlayerId] = 5f;
                            pc.MarkDirtySettings();
                            NotifyAboutRNG(pc);
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.SuperSpeedForAll:
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                RevertSpeedChangesForPlayer(pc, false);
                                AddEffectForPlayer(pc, effect);
                                Main.AllPlayerSpeed[pc.PlayerId] = 5f;
                                NotifyAboutRNG(pc);
                            }
                            Utils.MarkEveryoneDirtySettings();
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.FreezeRandomPlayer:
                        {
                            var pc = PickRandomPlayer();
                            RevertSpeedChangesForPlayer(pc, false);
                            AddEffectForPlayer(pc, effect);
                            Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                            pc.MarkDirtySettings();
                            NotifyAboutRNG(pc);
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.FreezeAll:
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                RevertSpeedChangesForPlayer(pc, false);
                                AddEffectForPlayer(pc, effect);
                                Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                                NotifyAboutRNG(pc);
                            }
                            Utils.MarkEveryoneDirtySettings();
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.SuperVisionForRandomPlayer:
                        {
                            var pc = PickRandomPlayer();
                            RevertVisionChangesForPlayer(pc, false);
                            AddEffectForPlayer(pc, effect);
                            pc.MarkDirtySettings();
                            NotifyAboutRNG(pc);
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.SuperVisionForAll:
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                RevertVisionChangesForPlayer(pc, false);
                                AddEffectForPlayer(pc, effect);
                                NotifyAboutRNG(pc);
                            }
                            Utils.MarkEveryoneDirtySettings();
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.BlindnessForRandomPlayer:
                        {
                            var pc = PickRandomPlayer();
                            RevertVisionChangesForPlayer(pc, false);
                            AddEffectForPlayer(pc, effect);
                            pc.MarkDirtySettings();
                            NotifyAboutRNG(pc);
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.BlindnessForAll:
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                RevertVisionChangesForPlayer(pc, false);
                                AddEffectForPlayer(pc, effect);
                                NotifyAboutRNG(pc);
                            }
                            Utils.MarkEveryoneDirtySettings();
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.AllKCDsReset:
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                if (pc.HasKillButton() && pc.CanUseKillButton())
                                {
                                    pc.SetKillCooldown();
                                    NotifyAboutRNG(pc);
                                }
                            }
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.AllKCDsTo0:
                        {
                            foreach (var pc in Main.AllAlivePlayerControls)
                            {
                                if (pc.HasKillButton() && pc.CanUseKillButton())
                                {
                                    pc.SetKillCooldown(0.1f);
                                    NotifyAboutRNG(pc);
                                }
                            }
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.Meeting when TimeSinceLastMeeting > Math.Max(Main.NormalOptions.EmergencyCooldown, 30):
                        {
                            var pc = PickRandomPlayer();
                            pc.CmdReportDeadBody(null);
                            notifyEveryone = false;
                        }
                        break;
                    case Effect.Rift:
                        Rifts.TryAdd(PickRandomPlayer().Pos(), PickRandomPlayer().Pos());
                        try
                        {
                            var e = Rifts.AsEnumerable();
                            e.DoIf(x => Vector2.Distance(x.Key, x.Value) <= 4f, x => Rifts.Remove(x.Key));
                            var keys = Rifts.Keys.AsEnumerable();
                            keys.DoIf(x => keys.Any(p => Vector2.Distance(x, p) <= 4f), x => Rifts.Remove(x));
                            var values = Rifts.Values.AsEnumerable();
                            values.DoIf(x => values.Any(p => Vector2.Distance(x, p) <= 4f), x => Rifts.Remove(Rifts.First(p => p.Value == x).Key));
                        }
                        catch (Exception ex)
                        {
                            Logger.Exception(ex, "Randomizer Rift Manager");
                        }
                        break;
                    case Effect.TimeBomb:
                        Bombs.TryAdd(PickRandomPlayer().Pos(), (Utils.GetTimeStamp(), IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration)));
                        break;
                    case Effect.Tornado:
                        Tornado.SpawnTornado(PickRandomPlayer());
                        break;
                    case Effect.RevertToBaseRole when TimeSinceLastMeeting > 40: // To make this less frequent than the others
                        break;
                    case Effect.InvertControls:
                        break;
                    case Effect.AddonAssign:
                        break;
                    case Effect.AddonRemove:
                        break;
                    case Effect.HandcuffRandomPlayer:
                        break;
                    case Effect.HandcuffAll:
                        break;
                    case Effect.DonutForAll:
                        break;
                    case Effect.AllDoorsOpen:
                        break;
                    case Effect.AllDoorsClose:
                        break;
                    case Effect.SetDoorsRandomly:
                        break;
                    case Effect.Patrol:
                        break;
                    case Effect.PuppetedEffect:
                        break;
                    case Effect.GhostPlayer:
                        break;
                    case Effect.Camouflage:
                        break;
                    case Effect.Deathpact:
                        break;
                    case Effect.DevourRandomPlayer:
                        break;
                    case Effect.Duel:
                        break;
                    case Effect.ManipulateRandomPlayer:
                        break;
                    case Effect.AgitaterBomb:
                        break;
                    case Effect.BubbleRandomPlayer:
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Randomizer Apply Effect");
            }
            finally
            {
                if (notifyEveryone)
                {
                    foreach (var pc in Main.AllAlivePlayerControls)
                    {
                        NotifyAboutRNG(pc);
                    }
                }
            }
        }

        private static bool RevertSpeedChangesForPlayer(PlayerControl pc, bool sync)
        {
            if (pc == null) return false;
            if (CurrentEffects.TryGetValue(pc.PlayerId, out var effects) && effects.Any(x => x.Key.IsSpeedChangingEffect()))
            {
                Main.AllPlayerSpeed[pc.PlayerId] = AllPlayerDefaultSpeed[pc.PlayerId];
                if (sync) pc.MarkDirtySettings();
                var keys = effects.Keys.AsEnumerable();
                keys.DoIf(x => x.IsSpeedChangingEffect(), x => effects.Remove(x));
                return true;
            }
            return false;
        }

        private static bool RevertVisionChangesForPlayer(PlayerControl pc, bool sync)
        {
            if (pc == null) return false;
            if (CurrentEffects.TryGetValue(pc.PlayerId, out var effects) && effects.Any(x => x.Key.IsVisionChangingEffect()))
            {
                effects.Keys.DoIf(x => x.IsVisionChangingEffect(), x => effects.Remove(x));
                if (sync) pc.MarkDirtySettings();
                return true;
            }
            return false;
        }

        public static void OnReportDeadBody()
        {
            TimeSinceLastMeeting = 0;
            Rifts.Clear();
            Bombs.Clear();
            foreach (var pc in Main.AllPlayerControls)
            {
                RevertSpeedChangesForPlayer(pc, false);
                RevertVisionChangesForPlayer(pc, false);
            }
        }

        public static void AfterMeetingTasks()
        {
            TimeSinceLastMeeting = 0;
        }

        public static void GlobalFixedUpdate(bool lowLoad)
        {
            if (GameStates.IsInTask) TimeSinceLastMeeting += Time.fixedDeltaTime;

            if (lowLoad) return;

            var now = Utils.GetTimeStamp();
            var r = IRandom.Instance;
            var randomizer = Utils.GetPlayerById(PlayerIdList.FirstOrDefault());

            foreach (var bomb in Bombs)
            {
                if (bomb.Value.PlaceTimeStamp + bomb.Value.ExplosionDelay < now)
                {
                    var radius = r.Next(0, 5) + (r.Next(0, 10) / 10f);
                    var players = Utils.GetPlayersInRadius(radius, bomb.Key);
                    foreach (var pc in players)
                    {
                        pc.Suicide(PlayerState.DeathReason.RNG, randomizer);
                    }
                    Bombs.Remove(bomb.Key);
                }
            }
        }

        public static void OnFixedUpdateForRandomizer(PlayerControl pc)
        {
            if (!GameStates.IsInTask || pc == null || !pc.IsAlive() || !pc.Is(CustomRoles.Randomizer)) return;

            if (LastEffectPick + EffectFrequency <= Utils.GetTimeStamp())
            {
                Effect effect = PickRandomEffect();
                effect.Apply(randomizer: pc);
            }
        }

        public static void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!GameStates.IsInTask) return;

            foreach (var rift in Rifts)
            {

            }
        }

        public static void OnFixedUpdateForPlayers(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive() || !GameStates.IsInTask) return;

            if (CurrentEffects.TryGetValue(pc.PlayerId, out var effects))
            {
                var now = Utils.GetTimeStamp();

                foreach (var item in effects)
                {
                    if (item.Value.StartTimeStamp + item.Value.Duration < now)
                    {
                        effects.Remove(item.Key);

                        if (item.Key.IsVisionChangingEffect())
                        {
                            RevertVisionChangesForPlayer(pc, true);
                        }
                        else if (item.Key.IsSpeedChangingEffect())
                        {
                            RevertSpeedChangesForPlayer(pc, true);
                        }
                        else
                        {
                            switch (item.Key)
                            {
                                case Effect.ShieldAll:
                                case Effect.ShieldRandomPlayer:
                                    break;
                            }
                        }
                    }
                }
            }
        }
    }
}
