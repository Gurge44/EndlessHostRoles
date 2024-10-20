using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Patches;
using Hazel;

namespace EHR.Impostor
{
    public class Wasp : RoleBase
    {
        public static bool On;
        private static List<Wasp> Instances = [];

        private static OptionItem StingCooldown;
        private static OptionItem KillDelay;
        private static OptionItem EvadeKills;
        private static OptionItem SwarmModeDuration;
        private static OptionItem WaspDiesAfterSwarmEnd;
        private static OptionItem PestControlDuration;
        private static OptionItem PestControlSpeed;
        private static OptionItem PestControlVision;
        public Dictionary<byte, long> DelayedKills;
        private bool EvadedKillThisRound;

        private long LastUpdate;
        public HashSet<byte> MeetingKills;
        private long PestControlEnd;
        private long SwarmModeEnd;
        private PlayerControl WaspPC;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(645350)
                .AutoSetupOption(ref StingCooldown, 30, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
                .AutoSetupOption(ref KillDelay, 5, new IntegerValueRule(1, 30, 1), OptionFormat.Seconds)
                .AutoSetupOption(ref EvadeKills, true)
                .AutoSetupOption(ref SwarmModeDuration, 15, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds, overrideParent: EvadeKills)
                .AutoSetupOption(ref WaspDiesAfterSwarmEnd, true, overrideParent: EvadeKills)
                .AutoSetupOption(ref PestControlDuration, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds, overrideParent: EvadeKills)
                .AutoSetupOption(ref PestControlSpeed, 0.5f, new FloatValueRule(0.05f, 2f, 0.05f), OptionFormat.Multiplier, overrideParent: EvadeKills)
                .AutoSetupOption(ref PestControlVision, 0.2f, new FloatValueRule(0f, 1f, 0.05f), OptionFormat.Multiplier, overrideParent: EvadeKills);
        }

        public override void Init()
        {
            On = false;
            Instances = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
            Instances.Add(this);
            WaspPC = playerId.GetPlayer();
            DelayedKills = [];
            MeetingKills = [];
            SwarmModeEnd = 0;
            PestControlEnd = 0;
            EvadedKillThisRound = false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = SwarmModeEnd == 0 ? StingCooldown.GetInt() : 0.01f;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (PestControlEnd == 0) return;
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, PestControlVision.GetFloat());
            opt.SetFloat(FloatOptionNames.CrewLightMod, PestControlVision.GetFloat());
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (DelayedKills.ContainsKey(target.PlayerId)) return false;

            if (target.HasKillButton()) DelayedKills[target.PlayerId] = Utils.TimeStamp + KillDelay.GetInt();
            else MeetingKills.Add(target.PlayerId);

            killer.SetKillCooldown(StingCooldown.GetInt());
            return false;
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || !GameStates.IsInTask || ExileController.Instance || !DelayedKills.TryGetValue(pc.PlayerId, out var ts) || ts > Utils.TimeStamp) return;
            pc.Suicide(PlayerState.DeathReason.Stung, WaspPC);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (SwarmModeEnd == 0 && PestControlEnd == 0) return;

            var now = Utils.TimeStamp;
            if (LastUpdate == now) return;
            LastUpdate = now;

            if (SwarmModeEnd != 0)
            {
                if (SwarmModeEnd <= now)
                {
                    SwarmModeEnd = 0;
                    Utils.SendRPC(CustomRPC.SyncRoleData, WaspPC.PlayerId, SwarmModeEnd);

                    if (WaspDiesAfterSwarmEnd.GetBool())
                    {
                        pc.Suicide();
                    }
                    else
                    {
                        pc.SyncSettings();
                        pc.SetKillCooldown(StingCooldown.GetInt());
                    }
                }

                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }

            if (PestControlEnd != 0 && PestControlEnd <= now)
            {
                PestControlEnd = 0;
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                pc.MarkDirtySettings();
            }
        }

        public override void OnReportDeadBody()
        {
            foreach (var id in DelayedKills.Keys)
            {
                var player = id.GetPlayer();
                if (player == null || !player.IsAlive()) continue;
                player.Suicide(PlayerState.DeathReason.Stung, WaspPC);
            }

            DelayedKills.Clear();
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (!EvadeKills.GetBool() || EvadedKillThisRound) return true;

            if (IRandom.Instance.Next(2) == 0)
            {
                SwarmModeEnd = Utils.TimeStamp + SwarmModeDuration.GetInt();
                Utils.SendRPC(CustomRPC.SyncRoleData, WaspPC.PlayerId, SwarmModeEnd);
                target.SyncSettings();
                target.SetKillCooldown(0.01f);
            }
            else
            {
                PestControlEnd = Utils.TimeStamp + PestControlDuration.GetInt();
                Main.AllPlayerSpeed[target.PlayerId] = PestControlSpeed.GetFloat();
                target.MarkDirtySettings();
            }

            EvadedKillThisRound = true;
            return false;
        }

        public override bool CanUseKillButton(PlayerControl pc) => !EvadedKillThisRound || SwarmModeEnd != 0;
        public override bool CanUseSabotage(PlayerControl pc) => !EvadedKillThisRound;
        public override bool CanUseImpostorVentButton(PlayerControl pc) => !EvadedKillThisRound;

        public override void AfterMeetingTasks()
        {
            EvadedKillThisRound = false;
            MeetingKills.Clear();
        }

        public static void OnExile(byte[] exileIds)
        {
            try
            {
                foreach (Wasp instance in Instances)
                {
                    foreach (byte id in exileIds)
                    {
                        if (id == instance.WaspPC.PlayerId)
                            instance.MeetingKills.Clear();
                    }
                }

                List<byte> waspDeathList = [];
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    foreach (Wasp instance in Instances)
                    {
                        if (Main.AfterMeetingDeathPlayers.ContainsKey(pc.PlayerId)) continue;

                        if (instance.MeetingKills.Contains(pc.PlayerId) && instance.WaspPC != null && instance.WaspPC.IsAlive())
                        {
                            pc.SetRealKiller(instance.WaspPC);
                            waspDeathList.Add(pc.PlayerId);
                        }
                    }
                }

                CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Stung, [.. waspDeathList]);
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
        }

        public static string GetStungMark(byte target)
        {
            return Instances.Any(x => x.MeetingKills.Contains(target)) ? Utils.ColorString(Palette.ImpostorRed, "\u25c0") : string.Empty;
        }

        public void ReceiveRPC(MessageReader reader) => SwarmModeEnd = long.Parse(reader.ReadString());

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (seer.PlayerId != WaspPC.PlayerId || seer.PlayerId != target.PlayerId || (seer.IsModClient() && !hud) || meeting || SwarmModeEnd == 0) return string.Empty;
            return string.Format(Translator.GetString("Wasp.SwarmModeSuffix"), SwarmModeEnd - Utils.TimeStamp);
        }
    }
}