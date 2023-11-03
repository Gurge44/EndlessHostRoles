using AmongUs.GameOptions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE;

public class PlayerState
{
    readonly byte PlayerId;
    public CustomRoles MainRole;
    public List<CustomRoles> SubRoles;
    public CountTypes countTypes;
    public bool IsDead { get; set; }
#pragma warning disable IDE1006 // Naming Styles
    public DeathReason deathReason { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    public TaskState taskState;
    public bool IsBlackOut { get; set; }
    public (DateTime, byte) RealKiller;
    public PlainShipRoom LastRoom;
    public Dictionary<byte, string> TargetColorData;
    public PlayerState(byte playerId)
    {
        MainRole = CustomRoles.NotAssigned;
        SubRoles = new();
        countTypes = CountTypes.OutOfGame;
        PlayerId = playerId;
        IsDead = false;
        deathReason = DeathReason.etc;
        taskState = new();
        IsBlackOut = false;
        RealKiller = (DateTime.MinValue, byte.MaxValue);
        LastRoom = null;
        TargetColorData = new();
    }
    public CustomRoles GetCustomRole()
    {
        var RoleInfo = Utils.GetPlayerInfoById(PlayerId);
        return RoleInfo.Role == null
            ? MainRole
            : RoleInfo.Role.Role switch
            {
                RoleTypes.Crewmate => CustomRoles.Crewmate,
                RoleTypes.Engineer => CustomRoles.Engineer,
                RoleTypes.Scientist => CustomRoles.Scientist,
                RoleTypes.GuardianAngel => CustomRoles.GuardianAngel,
                RoleTypes.Impostor => CustomRoles.Impostor,
                RoleTypes.Shapeshifter => CustomRoles.Shapeshifter,
                _ => CustomRoles.Crewmate,
            };
    }
    public void SetMainRole(CustomRoles role)
    {
        MainRole = role;
        countTypes = role.GetCountTypes();
        switch (role)
        {
            case CustomRoles.DarkHide:
                countTypes = !DarkHide.SnatchesWin.GetBool() ? CountTypes.DarkHide : CountTypes.Crew;
                break;
            case CustomRoles.Arsonist:
                countTypes = Options.ArsonistKeepsGameGoing.GetBool() ? CountTypes.Arsonist : CountTypes.Crew;
                break;
        }
    }
    public void SetSubRole(CustomRoles role, bool AllReplace = false)
    {
        if (role == CustomRoles.Cleansed)
            AllReplace = true;
        if (AllReplace)
            SubRoles.ToArray().Do(role => SubRoles.Remove(role));

        if (!SubRoles.Contains(role))
            SubRoles.Add(role);

        switch (role)
        {
            case CustomRoles.Madmate:
                countTypes = Options.MadmateCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Impostor,
                    2 => CountTypes.Crew,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Infected);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Soulless);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Admired);
                break;
            case CustomRoles.Charmed:
                countTypes = Succubus.CharmedCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Succubus,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Infected);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Soulless);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Admired);
                break;
            case CustomRoles.LastImpostor:
                SubRoles.Remove(CustomRoles.Mare);
                break;
            case CustomRoles.Recruit:
                countTypes = Jackal.SidekickCountMode.GetInt() switch
                {
                    0 => CountTypes.Jackal,
                    1 => CountTypes.OutOfGame,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Infected);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Soulless);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Admired);
                break;
            case CustomRoles.Infected:
                countTypes = CountTypes.Infectious;
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Soulless);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Admired);
                break;
            case CustomRoles.Contagious:
                countTypes = Virus.ContagiousCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Virus,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Infected);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Soulless);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Admired);
                break;
            case CustomRoles.Rogue:
                countTypes = CountTypes.Rogue;
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Infected);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Soulless);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Admired);
                break;
            case CustomRoles.Admired:
                countTypes = CountTypes.Crew;
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Infected);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Soulless);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Rogue);
                break;
            case CustomRoles.Soulless:
                countTypes = CountTypes.OutOfGame;
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Infected);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Admired);
                break;
        }
    }
    public void RemoveSubRole(CustomRoles role)
    {
        if (SubRoles.Contains(role))
            SubRoles.Remove(role);
    }

    public void SetDead()
    {
        IsDead = true;
        if (AmongUsClient.Instance.AmHost)
        {
            RPC.SendDeathReason(PlayerId, deathReason);
        }
    }
    public bool IsSuicide() { return deathReason == DeathReason.Suicide; }
    public TaskState GetTaskState() { return taskState; }
    public void InitTask(PlayerControl player)
    {
        taskState.Init(player);
    }
    public void UpdateTask(PlayerControl player)
    {
        taskState.Update(player);
    }
    public enum DeathReason
    {
        Kill,
        Vote,
        Suicide,
        Spell,
        Curse,
        Hex,
        FollowingSuicide,
        Bite,
        Poison,
        Bombed,
        Misfire,
        Torched,
        Sniped,
        Revenge,
        Execution,
        Disconnected,
        Fall,

        // TOHE
        Gambled,
        Eaten,
        Sacrifice,
        Quantization,
        Overtired,
        Ashamed,
        PissedOff,
        Dismembered,
        LossOfHead,
        Trialed,
        Infected,
        Jinx,
        Demolished,
        Hack,

        etc = -1,
    }
    public byte GetRealKiller()
        => IsDead && RealKiller.Item1 != DateTime.MinValue ? RealKiller.Item2 : byte.MaxValue;
    public int GetKillCount(bool ExcludeSelfKill = false)
    {
        return Main.PlayerStates.Values.Where(state => !(ExcludeSelfKill && state.PlayerId == PlayerId) && state.GetRealKiller() == PlayerId).Count();
    }
}
public class TaskState
{
    public static int InitialTotalTasks;
    public int AllTasksCount;
    public int CompletedTasksCount;
    public bool hasTasks;
    public int RemainingTasksCount => AllTasksCount - CompletedTasksCount;
    public bool DoExpose => RemainingTasksCount <= Options.SnitchExposeTaskLeft && hasTasks;
    public bool IsTaskFinished => RemainingTasksCount <= 0 && hasTasks;
    public TaskState()
    {
        AllTasksCount = -1;
        CompletedTasksCount = 0;
        hasTasks = false;
    }

