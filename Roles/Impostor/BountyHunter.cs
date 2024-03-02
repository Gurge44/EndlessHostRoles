using System.Collections.Generic;
using System.Linq;
using Hazel;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public class BountyHunter : RoleBase
{
    private const int Id = 800;
    private static List<byte> playerIdList = [];

    private static OptionItem OptionTargetChangeTime;
    private static OptionItem OptionSuccessKillCooldown;
    private static OptionItem OptionFailureKillCooldown;
    private static OptionItem OptionShowTargetArrow;

    public static float TargetChangeTime;
    private static float SuccessKillCooldown;
    private static float FailureKillCooldown;
    private static bool ShowTargetArrow;

    private int Timer;
    public byte Target;
    public float ChangeTimer;
    private byte BountyId;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.BountyHunter);
        OptionTargetChangeTime = FloatOptionItem.Create(Id + 10, "BountyTargetChangeTime", new(10f, 180f, 2.5f), 60f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);
        OptionSuccessKillCooldown = FloatOptionItem.Create(Id + 11, "BountySuccessKillCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);
        OptionFailureKillCooldown = FloatOptionItem.Create(Id + 12, "BountyFailureKillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);
        OptionShowTargetArrow = BooleanOptionItem.Create(Id + 13, "BountyShowTargetArrow", true, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter]);
    }

    public override void Init()
    {
        playerIdList = [];

        Target = byte.MaxValue;
        ChangeTimer = OptionTargetChangeTime.GetFloat();
        Timer = OptionTargetChangeTime.GetInt();
        BountyId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        BountyId = playerId;

        TargetChangeTime = OptionTargetChangeTime.GetFloat();
        SuccessKillCooldown = OptionSuccessKillCooldown.GetFloat();
        FailureKillCooldown = OptionFailureKillCooldown.GetFloat();
        ShowTargetArrow = OptionShowTargetArrow.GetBool();

        Timer = (int)TargetChangeTime;

        Target = byte.MaxValue;
        ChangeTimer = TargetChangeTime;

        if (AmongUsClient.Instance.AmHost)
            ResetTarget(Utils.GetPlayerById(playerId));
    }

    public override bool IsEnable => playerIdList.Count > 0;

    void SendRPC()
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBountyTarget, SendOption.Reliable);
        writer.Write(BountyId);
        writer.Write(Target);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(byte bountyId, byte targetId)
    {
        Target = targetId;
        if (ShowTargetArrow) TargetArrow.Add(bountyId, targetId);
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

    public override void OnReportDeadBody()
    {
        ChangeTimer = TargetChangeTime;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || float.IsNaN(ChangeTimer)) return;

        if (!player.IsAlive()) ChangeTimer = float.NaN;
        else
        {
            var targetId = GetTarget(player);
            if (ChangeTimer >= TargetChangeTime)
            {
                var newTargetId = ResetTarget(player);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: Utils.GetPlayerById(newTargetId));
            }

            if (ChangeTimer >= 0)
            {
                ChangeTimer += Time.fixedDeltaTime;
                int tempTimer = Timer;
                Timer = (int)(TargetChangeTime - ChangeTimer);
                if (tempTimer != Timer && Timer <= 15 && !player.IsModClient()) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
            }

            if (Main.PlayerStates[targetId].IsDead)
            {
                ResetTarget(player);
                Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}のターゲットが無効だったため、ターゲットを更新しました", "BountyHunter");
                Utils.NotifyRoles(SpecifySeer: player);
            }
        }
    }

    public byte GetTarget(PlayerControl player)
    {
        if (player == null) return 0xff;

        byte targetId = Target == byte.MaxValue ? ResetTarget(player) : Target;
        return targetId;
    }

    public byte ResetTarget(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return 0xff;

        var playerId = player.PlayerId;

        ChangeTimer = 0f;
        Timer = (int)TargetChangeTime;

        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: Reset Target", "BountyHunter");

        var cTargets = new List<PlayerControl>(Main.AllAlivePlayerControls.Where(pc => !pc.Is(CustomRoleTypes.Impostor)));

        if (cTargets.Count >= 2 && Target != byte.MaxValue)
            cTargets.RemoveAll(x => x.PlayerId == Target);

        if (cTargets.Count == 0)
        {
            Logger.Warn("No Targets Available", "BountyHunter");
            return 0xff;
        }

        var rand = IRandom.Instance;
        var target = cTargets[rand.Next(0, cTargets.Count)];
        var targetId = target.PlayerId;
        Target = targetId;
        if (ShowTargetArrow) TargetArrow.Add(playerId, targetId);
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}'s New Target Is {target.GetNameWithRole().RemoveHtmlTags()}", "BountyHunter");

        SendRPC();
        return targetId;
    }

    public override void AfterMeetingTasks()
    {
        foreach (byte id in playerIdList.ToArray())
        {
            if (!Main.PlayerStates[id].IsDead)
            {
                ChangeTimer = 0f;
                Timer = (int)TargetChangeTime;
                if (Utils.GetPlayerById(id).GetCustomRole() == CustomRoles.BountyHunter)
                {
                    Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;
                    Utils.GetPlayerById(id).SyncSettings();
                }
            }
        }
    }

    public static string GetTargetText(PlayerControl bounty, bool hud)
    {
        if (GameStates.IsMeeting) return string.Empty;
        if (Main.PlayerStates[bounty.PlayerId].Role is not BountyHunter bh) return string.Empty;

        var targetId = bh.GetTarget(bounty);
        return targetId != 0xff ? $"<color=#00ffa5>{(hud ? GetString("BountyCurrentTarget") : GetString("Target"))}:</color> <b>{Main.AllPlayerNames[targetId].RemoveHtmlTags().Replace("\r\n", string.Empty)}</b>" : string.Empty;
    }

    public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
        if (!ShowTargetArrow || GameStates.IsMeeting) return string.Empty;
        if (Main.PlayerStates[seer.PlayerId].Role is not BountyHunter bh) return string.Empty;

        var targetId = bh.GetTarget(seer);
        return $"<color=#ffffff> {TargetArrow.GetArrows(seer, targetId)}</color>";
    }
}