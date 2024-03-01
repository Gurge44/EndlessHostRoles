using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TOHE.Modules;
using UnityEngine;

namespace TOHE.Roles.Impostor
{
    internal class Crewpostor : RoleBase
    {
        public static Dictionary<byte, int> TasksDone = [];

        public static bool On;
        public override bool IsEnable => On;

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = 0f;
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }

        public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
        {
            if (!TasksDone.TryAdd(player.PlayerId, 0)) TasksDone[player.PlayerId]++;

            SendRPC(player.PlayerId, TasksDone[player.PlayerId]);

            PlayerControl[] list = Main.AllAlivePlayerControls.Where(x => x.PlayerId != player.PlayerId && (Options.CrewpostorCanKillAllies.GetBool() || !x.GetCustomRole().IsImpostorTeam())).ToArray();
            if (list.Length == 0)
            {
                Logger.Info("No target to kill", "Crewpostor");
            }
            else if (TasksDone[player.PlayerId] % Options.CrewpostorKillAfterTask.GetInt() != 0 && TasksDone[player.PlayerId] != 0)
            {
                Logger.Info($"Crewpostor task done but kill skipped, {TasksDone[player.PlayerId]} tasks completed, but it kills after {Options.CrewpostorKillAfterTask.GetInt()} tasks", "Crewpostor");
            }
            else
            {
                list = [.. list.OrderBy(x => Vector2.Distance(player.Pos(), x.Pos()))];
                var target = list[0];
                if (!target.Is(CustomRoles.Pestilence))
                {
                    if (!Options.CrewpostorLungeKill.GetBool())
                    {
                        target.SetRealKiller(player);
                        if (player.RpcCheckAndMurder(target, true))
                        {
                            target.Suicide(PlayerState.DeathReason.Kill, player);
                            player.RpcGuardAndKill();
                        }

                        Logger.Info("No lunge mode kill", "Crewpostor");
                    }
                    else
                    {
                        player.SetRealKiller(target);
                        player.RpcCheckAndMurder(target);
                        //player.RpcGuardAndKill();
                        Logger.Info("lunge mode kill", "Crewpostor");
                    }

                    Logger.Info($"Crewpostor completed task to kill：{player.GetNameWithRole()} => {target.GetNameWithRole()}", "Crewpostor");
                }
                else
                {
                    target.SetRealKiller(player);
                    target.Kill(player);
                    //player.RpcGuardAndKill();
                    Logger.Info($"Crewpostor tried to kill Pestilence：{target.GetNameWithRole()} => {player.GetNameWithRole().RemoveHtmlTags()}", "Pestilence Reflect");
                }
            }
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return false;
        }

        public static void SendRPC(byte cpID, int tasksDone)
        {
            if (PlayerControl.LocalPlayer.PlayerId == cpID)
            {
                if (!TasksDone.TryAdd(cpID, 0))
                    TasksDone[cpID] = tasksDone;
            }
            else
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCPTasksDone, SendOption.Reliable);
                writer.Write(cpID);
                writer.Write(tasksDone);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }

        public static void RecieveRPC(MessageReader reader)
        {
            byte PlayerId = reader.ReadByte();
            int tasksDone = reader.ReadInt32();
            if (!TasksDone.TryAdd(PlayerId, 0))
                TasksDone[PlayerId] = tasksDone;
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            TasksDone[playerId] = 0;
        }
    }
}