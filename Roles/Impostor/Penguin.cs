using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor
{
    public static class Penguin
    {
        private static readonly int Id = 641800;
        public static List<byte> playerIdList = new();

        private static OptionItem OptionAbductTimerLimit;
        private static OptionItem OptionMeetingKill;

        private static PlayerControl AbductVictim;
        private static float AbductTimer;
        private static float AbductTimerLimit;
        private static bool stopCount;
        private static bool MeetingKill;

        // Measures to prevent the opponent who is about to be killed during abduction from using their abilities
        public static bool IsKiller => AbductVictim == null;
        public static void SetupCustomOption()
        {
            OptionAbductTimerLimit = FloatOptionItem.Create(Id + 11, "PenguinAbductTimerLimit", new(5f, 20f, 1f), 10f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Penguin])
                .SetValueFormat(OptionFormat.Seconds);
            OptionMeetingKill = BooleanOptionItem.Create(Id + 12, "PenguinMeetingKill", false, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Penguin]);
        }
        public static void Init()
        {
            playerIdList = new();
        }
        public static void Add(byte playerId)
        {
            AbductTimerLimit = OptionAbductTimerLimit.GetFloat();
            MeetingKill = OptionMeetingKill.GetBool();

            playerIdList.Add(playerId);

            AbductTimer = 255f;
            stopCount = false;
        }
        public static void ApplyGameOptions(IGameOptions opt) => AURoleOptions.ShapeshifterCooldown = AbductVictim != null ? AbductTimer : 255f;
        private static void SendRPC()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PenguinSync, SendOption.Reliable, -1);
            writer.Write(AbductVictim?.PlayerId ?? 255);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            var victim = reader.ReadByte();

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
        static void AddVictim(PlayerControl target)
        {
            //Prevent using of moving platform??
            AbductVictim = target;
            AbductTimer = AbductTimerLimit;
            Utils.GetPlayerById(playerIdList[0]).SyncSettings();
            Utils.GetPlayerById(playerIdList[0]).RpcResetAbilityCooldown();
            SendRPC();
        }
        static void RemoveVictim()
        {
            if (AbductVictim != null)
            {
                //PlayerState.GetByPlayerId(AbductVictim.PlayerId).CanUseMovingPlatform = true;
                AbductVictim = null;
            }
            //MyState.CanUseMovingPlatform = true;
            AbductTimer = 255f;
            Utils.GetPlayerById(playerIdList[0]).SyncSettings();
            Utils.GetPlayerById(playerIdList[0]).RpcResetAbilityCooldown();
            SendRPC();
        }
        public static bool OnCheckMurderAsKiller(PlayerControl killer, PlayerControl target)
        {
            bool doKill = true;
            if (AbductVictim != null)
            {
                if (target != AbductVictim)
                {
                    // During an abduction, only the abductee can be killed.
                    Utils.GetPlayerById(playerIdList[0]).Kill(AbductVictim);
                    Utils.GetPlayerById(playerIdList[0]).ResetKillCooldown();
                    doKill = false;
                }
                RemoveVictim();
            }
            else
            {
                doKill = false;
                AddVictim(target);
            }
            return doKill;
        }
        public static bool OverrideKillButtonText(out string text)
        {
            if (AbductVictim != null)
            {
                text = GetString("KillButtonText");
            }
            else
            {
                text = GetString("PenguinKillButtonText");
            }
            return true;
        }
        public static string GetAbilityButtonText()
        {
            return GetString("PenguinTimerText");
        }
        public static bool CanUseAbilityButton()
        {
            return AbductVictim != null;
        }
        public static void OnReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target)
        {
            stopCount = true;
            // 時間切れ状態で会議を迎えたらはしご中でも構わずキルする
            if (AbductVictim != null && AbductTimer <= 0f)
            {
                Utils.GetPlayerById(playerIdList[0]).Kill(AbductVictim);
            }
            if (MeetingKill)
            {
                if (!AmongUsClient.Instance.AmHost) return;
                if (AbductVictim == null) return;
                Utils.GetPlayerById(playerIdList[0]).Kill(AbductVictim);
                RemoveVictim();
            }
        }
        public static void AfterMeetingTasks()
        {
            if (Main.NormalOptions.MapId == 4) return;

            //マップがエアシップ以外
            RestartAbduct();
        }
        public static void OnSpawnAirship()
        {
            RestartAbduct();
        }
        public static void RestartAbduct()
        {
            if (AbductVictim != null)
            {
                Utils.GetPlayerById(playerIdList[0]).SyncSettings();
                Utils.GetPlayerById(playerIdList[0]).RpcResetAbilityCooldown();
                stopCount = false;
            }
        }
        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!GameStates.IsInTask) return;

            if (!stopCount)
                AbductTimer -= Time.fixedDeltaTime;

            if (AbductVictim != null)
            {
                if (!Utils.GetPlayerById(playerIdList[0]).IsAlive() || !AbductVictim.IsAlive())
                {
                    RemoveVictim();
                    return;
                }
                if (AbductTimer <= 0f && !Utils.GetPlayerById(playerIdList[0]).MyPhysics.Animations.IsPlayingAnyLadderAnimation())
                {
                    // 先にIsDeadをtrueにする(はしごチェイス封じ)
                    AbductVictim.Data.IsDead = true;
                    GameData.Instance.SetDirty();
                    // ペンギン自身がはしご上にいる場合，はしごを降りてからキルする
                    if (!AbductVictim.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
                    {
                        var abductVictim = AbductVictim;
                        _ = new LateTask(() =>
                        {
                            var sId = abductVictim.NetTransform.lastSequenceId + 5;
                            abductVictim.NetTransform.SnapTo(Utils.GetPlayerById(playerIdList[0]).transform.position, (ushort)sId);
                            Utils.GetPlayerById(playerIdList[0]).Kill(abductVictim);

                            var sender = CustomRpcSender.Create("PenguinMurder");
                            {
                                sender.AutoStartRpc(abductVictim.NetTransform.NetId, (byte)RpcCalls.SnapTo);
                                {
                                    NetHelpers.WriteVector2(Utils.GetPlayerById(playerIdList[0]).transform.position, sender.stream);
                                    sender.Write(abductVictim.NetTransform.lastSequenceId);
                                }
                                sender.EndRpc();
                                sender.AutoStartRpc(Utils.GetPlayerById(playerIdList[0]).NetId, (byte)RpcCalls.MurderPlayer);
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
                // はしごの上にいるプレイヤーにはSnapToRPCが効かずホストだけ挙動が変わるため，一律でテレポートを行わない
                else if (!AbductVictim.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
                {
                    var position = Utils.GetPlayerById(playerIdList[0]).transform.position;
                    if (Utils.GetPlayerById(playerIdList[0]).PlayerId != 0)
                    {
                        RandomSpawn.TP(AbductVictim.NetTransform, position);
                    }
                    else
                    {
                        _ = new LateTask(() =>
                        {
                            if (AbductVictim != null)
                                RandomSpawn.TP(AbductVictim.NetTransform, position);
                        }
                        , 0.25f, "");
                    }
                }
            }
            else if (AbductTimer <= 100f)
            {
                AbductTimer = 255f;
                Utils.GetPlayerById(playerIdList[0]).RpcResetAbilityCooldown();
            }
        }
    }
}
