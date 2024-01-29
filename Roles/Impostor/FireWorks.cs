using Hazel;
using System.Collections.Generic;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public static class FireWorks
{
    public enum FireWorksState
    {
        Initial = 1,
        SettingFireWorks = 2,
        WaitTime = 4,
        ReadyFire = 8,
        FireEnd = 16,
        CanUseKill = Initial | FireEnd
    }

    private static readonly int Id = 2800;
    private static OptionItem FireWorksCount;
    private static OptionItem FireWorksRadius;
    public static OptionItem CanKill;

    public static bool IsEnable;

    public static Dictionary<byte, int> nowFireWorksCount = [];
    private static Dictionary<byte, List<Vector3>> fireWorksPosition = [];
    private static Dictionary<byte, FireWorksState> state = [];
    private static Dictionary<byte, int> fireWorksBombKill = [];
    private static int fireWorksCount = 1;
    private static float fireWorksRadius = 1;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.FireWorks);
        FireWorksCount = IntegerOptionItem.Create(Id + 10, "FireWorksMaxCount", new(1, 10, 1), 3, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.FireWorks])
            .SetValueFormat(OptionFormat.Pieces);
        FireWorksRadius = FloatOptionItem.Create(Id + 11, "FireWorksRadius", new(0.5f, 5f, 0.5f), 2f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.FireWorks])
            .SetValueFormat(OptionFormat.Multiplier);
        CanKill = BooleanOptionItem.Create(Id + 12, "CanKill", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.FireWorks]);
    }

    public static void Init()
    {
        IsEnable = false;
        nowFireWorksCount = [];
        fireWorksPosition = [];
        state = [];
        fireWorksBombKill = [];
        fireWorksCount = FireWorksCount.GetInt();
        fireWorksRadius = FireWorksRadius.GetFloat();
    }

    public static void Add(byte playerId)
    {
        IsEnable = true;
        nowFireWorksCount[playerId] = fireWorksCount;
        fireWorksPosition[playerId] = [];
        state[playerId] = FireWorksState.Initial;
        fireWorksBombKill[playerId] = 0;
    }

    public static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        Logger.Info($"Player{playerId}:SendRPC", "FireWorks");
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SendFireWorksState, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(nowFireWorksCount[playerId]);
        writer.Write((int)state[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader msg)
    {
        var playerId = msg.ReadByte();
        nowFireWorksCount[playerId] = msg.ReadInt32();
        state[playerId] = (FireWorksState)msg.ReadInt32();
        Logger.Info($"Player{playerId}:ReceiveRPC", "FireWorks");
    }

    public static bool CanUseKillButton(PlayerControl pc)
    {
        if (pc == null || pc.Data.IsDead) return false;

        try
        {
            return CanKill.GetBool() || (state.TryGetValue(pc.PlayerId, out var fwState) && (fwState & FireWorksState.CanUseKill) != 0);
        }
        catch
        {
            return false;
        }
    }

    public static void ShapeShiftState(PlayerControl pc, bool shapeshifting)
    {
        Logger.Info($"FireWorks ShapeShift", "FireWorks");
        if (pc == null || pc.Data.IsDead || !shapeshifting || Pelican.IsEaten(pc.PlayerId) || Medic.ProtectList.Contains(pc.PlayerId)) return;
        switch (state[pc.PlayerId])
        {
            case FireWorksState.Initial:
            case FireWorksState.SettingFireWorks:
                Logger.Info("Install Firework", "FireWorks");
                fireWorksPosition[pc.PlayerId].Add(pc.Pos());
                nowFireWorksCount[pc.PlayerId]--;
                state[pc.PlayerId] = nowFireWorksCount[pc.PlayerId] == 0
                    ? Main.AliveImpostorCount <= 1 ? FireWorksState.ReadyFire : FireWorksState.WaitTime
                    : FireWorksState.SettingFireWorks;
                break;
            case FireWorksState.ReadyFire:
                Logger.Info("Explode fireworks", "FireWorks");
                bool suicide = false;
                foreach (PlayerControl target in Main.AllAlivePlayerControls)
                {
                    foreach (Vector3 pos in fireWorksPosition[pc.PlayerId].ToArray())
                    {
                        var dis = Vector2.Distance(pos, target.transform.position);
                        if (dis > fireWorksRadius) continue;

                        if (target == pc)
                        {
                            suicide = true;
                        }
                        else
                        {
                            target.Suicide(PlayerState.DeathReason.Bombed, pc);
                        }
                    }
                }
                if (suicide)
                {
                    var totalAlive = Main.AllAlivePlayerControls.Length;
                    if (totalAlive != 1)
                    {
                        pc.Suicide();
                    }
                }
                state[pc.PlayerId] = FireWorksState.FireEnd;
                break;
            default:
                break;
        }
        SendRPC(pc.PlayerId);
        Utils.NotifyRoles(ForceLoop: true);
    }

    public static string GetStateText(PlayerControl pc/*, bool isLocal = true*/)
    {
        string retText = string.Empty;
        if (pc == null || pc.Data.IsDead) return retText;

        if (state[pc.PlayerId] == FireWorksState.WaitTime && Main.AliveImpostorCount <= 1)
        {
            Logger.Info("爆破準備OK", "FireWorks");
            state[pc.PlayerId] = FireWorksState.ReadyFire;
            SendRPC(pc.PlayerId);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
        switch (state[pc.PlayerId])
        {
            case FireWorksState.Initial:
            case FireWorksState.SettingFireWorks:
                retText = string.Format(GetString("FireworksPutPhase"), nowFireWorksCount[pc.PlayerId]);
                break;
            case FireWorksState.WaitTime:
                retText = GetString("FireworksWaitPhase");
                break;
            case FireWorksState.ReadyFire:
                retText = GetString("FireworksReadyFirePhase");
                break;
            case FireWorksState.FireEnd:
                break;
        }
        return retText;
    }
}