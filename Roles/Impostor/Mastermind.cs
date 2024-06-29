using System;
using System.Collections.Generic;
using EHR.Crewmate;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor
{
    public class Mastermind : RoleBase
    {
        private const int Id = 640600;

        private static List<byte> PlayerIdList = [];

        public static Dictionary<byte, long> ManipulatedPlayers = [];
        public static Dictionary<byte, long> ManipulateDelays = [];
        public static Dictionary<byte, float> TempKCDs = [];

        public static OptionItem KillCooldown;
        public static OptionItem TimeLimit;
        public static OptionItem Delay;

        public static float ManipulateCD;
        public byte MastermindId = byte.MaxValue;

        private PlayerControl Mastermind_ => GetPlayerById(MastermindId);

        public override bool IsEnable => MastermindId != byte.MaxValue || Randomizer.Exists;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Mastermind);
            KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
            // Manipulation Cooldown = Kill Cooldown + Delay + Time Limit
            TimeLimit = new IntegerOptionItem(Id + 12, "MastermindTimeLimit", new(1, 60, 1), 20, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
            Delay = new IntegerOptionItem(Id + 13, "MastermindDelay", new(0, 30, 1), 7, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            MastermindId = byte.MaxValue;
            ManipulatedPlayers = [];
            ManipulateDelays = [];
            TempKCDs = [];
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            MastermindId = playerId;
            ManipulateCD = KillCooldown.GetFloat() + TimeLimit.GetFloat() + Delay.GetFloat();
            PlayerIdList.Add(playerId);
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return false;
            if (killer == null) return false;
            if (target == null) return false;

            return killer.CheckDoubleTrigger(target, () =>
            {
                killer.SetKillCooldown(time: ManipulateCD);
                if (target.HasKillButton() || target.GetTaskState().hasTasks || UsePets.GetBool())
                {
                    ManipulateDelays.TryAdd(target.PlayerId, TimeStamp);
                    NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                }
            });
        }

        public override void OnFixedUpdate(PlayerControl _)
        {
            if (!IsEnable || GameStates.IsMeeting || (ManipulatedPlayers.Count == 0 && ManipulateDelays.Count == 0)) return;

            foreach (var x in ManipulateDelays)
            {
                var pc = GetPlayerById(x.Key);

                if (!pc.IsAlive())
                {
                    ManipulateDelays.Remove(x.Key);
                    continue;
                }

                if (x.Value + Delay.GetInt() < TimeStamp)
                {
                    ManipulateDelays.Remove(x.Key);
                    ManipulatedPlayers.TryAdd(x.Key, TimeStamp);

                    if (!pc.GetTaskState().hasTasks || UsePets.GetBool())
                    {
                        TempKCDs.TryAdd(pc.PlayerId, Main.KillTimers[pc.PlayerId]);
                        pc.SetKillCooldown(time: 1f);
                    }

                    NotifyRoles(SpecifySeer: Mastermind_, SpecifyTarget: pc);
                }
            }

            foreach (var x in ManipulatedPlayers)
            {
                var player = GetPlayerById(x.Key);

                if (!player.IsAlive())
                {
                    ManipulatedPlayers.Remove(x.Key);
                    TempKCDs.Remove(x.Key);
                    continue;
                }

                if (x.Value + TimeLimit.GetInt() < TimeStamp)
                {
                    ManipulatedPlayers.Remove(x.Key);
                    TempKCDs.Remove(x.Key);
                    player.Suicide(realKiller: Mastermind_);
                    RPC.PlaySoundRPC(MastermindId, Sounds.KillSound);
                }

                var time = TimeLimit.GetInt() - (TimeStamp - x.Value);

                player.Notify(string.Format(GetString(UsePets.GetBool() ? "ManipulatePetNotify" : player.GetTaskState().hasTasks ? "ManipulateTaskNotify" : "ManipulateNotify"), time), 1.1f);
            }
        }

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            foreach (var x in ManipulatedPlayers)
            {
                var pc = GetPlayerById(x.Key);
                if (pc.IsAlive()) pc.Suicide(realKiller: Mastermind_);
            }

            ManipulateDelays.Clear();
            ManipulatedPlayers.Clear();
            TempKCDs.Clear();
        }

        public static bool ForceKillForManipulatedPlayer(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            ManipulatedPlayers.Remove(killer.PlayerId);

            foreach (var id in PlayerIdList)
            {
                (Main.PlayerStates[id].Role as Mastermind)?.NotifyMastermindTargetSurvived();
            }

            if (target.Is(CustomRoles.Pestilence) || Veteran.VeteranInProtect.ContainsKey(target.PlayerId) || target.Is(CustomRoles.Mastermind))
            {
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                Main.PlayerStates[killer.PlayerId].SetDead();
                target.Kill(killer);
                TempKCDs.Remove(killer.PlayerId);
                return false;
            }

            killer.Kill(target);

            killer.Notify(GetString("MastermindTargetSurvived"));

            LateTask.New(() =>
            {
                var kcd = TempKCDs.TryGetValue(killer.PlayerId, out var cd) ? cd : Main.AllPlayerKillCooldown.GetValueOrDefault(killer.PlayerId, DefaultKillCooldown);
                killer.SetKillCooldown(time: kcd);
                if (killer.GetCustomRole().PetActivatedAbility()) killer.AddAbilityCD((int)Math.Round(kcd));
                TempKCDs.Remove(killer.PlayerId);
            }, 0.1f, "Set KCD for Manipulated Kill");

            return false;
        }

        public static void OnManipulatedPlayerTaskComplete(PlayerControl pc)
        {
            ManipulatedPlayers.Remove(pc.PlayerId);

            foreach (var id in PlayerIdList)
            {
                (Main.PlayerStates[id].Role as Mastermind)?.NotifyMastermindTargetSurvived();
            }

            pc.Notify(GetString("MastermindTargetSurvived"));
            TempKCDs.Remove(pc.PlayerId);
        }

        private void NotifyMastermindTargetSurvived()
        {
            if (!IsEnable) return;
            Mastermind_.Notify(GetString("ManipulatedKilled"));
            if (Main.KillTimers[MastermindId] > KillCooldown.GetFloat()) Mastermind_.SetKillCooldown(time: KillCooldown.GetFloat());
        }
    }
}