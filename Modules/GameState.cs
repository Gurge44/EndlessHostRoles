using AmongUs.GameOptions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE;

public class PlayerState(byte playerId)
{
    readonly byte PlayerId = playerId;
    public CustomRoles MainRole = CustomRoles.NotAssigned;
    public List<CustomRoles> SubRoles = [];
    public CountTypes countTypes = CountTypes.OutOfGame;
    public bool IsDead { get; set; }
#pragma warning disable IDE1006 // Naming Styles
    public DeathReason deathReason { get; set; } = DeathReason.etc;
#pragma warning restore IDE1006 // Naming Styles
    public TaskState taskState = new();
    public bool IsBlackOut { get; set; }
    public (DateTime TIMESTAMP, byte ID) RealKiller = (DateTime.MinValue, byte.MaxValue);
    public PlainShipRoom LastRoom;
    public Dictionary<byte, string> TargetColorData = [];

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
        countTypes = role switch
        {
            CustomRoles.DarkHide => !DarkHide.SnatchesWin.GetBool() ? CountTypes.DarkHide : CountTypes.Crew,
            CustomRoles.Arsonist => Options.ArsonistKeepsGameGoing.GetBool() ? CountTypes.Arsonist : CountTypes.Crew,
            _ => role.GetCountTypes(),
        };
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
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
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
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                break;
            case CustomRoles.Undead:
                countTypes = Necromancer.UndeadCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Necromancer,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Charmed);
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
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
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
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                break;
            case CustomRoles.Rogue:
                countTypes = CountTypes.Rogue;
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
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
    public bool IsSuicide => deathReason == DeathReason.Suicide;
    public TaskState TaskState => taskState;
    public void InitTask(PlayerControl player) => taskState.Init(player);
    public void UpdateTask(PlayerControl player) => taskState.Update(player);
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
        YinYanged,
        Hack,
        Kamikazed,

        etc = -1,
    }
    public byte GetRealKiller() => IsDead && RealKiller.TIMESTAMP != DateTime.MinValue ? RealKiller.ID : byte.MaxValue;
    public int GetKillCount(bool ExcludeSelfKill = false) => Main.PlayerStates.Values.Where(state => !(ExcludeSelfKill && state.PlayerId == PlayerId) && state.GetRealKiller() == PlayerId).ToArray().Length;
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
            bool alive = player.IsAlive();
            if (alive
            && player.Is(CustomRoles.SpeedBooster)
            && ((CompletedTasksCount + 1) <= Options.SpeedBoosterTimes.GetInt()))
            {
                Logger.Info("增速者触发加速:" + player.GetNameWithRole().RemoveHtmlTags(), "SpeedBooster");
                Main.AllPlayerSpeed[player.PlayerId] += Options.SpeedBoosterUpSpeed.GetFloat();
                if (Main.AllPlayerSpeed[player.PlayerId] > 3) player.Notify(Translator.GetString("SpeedBoosterSpeedLimit"));
                else player.Notify(string.Format(Translator.GetString("SpeedBoosterTaskDone"), Main.AllPlayerSpeed[player.PlayerId].ToString("0.0#####")));
            }

            if (alive
            && player.Is(CustomRoles.Transporter)
            && ((CompletedTasksCount + 1) <= Options.TransporterTeleportMax.GetInt()))
            {
                Logger.Info("传送师触发传送:" + player.GetNameWithRole().RemoveHtmlTags(), "Transporter");
                var rd = IRandom.Instance;
                List<PlayerControl> AllAlivePlayer = Main.AllAlivePlayerControls.Where(x => !Pelican.IsEaten(x.PlayerId) && !x.inVent && !x.onLadder).ToList();
                if (AllAlivePlayer.Count >= 2)
                {
                    var tar1 = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Count)];
                    AllAlivePlayer.Remove(tar1);
                    var tar2 = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Count)];
                    var pos = tar1.Pos();
                    tar1.TP(tar2);
                    tar2.TP(pos);
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
            if (player.Is(CustomRoles.Unlucky) && alive)
            {
                var Ue = IRandom.Instance;
                if (Ue.Next(0, 100) < Options.UnluckyTaskSuicideChance.GetInt())
                {
                    player.Suicide();

                }
            }

            if (alive && Mastermind.ManipulatedPlayers.ContainsKey(player.PlayerId))
            {
                Mastermind.OnManipulatedPlayerTaskComplete(player);
            }

            // Ability Use Gain with this task completed
            if (alive)
            {
                switch (player.GetCustomRole())
                {
                    case CustomRoles.Divinator:
                        Divinator.CheckLimit[player.PlayerId] += Divinator.AbilityUseGainWithEachTaskCompleted.GetFloat();
                        Divinator.SendRPC(player.PlayerId);
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
                        Mediumshiper.SendRPC(player.PlayerId);
                        break;
                    case CustomRoles.ParityCop:
                        ParityCop.MaxCheckLimit[player.PlayerId] += ParityCop.ParityAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Oracle:
                        Oracle.CheckLimit[player.PlayerId] += Oracle.OracleAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Oracle.SendRPC(player.PlayerId);
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
                        Bloodhound.SendRPCPlus(player.PlayerId);
                        break;
                    case CustomRoles.Chameleon:
                        Chameleon.UseLimit[player.PlayerId] += Chameleon.ChameleonAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Chameleon.SendRPCPlus(player.PlayerId);
                        break;
                    case CustomRoles.NiceSwapper:
                        NiceSwapper.NiceSwappermax[player.PlayerId] += NiceSwapper.NiceSwapperAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Doormaster:
                        Doormaster.UseLimit[player.PlayerId] += Doormaster.DoormasterAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Doormaster.SendRPC(player.PlayerId);
                        break;
                    case CustomRoles.Ricochet:
                        Ricochet.UseLimit[player.PlayerId] += Ricochet.RicochetAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Ricochet.SendRPC(player.PlayerId);
                        break;
                    case CustomRoles.Tether:
                        Tether.UseLimit[player.PlayerId] += Tether.TetherAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Tether.SendRPC(player.PlayerId);
                        break;
                    case CustomRoles.Spy:
                        Spy.UseLimit[player.PlayerId] += Spy.SpyAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Spy.SendRPC(2, id: player.PlayerId);
                        break;
                    case CustomRoles.NiceHacker:
                        if (!player.IsModClient() && NiceHacker.UseLimit.ContainsKey(player.PlayerId)) NiceHacker.UseLimit[player.PlayerId] += NiceHacker.NiceHackerAbilityUseGainWithEachTaskCompleted.GetFloat();
                        else if (NiceHacker.UseLimitSeconds.ContainsKey(player.PlayerId)) NiceHacker.UseLimitSeconds[player.PlayerId] += NiceHacker.NiceHackerAbilityUseGainWithEachTaskCompleted.GetInt() * NiceHacker.ModdedClientAbilityUseSecondsMultiplier.GetInt();
                        if (NiceHacker.UseLimitSeconds.ContainsKey(player.PlayerId)) NiceHacker.SendRPC(player.PlayerId, NiceHacker.UseLimitSeconds[player.PlayerId]);
                        break;
                    case CustomRoles.CameraMan:
                        CameraMan.UseLimit[player.PlayerId] += CameraMan.CameraManAbilityUseGainWithEachTaskCompleted.GetFloat();
                        CameraMan.SendRPC(player.PlayerId);
                        break;
                    case CustomRoles.Drainer:
                        Drainer.DrainLimit += Drainer.DrainerAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Drainer.SendRPC();
                        break;
                    case CustomRoles.Druid:
                        Druid.UseLimit[player.PlayerId] += Druid.DruidAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Druid.SendRPCSyncAbilityUse(player.PlayerId);
                        break;
                    case CustomRoles.Judge:
                        Judge.TrialLimit[player.PlayerId] += Judge.JudgeAbilityUseGainWithEachTaskCompleted.GetFloat();
                        break;
                    case CustomRoles.Perceiver:
                        Perceiver.UseLimit[player.PlayerId] += Perceiver.PerceiverAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Perceiver.SendRPC(player.PlayerId);
                        break;
                    case CustomRoles.Convener:
                        Convener.UseLimit[player.PlayerId] += Convener.ConvenerAbilityUseGainWithEachTaskCompleted.GetFloat();
                        Convener.SendRPC(player.PlayerId);
                        break;
                }

                switch (player.GetCustomRole())
                {
                    case CustomRoles.Express:
                        if (!Main.ExpressSpeedUp.ContainsKey(player.PlayerId)) Main.ExpressSpeedNormal = Main.AllPlayerSpeed[player.PlayerId];
                        Main.AllPlayerSpeed[player.PlayerId] = Options.ExpressSpeed.GetFloat();
                        Main.ExpressSpeedUp.Remove(player.PlayerId);
                        Main.ExpressSpeedUp.TryAdd(player.PlayerId, Utils.GetTimeStamp());
                        player.MarkDirtySettings();
                        break;
                    case CustomRoles.Alchemist:
                        Alchemist.OnTaskComplete(player);
                        break;
                    case CustomRoles.Transmitter:
                        Transmitter.OnTaskComplete(player);
                        break;
                    case CustomRoles.Autocrat:
                        Autocrat.OnTaskComplete(player);
                        break;
                    case CustomRoles.Speedrunner:
                        var completedTasks = CompletedTasksCount + 1;
                        int remainingTasks = AllTasksCount - completedTasks;
                        if (completedTasks >= AllTasksCount)
                        {
                            Logger.Info("Speedrunner finished tasks", "Speedrunner");
                            player.RPCPlayCustomSound("Congrats");
                            GameData.Instance.CompletedTasks = GameData.Instance.TotalTasks;
                        }
                        else if (completedTasks >= Options.SpeedrunnerNotifyAtXTasksLeft.GetInt() && Options.SpeedrunnerNotifyKillers.GetBool())
                        {
                            string speedrunnerName = player.GetRealName().RemoveHtmlTags();
                            string notifyString = Translator.GetString("SpeedrunnerHasXTasksLeft");
                            foreach (var pc in Main.AllAlivePlayerControls.Where(pc => !pc.Is(Team.Crewmate)).ToArray())
                            {
                                pc.Notify(string.Format(notifyString, speedrunnerName, remainingTasks));
                            }
                        }
                        break;
                    case CustomRoles.Electric:
                        Electric.OnTaskComplete(player);
                        break;
                    case CustomRoles.Insight:
                        var list2 = Main.AllPlayerControls.Where(x => !Main.InsightKnownRolesOfPlayerIds.Contains(x.PlayerId) && !x.Is(CountTypes.OutOfGame) && !x.Is(CustomRoles.Insight) && !x.Is(CustomRoles.GM) && !x.Is(CustomRoles.NotAssigned))?.ToList();
                        if (list2 != null && list2.Count != 0)
                        {
                            var target = list2[IRandom.Instance.Next(0, list2.Count)];
                            Main.InsightKnownRolesOfPlayerIds.Add(target.PlayerId);
                            player.Notify(string.Format(Utils.ColorString(target.GetRoleColor(), Translator.GetString("InsightNotify")), target.GetDisplayRoleName(pure: true)));
                        }
                        break;
                    case CustomRoles.Ignitor:
                        Ignitor.OnCompleteTask(player);
                        if ((CompletedTasksCount + 1) >= AllTasksCount) Ignitor.OnTasksFinished(player);
                        break;
                    case CustomRoles.Merchant:
                        Merchant.OnTaskFinished(player);
                        break;
                    case CustomRoles.Crewpostor:
                        {
                            if (Main.CrewpostorTasksDone.ContainsKey(player.PlayerId)) Main.CrewpostorTasksDone[player.PlayerId]++;
                            else Main.CrewpostorTasksDone[player.PlayerId] = 0;
                            RPC.CrewpostorTasksSendRPC(player.PlayerId, Main.CrewpostorTasksDone[player.PlayerId]);

                            PlayerControl[] list = Main.AllAlivePlayerControls.Where(x => x.PlayerId != player.PlayerId && (Options.CrewpostorCanKillAllies.GetBool() || !x.GetCustomRole().IsImpostorTeam())).ToArray();
                            if (list.Length == 0 || list == null)
                            {
                                Logger.Info($"No target to kill", "Crewpostor");
                            }
                            else if (Main.CrewpostorTasksDone[player.PlayerId] % Options.CrewpostorKillAfterTask.GetInt() != 0 && Main.CrewpostorTasksDone[player.PlayerId] != 0)
                            {
                                Logger.Info($"Crewpostor task done but kill skipped, {Main.CrewpostorTasksDone[player.PlayerId]} tasks completed, but it kills after {Options.CrewpostorKillAfterTask.GetInt()} tasks", "Crewpostor");
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
                                else
                                {
                                    target.SetRealKiller(player);
                                    target.Kill(player);
                                    //player.RpcGuardAndKill();
                                    Logger.Info($"Crewpostor tried to kill Pestilence：{target.GetNameWithRole()} => {player.GetNameWithRole().RemoveHtmlTags()}", "Pestilence Reflect");
                                }
                            }
                        }
                        break;
                    case CustomRoles.Rabbit:
                        Rabbit.OnTaskComplete(player);
                        break;
                }
            }

            var addons = Main.PlayerStates[player.PlayerId].SubRoles;

            if (addons.Contains(CustomRoles.Ghoul) && (CompletedTasksCount + 1) >= AllTasksCount)
            {
                if (alive)
                {
                    _ = new LateTask(() =>
                    {
                        player.Suicide();
                    }, 0.2f, "Ghoul Suicide");
                }
                else
                {
                    foreach (var pc in Main.AllPlayerControls.Where(pc => !pc.Is(CustomRoles.Pestilence) && Main.KillGhoul.Contains(pc.PlayerId) && player.PlayerId != pc.PlayerId && pc.IsAlive()).ToArray())
                    {
                        player.Kill(pc);
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                    }
                }
            }

            if (addons.Contains(CustomRoles.Stressed))
            {
                Stressed.OnTaskComplete(player);
            }

            // Update the player's task count for Task Managers
            foreach (var taskmanager in Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoles.TaskManager)).ToArray())
            {
                Utils.NotifyRoles(SpecifySeer: taskmanager, SpecifyTarget: player);
            }

            // Workaholic Task Completion
            if (player.Is(CustomRoles.Workaholic) && (CompletedTasksCount + 1) >= AllTasksCount && !(Options.WorkaholicCannotWinAtDeath.GetBool() && !alive))
            {
                Logger.Info("Workaholic Tasks Finished", "Workaholic");
                RPC.PlaySoundRPC(player.PlayerId, Sounds.KillSound);
                foreach (var pc in Main.AllAlivePlayerControls.Where(pc => pc.PlayerId != player.PlayerId).ToArray())
                {
                    pc.Suicide(pc.PlayerId == player.PlayerId ? PlayerState.DeathReason.Overtired : PlayerState.DeathReason.Ashamed, player);
                }

                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Workaholic);
                CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
            }
        }

        if (CompletedTasksCount >= AllTasksCount) return;

        CompletedTasksCount++;

        CompletedTasksCount = Math.Min(AllTasksCount, CompletedTasksCount);
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: TaskCounts = {CompletedTasksCount}/{AllTasksCount}", "TaskState.Update");
    }
}
public class PlayerVersion(Version ver, string tag_str, string forkId)
{
    public readonly Version version = ver;
    public readonly string tag = tag_str;
    public readonly string forkId = forkId;
#pragma warning disable CA1041 // Provide ObsoleteAttribute message
    [Obsolete] public PlayerVersion(string ver, string tag_str) : this(Version.Parse(ver), tag_str, string.Empty) { }
    [Obsolete] public PlayerVersion(Version ver, string tag_str) : this(ver, tag_str, string.Empty) { }
#pragma warning restore CA1041 // Provide ObsoleteAttribute message
    public PlayerVersion(string ver, string tag_str, string forkId) : this(Version.Parse(ver), tag_str, forkId) { }

    public bool IsEqual(PlayerVersion pv)
    {
        return pv.version == version && pv.tag == tag;
    }
}
public static class GameStates
{
    public static bool InGame;
    public static bool AlreadyDied;
    public static bool IsModHost => PlayerControl.AllPlayerControls.ToArray().Any(x => x.PlayerId == 0 && x.IsModClient());
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