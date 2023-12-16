using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Roles.Crewmate;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Wraith
{
    private static readonly int Id = 13300;
    private static List<byte> playerIdList = [];

    public static OptionItem WraithCooldown;
    private static OptionItem WraithDuration;
    private static OptionItem WraithVentNormallyOnCooldown;

    private static Dictionary<byte, long> InvisTime = [];
    public static Dictionary<byte, long> lastTime = [];
    private static Dictionary<byte, int> ventedId = [];
    private static int CD;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Wraith, 1, zeroOne: false);
        WraithCooldown = FloatOptionItem.Create(Id + 2, "WraithCooldown", new(1f, 60f, 1f), 20f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith])
            .SetValueFormat(OptionFormat.Seconds);
        WraithDuration = FloatOptionItem.Create(Id + 3, "WraithDuration", new(1f, 30f, 1f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith])
            .SetValueFormat(OptionFormat.Seconds);
        WraithVentNormallyOnCooldown = BooleanOptionItem.Create(Id + 4, "WraithVentNormallyOnCooldown", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith]);
    }
    public static void Init()
    {
        playerIdList = [];
        InvisTime = [];
        lastTime = [];
        ventedId = [];
        CD = 0;
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);

    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(PlayerControl pc)
    {
        if (pc.AmOwner || !IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetWraithTimer, SendOption.Reliable, pc.GetClientId());
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
            lastTime.Add(pc.PlayerId, Utils.GetTimeStamp());
            SendRPC(pc);
        }
    }
    public static void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable) return;

        var now = Utils.GetTimeStamp();

        if (lastTime.TryGetValue(player.PlayerId, out var WWtime) && !player.IsModClient())
        {
            var cooldown = WWtime + (long)WraithCooldown.GetFloat() - now;
            if ((int)cooldown != CD) player.Notify(string.Format(GetString("CDPT"), cooldown + 1), 1.1f);
            CD = (int)cooldown;
        }

        if (lastTime.TryGetValue(player.PlayerId, out var time) && time + (long)WraithCooldown.GetFloat() < now)
        {
            lastTime.Remove(player.PlayerId);
            if (!player.IsModClient()) player.Notify(GetString("WraithCanVent"));
            SendRPC(player);
            CD = 0;
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
                var remainTime = it.Value + (long)WraithDuration.GetFloat() - now;
                if (remainTime < 0)
                {
                    lastTime.Add(pc.PlayerId, now);
                    pc?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(pc.PlayerId, out var id) ? id : Main.LastEnteredVent[pc.PlayerId].Id);
                    pc.Notify(GetString("WraithInvisStateOut"));
                    SendRPC(pc);
                    continue;
                }
                else if (remainTime <= 10)
                {
                    if (!pc.IsModClient()) pc.Notify(string.Format(GetString("WraithInvisStateCountdown"), remainTime + 1));
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
                ventedId.Remove(pc.PlayerId);
                ventedId.Add(pc.PlayerId, ventId);

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, 34, SendOption.Reliable, pc.GetClientId());
                writer.WritePacked(ventId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                InvisTime.Add(pc.PlayerId, Utils.GetTimeStamp());
                SendRPC(pc);
                pc.Notify(GetString("WraithInvisState"), WraithDuration.GetFloat());
            }
            else
            {
                if (!WraithVentNormallyOnCooldown.GetBool())
                {
                    __instance.myPlayer.MyPhysics.RpcBootFromVent(ventId);
                    pc.Notify(GetString("WraithInvisInCooldown"));
                }
            }
        }, 0.5f, "Wraith Vent");
    }
    public static void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!pc.Is(CustomRoles.Wraith) || !IsInvis(pc.PlayerId)) return;

        InvisTime.Remove(pc.PlayerId);
        lastTime.Add(pc.PlayerId, Utils.GetTimeStamp());
        SendRPC(pc);

        pc?.MyPhysics?.RpcBootFromVent(vent.Id);
        pc.Notify(GetString("WraithInvisStateOut"));
    }
    public static string GetHudText(PlayerControl pc)
    {
        if (pc == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
        var str = new StringBuilder();
        if (IsInvis(pc.PlayerId))
        {
            var remainTime = InvisTime[pc.PlayerId] + (long)WraithDuration.GetFloat() - Utils.GetTimeStamp();
            str.Append(string.Format(GetString("WraithInvisStateCountdown"), remainTime + 1));
        }
        else if (lastTime.TryGetValue(pc.PlayerId, out var time))
        {
            var cooldown = time + (long)WraithCooldown.GetFloat() - Utils.GetTimeStamp();
            str.Append(string.Format(GetString("WraithInvisCooldownRemain"), cooldown + 2));
        }
        else
        {
            str.Append(GetString("WraithCanVent"));
        }
        return str.ToString();
    }

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;
        if (target.Is(CustomRoles.Bait)) return true;
        if (!IsInvis(killer.PlayerId)) return true;
        killer.SetKillCooldown();
        target.RpcCheckAndMurder(target);
        target.SetRealKiller(killer);
        return false;
    }
}