using System;
using System.Collections.Generic;
using Hazel;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public class FireWorks : RoleBase
{
    [Flags]
    public enum FireWorksState
    {
        Initial = 1,
        SettingFireWorks = 2,
        WaitTime = 4,
        ReadyFire = 8,
        FireEnd = 16,
        CanUseKill = Initial | FireEnd
    }

    private const int Id = 2800;
    private static OptionItem FireWorksCount;
    private static OptionItem FireWorksRadius;
    public static OptionItem CanKill;

    public static bool On;

    public int nowFireWorksCount;
    private List<Vector3> fireWorksPosition = [];
    private FireWorksState state;
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

    public override void Init()
    {
        On = false;
        nowFireWorksCount = 0;
        fireWorksPosition = [];
        state = FireWorksState.Initial;
    }

    public override void Add(byte playerId)
    {
        On = true;
        fireWorksCount = FireWorksCount.GetInt();
        fireWorksRadius = FireWorksRadius.GetFloat();
        nowFireWorksCount = fireWorksCount;
        fireWorksPosition = [];
        state = FireWorksState.Initial;
    }

    public override bool IsEnable => On;

    void SendRPC(byte playerId)
    {
        if (!On || !Utils.DoRPC) return;
        Logger.Info($"Player{playerId}:SendRPC", "FireWorks");
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SendFireWorksState, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(nowFireWorksCount);
        writer.Write((int)state);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(int count, FireWorksState newState)
    {
        nowFireWorksCount = count;
        state = newState;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        if (pc == null || pc.Data.IsDead) return false;

        try
        {
            return CanKill.GetBool() || (state & FireWorksState.CanUseKill) != 0;
        }
        catch
        {
            return false;
        }
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl _, bool shapeshifting)
    {
        Logger.Info("FireWorks ShapeShift", "FireWorks");
        if (pc == null || pc.Data.IsDead || !shapeshifting || Pelican.IsEaten(pc.PlayerId) || Medic.ProtectList.Contains(pc.PlayerId)) return false;
        switch (state)
        {
            case FireWorksState.Initial:
            case FireWorksState.SettingFireWorks:
                Logger.Info("Install Firework", "FireWorks");
                fireWorksPosition.Add(pc.Pos());
                nowFireWorksCount--;
                state = nowFireWorksCount == 0
                    ? Main.AliveImpostorCount <= 1 ? FireWorksState.ReadyFire : FireWorksState.WaitTime
                    : FireWorksState.SettingFireWorks;
                break;
            case FireWorksState.ReadyFire:
                Logger.Info("Explode fireworks", "FireWorks");
                bool suicide = false;
                foreach (PlayerControl target in Main.AllAlivePlayerControls)
                {
                    foreach (Vector3 pos in fireWorksPosition)
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

                state = FireWorksState.FireEnd;
                break;
        }

        SendRPC(pc.PlayerId);
        Utils.NotifyRoles(ForceLoop: true);

        return false;
    }

    public static string GetStateText(PlayerControl pc /*, bool isLocal = true*/)
    {
        string retText = string.Empty;
        if (pc == null || pc.Data.IsDead) return retText;

        if (Main.PlayerStates[pc.PlayerId].Role is not FireWorks fw) return retText;

        if (fw.state == FireWorksState.WaitTime && Main.AliveImpostorCount <= 1)
        {
            fw.state = FireWorksState.ReadyFire;
            fw.SendRPC(pc.PlayerId);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        switch (fw.state)
        {
            case FireWorksState.Initial:
            case FireWorksState.SettingFireWorks:
                retText = string.Format(GetString("FireworksPutPhase"), fw.nowFireWorksCount);
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