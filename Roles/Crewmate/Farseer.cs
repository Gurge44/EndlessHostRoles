﻿using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Roles.Crewmate
{
    public class Farseer : RoleBase
    {
        public static Dictionary<byte, (PlayerControl PLAYER, float TIMER)> FarseerTimer = [];
        public static Dictionary<(byte, byte), bool> IsRevealed = [];

        private const int Id = 9700;

        private const string FontSize = "1.6";

        public static OptionItem FarseerCooldown;
        public static OptionItem FarseerRevealTime;
        public static OptionItem Vision;
        public static OptionItem UsePet;

        private static CustomRoles[] RandomRolesForTrickster => EnumHelper.GetAllValues<CustomRoles>().Where(x => x.IsCrewmate()).ToArray();

        public static Dictionary<int, string> RandomRole = [];

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Farseer);
            FarseerCooldown = FloatOptionItem.Create(Id + 10, "FarseerRevealCooldown", new(0f, 60f, 2.5f), 15f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Farseer])
                .SetValueFormat(OptionFormat.Seconds);
            FarseerRevealTime = FloatOptionItem.Create(Id + 11, "FarseerRevealTime", new(0f, 30f, 1f), 10f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Farseer])
                .SetValueFormat(OptionFormat.Seconds);
            Vision = FloatOptionItem.Create(Id + 12, "FarseerVision", new(0f, 1f, 0.05f), 0.25f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Farseer])
                .SetValueFormat(OptionFormat.Multiplier);
            UsePet = CreatePetUseSetting(Id + 13, CustomRoles.Farseer);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;

            foreach (PlayerControl ar in Main.AllPlayerControls)
            {
                IsRevealed[(playerId, ar.PlayerId)] = false;
            }

            RandomRole[playerId] = GetRandomCrewRoleString();

            if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public static bool On;
        public override bool IsEnable => On;

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive();
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = FarseerCooldown.GetFloat();

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            killer.SetKillCooldown(FarseerRevealTime.GetFloat());
            if (!IsRevealed[(killer.PlayerId, target.PlayerId)] && !FarseerTimer.ContainsKey(killer.PlayerId))
            {
                FarseerTimer.TryAdd(killer.PlayerId, (target, 0f));
                NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                RPC.SetCurrentRevealTarget(killer.PlayerId, target.PlayerId);
            }

            return false;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (GameStates.IsInTask && FarseerTimer.ContainsKey(player.PlayerId))
            {
                if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                {
                    FarseerTimer.Remove(player.PlayerId);
                    NotifyRoles(SpecifySeer: player);
                    RPC.ResetCurrentRevealTarget(player.PlayerId);
                }
                else
                {
                    var arTarget = FarseerTimer[player.PlayerId].PLAYER;
                    var arTime = FarseerTimer[player.PlayerId].TIMER;
                    if (!arTarget.IsAlive())
                    {
                        FarseerTimer.Remove(player.PlayerId);
                    }
                    else if (arTime >= FarseerRevealTime.GetFloat())
                    {
                        if (UsePets.GetBool()) player.AddKCDAsAbilityCD();
                        else player.SetKillCooldown();
                        FarseerTimer.Remove(player.PlayerId);
                        IsRevealed[(player.PlayerId, arTarget.PlayerId)] = true;
                        player.RpcSetRevealtPlayer(arTarget, true);
                        NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget);
                        RPC.ResetCurrentRevealTarget(player.PlayerId);
                    }
                    else
                    {
                        float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                        float dis = Vector2.Distance(player.transform.position, arTarget.transform.position);
                        if (dis <= range)
                        {
                            FarseerTimer[player.PlayerId] = (arTarget, arTime + Time.fixedDeltaTime);
                        }
                        else
                        {
                            FarseerTimer.Remove(player.PlayerId);
                            NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true);
                            RPC.ResetCurrentRevealTarget(player.PlayerId);

                            Logger.Info($"Canceled: {player.GetNameWithRole().RemoveHtmlTags()}", "Arsonist");
                        }
                    }
                }
            }
        }

        public static string GetRandomCrewRoleString()
        {
            var rd = IRandom.Instance;
            var randomRole = RandomRolesForTrickster[rd.Next(0, RandomRolesForTrickster.Length)];

            return $"<size={FontSize}>{ColorString(GetRoleColor(randomRole), GetString(randomRole.ToString()))}</size>";
        }

        public static string GetTaskState()
        {
            var playersWithTasks = Main.PlayerStates.Where(a => a.Value.TaskState.hasTasks).ToArray();
            if (playersWithTasks.Length == 0)
            {
                return "\r\n";
            }

            var rd = IRandom.Instance;
            var randomPlayer = playersWithTasks[rd.Next(0, playersWithTasks.Length)];
            var taskState = randomPlayer.Value.TaskState;

            var taskCompleteColor = Color.green;
            var nonCompleteColor = Color.yellow;
            var normalColor = taskState.IsTaskFinished ? taskCompleteColor : nonCompleteColor;

            Color textColor = Camouflager.IsActive ? Color.gray : normalColor;
            string completed = Camouflager.IsActive ? "?" : $"{taskState.CompletedTasksCount}";

            return $" <size={FontSize}>" + ColorString(textColor, $"({completed}/{taskState.AllTasksCount})") + "</size>\r\n";
        }
    }
}