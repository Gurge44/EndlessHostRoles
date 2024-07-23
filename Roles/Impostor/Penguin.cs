﻿using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Impostor
{
    public class Penguin : RoleBase
    {
        private const int Id = 641800;

        private static OptionItem OptionAbductTimerLimit;
        private static OptionItem OptionMeetingKill;
        private static OptionItem OptionSpeedDuringDrag;
        private static OptionItem OptionVictimCanUseAbilities;

        private float AbductTimer;
        private float AbductTimerLimit;

        private PlayerControl AbductVictim;
        private float Cooldown;

        private int Count;
        private float DefaultSpeed;

        private bool IsGoose;
        private long LastNotify;
        private bool MeetingKill;

        private PlayerControl Penguin_;
        private byte PenguinId = byte.MaxValue;
        private float SpeedDuringDrag;

        private bool stopCount;
        private bool VictimCanUseAbilities;

        public override bool IsEnable => PenguinId != byte.MaxValue;

        // Measures to prevent the opponent who is about to be killed during abduction from using their abilities
        public static bool IsVictim(PlayerControl pc)
        {
            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is Penguin { IsEnable: true, VictimCanUseAbilities: false } pg && pg.AbductVictim != null && pg.AbductVictim.PlayerId == pc.PlayerId)
                    return true;
            }

            return false;
        }

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Penguin);
            OptionAbductTimerLimit = new FloatOptionItem(Id + 11, "PenguinAbductTimerLimit", new(1f, 20f, 1f), 10f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Penguin])
                .SetValueFormat(OptionFormat.Seconds);
            OptionMeetingKill = new BooleanOptionItem(Id + 12, "PenguinMeetingKill", false, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Penguin]);
            OptionSpeedDuringDrag = new FloatOptionItem(Id + 13, "PenguinSpeedDuringDrag", new(0.1f, 3f, 0.1f), 1f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Penguin])
                .SetValueFormat(OptionFormat.Multiplier);
            OptionVictimCanUseAbilities = new BooleanOptionItem(Id + 14, "PenguinVictimCanUseAbilities", false, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Penguin]);
        }

        public override void Init()
        {
            PenguinId = byte.MaxValue;
            Penguin_ = null;
        }

        public override void Add(byte playerId)
        {
            IsGoose = Main.PlayerStates[playerId].MainRole == CustomRoles.Goose;

            if (!IsGoose)
            {
                AbductTimerLimit = OptionAbductTimerLimit.GetFloat();
                MeetingKill = OptionMeetingKill.GetBool();
                SpeedDuringDrag = OptionSpeedDuringDrag.GetFloat();
                VictimCanUseAbilities = OptionVictimCanUseAbilities.GetBool();
                Cooldown = Options.DefaultKillCooldown;
            }
            else
            {
                AbductTimerLimit = Goose.OptionAbductTimerLimit.GetFloat();
                MeetingKill = false;
                SpeedDuringDrag = Goose.OptionSpeedDuringDrag.GetFloat();
                VictimCanUseAbilities = Goose.OptionVictimCanUseAbilities.GetBool();
                Cooldown = Goose.Cooldown.GetFloat();
            }

            LateTask.New(() => DefaultSpeed = Main.AllPlayerSpeed[playerId], 9f, log: false);

            PenguinId = playerId;
            Penguin_ = Utils.GetPlayerById(playerId);

            AbductTimer = 255f;
            stopCount = false;
            LastNotify = 0;
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Cooldown;
        public override bool CanUseImpostorVentButton(PlayerControl pc) => AbductVictim == null && !IsGoose;
        public override bool CanUseKillButton(PlayerControl pc) => pc.IsAlive();
        public override bool CanUseSabotage(PlayerControl pc) => !IsGoose && pc.IsAlive();

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (IsGoose) opt.SetVision(false);
        }

        void SendRPC()
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PenguinSync, SendOption.Reliable);
            writer.Write(PenguinId);
            writer.Write(1);
            writer.Write(AbductVictim?.PlayerId ?? 255);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPC(byte victim)
        {
            if (victim == 255)
            {
                AbductVictim = null;
                AbductTimer = 255f;
            }
            else
            {
                AbductVictim = Utils.GetPlayerById(victim);
                AbductTimer = AbductTimerLimit;
            }
        }

        public void ReceiveRPC(float timer)
        {
            AbductTimer = timer;
        }

        void AddVictim(PlayerControl target)
        {
            if (!IsEnable) return;
            AbductVictim = target;
            AbductTimer = AbductTimerLimit;
            Main.AllPlayerSpeed[PenguinId] = SpeedDuringDrag;
            Penguin_.MarkDirtySettings();
            LogSpeed();
            Utils.NotifyRoles(SpecifySeer: Penguin_, SpecifyTarget: Penguin_);
            SendRPC();
        }

        void RemoveVictim(bool allowDelay = true)
        {
            if (!IsEnable) return;
            if (!allowDelay)
            {
                AbductVictim = null;
                SendRPC();
            }
            else
            {
                LateTask.New(() =>
                {
                    AbductVictim = null;
                    SendRPC();
                }, 1f, log: false);
            }

            AbductTimer = 255f;
            Main.AllPlayerSpeed[PenguinId] = DefaultSpeed;
            Penguin_.MarkDirtySettings();
            LogSpeed();
            Utils.NotifyRoles(SpecifySeer: Penguin_, SpecifyTarget: Penguin_);
        }

        void LogSpeed() => Logger.Info($" Speed: {Main.AllPlayerSpeed[PenguinId]}", IsGoose ? "Goose" : "Penguin");

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return true;

            bool doKill = !IsGoose;
            if (AbductVictim != null)
            {
                if (target.PlayerId != AbductVictim.PlayerId)
                {
                    // During an abduction, only the abductee can be killed.
                    if (!IsGoose)
                    {
                        Penguin_.Kill(AbductVictim);
                        Penguin_.ResetKillCooldown();
                    }

                    doKill = false;
                }

                RemoveVictim();
            }
            else
            {
                if (!IsGoose && !killer.RpcCheckAndMurder(target, check: true)) return false;
                doKill = false;
                AddVictim(target);
            }

            return doKill;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(AbductVictim != null && !IsGoose ? GetString("KillButtonText") : GetString("PenguinKillButtonText"));
        }

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            stopCount = true;
            // If you meet a meeting with time running out, kill it even if you're on a ladder.
            if (AbductVictim != null && AbductTimer <= 0f)
            {
                if (!IsGoose) Penguin_.Kill(AbductVictim);
                RemoveVictim(allowDelay: false);
            }

            if (MeetingKill)
            {
                if (!AmongUsClient.Instance.AmHost) return;
                if (AbductVictim == null) return;
                if (!IsGoose) Penguin_.Kill(AbductVictim);
                RemoveVictim();
            }
        }

        public override void AfterMeetingTasks()
        {
            if (Main.NormalOptions.MapId == 4) return;

            //Maps other than Airship
            RestartAbduct();
        }

        public static void OnSpawnAirship()
        {
            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is Penguin { IsEnable: true } pg)
                {
                    pg.RestartAbduct();
                }
            }
        }

        void RestartAbduct()
        {
            if (!IsEnable) return;
            if (AbductVictim != null)
            {
                Penguin_.MarkDirtySettings();
            }

            stopCount = false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable) return;
            if (!AmongUsClient.Instance.AmHost) return;
            if (!GameStates.IsInTask) return;

            if (!stopCount)
            {
                AbductTimer -= Time.fixedDeltaTime;

                Count++;
                if (Count >= 10)
                {
                    Count = 0;
                    Utils.SendRPC(CustomRPC.PenguinSync, PenguinId, 2, AbductTimer);
                }
            }

            if (AbductVictim != null)
            {
                if (!Penguin_.IsAlive() || !AbductVictim.IsAlive())
                {
                    RemoveVictim();
                    return;
                }

                if (LastNotify != Utils.TimeStamp)
                {
                    Utils.NotifyRoles(SpecifySeer: Penguin_, SpecifyTarget: Penguin_);
                    LastNotify = Utils.TimeStamp;
                }

                if (AbductTimer <= 0f && !Penguin_.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
                {
                    // Set IsDead to true first (prevents ladder chase)
                    if (!IsGoose)
                    {
                        AbductVictim.Data.IsDead = true;
                        AbductVictim.Data.MarkDirty();
                    }

                    // If the penguin himself is on a ladder, kill him after getting off the ladder.
                    if (!AbductVictim.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
                    {
                        var abductVictim = AbductVictim;
                        LateTask.New(() =>
                        {
                            if (IsGoose) return;

                            var sId = abductVictim.NetTransform.lastSequenceId + 5;
                            abductVictim.NetTransform.SnapTo(Penguin_.transform.position, (ushort)sId);
                            Penguin_.Kill(abductVictim);

                            var sender = CustomRpcSender.Create("PenguinMurder");
                            {
                                sender.AutoStartRpc(abductVictim.NetTransform.NetId, (byte)RpcCalls.SnapTo);
                                {
                                    NetHelpers.WriteVector2(Penguin_.transform.position, sender.stream);
                                    sender.Write(abductVictim.NetTransform.lastSequenceId);
                                }
                                sender.EndRpc();
                                sender.AutoStartRpc(Penguin_.NetId, (byte)RpcCalls.MurderPlayer);
                                {
                                    sender.WriteNetObject(abductVictim);
                                }
                                sender.EndRpc();
                            }
                            sender.SendMessage();
                        }, 0.3f, "PenguinMurder");

                        RemoveVictim();
                    }
                }
                // SnapToRPC does not work for players on top of the ladder, and only the host behaves differently, so teleporting is not done uniformly.
                else if (!AbductVictim.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
                {
                    var position = Penguin_.transform.position;
                    if (!Penguin_.IsHost())
                    {
                        Utils.TP(AbductVictim.NetTransform, position, log: false);
                    }
                    else
                    {
                        LateTask.New(() =>
                        {
                            if (AbductVictim != null)
                                Utils.TP(AbductVictim.NetTransform, position, log: false);
                        }, 0.25f, log: false);
                    }
                }
            }
            else if (AbductTimer <= 100f)
            {
                AbductTimer = 255f;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
        {
            if (seer == null || seer.PlayerId != target.PlayerId) return string.Empty;
            if (Main.PlayerStates.TryGetValue(seer.PlayerId, out var state) && state.Role is Penguin pg && pg.AbductVictim != null)
            {
                return $"\u21b9 {(int)(pg.AbductTimer + 1f)}s";
            }

            return string.Empty;
        }
    }
}