using System.Collections.Generic;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

using static Options;
using static Utils;

public class Spy : RoleBase
{
    private const int Id = 640400;
    private static List<byte> PlayerIdList = [];
    public static Dictionary<byte, long> SpyRedNameList = [];

    private static OptionItem SpyRedNameDur;
    private static OptionItem UseLimitOpt;
    public static OptionItem SpyAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private long LastUpdate;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Spy);

        UseLimitOpt = new IntegerOptionItem(Id + 10, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
            .SetValueFormat(OptionFormat.Times);

        SpyRedNameDur = new FloatOptionItem(Id + 11, "SpyRedNameDur", new(0f, 70f, 1f), 3f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
            .SetValueFormat(OptionFormat.Seconds);

        SpyAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        SpyRedNameList = [];
    }

    public override void Add(byte playerId)
    {
        LastUpdate = TimeStamp;
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private static void SendRPC(int operate, byte id = byte.MaxValue, bool changeColor = false)
    {
        if (!DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncSpy, SendOption.Reliable);
        writer.Write(operate);

        switch (operate)
        {
            case 1: // Red Name Add
                writer.Write(id);
                writer.Write(SpyRedNameList[id].ToString());
                break;
            case 3: // Red Name Remove
                writer.Write(id);
                writer.Write(changeColor);
                break;
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        int operate = reader.ReadInt32();

        switch (operate)
        {
            case 1:
                byte susId = reader.ReadByte();
                string stimeStamp = reader.ReadString();
                if (long.TryParse(stimeStamp, out long timeStamp)) SpyRedNameList[susId] = timeStamp;

                return;
            case 3:
                SpyRedNameList.Remove(reader.ReadByte());
                reader.ReadBoolean();
                return;
        }
    }

    public static bool OnKillAttempt(PlayerControl killer, PlayerControl target) // Special handling for Spy ---- remains as a static method
    {
        if (killer == null || target == null || !target.Is(CustomRoles.Spy) || killer.PlayerId == target.PlayerId || target.GetAbilityUseLimit() < 1) return true;

        target.RpcRemoveAbilityUse();
        SpyRedNameList.TryAdd(killer.PlayerId, TimeStamp);
        SendRPC(1, killer.PlayerId);
        NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
        killer.SetKillCooldown();

        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc == null || !pc.Is(CustomRoles.Spy) || SpyRedNameList.Count == 0) return;

        long now = TimeStamp;
        if (now == LastUpdate) return;

        LastUpdate = now;

        foreach (KeyValuePair<byte, long> x in SpyRedNameList)
        {
            if (x.Value + SpyRedNameDur.GetInt() < now || !GameStates.IsInTask)
            {
                SpyRedNameList.Remove(x.Key);
                SendRPC(3, x.Key, true);
                NotifyRoles(SpecifySeer: pc, SpecifyTarget: x.Key.GetPlayer());
            }
        }
    }
}