    public void Init(PlayerControl player)
    {
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags().RemoveHtmlTags()}: InitTask", "TaskState.Init");
        if (player == null || player.Data == null || player.Data.Tasks == null) return;
        if (!Utils.HasTasks(player.Data, false))
        {
            AllTasksCount = 0;
            return;
        }
        hasTasks = true;
        AllTasksCount = player.Data.Tasks.Count;
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags().RemoveHtmlTags()}: TaskCounts = {CompletedTasksCount}/{AllTasksCount}", "TaskState.Init");
    }
    public void Update(PlayerControl player)
    {
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags().RemoveHtmlTags()}: UpdateTask", "TaskState.Update");
        GameData.Instance.RecomputeTaskCounts();
        Logger.Info($"TotalTaskCounts = {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}", "TaskState.Update");

        //初期化出来ていなかったら初期化
        if (AllTasksCount == -1) Init(player);

        if (!hasTasks) return;

        if (AmongUsClient.Instance.AmHost)
        {
            //FIXME:SpeedBooster class transplant
            if (player.IsAlive()
            && player.Is(CustomRoles.SpeedBooster)
            && ((CompletedTasksCount + 1) <= Options.SpeedBoosterTimes.GetInt()))
            {
                Logger.Info("增速者触发加速:" + player.GetNameWithRole().RemoveHtmlTags(), "SpeedBooster");
                Main.AllPlayerSpeed[player.PlayerId] += Options.SpeedBoosterUpSpeed.GetFloat();
                if (Main.AllPlayerSpeed[player.PlayerId] > 3) player.Notify(Translator.GetString("SpeedBoosterSpeedLimit"));
                else player.Notify(string.Format(Translator.GetString("SpeedBoosterTaskDone"), Main.AllPlayerSpeed[player.PlayerId].ToString("0.0#####")));
            }



            /*
            //叛徒修理搞破坏
            if (player.IsAlive()
            && player.Is(CustomRoles.SabotageMaster)
            && player.Is(CustomRoles.Madmate))
            {
                List<SystemTypes> SysList = new();
                foreach (SystemTypes sys in Enum.GetValues(typeof(SystemTypes)))
                    if (Utils.IsActive(sys)) SysList.Add(sys);

                if (SysList.Any())
                {
                    var SbSys = SysList[IRandom.Instance.Next(0, SysList.Count)];

                    MessageWriter SabotageFixWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.RepairSystem, SendOption.Reliable, player.GetClientId());
                    SabotageFixWriter.Write((byte)SbSys);
                    MessageExtensions.WriteNetObject(SabotageFixWriter, player);
                    AmongUsClient.Instance.FinishRpcImmediately(SabotageFixWriter);

                    foreach (var target in Main.AllPlayerControls)
                    {
                        if (target == player || target.Data.Disconnected) continue;
                        SabotageFixWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.RepairSystem, SendOption.Reliable, target.GetClientId());
                        SabotageFixWriter.Write((byte)SbSys);
                        MessageExtensions.WriteNetObject(SabotageFixWriter, target);
                        AmongUsClient.Instance.FinishRpcImmediately(SabotageFixWriter);
                    }
                    Logger.Info("叛徒修理工造成破坏:" + player.cosmetics.nameText.text, "SabotageMaster");
                }
            }
            */

            //传送师完成任务
            if (player.IsAlive()
            && player.Is(CustomRoles.Transporter)
            && ((CompletedTasksCount + 1) <= Options.TransporterTeleportMax.GetInt()))
            {
                Logger.Info("传送师触发传送:" + player.GetNameWithRole().RemoveHtmlTags(), "Transporter");
                var rd = IRandom.Instance;
                List<PlayerControl> AllAlivePlayer = new();
                AllAlivePlayer.AddRange(Main.AllAlivePlayerControls.Where(x => !Pelican.IsEaten(x.PlayerId) && !x.inVent && !x.onLadder));
                if (AllAlivePlayer.Count >= 2)
                {
                    var tar1 = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Count)];
                    AllAlivePlayer.Remove(tar1);
                    var tar2 = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Count)];
                    var pos = tar1.GetTruePosition();
                    Utils.TP(tar1.NetTransform, tar2.GetTruePosition());
                    Utils.TP(tar2.NetTransform, pos);
                    tar1.RPCPlayCustomSound("Teleport");
                    tar2.RPCPlayCustomSound("Teleport");
                    tar1.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Transporter), string.Format(Translator.GetString("TeleportedByTransporter"), tar2.GetRealName())));
                    tar2.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Transporter), string.Format(Translator.GetString("TeleportedByTransporter"), tar1.GetRealName())));
                }
                else if (player.Is(CustomRoles.Transporter))
                {
                    player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), string.Format(Translator.GetString("ErrorTeleport"), player.GetRealName())));
                }
            }
            if (player.Is(CustomRoles.Unlucky) && player.IsAlive())
            {
                var Ue = IRandom.Instance;
                if (Ue.Next(0, 100) < Options.UnluckyTaskSuicideChance.GetInt())
                {
                    player.Kill(player);
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Suicide;

                }
            }
            // Ability Use Gain with this task completed
            if (player.IsAlive())
            {
                switch (player.GetCustomRole())
                {
                    case CustomRoles.Divinator:
                        Divinator.CheckLimit[player.PlayerId] += Divinator.AbilityUseGainWithEachTaskCompleted.GetFloat();
                        Divinator.SendRPC(player.PlayerId, false);
                        break;
                    case CustomRoles.Veteran:
                        Main.VeteranNumOfUsed[player.PlayerId] += Options.VeteranAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Grenadier:
                        Main.GrenadierNumOfUsed[player.PlayerId] += Options.GrenadierAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Lighter:
                        Main.LighterNumOfUsed[player.PlayerId] += Options.LighterAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.SecurityGuard:
                        Main.SecurityGuardNumOfUsed[player.PlayerId] += Options.SecurityGuardAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Ventguard:
                        Main.VentguardNumberOfAbilityUses += Options.VentguardAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.DovesOfNeace:
                        Main.DovesOfNeaceNumOfUsed[player.PlayerId] += Options.DovesOfNeaceAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.TimeMaster:
                        Main.TimeMasterNumOfUsed[player.PlayerId] += Options.TimeMasterAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Mediumshiper:
                        Mediumshiper.ContactLimit[player.PlayerId] += Mediumshiper.MediumAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Mediumshiper.SendRPC(player.PlayerId, false);
                        break;
                    case CustomRoles.ParityCop:
                        ParityCop.MaxCheckLimit[player.PlayerId] += ParityCop.ParityAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Oracle:
                        Oracle.CheckLimit[player.PlayerId] += Oracle.OracleAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Oracle.SendRPC(player.PlayerId, false);
                        break;
                    case CustomRoles.SabotageMaster:
                        SabotageMaster.UsedSkillCount -= SabotageMaster.SMAbilityUseGainWithEachTaskCompleted.GetFloat();
                        SabotageMaster.SendRPC(SabotageMaster.UsedSkillCount);
                        break;
                    case CustomRoles.Tracker:
                        Tracker.TrackLimit[player.PlayerId] += Tracker.TrackerAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Bloodhound:
                        Bloodhound.UseLimit[player.PlayerId] += Bloodhound.BloodhoundAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Bloodhound.SendRPCPlus(player.PlayerId, false);
                        break;
                    case CustomRoles.Chameleon:
                        Chameleon.UseLimit[player.PlayerId] += Chameleon.ChameleonAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Chameleon.SendRPCPlus(player.PlayerId, false);
                        break;
                    case CustomRoles.NiceSwapper:
                        NiceSwapper.NiceSwappermax[player.PlayerId] += NiceSwapper.NiceSwapperAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Doormaster:
                        Doormaster.UseLimit[player.PlayerId] += Doormaster.DoormasterAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Doormaster.SendRPC(player.PlayerId, false);
                        break;
                    case CustomRoles.Ricochet:
                        Ricochet.UseLimit[player.PlayerId] += Ricochet.RicochetAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Ricochet.SendRPC(player.PlayerId, false);
                        break;
                    case CustomRoles.Tether:
                        Tether.UseLimit[player.PlayerId] += Tether.TetherAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Tether.SendRPC(player.PlayerId, false);
                        break;
                    case CustomRoles.Spy:
                        Spy.UseLimit[player.PlayerId] += Spy.SpyAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Spy.SendAbilityRPC(player.PlayerId);
                        break;
                    case CustomRoles.NiceHacker:
                        if (!player.IsModClient()) NiceHacker.UseLimit[player.PlayerId] += NiceHacker.NiceHackerAbilityUseGainWithEachTaskCompleted.GetFloat();
                        else NiceHacker.UseLimitSeconds[player.PlayerId] += NiceHacker.NiceHackerAbilityUseGainWithEachTaskCompleted.GetInt() * NiceHacker.ModdedClientAbilityUseSecondsMultiplier.GetInt();
                        NiceHacker.SendRPC(player.PlayerId, NiceHacker.UseLimitSeconds[player.PlayerId]);
                        break;
                    case CustomRoles.CameraMan:
                        CameraMan.UseLimit[player.PlayerId] += CameraMan.CameraManAbilityUseGainWithEachTaskCompleted.GetFloat();
                        CameraMan.SendRPC(player.PlayerId, false);
                        break;
                }
            }
            if (player.Is(CustomRoles.Express) && player.IsAlive())
            {
                if (!Main.ExpressSpeedUp.ContainsKey(player.PlayerId)) Main.ExpressSpeedNormal = Main.AllPlayerSpeed[player.PlayerId];
                Main.AllPlayerSpeed[player.PlayerId] = Options.ExpressSpeed.GetFloat();
                Main.ExpressSpeedUp.Remove(player.PlayerId);
                Main.ExpressSpeedUp.TryAdd(player.PlayerId, Utils.GetTimeStamp());
                player.SyncSettings();
            }
            if (player.Is(CustomRoles.Alchemist) && player.IsAlive()) Alchemist.OnTaskComplete(player);

            if (player.Is(CustomRoles.Ghoul) && (CompletedTasksCount + 1) >= AllTasksCount && player.IsAlive())
                _ = new LateTask(() =>
                {
                    player.Kill(player);
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                }, 0.2f, "Ghoul Suicide");
            if (player.Is(CustomRoles.Ghoul) && (CompletedTasksCount + 1) >= AllTasksCount && !player.IsAlive())
            {
                foreach (var pc in Main.AllPlayerControls.Where(pc => !pc.Is(CustomRoles.Pestilence)).Where(pc => Main.KillGhoul.Contains(pc.PlayerId) && player.PlayerId != pc.PlayerId && pc.IsAlive()))
                {
                    player.Kill(pc);
                    Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                }
            }

            foreach (var taskmanager in Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoles.TaskManager)))
            {
                Utils.NotifyRoles(SpecifySeer: taskmanager);
            }

            //工作狂做完了
            if (player.Is(CustomRoles.Workaholic) && (CompletedTasksCount + 1) >= AllTasksCount
                    && !(Options.WorkaholicCannotWinAtDeath.GetBool() && !player.IsAlive()))
            {
                Logger.Info("工作狂任务做完了", "Workaholic");
                RPC.PlaySoundRPC(player.PlayerId, Sounds.KillSound);
                foreach (var pc in Main.AllAlivePlayerControls.Where(pc => pc.PlayerId != player.PlayerId))
                {
                    Main.PlayerStates[pc.PlayerId].deathReason = pc.PlayerId == player.PlayerId ?
                                            PlayerState.DeathReason.Overtired : PlayerState.DeathReason.Ashamed;
                    pc.Kill(pc);
                    Main.PlayerStates[pc.PlayerId].SetDead();
                    pc.SetRealKiller(player);
                }

                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Workaholic); //爆破で勝利した人も勝利させる
                CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
            }

            if (player.Is(CustomRoles.Speedrunner) && (CompletedTasksCount + 1) >= AllTasksCount && player.IsAlive())
            {
                Logger.Info("Speedrunner finished tasks", "Speedrunner");
                player.RPCPlayCustomSound("Congrats");
                GameData.Instance.CompletedTasks = GameData.Instance.TotalTasks;
            }

            Merchant.OnTaskFinished(player);
            if (player.Is(CustomRoles.Ignitor) && player.IsAlive()) Ignitor.OnCompleteTask(player);
            if (player.Is(CustomRoles.Ignitor) && (CompletedTasksCount + 1) >= AllTasksCount && player.IsAlive()) Ignitor.OnTasksFinished(player);

            //船鬼要抽奖啦
            if (player.Is(CustomRoles.Crewpostor))
            {
                if (Main.CrewpostorTasksDone.ContainsKey(player.PlayerId)) Main.CrewpostorTasksDone[player.PlayerId]++;
                else Main.CrewpostorTasksDone[player.PlayerId] = 0;
                RPC.CrewpostorTasksSendRPC(player.PlayerId, Main.CrewpostorTasksDone[player.PlayerId]);

                List<PlayerControl> list = Main.AllAlivePlayerControls.Where(x => x.PlayerId != player.PlayerId && (Options.CrewpostorCanKillAllies.GetBool() || !x.GetCustomRole().IsImpostorTeam())).ToList();
                if (!list.Any())
                {
                    Logger.Info($"No target to kill", "Crewpostor");
                }
                else if (Main.CrewpostorTasksDone[player.PlayerId] % Options.CrewpostorKillAfterTask.GetInt() != 0 && Main.CrewpostorTasksDone[player.PlayerId] != 0)
                {
                    Logger.Info($"Crewpostor task done but kill skipped, {Main.CrewpostorTasksDone[player.PlayerId]} tasks completed, but it kills after {Options.CrewpostorKillAfterTask.GetInt()} tasks", "Crewpostor");
                }
                else
                {
                    list = list.OrderBy(x => Vector2.Distance(player.GetTruePosition(), x.GetTruePosition())).ToList();
                    var target = list[0];
                    if (!target.Is(CustomRoles.Pestilence))
                    {

                        if (!Options.CrewpostorLungeKill.GetBool())
                        {
                            target.SetRealKiller(player);
                            target.RpcCheckAndMurder(target);
                            player.RpcGuardAndKill();
                            Logger.Info("No lunge mode kill", "Crewpostor");
                        }
                        else
                        {
                            player.SetRealKiller(target);
                            player.Kill(target);
                            player.RpcGuardAndKill();
                            Logger.Info("lunge mode kill", "Crewpostor");

                        }
                        Logger.Info($"Crewpostor completed task to kill：{player.GetNameWithRole()} => {target.GetNameWithRole()}", "Crewpostor");
                    }
                    if (target.Is(CustomRoles.Pestilence))
                    {
                        target.SetRealKiller(player);
                        target.Kill(player);
                        //player.RpcGuardAndKill();
                        Logger.Info($"Crewpostor tried to kill Pestilence：{target.GetNameWithRole()} => {player.GetNameWithRole().RemoveHtmlTags()}", "Pestilence Reflect");
                    }
                }
            }

        }

        //クリアしてたらカウントしない
        if (CompletedTasksCount >= AllTasksCount) return;

        CompletedTasksCount++;

        //調整後のタスク量までしか表示しない
        CompletedTasksCount = Math.Min(AllTasksCount, CompletedTasksCount);
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: TaskCounts = {CompletedTasksCount}/{AllTasksCount}", "TaskState.Update");

    }
}
public class PlayerVersion
{
    public readonly Version version;
    public readonly string tag;
    public readonly string forkId;
#pragma warning disable CA1041 // Provide ObsoleteAttribute message
    [Obsolete] public PlayerVersion(string ver, string tag_str) : this(Version.Parse(ver), tag_str, string.Empty) { }
    [Obsolete] public PlayerVersion(Version ver, string tag_str) : this(ver, tag_str, string.Empty) { }
#pragma warning restore CA1041 // Provide ObsoleteAttribute message
    public PlayerVersion(string ver, string tag_str, string forkId) : this(Version.Parse(ver), tag_str, forkId) { }
    public PlayerVersion(Version ver, string tag_str, string forkId)
    {
        version = ver;
        tag = tag_str;
        this.forkId = forkId;
    }
    public bool IsEqual(PlayerVersion pv)
    {
        return pv.version == version && pv.tag == tag;
    }
}
public static class GameStates
{
    public static bool InGame;
    public static bool AlreadyDied;
    public static bool IsModHost => PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(x => x.PlayerId == 0 && x.IsModClient());
    public static bool IsLobby => AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Joined;
    public static bool IsInGame => InGame;
    public static bool IsEnded => AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Ended;
    public static bool IsNotJoined => AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.NotJoined;
    public static bool IsOnlineGame => AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame;
    public static bool IsLocalGame => AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame;
    public static bool IsFreePlay => AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;
    public static bool IsInTask => InGame && !MeetingHud.Instance;
    public static bool IsMeeting => InGame && MeetingHud.Instance;
    public static bool IsVoting => IsMeeting && MeetingHud.Instance.state is MeetingHud.VoteStates.Voted or MeetingHud.VoteStates.NotVoted;
    public static bool IsCountDown => GameStartManager.InstanceExists && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown;
    /**********TOP ZOOM.cs***********/
    public static bool IsShip => ShipStatus.Instance != null;
    public static bool IsCanMove => PlayerControl.LocalPlayer?.CanMove is true;
    public static bool IsDead => PlayerControl.LocalPlayer?.Data?.IsDead is true;
}
public static class MeetingStates
{
    public static DeadBody[] DeadBodies;
    private static GameData.PlayerInfo reportTarget;
    public static bool IsEmergencyMeeting => ReportTarget == null;
    public static bool IsExistDeadBody => DeadBodies.Length > 0;

    public static GameData.PlayerInfo ReportTarget { get => reportTarget; set => reportTarget = value; }

    public static bool MeetingCalled;
    public static bool FirstMeeting = true;
}