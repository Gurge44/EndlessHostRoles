using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Roles;

public class Investigator : RoleBase
{
    private const int Id = 9700;

    private const string FontSize = "1.7";
    public static Dictionary<byte, (PlayerControl PLAYER, float TIMER)> InvestigatorTimer = [];
    public static Dictionary<(byte, byte), bool> IsRevealed = [];
    public static byte CurrentRevealTarget = byte.MaxValue;

    private static OptionItem InvestigatorCooldown;
    private static OptionItem InvestigatorTime;
    private static OptionItem Vision;
    public static OptionItem UsePet;

    public static Dictionary<int, string> RandomRole = [];

    public static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Investigator);

        InvestigatorCooldown = new FloatOptionItem(Id + 10, "InvestigatorRevealCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Investigator])
            .SetValueFormat(OptionFormat.Seconds);

        InvestigatorTime = new FloatOptionItem(Id + 11, "InvestigatorTime", new(0f, 30f, 1f), 10f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Investigator])
            .SetValueFormat(OptionFormat.Seconds);

        Vision = new FloatOptionItem(Id + 12, "InvestigatorVision", new(0f, 1f, 0.05f), 0.25f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Investigator])
            .SetValueFormat(OptionFormat.Multiplier);

        UsePet = CreatePetUseSetting(Id + 13, CustomRoles.Investigator);
    }

    public override void Init()
    {
        On = false;
        InvestigatorTimer = [];
        IsRevealed = [];
        RandomRole = [];
    }

    public override void Add(byte playerId)
    {
        On = true;

        foreach (PlayerControl ar in Main.CachedAllPlayerControls())
            IsRevealed[(playerId, ar.PlayerId)] = false;

        RandomRole[playerId] = GetRandomCrewRoleString();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return true;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = InvestigatorCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        float revealTime = InvestigatorTime.GetFloat();
        killer.SetKillCooldown(revealTime == 0f ? InvestigatorCooldown.GetFloat() : revealTime);

        if (!IsRevealed[(killer.PlayerId, target.PlayerId)] && !InvestigatorTimer.ContainsKey(killer.PlayerId))
        {
            InvestigatorTimer.TryAdd(killer.PlayerId, (target, 0f));
            NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            RPC.SetCurrentRevealTarget(killer.PlayerId, target.PlayerId);
        }

        return false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (GameStates.IsInTask && InvestigatorTimer.ContainsKey(player.PlayerId))
        {
            if (!player.IsAliveWithConditions())
            {
                InvestigatorTimer.Remove(player.PlayerId);
                RPC.ResetCurrentRevealTarget(player.PlayerId);
            }
            else
            {
                PlayerControl target = InvestigatorTimer[player.PlayerId].PLAYER;
                float timer = InvestigatorTimer[player.PlayerId].TIMER;

                if (!target.IsAlive())
                    InvestigatorTimer.Remove(player.PlayerId);
                else if (timer >= InvestigatorTime.GetFloat())
                {
                    if (UsePets.GetBool() && UsePet.GetBool())
                        player.AddKCDAsAbilityCD();
                    else
                        player.SetKillCooldown();

                    InvestigatorTimer.Remove(player.PlayerId);
                    IsRevealed[(player.PlayerId, target.PlayerId)] = true;
                    player.RpcSetRevealedPlayer(target, true);
                    NotifyRoles(SpecifySeer: player, SpecifyTarget: target);
                    RPC.ResetCurrentRevealTarget(player.PlayerId);
                }
                else
                {
                    float range = player.GetKillDistance();
                    
                    if (FastVector2.DistanceWithinRange(player.Pos(), target.Pos(), range))
                        InvestigatorTimer[player.PlayerId] = (target, timer + Time.fixedDeltaTime);
                    else
                    {
                        InvestigatorTimer.Remove(player.PlayerId);
                        NotifyRoles(SpecifySeer: player, SpecifyTarget: target);
                        RPC.ResetCurrentRevealTarget(player.PlayerId);

                        Logger.Info($"Canceled: {player.GetNameWithRole().RemoveHtmlTags()}", "Investigator");
                    }
                }
            }
        }
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;
        return IsRevealed.GetValueOrDefault((seer.PlayerId, target.PlayerId));
    }

    private static string GetRandomCrewRoleString()
    {
        CustomRoles randomRole = Main.CustomRoleValues.Where(x => x.IsCrewmate()).RandomElement();
        return $"<size={FontSize}>{ColorString(GetRoleColor(randomRole), GetString(randomRole.ToString()))}</size>";
    }

    public static string GetTaskState()
    {
        KeyValuePair<byte, PlayerState>[] playersWithTasks = Main.PlayerStates.Where(a => a.Value.TaskState.HasTasks).ToArray();
        if (playersWithTasks.Length == 0) return "\r\n";

        KeyValuePair<byte, PlayerState> randomPlayer = playersWithTasks.RandomElement();
        TaskState taskState = randomPlayer.Value.TaskState;

        Color taskCompleteColor = Color.green;
        Color nonCompleteColor = Color.yellow;
        Color normalColor = taskState.IsTaskFinished ? taskCompleteColor : nonCompleteColor;

        Color textColor = Camouflager.IsActive ? Color.gray : normalColor;
        string completed = Camouflager.IsActive ? "?" : $"{taskState.CompletedTasksCount}";

        return $" <size={FontSize}>" + ColorString(textColor, $"({completed}/{taskState.AllTasksCount})") + "</size>\r\n";
    }
}