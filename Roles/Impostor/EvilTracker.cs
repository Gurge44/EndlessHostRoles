using AmongUs.GameOptions;
using Hazel;
using Il2CppSystem.Text;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public static class EvilTracker
{
    private static readonly int Id = 500;
    private static List<byte> playerIdList = [];

    private static OptionItem OptionCanSeeKillFlash;
    private static OptionItem OptionTargetMode;
    private static OptionItem OptionCanSeeLastRoomInMeeting;

    private static bool CanSeeKillFlash;
    private static TargetMode CurrentTargetMode;
    public static RoleTypes RoleTypes;
    public static bool CanSeeLastRoomInMeeting;

    private enum TargetMode
    {
        Never,
        OnceInGame,
        EveryMeeting,
        Always,
    };
    private static readonly string[] TargetModeText =
    [
        "EvilTrackerTargetMode.Never",
        "EvilTrackerTargetMode.OnceInGame",
        "EvilTrackerTargetMode.EveryMeeting",
        "EvilTrackerTargetMode.Always",
    ];

    public static Dictionary<byte, byte> Target = [];
    public static Dictionary<byte, bool> CanSetTarget = [];
    private static Dictionary<byte, HashSet<byte>> ImpostorsId = [];
    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.EvilTracker);
        OptionCanSeeKillFlash = BooleanOptionItem.Create(Id + 10, "EvilTrackerCanSeeKillFlash", true, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilTracker]);
        OptionTargetMode = StringOptionItem.Create(Id + 11, "EvilTrackerTargetMode", TargetModeText, 2, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilTracker]);
        OptionCanSeeLastRoomInMeeting = BooleanOptionItem.Create(Id + 12, "EvilTrackerCanSeeLastRoomInMeeting", false, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilTracker]);
    }
    public static void Init()
    {
        playerIdList = [];
        Target = [];
        CanSetTarget = [];
        ImpostorsId = [];

        CanSeeKillFlash = OptionCanSeeKillFlash.GetBool();
        CurrentTargetMode = (TargetMode)OptionTargetMode.GetValue();
        RoleTypes = CurrentTargetMode == TargetMode.Never ? RoleTypes.Impostor : RoleTypes.Shapeshifter;
        CanSeeLastRoomInMeeting = OptionCanSeeLastRoomInMeeting.GetBool();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        Target.Add(playerId, byte.MaxValue);
        CanSetTarget.Add(playerId, CurrentTargetMode != TargetMode.Never);
        //ImpostorsIdはEvilTracker内で共有
        ImpostorsId[playerId] = [];
        foreach (PlayerControl target in Main.AllAlivePlayerControls)
        {
            var targetId = target.PlayerId;
            if (targetId != playerId && target.Is(CustomRoleTypes.Impostor))
            {
                ImpostorsId[playerId].Add(targetId);
                TargetArrow.Add(playerId, targetId);
            }
        }
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void ApplyGameOptions(byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = CanTarget(playerId) ? 1f : 255f;
        AURoleOptions.ShapeshifterDuration = 1f;
    }
    public static void GetAbilityButtonText(HudManager __instance, byte playerId)
    {
        __instance.AbilityButton.ToggleVisible(CanTarget(playerId));
        __instance.AbilityButton.OverrideText(GetString("EvilTrackerChangeButtonText"));
    }

    // 値取得の関数
    private static bool CanTarget(byte playerId)
        => !Main.PlayerStates[playerId].IsDead && CanSetTarget.TryGetValue(playerId, out var value) && value;
    private static byte GetTargetId(byte playerId)
        => Target.TryGetValue(playerId, out var targetId) ? targetId : byte.MaxValue;
    public static bool IsTrackTarget(PlayerControl seer, PlayerControl target)
        => seer.IsAlive() && playerIdList.Contains(seer.PlayerId)
        && target.IsAlive() && seer != target
        && (target.Is(CustomRoleTypes.Impostor) || GetTargetId(seer.PlayerId) == target.PlayerId);
    public static bool KillFlashCheck(PlayerControl killer, PlayerControl target)
    {
        if (!CanSeeKillFlash) return false;
        //インポスターによるキルかどうかの判別
        var realKiller = target.GetRealKiller() ?? killer;
        return realKiller.Is(CustomRoleTypes.Impostor) && realKiller != target;
    }

    // 各所で呼ばれる処理
    public static void OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!CanTarget(shapeshifter.PlayerId) || !shapeshifting) return;
        if (target == null || target.Is(CustomRoleTypes.Impostor)) return;

        SetTarget(shapeshifter.PlayerId, target.PlayerId);
        Logger.Info($"{shapeshifter.GetNameWithRole().RemoveHtmlTags()}のターゲットを{target.GetNameWithRole().RemoveHtmlTags()}に設定", "EvilTrackerTarget");
        shapeshifter.MarkDirtySettings();
        Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: shapeshifter);
    }
    public static void AfterMeetingTasks()
    {
        try
        {
            if (CurrentTargetMode == TargetMode.EveryMeeting)
            {
                SetTarget();
            }
            foreach (byte playerId in playerIdList.ToArray())
            {
                var pc = Utils.GetPlayerById(playerId);
                var target = Utils.GetPlayerById(GetTargetId(playerId));
                if (!pc.IsAlive() || !target.IsAlive())
                    SetTarget(playerId);
                pc?.SyncSettings();
                pc?.RpcResetAbilityCooldown();
                target?.MarkDirtySettings();
            }
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex.ToString(), "EvilTracker.AfterMeetingTasks");
        }
    }
    ///<summary>
    ///引数が両方空：再設定可能に,
    ///trackerIdのみ：該当IDのターゲット削除,
    ///trackerIdとtargetId両方あり：該当IDのプレイヤーをターゲットに設定
    ///</summary>
    public static void SetTarget(byte trackerId = byte.MaxValue, byte targetId = byte.MaxValue)
    {
        if (trackerId == byte.MaxValue) // ターゲット再設定可能に
        {
            foreach (byte playerId in playerIdList.ToArray())
            {
                CanSetTarget[playerId] = true;
            }
        }
        else if (targetId == byte.MaxValue) // ターゲット削除
            Target[trackerId] = byte.MaxValue;
        else
        {
            Target[trackerId] = targetId; // ターゲット設定
            if (CurrentTargetMode != TargetMode.Always)
                CanSetTarget[trackerId] = false; // ターゲット再設定不可に
            TargetArrow.Add(trackerId, targetId);
        }

        if (!AmongUsClient.Instance.AmHost) return;
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetEvilTrackerTarget, SendOption.Reliable, -1);
        writer.Write(trackerId);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte trackerId = reader.ReadByte();
        byte targetId = reader.ReadByte();
        SetTarget(trackerId, targetId);
    }

    // 表示系の関数
    public static string GetMarker(byte playerId) => CanTarget(playerId) ? Utils.ColorString(Palette.ImpostorRed.ShadeColor(0.5f), "◁") : string.Empty;
    public static string GetTargetMark(PlayerControl seer, PlayerControl target) => GetTargetId(seer.PlayerId) == target.PlayerId ? Utils.ColorString(Palette.ImpostorRed, "◀") : string.Empty;
    public static string GetTargetArrow(PlayerControl seer, PlayerControl target)
    {
        if (!GameStates.IsInTask || !target.Is(CustomRoles.EvilTracker)) return string.Empty;

        var trackerId = target.PlayerId;
        if (seer.PlayerId != trackerId) return string.Empty;

        ImpostorsId[trackerId].RemoveWhere(id => Main.PlayerStates[id].IsDead);

        var sb = new StringBuilder(80);
        if (ImpostorsId[trackerId].Count > 0)
        {
            sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Impostor)}>");
            foreach (var impostorId in ImpostorsId[trackerId])
            {
                sb.Append(TargetArrow.GetArrows(target, impostorId));
            }
            sb.Append($"</color>");
        }

        var targetId = Target[trackerId];
        if (targetId != byte.MaxValue)
        {
            sb.Append(Utils.ColorString(Color.white, TargetArrow.GetArrows(target, targetId)));
        }
        return sb.ToString();
    }
    public static string GetArrowAndLastRoom(PlayerControl seer, PlayerControl target)
    {
        string text = Utils.ColorString(Palette.ImpostorRed, TargetArrow.GetArrows(seer, target.PlayerId));
        var room = Main.PlayerStates[target.PlayerId].LastRoom;
        if (room == null) text += Utils.ColorString(Color.gray, "@" + GetString("FailToTrack"));
        else text += Utils.ColorString(Palette.ImpostorRed, "@" + GetString(room.RoomId.ToString()));
        return text;
    }
}