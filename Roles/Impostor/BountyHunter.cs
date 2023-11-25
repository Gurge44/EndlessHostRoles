using Hazel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public static class BountyHunter
{
    private static readonly int Id = 800;
    private static List<byte> playerIdList = [];

    private static OptionItem OptionTargetChangeTime;
    private static OptionItem OptionSuccessKillCooldown;
    private static OptionItem OptionFailureKillCooldown;
    private static OptionItem OptionShowTargetArrow;

    public static float TargetChangeTime;
    private static float SuccessKillCooldown;
    private static float FailureKillCooldown;
    private static bool ShowTargetArrow;

    private static Dictionary<byte, int> Timer;
    public static Dictionary<byte, byte> Targets = [];
    public static Dictionary<byte, float> ChangeTimer = [];

    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.BountyHunter, 1);
        OptionTargetChangeTime = FloatOptionItem.Create(Id + 10, "BountyTargetChangeTime", new(10f, 180f, 2.5f), 60f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);
        OptionSuccessKillCooldown = FloatOptionItem.Create(Id + 11, "BountySuccessKillCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);
        OptionFailureKillCooldown = FloatOptionItem.Create(Id + 12, "BountyFailureKillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
            .SetValueFormat(OptionFormat.Seconds);
        OptionShowTargetArrow = BooleanOptionItem.Create(Id + 13, "BountyShowTargetArrow", true, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter]);
    }
    public static void Init()
    {
        playerIdList = [];
        IsEnable = false;

        Targets = [];
        ChangeTimer = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        IsEnable = true;

        TargetChangeTime = OptionTargetChangeTime.GetFloat();
        SuccessKillCooldown = OptionSuccessKillCooldown.GetFloat();
        FailureKillCooldown = OptionFailureKillCooldown.GetFloat();
        ShowTargetArrow = OptionShowTargetArrow.GetBool();

        Timer[playerId] = (int)TargetChangeTime;

        if (AmongUsClient.Instance.AmHost)
            ResetTarget(Utils.GetPlayerById(playerId));
    }
    public static bool IsEnable;
    private static void SendRPC(byte bountyId, byte targetId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBountyTarget, SendOption.Reliable, -1);
        writer.Write(bountyId);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte bountyId = reader.ReadByte();
        byte targetId = reader.ReadByte();

        Targets[bountyId] = targetId;
        if (ShowTargetArrow) TargetArrow.Add(bountyId, targetId);
    }
    //public static void SetKillCooldown(byte id, float amount) => Main.AllPlayerKillCooldown[id] = amount;
    //public static void ApplyGameOptions()
    //{
    //    AURoleOptions.ShapeshifterCooldown = TargetChangeTime;
    //    AURoleOptions.ShapeshifterDuration = 1f;
    //}

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (GetTarget(killer) == target.PlayerId)
        {//ターゲットをキルした場合
            Logger.Info($"{killer?.Data?.PlayerName}:ターゲットをキル", "BountyHunter");
            Main.AllPlayerKillCooldown[killer.PlayerId] = SuccessKillCooldown;
            killer.SyncSettings();//キルクール処理を同期
            ResetTarget(killer);
        }
        else
        {
            Logger.Info($"{killer?.Data?.PlayerName}:ターゲット以外をキル", "BountyHunter");
            Main.AllPlayerKillCooldown[killer.PlayerId] = FailureKillCooldown;
            killer.SyncSettings();//キルクール処理を同期
        }
    }
    public static void OnReportDeadBody()
    {
        ChangeTimer.Clear();
    }
    public static void FixedUpdate(PlayerControl player)
    {
        if (!player.Is(CustomRoles.BountyHunter)) return; //以下、バウンティハンターのみ実行

        if (GameStates.IsInTask && ChangeTimer.ContainsKey(player.PlayerId))
        {
            if (!player.IsAlive())
                ChangeTimer.Remove(player.PlayerId);
            else
            {
                var targetId = GetTarget(player);
                if (ChangeTimer[player.PlayerId] >= TargetChangeTime)//時間経過でターゲットをリセットする処理
                {
                    var newTargetId = ResetTarget(player);//ターゲットの選びなおし
                    Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: Utils.GetPlayerById(newTargetId));
                }
                if (ChangeTimer[player.PlayerId] >= 0)
                {
                    ChangeTimer[player.PlayerId] += Time.fixedDeltaTime;
                    int tempTimer = Timer[player.PlayerId];
                    Timer[player.PlayerId] = (int)(TargetChangeTime - ChangeTimer[player.PlayerId]);
                    if (tempTimer != Timer[player.PlayerId] && Timer[player.PlayerId] <= 15 && !player.IsModClient()) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                }


                //BountyHunterのターゲット更新
                if (Main.PlayerStates[targetId].IsDead)
                {
                    ResetTarget(player);
                    Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}のターゲットが無効だったため、ターゲットを更新しました", "BountyHunter");
                    Utils.NotifyRoles(SpecifySeer: player);
                }
            }
        }
    }
    public static byte GetTarget(PlayerControl player)
    {
        if (player == null) return 0xff;
        Targets ??= [];

        if (!Targets.TryGetValue(player.PlayerId, out var targetId))
            targetId = ResetTarget(player);
        return targetId;
    }
    public static PlayerControl GetTargetPC(PlayerControl player)
    {
        var targetId = GetTarget(player);
        return targetId == 0xff ? null : Utils.GetPlayerById(targetId);
    }
    public static byte ResetTarget(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return 0xff;

        var playerId = player.PlayerId;

        ChangeTimer[playerId] = 0f;
        Timer[playerId] = (int)TargetChangeTime;

        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}:ターゲットリセット", "BountyHunter");
        //player.RpcResetAbilityCooldown(); ;//タイマー（変身クールダウン）のリセットと

        var cTargets = new List<PlayerControl>(Main.AllAlivePlayerControls.Where(pc => !pc.Is(CustomRoleTypes.Impostor)));

        if (cTargets.Count >= 2 && Targets.TryGetValue(player.PlayerId, out var nowTarget))
            cTargets.RemoveAll(x => x.PlayerId == nowTarget); //前回のターゲットは除外

        if (!cTargets.Any())
        {
            Logger.Warn("ターゲットの指定に失敗しました:ターゲット候補が存在しません", "BountyHunter");
            return 0xff;
        }

        var rand = IRandom.Instance;
        var target = cTargets[rand.Next(0, cTargets.Count)];
        var targetId = target.PlayerId;
        Targets[playerId] = targetId;
        if (ShowTargetArrow) TargetArrow.Add(playerId, targetId);
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}のターゲットを{target.GetNameWithRole().RemoveHtmlTags()}に変更", "BountyHunter");

        //RPCによる同期
        SendRPC(player.PlayerId, targetId);
        return targetId;
    }
    //public static void SetAbilityButtonText(HudManager __instance) => __instance.AbilityButton.OverrideText(GetString("BountyHunterChangeButtonText"));
    public static void AfterMeetingTasks()
    {
        foreach (byte id in playerIdList.ToArray())
        {
            if (!Main.PlayerStates[id].IsDead)
            {
                ChangeTimer[id] = 0f;
                Timer[id] = (int)TargetChangeTime;
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
        var targetId = GetTarget(bounty);
        return targetId != 0xff ? $"<color=#00ffa5>{(hud ? GetString("BountyCurrentTarget") : GetString("Target"))}:</color> <b>{Main.AllPlayerNames[targetId].RemoveHtmlTags().Replace("\r\n", string.Empty)}</b>" : string.Empty;
    }
    public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (!seer.Is(CustomRoles.BountyHunter)) return string.Empty;
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
        if (!ShowTargetArrow || GameStates.IsMeeting) return string.Empty;

        //seerがtarget自身でBountyHunterのとき、
        //矢印オプションがありミーティング以外で矢印表示
        var targetId = GetTarget(seer);
        return $"<color=#ffffff> {TargetArrow.GetArrows(seer, targetId)}</color>";
    }
}
