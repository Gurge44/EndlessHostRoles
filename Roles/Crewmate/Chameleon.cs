using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Hazel;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class Chameleon
{
    private static readonly int Id = 6300;
    private static List<byte> playerIdList = [];

    public static OptionItem ChameleonCooldown;
    private static OptionItem ChameleonDuration;
    public static OptionItem UseLimitOpt;
    public static OptionItem ChameleonAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static Dictionary<byte, long> InvisTime = [];
    private static Dictionary<byte, long> lastTime = [];
    private static Dictionary<byte, int> ventedId = [];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Chameleon);
        ChameleonCooldown = FloatOptionItem.Create(Id + 2, "ChameleonCooldown", new(1f, 60f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Seconds);
        ChameleonDuration = FloatOptionItem.Create(Id + 3, "ChameleonDuration", new(1f, 30f, 1f), 10f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Seconds);
        UseLimitOpt = IntegerOptionItem.Create(Id + 4, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Times);
        ChameleonAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 5, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 6, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Times);
    }
    public static void Init()
    {
        playerIdList = [];
        InvisTime = [];
        lastTime = [];
        ventedId = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(PlayerControl pc)
    {
        if (!IsEnable || !Utils.DoRPC || pc.AmOwner) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetChameleonTimer, SendOption.Reliable, pc.GetClientId());
        writer.Write((InvisTime.TryGetValue(pc.PlayerId, out var x) ? x : -1).ToString());
        writer.Write((lastTime.TryGetValue(pc.PlayerId, out var y) ? y : -1).ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        InvisTime = [];
        lastTime = [];
        long invis = long.Parse(reader.ReadString());
        long last = long.Parse(reader.ReadString());
        if (invis > 0) InvisTime.Add(PlayerControl.LocalPlayer.PlayerId, invis);
        if (last > 0) lastTime.Add(PlayerControl.LocalPlayer.PlayerId, last);
    }
    public static bool CanGoInvis(byte id)
        => GameStates.IsInTask && !InvisTime.ContainsKey(id) && !lastTime.ContainsKey(id);
    public static bool IsInvis(byte id) => InvisTime.ContainsKey(id);

    private static long lastFixedTime;
    public static void AfterMeetingTasks()
    {
        lastTime = [];
        InvisTime = [];
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => playerIdList.Contains(x.PlayerId)).ToArray())
        {
            lastTime.Add(pc.PlayerId, Utils.TimeStamp);
            SendRPC(pc);
        }
    }
    public static void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable) return;

        var now = Utils.TimeStamp;

        if (lastTime.TryGetValue(player.PlayerId, out var time) && time + (long)ChameleonCooldown.GetFloat() < now)
        {
            lastTime.Remove(player.PlayerId);
            if (!player.IsModClient()) player.Notify(GetString("ChameleonCanVent"));
            SendRPC(player);
        }

        if (lastFixedTime != now)
        {
            lastFixedTime = now;
            Dictionary<byte, long> newList = [];
            List<byte> refreshList = [];
            foreach (var it in InvisTime)
            {
                var pc = Utils.GetPlayerById(it.Key);
                if (pc == null) continue;
                var remainTime = it.Value + (long)ChameleonDuration.GetFloat() - now;
                if (remainTime < 0)
                {
                    lastTime.Add(pc.PlayerId, now);
                    pc?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(pc.PlayerId, out var id) ? id : Main.LastEnteredVent[pc.PlayerId].Id);
                    pc.Notify(GetString("ChameleonInvisStateOut"));
                    pc.RpcResetAbilityCooldown();
                    SendRPC(pc);
                    continue;
                }

                if (remainTime <= 10)
                {
                    if (!pc.IsModClient()) pc.Notify(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
                }
                newList.Add(it.Key, it.Value);
            }
            InvisTime.Where(x => !newList.ContainsKey(x.Key)).Do(x => refreshList.Add(x.Key));
            InvisTime = newList;
            refreshList.Do(x => SendRPC(Utils.GetPlayerById(x)));
        }
    }
    public static void OnCoEnterVent(PlayerPhysics __instance, int ventId)
    {
        var pc = __instance.myPlayer;
        if (!AmongUsClient.Instance.AmHost || IsInvis(pc.PlayerId)) return;
        _ = new LateTask(() =>
        {
            if (CanGoInvis(pc.PlayerId))
            {
                if (pc.GetAbilityUseLimit() >= 1)
                {
                    ventedId.Remove(pc.PlayerId);
                    ventedId.Add(pc.PlayerId, ventId);

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, 34, SendOption.Reliable, pc.GetClientId());
                    writer.WritePacked(ventId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);

                    InvisTime.Add(pc.PlayerId, Utils.TimeStamp);
                    SendRPC(pc);
                    pc.Notify(GetString("ChameleonInvisState"), ChameleonDuration.GetFloat());

                    pc.RpcRemoveAbilityUse();
                }
                else
                {
                    pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
            }
            else
            {
                //__instance.myPlayer.MyPhysics.RpcBootFromVent(ventId);
                pc.Notify(GetString("ChameleonInvisInCooldown"));
            }
        }, 0.5f, "Chameleon Vent");
    }
    public static void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!pc.Is(CustomRoles.Chameleon) || !IsInvis(pc.PlayerId)) return;

        InvisTime.Remove(pc.PlayerId);
        lastTime.Add(pc.PlayerId, Utils.TimeStamp);
        SendRPC(pc);

        pc?.MyPhysics?.RpcBootFromVent(vent.Id);
        pc.Notify(GetString("ChameleonInvisStateOut"));
    }
    public static string GetHudText(PlayerControl pc)
    {
        if (pc == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
        var str = new StringBuilder();
        if (IsInvis(pc.PlayerId))
        {
            var remainTime = InvisTime[pc.PlayerId] + (long)ChameleonDuration.GetFloat() - Utils.TimeStamp;
            str.Append(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
        }
        else if (lastTime.TryGetValue(pc.PlayerId, out var time))
        {
            var cooldown = time + (long)ChameleonCooldown.GetFloat() - Utils.TimeStamp;
            str.Append(string.Format(GetString("ChameleonInvisCooldownRemain"), cooldown + 1));
        }
        else
        {
            str.Append(GetString("ChameleonCanVent"));
        }
        return str.ToString();
    }
}