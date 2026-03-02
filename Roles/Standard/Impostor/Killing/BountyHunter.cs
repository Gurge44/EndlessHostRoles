using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;
using static EHR.Translator;

namespace EHR.Roles;

public class BountyHunter : RoleBase
{
    private const int Id = 800;
    private static List<byte> PlayerIdList = [];

    private static OptionItem OptionTargetChangeTime;
    private static OptionItem OptionSuccessKillCooldown;
    private static OptionItem OptionFailureKillCooldown;
    private static OptionItem OptionShowTargetArrow;

    private static float TargetChangeTime;
    private static float SuccessKillCooldown;
    private static float FailureKillCooldown;
    private static bool ShowTargetArrow;

    private byte BountyHunterId;
    private CountdownTimer Timer;
    private byte Target;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.BountyHunter);

        OptionTargetChangeTime = new FloatOptionItem(Id + 10, "BountyTargetChangeTime", new(10f, 180f, 0.5f), 50f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);

        OptionSuccessKillCooldown = new FloatOptionItem(Id + 11, "BountySuccessKillCooldown", new(0f, 180f, 0.5f), 3f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);

        OptionFailureKillCooldown = new FloatOptionItem(Id + 12, "BountyFailureKillCooldown", new(0f, 180f, 0.5f), 35f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);

        OptionShowTargetArrow = new BooleanOptionItem(Id + 13, "BountyShowTargetArrow", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter]);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        BountyHunterId = playerId;
        Target = byte.MaxValue;

        TargetChangeTime = OptionTargetChangeTime.GetFloat();
        SuccessKillCooldown = OptionSuccessKillCooldown.GetFloat();
        FailureKillCooldown = OptionFailureKillCooldown.GetFloat();
        ShowTargetArrow = OptionShowTargetArrow.GetBool();

        if (AmongUsClient.Instance.AmHost) ResetTarget(Utils.GetPlayerById(playerId));
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private void SendRPC()
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBountyTarget, SendOption.Reliable);
        writer.Write(BountyHunterId);
        writer.Write(Target);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(byte targetId)
    {
        Target = targetId;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;

        if (GetTarget(killer) == target.PlayerId)
        {
            Logger.Info($"{killer.Data?.PlayerName}: Killed Target", "BountyHunter");
            Main.AllPlayerKillCooldown[killer.PlayerId] = SuccessKillCooldown;
            killer.SyncSettings();
            ResetTarget(killer);
        }
        else
        {
            Logger.Info($"{killer.Data?.PlayerName}: Killed Non-Target", "BountyHunter");
            Main.AllPlayerKillCooldown[killer.PlayerId] = FailureKillCooldown;
            killer.SyncSettings();
        }

        return base.OnCheckMurder(killer, target);
    }

    public byte GetTarget(PlayerControl player)
    {
        if (player == null) return 0xff;

        byte targetId = Target == byte.MaxValue ? ResetTarget(player) : Target;
        return targetId;
    }

    private byte ResetTarget(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return 0xff;

        byte playerId = player.PlayerId;

        Timer?.Dispose();
        Timer = new CountdownTimer(TargetChangeTime, () =>
        {
            byte newTargetId = ResetTarget(player);
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: Utils.GetPlayerById(newTargetId));
        }, onTick: () =>
        {
            if (player == null || !player.IsAlive())
            {
                Timer.Dispose();
                Timer = null;
                Utils.SendRPC(CustomRPC.SyncRoleData, playerId, false);
                return;
            }
            
            var currentTarget = GetTarget(player).GetPlayer();

            if (currentTarget == null || !currentTarget.IsAlive())
            {
                byte newTargetId = ResetTarget(player);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: Utils.GetPlayerById(newTargetId));
            }

            if (Timer.Remaining.TotalSeconds > 15) return;
            
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player, SendOption: SendOption.None);
        }, onCanceled: () => Timer = null);
        Utils.SendRPC(CustomRPC.SyncRoleData, playerId, true);

        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: Reset Target", "BountyHunter");

        List<PlayerControl> cTargets = new(Main.EnumerateAlivePlayerControls().Where(pc => !pc.Is(CustomRoleTypes.Impostor)));

        if (cTargets.Count >= 2 && Target != byte.MaxValue) cTargets.RemoveAll(x => x.PlayerId == Target);

        if (cTargets.Count == 0)
        {
            Logger.Warn("No Targets Available", "BountyHunter");
            return 0xff;
        }

        PlayerControl target = cTargets.RandomElement();
        byte targetId = target.PlayerId;
        Target = targetId;
        if (ShowTargetArrow) TargetArrow.Add(playerId, targetId);

        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}'s New Target Is {target.GetNameWithRole().RemoveHtmlTags()}", "BountyHunter");

        SendRPC();
        return targetId;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = reader.ReadBoolean() ? new CountdownTimer(TargetChangeTime, () => Timer = null, onCanceled: () => Timer = null) : null;
    }

    public override void AfterMeetingTasks()
    {
        if (!Main.PlayerStates[BountyHunterId].IsDead)
        {
            PlayerControl bh = Utils.GetPlayerById(BountyHunterId);
            if (bh == null) return;
            
            ResetTarget(bh);
        }
    }

    public override void OnReportDeadBody()
    {
        Main.AllPlayerKillCooldown[BountyHunterId] = Options.AdjustedDefaultKillCooldown;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        if (!AmongUsClient.Instance.AmHost) return string.Empty;
        if (Timer.Remaining.TotalSeconds > 15) return base.GetProgressText(playerId, comms);
        return $"{base.GetProgressText(playerId, comms)} <#777777>-</color> {string.Format(GetString("BountyHunterSwapTimer"), (int)Timer.Remaining.TotalSeconds)}";
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.IsModdedClient() && !hud) return string.Empty;
        return GetTargetText(seer, target, hud) + GetTargetArrow(seer, target);
    }

    private static string GetTargetText(PlayerControl bounty, PlayerControl tar, bool hud)
    {
        if (GameStates.IsMeeting || bounty.PlayerId != tar.PlayerId || Main.PlayerStates[bounty.PlayerId].Role is not BountyHunter bh) return string.Empty;

        byte targetId = bh.GetTarget(bounty);
        return targetId != 0xff ? $"<color=#00ffa5>{(hud ? GetString("BountyCurrentTarget") : GetString("Target"))}:</color> <b>{Main.AllPlayerNames[targetId].RemoveHtmlTags().Replace("\r\n", string.Empty)}</b>" : string.Empty;
    }

    private static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
    {
        if ((target != null && seer.PlayerId != target.PlayerId) || !ShowTargetArrow || GameStates.IsMeeting || Main.PlayerStates[seer.PlayerId].Role is not BountyHunter bh) return string.Empty;

        byte targetId = bh.GetTarget(seer);
        return $"<color=#ffffff> {TargetArrow.GetArrows(seer, targetId)}</color>";
    }
}