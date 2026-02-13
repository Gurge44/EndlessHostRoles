using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Fireworker : RoleBase
{
    [Flags]
    public enum FireworkerState
    {
        Initial = 1,
        SettingFireworks = 2,
        WaitTime = 4,
        ReadyFire = 8,
        FireEnd = 16,
        CanUseKill = Initial | FireEnd
    }

    private const int Id = 2800;
    private static OptionItem FireworkerCountOpt;
    private static OptionItem FireworkerRadiusOpt;
    private static OptionItem CanKill;
    private static OptionItem KillCooldown;
    private static OptionItem CanIgniteBeforePlacingAllFireworks;

    public static bool On;
    private static int FireworksCount = 1;
    private static float FireworksRadius = 1;
    private List<Vector3> fireworksPosition = [];

    public int nowFireworksCount;
    private FireworkerState state;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Fireworker);

        FireworkerCountOpt = new IntegerOptionItem(Id + 10, "FireworkerMaxCount", new(1, 10, 1), 3, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Fireworker])
            .SetValueFormat(OptionFormat.Pieces);

        FireworkerRadiusOpt = new FloatOptionItem(Id + 11, "FireworkerRadius", new(0.5f, 5f, 0.5f), 2f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Fireworker])
            .SetValueFormat(OptionFormat.Multiplier);

        CanKill = new BooleanOptionItem(Id + 12, "CanKill", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Fireworker]);

        KillCooldown = new FloatOptionItem(Id + 13, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Fireworker])
            .SetValueFormat(OptionFormat.Seconds);

        CanIgniteBeforePlacingAllFireworks = new BooleanOptionItem(Id + 14, "CanIgniteBeforePlacingAllFireworks", false, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Fireworker]);
    }

    public override void Init()
    {
        On = false;
        nowFireworksCount = 0;
        fireworksPosition = [];
        state = FireworkerState.Initial;
    }

    public override void Add(byte playerId)
    {
        On = true;
        FireworksCount = FireworkerCountOpt.GetInt();
        FireworksRadius = FireworkerRadiusOpt.GetFloat();
        nowFireworksCount = FireworksCount;
        fireworksPosition = [];
        state = FireworkerState.Initial;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    private void SendRPC(byte playerId)
    {
        if (!On || !Utils.DoRPC) return;

        Logger.Info($"Player{playerId}:SendRPC", "Fireworker");
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SendFireworkerState, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(nowFireworksCount);
        writer.Write((int)state);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(int count, FireworkerState newState)
    {
        nowFireworksCount = count;
        state = newState;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return false;

        try { return CanKill.GetBool() || (state & FireworkerState.CanUseKill) != 0; }
        catch { return false; }
    }

    public override void OnPet(PlayerControl pc)
    {
        FireworkerState beforeState = state;
        if (CanIgniteBeforePlacingAllFireworks.GetBool()) state = FireworkerState.ReadyFire;

        OnShapeshift(pc, null, true);

        if (beforeState == FireworkerState.ReadyFire) return;

        state = beforeState;
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl _, bool shapeshifting)
    {
        Logger.Info("Fireworker ShapeShift", "Fireworker");
        if (pc == null || !pc.IsAlive() || !shapeshifting || Pelican.IsEaten(pc.PlayerId)) return false;

        UseAbility(pc);

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        Logger.Info("Fireworker Vanish", "Fireworker");
        if (pc == null || !pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return false;

        UseAbility(pc);

        return false;
    }

    private void UseAbility(PlayerControl pc)
    {
        switch (state)
        {
            case FireworkerState.Initial:
            case FireworkerState.SettingFireworks:
                Logger.Info("Install Firework", "Fireworker");
                fireworksPosition.Add(pc.Pos());
                nowFireworksCount--;

                state = nowFireworksCount == 0
                    ? Main.EnumerateAlivePlayerControls().Count(x => x.Is(CustomRoleTypes.Impostor)) <= 1 ? FireworkerState.ReadyFire : FireworkerState.WaitTime
                    : FireworkerState.SettingFireworks;

                break;
            case FireworkerState.ReadyFire:
                Logger.Info("Explode fireworks", "Fireworker");
                var suicide = false;

                foreach (PlayerControl target in Main.EnumerateAlivePlayerControls())
                {
                    foreach (Vector3 pos in fireworksPosition)
                    {
                        if (!FastVector2.DistanceWithinRange(pos, target.Pos(), FireworksRadius)) continue;

                        if (target == pc)
                            suicide = true;
                        else
                            target.Suicide(PlayerState.DeathReason.Bombed, pc);
                    }
                }

                if (suicide)
                {
                    int totalAlive = Main.AllAlivePlayerControls.Count;
                    if (totalAlive != 1) pc.Suicide();
                }

                state = FireworkerState.FireEnd;
                break;
        }

        SendRPC(pc.PlayerId);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        var retText = string.Empty;
        if (seer == null || !seer.IsAlive() || seer.PlayerId != target.PlayerId || Main.PlayerStates[seer.PlayerId].Role is not Fireworker fw) return retText;

        if (fw.state == FireworkerState.WaitTime && Main.EnumerateAlivePlayerControls().Count(pc => pc.Is(CustomRoleTypes.Impostor)) <= 1)
        {
            fw.state = FireworkerState.ReadyFire;
            fw.SendRPC(seer.PlayerId);
            Utils.NotifyRoles(SpecifySeer: seer, SpecifyTarget: seer);
        }

        switch (fw.state)
        {
            case FireworkerState.Initial:
            case FireworkerState.SettingFireworks:
                retText = string.Format(GetString("FireworkerPutPhase"), fw.nowFireworksCount);
                break;
            case FireworkerState.WaitTime:
                retText = GetString("FireworkerWaitPhase");
                break;
            case FireworkerState.ReadyFire:
                retText = GetString("FireworkerReadyFirePhase");
                break;
            case FireworkerState.FireEnd:
                break;
        }

        return retText;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(nowFireworksCount == 0 ? GetString("FireworkerExplosionButtonText") : GetString("FireworkerInstallAtionButtonText"));
    }
}