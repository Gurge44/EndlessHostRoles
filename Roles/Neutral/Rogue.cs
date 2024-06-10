﻿using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral
{
    public class Rogue : RoleBase
    {
        private const int Id = 644300;
        public static bool On;

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private bool AllTasksCompleted;

        private int Count;
        private (Objective Objective, Reward Reward, object Data, bool IsCompleted) CurrentTask;
        private bool DoCheck;
        private List<Objective> GotObjectives;
        private List<Reward> GotRewards;
        private Vector2? LastPos;

        private int MorphCooldown;
        private bool Moving = true;

        private PlayerControl RoguePC;

        public override bool IsEnable => On;
        public bool DisableDevices => GotRewards.Contains(Reward.DisableDevices);

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Rogue);
            KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rogue])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rogue]);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;

            RoguePC = Utils.GetPlayerById(playerId);
            GotObjectives = [];
            GotRewards = [];
            CurrentTask = (default, default, null, false);
            AllTasksCompleted = false;
            SendRPC();

            MorphCooldown = 0;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = GotRewards.Contains(Reward.DecreasedKillCooldown) ? KillCooldown.GetFloat() / 2f : KillCooldown.GetFloat();
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CurrentTask.Objective == Objective.VentXTimes || CanVent.GetBool();
        public override bool CanUseSabotage(PlayerControl pc) => GotRewards.Contains(Reward.Sabotage) || GotRewards.Contains(Reward.Morph);

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(GotRewards.Contains(Reward.ImpostorVision));
            opt.SetInt(Int32OptionNames.KillDistance, GotRewards.Contains(Reward.LongReach) ? 2 : 0);
            var defSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            Main.AllPlayerSpeed[id] = GotRewards.Contains(Reward.IncreasedSpeed) ? defSpeed + 0.5f : defSpeed;
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            if (GotRewards.Contains(Reward.Morph) && MorphCooldown <= 0)
            {
                MorphCooldown = 15 + (int)Main.RealOptionsData.GetFloat(FloatOptionNames.KillCooldown);
                Utils.SendRPC(CustomRPC.SyncRogue, pc.PlayerId, 2, MorphCooldown);
                var target = Main.AllAlivePlayerControls.Except([pc]).Shuffle()[0];
                pc.RpcShapeshift(target, !Options.DisableAllShapeshiftAnimations.GetBool());
                return false;
            }

            return GotRewards.Contains(Reward.Sabotage);
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !GotRewards.Contains(Reward.Shield);
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            switch (CurrentTask.Objective)
            {
                case Objective.KillSpecificPlayer when target.PlayerId == (byte)CurrentTask.Data:
                    SetTaskCompleted();
                    break;
                case Objective.KillXTimes:
                    CurrentTask.Data = (int)CurrentTask.Data - 1;
                    if ((int)CurrentTask.Data <= 0)
                    {
                        SetTaskCompleted();
                    }

                    break;
                case Objective.KillInSpecificRoom when Translator.GetString(target.GetPlainShipRoom().RoomId.ToString()) == (string)CurrentTask.Data:
                    SetTaskCompleted();
                    break;
            }
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (CurrentTask.Objective == Objective.VentXTimes)
            {
                CurrentTask.Data = (int)CurrentTask.Data - 1;
                if ((int)CurrentTask.Data <= 0)
                {
                    SetTaskCompleted();
                }
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive() || !GameStates.IsInTask) return;

            Count++;
            if (Count < 30) return;
            Count = 0;

            if (DoCheck && Moving)
            {
                if (LastPos is null)
                {
                    LastPos = pc.Pos();
                    return;
                }

                Moving = Vector2.Distance(pc.Pos(), LastPos.Value) >= 0.1f;
                LastPos = pc.Pos();
                if (!Moving) pc.Notify(Utils.ColorString(Color.red, "<size=4>x</size>"));
            }

            if (MorphCooldown > 0)
            {
                MorphCooldown--;
                Utils.SendRPC(CustomRPC.SyncRogue, pc.PlayerId, 2, MorphCooldown);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

                if (pc.IsShifted() && MorphCooldown <= Main.RealOptionsData.GetFloat(FloatOptionNames.KillCooldown))
                {
                    pc.RpcShapeshift(pc, !Options.DisableAllShapeshiftAnimations.GetBool());
                }
            }
        }

        public void OnButtonPressed()
        {
            if (CurrentTask.Objective == Objective.CallEmergencyMeeting)
            {
                SetTaskCompleted(chatMessage: true);
            }
        }

        public void OnFixSabotage()
        {
            if (CurrentTask.Objective == Objective.FixSabotage)
            {
                SetTaskCompleted();
            }
        }

        void SetTaskCompleted(bool chatMessage = false)
        {
            CurrentTask.IsCompleted = true;
            SendRPC();

            if (chatMessage) LateTask.New(() => { Utils.SendMessage("\n", RoguePC.PlayerId, Translator.GetString("Rogue.TaskCompleted")); }, 8f, log: false);
            else Utils.NotifyRoles(SpecifySeer: RoguePC, SpecifyTarget: RoguePC);
        }

        public override void OnReportDeadBody()
        {
            DoCheck = false;

            if (CurrentTask.Objective == Objective.DontStopWalking && Moving)
            {
                SetTaskCompleted(chatMessage: true);
            }
        }

        public override void AfterMeetingTasks()
        {
            try
            {
                if (AllTasksCompleted || !RoguePC.IsAlive()) return;

                if (CurrentTask.IsCompleted)
                {
                    GotObjectives.Add(CurrentTask.Objective);
                    GotRewards.Add(CurrentTask.Reward);
                }

                switch (CurrentTask.Reward)
                {
                    case Reward.HelpfulAddon:
                        var addon = Options.GroupedAddons[AddonTypes.Helpful].RandomElement();
                        RoguePC.RpcSetCustomRole(addon);
                        break;
                    case Reward.DecreasedKillCooldown:
                        RoguePC.ResetKillCooldown();
                        RoguePC.SyncSettings();
                        RoguePC.SetKillCooldown();
                        break;
                    case Reward.Sabotage when RoguePC.IsHost():
                        HudManager.Instance.SetHudActive(RoguePC, RoguePC.Data.Role, true);
                        break;
                }

                var objective = Enum.GetValues<Objective>().Except(GotObjectives).Shuffle()[0];
                var reward = Enum.GetValues<Reward>().Except(GotRewards).Shuffle()[0];
                object data = objective switch
                {
                    Objective.KillInSpecificRoom => Translator.GetString(ShipStatus.Instance.AllRooms.RandomElement().RoomId.ToString()),
                    Objective.KillSpecificPlayer => Main.AllAlivePlayerControls.Select(x => x.PlayerId).Except([RoguePC.PlayerId]).Shuffle()[0],
                    Objective.VentXTimes => IRandom.Instance.Next(2, 20),
                    Objective.KillXTimes => IRandom.Instance.Next(2, 5),
                    _ => null
                };

                CurrentTask = (objective, reward, data, false);
                SendRPC();

                if (objective == Objective.DontStopWalking)
                {
                    LateTask.New(() =>
                    {
                        Moving = true;
                        DoCheck = true;
                    }, 5f, log: false);
                }

                Utils.NotifyRoles(SpecifySeer: RoguePC, SpecifyTarget: RoguePC);
                Logger.Info($" Objective: {Translator.GetString("Rogue.Objective." + objective)} - Reward: {Translator.GetString("Rogue.Reward." + reward)} - Data: {data}", "Rogue");
            }
            catch (Exception e)
            {
                if (e is IndexOutOfRangeException)
                {
                    AllTasksCompleted = true;
                    return;
                }

                Utils.ThrowException(e);
            }
        }

        void SendRPC() => Utils.SendRPC(CustomRPC.SyncRogue, RoguePC.PlayerId, 1, (int)CurrentTask.Objective, (int)CurrentTask.Reward, CurrentTask.IsCompleted, AllTasksCompleted, DataType(), CurrentTask.Data);

        int DataType() => CurrentTask.Data switch
        {
            string => 0,
            byte => 1,
            int => 2,
            null => 3,
            _ => -1
        };

        public void ReceiveRPC(MessageReader reader)
        {
            if (reader.ReadPackedInt32() == 1)
            {
                var objective = (Objective)reader.ReadPackedInt32();
                var reward = (Reward)reader.ReadPackedInt32();
                var isCompleted = reader.ReadBoolean();
                AllTasksCompleted = reader.ReadBoolean();
                object data = reader.ReadPackedInt32() switch
                {
                    0 => reader.ReadString(),
                    1 => reader.ReadByte(),
                    2 => reader.ReadPackedInt32(),
                    3 => null,
                    _ => default
                };
                CurrentTask = (objective, reward, data, isCompleted);
            }
            else
            {
                MorphCooldown = reader.ReadPackedInt32();
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != RoguePC.PlayerId || seer.PlayerId != target.PlayerId || (seer.IsModClient() && !isHUD) || MeetingStates.FirstMeeting) return string.Empty;
            if (AllTasksCompleted) return Translator.GetString("Rogue.AllTasksCompleted");
            if (CurrentTask.IsCompleted) return Translator.GetString("Rogue.TaskCompleted");

            float d = Main.RealOptionsData.GetFloat(FloatOptionNames.KillCooldown);
            string c = GotRewards.Contains(Reward.Morph) && MorphCooldown > 0
                ? MorphCooldown <= d
                    ? string.Format(Translator.GetString("CDPT"), MorphCooldown) + "\n"
                    : $"\u21b9 ({MorphCooldown - d}s)"
                : string.Empty;

            string o = Translator.GetString("Rogue.Objective." + CurrentTask.Objective);
            if (CurrentTask.Objective is Objective.KillInSpecificRoom or Objective.VentXTimes or Objective.KillSpecificPlayer or Objective.KillXTimes)
                o = string.Format(o, CurrentTask.Data is byte id ? Utils.ColorString(Main.PlayerColors.GetValueOrDefault(id, Color.white), Utils.GetPlayerById(id).GetRealName()) : CurrentTask.Data);
            string r = Translator.GetString("Rogue.Reward." + CurrentTask.Reward);
            string s = string.Format(Translator.GetString("Rogue.Task"), o, r);
            return $"<size=80%>{c}{s}</size>";
        }

        enum Objective
        {
            KillInSpecificRoom,
            VentXTimes,
            CallEmergencyMeeting,
            KillSpecificPlayer,
            KillXTimes,
            DontStopWalking,
            FixSabotage
        }

        enum Reward
        {
            DisableDevices,
            Morph,
            IncreasedSpeed,
            DecreasedKillCooldown,
            Sabotage,
            Shield,
            LongReach,
            HelpfulAddon,
            ImpostorVision
        }
    }
}