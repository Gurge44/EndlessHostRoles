using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using static EHR.Translator;

namespace EHR.Crewmate;

internal class Benefactor : RoleBase
{
    private const int Id = 8670;
    private static List<byte> PlayerIdList = [];

    private static Dictionary<byte, List<int>> TaskIndex = [];
    private static Dictionary<byte, long> ShieldedPlayers = [];
    private static int MaxTasksMarkedPerRound;

    private static OptionItem TaskMarkPerRoundOpt;
    private static OptionItem ShieldDuration;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Benefactor);

        TaskMarkPerRoundOpt = new IntegerOptionItem(Id + 10, "TaskMarkPerRound", new(1, 14, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Benefactor])
            .SetValueFormat(OptionFormat.Votes);

        ShieldDuration = new IntegerOptionItem(Id + 11, "AidDur", new(1, 30, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Benefactor])
            .SetValueFormat(OptionFormat.Seconds);

        Options.OverrideTasksData.Create(Id + 12, TabGroup.CrewmateRoles, CustomRoles.Benefactor);
    }

    public override void Init()
    {
        PlayerIdList = [];
        TaskIndex = [];
        ShieldedPlayers = [];
        MaxTasksMarkedPerRound = TaskMarkPerRoundOpt.GetInt();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(MaxTasksMarkedPerRound);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private static void SendRPC(byte benefactorId, int taskIndex = -1, bool isShield = false, bool clearAll = false, bool shieldExpire = false, byte shieldedId = byte.MaxValue)
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncBenefactorMarkedTask, SendOption.Reliable);
        writer.Write(benefactorId);
        writer.Write(taskIndex);
        writer.Write(isShield);
        writer.Write(clearAll);
        writer.Write(shieldExpire);
        writer.Write(shieldedId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte benefactorID = reader.ReadByte();
        if (Main.PlayerStates[benefactorID].Role is not Benefactor { IsEnable: true }) return;

        int taskInd = reader.ReadInt32();
        bool IsShield = reader.ReadBoolean();
        bool clearAll = reader.ReadBoolean();
        bool shieldExpire = reader.ReadBoolean();
        byte shieldedId = reader.ReadByte();

        bool tryGetValue = TaskIndex.TryGetValue(benefactorID, out var list);

        if (!IsShield)
        {
            if (!clearAll && !shieldExpire)
            {
                if (tryGetValue) list.Add(taskInd);
                else TaskIndex[benefactorID] = [taskInd];
            }
        }
        else if (tryGetValue)
            list.Remove(taskInd);

        if (clearAll && tryGetValue)
            list.Clear();

        if (IsShield)
            ShieldedPlayers[shieldedId] = Utils.TimeStamp;
        
        if (shieldExpire)
            ShieldedPlayers.Remove(shieldedId);

        if (clearAll && ShieldedPlayers.Count > 0)
            ShieldedPlayers.Clear();
    }

    public override void AfterMeetingTasks()
    {
        if (!IsEnable) return;

        ShieldedPlayers.Clear();

        foreach ((byte id, List<int> list) in TaskIndex)
        {
            list.Clear();
            SendRPC(id, clearAll: true);
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!IsEnable) return;

        foreach (KeyValuePair<byte, long> x in ShieldedPlayers.Where(x => x.Value + ShieldDuration.GetInt() < Utils.TimeStamp).ToArray())
        {
            ShieldedPlayers.Remove(x.Key);
            SendRPC(pc.PlayerId, shieldExpire: true, shieldedId: Utils.GetPlayerById(x.Key).PlayerId);
        }
    }

    public static void OnTaskComplete(PlayerControl player, PlayerTask task) // Special case for Benefactor
    {
        if (player == null) return;

        byte playerId = player.PlayerId;

        if (player.Is(CustomRoles.Benefactor))
        {
            float abilityUseLimit = playerId.GetAbilityUseLimit();

            if (abilityUseLimit < 1)
            {
                Logger.Info($"Max task per round ({abilityUseLimit}) reached for {player.GetNameWithRole()}", "Benefactor");
                return;
            }

            playerId.SetAbilityUseLimit(abilityUseLimit - 1);

            if (TaskIndex.TryGetValue(playerId, out var list)) list.Add(task.Index);
            else TaskIndex[playerId] = [task.Index];
            
            SendRPC(playerId, task.Index);
            player.Notify(GetString("BenefactorTaskMarked"));
        }
        else
        {
            foreach (byte benefactorId in TaskIndex.Keys.ToArray())
            {
                if (TaskIndex[benefactorId].Contains(task.Index))
                {
                    PlayerControl benefactorPC = Utils.GetPlayerById(benefactorId);
                    if (benefactorPC == null) continue;

                    player.Notify(GetString("BenefactorTargetGotShieldNotify"));
                    TaskIndex[benefactorId].Remove(task.Index);
                    SendRPC(benefactorId, task.Index, true, shieldedId: player.PlayerId);
                    Logger.Info($"{player.GetAllRoleName()} got a shield because the task was marked by {benefactorPC.GetNameWithRole()}", "Benefactor");
                }
            }
        }
    }
}