using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Utils;

namespace EHR.Impostor;

public class Hitman : RoleBase
{
    private const int Id = 640800;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem SuccessKCD;
    private static OptionItem ShapeshiftCooldown;

    private byte HitmanId = byte.MaxValue;
    public byte TargetId = byte.MaxValue;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Hitman);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
            .SetValueFormat(OptionFormat.Seconds);

        SuccessKCD = new FloatOptionItem(Id + 11, "HitmanLowKCD", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
            .SetValueFormat(OptionFormat.Seconds);

        ShapeshiftCooldown = new FloatOptionItem(Id + 12, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        TargetId = byte.MaxValue;
        HitmanId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        HitmanId = playerId;
        TargetId = byte.MaxValue;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    private void SendRPC()
    {
        if (!DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetHitmanTarget, SendOption.Reliable);
        writer.Write(HitmanId);
        writer.Write(TargetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(byte id)
    {
        TargetId = id;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;

        if (target == null) return false;

        if (target.PlayerId == TargetId)
        {
            TargetId = byte.MaxValue;
            SendRPC();
            LateTask.New(() => { killer.SetKillCooldown(SuccessKCD.GetFloat()); }, 0.1f, "Hitman Killed Target - SetKillCooldown Task");
        }

        return true;
    }

    public static void CheckAndResetTargets()
    {
        foreach (byte id in PlayerIdList)
            if (Main.PlayerStates[id].Role is Hitman { IsEnable: true } hm)
                hm.OnReportDeadBody();
    }

    public override void OnReportDeadBody()
    {
        PlayerControl target = GetPlayerById(TargetId);

        if (!target.IsAlive() || target.Data.Disconnected)
        {
            TargetId = byte.MaxValue;
            SendRPC();
        }
    }

    public override bool OnShapeshift(PlayerControl hitman, PlayerControl target, bool shapeshifting)
    {
        if (target == null || hitman == null || !shapeshifting || TargetId != byte.MaxValue || !target.IsAlive()) return false;

        TargetId = target.PlayerId;
        SendRPC();
        NotifyRoles(SpecifySeer: hitman, SpecifyTarget: target);

        return false;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId) return string.Empty;

        byte id = (Main.PlayerStates[seer.PlayerId].Role as Hitman)?.TargetId ?? byte.MaxValue;
        return id == byte.MaxValue ? string.Empty : $"<color=#00ffa5>Target:</color> <color=#ffffff>{GetPlayerById(id).GetRealName().RemoveHtmlTags()}</color>";
    }
}