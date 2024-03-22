using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using Il2CppSystem.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Impostor;

public class EvilTracker : RoleBase
{
    private const int Id = 500;
    private static List<byte> playerIdList = [];

    private static OptionItem OptionCanSeeKillFlash;
    private static OptionItem OptionTargetMode;
    private static OptionItem OptionCanSeeLastRoomInMeeting;

    private static bool CanSeeKillFlash;
    private static TargetMode CurrentTargetMode;
    public static RoleTypes RoleTypes;
    public static bool CanSeeLastRoomInMeeting;

#pragma warning disable IDE0079 // Remove unnecessary suppression
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
    private enum TargetMode
    {
        Never,
        OnceInGame,
        EveryMeeting,
        Always
    }

    private static readonly string[] TargetModeText =
    [
        "EvilTrackerTargetMode.Never",
        "EvilTrackerTargetMode.OnceInGame",
        "EvilTrackerTargetMode.EveryMeeting",
        "EvilTrackerTargetMode.Always",
    ];

    private byte EvilTrackerId;
    public byte Target = byte.MaxValue;
    public bool CanSetTarget;
    private byte[] ImpostorsId => Main.AllAlivePlayerControls.Where(x => x.PlayerId != EvilTrackerId && x.Is(CustomRoleTypes.Impostor)).Select(x => x.PlayerId).ToArray();

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

    public override void Init()
    {
        playerIdList = [];
        Target = byte.MaxValue;
        CanSetTarget = false;
        EvilTrackerId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        CanSeeKillFlash = OptionCanSeeKillFlash.GetBool();
        CurrentTargetMode = (TargetMode)OptionTargetMode.GetValue();
        RoleTypes = CurrentTargetMode == TargetMode.Never ? RoleTypes.Impostor : RoleTypes.Shapeshifter;
        CanSeeLastRoomInMeeting = OptionCanSeeLastRoomInMeeting.GetBool();

        playerIdList.Add(playerId);
        Target = byte.MaxValue;
        CanSetTarget = CurrentTargetMode != TargetMode.Never;
        EvilTrackerId = playerId;

        _ = new LateTask(() =>
        {
            foreach (var id in ImpostorsId)
            {
                TargetArrow.Add(playerId, id);
            }
        }, 3f, "Add Evil Tracker Arrows");
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (RoleTypes != RoleTypes.Shapeshifter) return;
        AURoleOptions.ShapeshifterCooldown = CanTarget(playerId) ? 1f : 255f;
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override void SetButtonTexts(HudManager __instance, byte playerId)
    {
        __instance.AbilityButton.ToggleVisible(CanTarget(playerId));
        __instance.AbilityButton.OverrideText(GetString("EvilTrackerChangeButtonText"));
    }

    private bool CanTarget(byte playerId) => !Main.PlayerStates[playerId].IsDead && CanSetTarget;

    public static bool IsTrackTarget(PlayerControl seer, PlayerControl target) =>
        Main.PlayerStates[seer.PlayerId].Role is EvilTracker et
        && seer.IsAlive() && playerIdList.Contains(seer.PlayerId)
        && target.IsAlive() && seer != target
        && (target.Is(CustomRoleTypes.Impostor) || et.Target == target.PlayerId);

    public static bool KillFlashCheck(PlayerControl killer, PlayerControl target)
    {
        if (!CanSeeKillFlash) return false;
        var realKiller = target.GetRealKiller() ?? killer;
        return realKiller.Is(CustomRoleTypes.Impostor) && realKiller != target;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!CanTarget(shapeshifter.PlayerId) || !shapeshifting) return false;
        if (target == null || target.Is(CustomRoleTypes.Impostor)) return false;

        SetTarget(shapeshifter.PlayerId, target.PlayerId);
        Logger.Info($"{shapeshifter.GetNameWithRole().RemoveHtmlTags()}'s target is now {target.GetNameWithRole().RemoveHtmlTags()}", "EvilTrackerTarget");
        shapeshifter.MarkDirtySettings();
        Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: shapeshifter);

        return false;
    }

    public override void AfterMeetingTasks()
    {
        try
        {
            if (CurrentTargetMode == TargetMode.EveryMeeting)
            {
                SetTarget();
            }

            foreach (byte playerId in playerIdList)
            {
                var pc = Utils.GetPlayerById(playerId);
                var target = Utils.GetPlayerById(Target);
                try
                {
                    if (!pc.IsAlive() || !target.IsAlive())
                        SetTarget(playerId);
                }
                catch (NullReferenceException)
                {
                }

                pc?.SyncSettings();
                pc?.RpcResetAbilityCooldown();
                target?.MarkDirtySettings();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString(), "EvilTracker.AfterMeetingTasks");
        }
    }

    public void SetTarget(byte trackerId = byte.MaxValue, byte targetId = byte.MaxValue)
    {
        if (trackerId == byte.MaxValue) CanSetTarget = true;
        else if (targetId == byte.MaxValue) Target = byte.MaxValue;
        else
        {
            Target = targetId;
            if (CurrentTargetMode != TargetMode.Always) CanSetTarget = false;
            TargetArrow.Add(trackerId, targetId);
        }

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetEvilTrackerTarget, SendOption.Reliable);
        writer.Write(trackerId);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte trackerId = reader.ReadByte();
        byte targetId = reader.ReadByte();
        (Main.PlayerStates[trackerId].Role as EvilTracker)?.SetTarget(trackerId, targetId);
    }

    public override string GetProgressText(byte playerId, bool comms) => Main.PlayerStates[playerId].Role is not EvilTracker et ? null : et.CanTarget(playerId) ? Utils.ColorString(Palette.ImpostorRed.ShadeColor(0.5f), "◁") : string.Empty;
    public static string GetTargetMark(PlayerControl seer, PlayerControl target) => Main.PlayerStates[seer.PlayerId].Role is not EvilTracker et ? string.Empty : et.Target == target.PlayerId ? Utils.ColorString(Palette.ImpostorRed, "◀") : string.Empty;

    public static string GetTargetArrow(PlayerControl seer, PlayerControl target)
    {
        if (!GameStates.IsInTask) return string.Empty;

        var trackerId = target.PlayerId;
        if (seer.PlayerId != trackerId) return string.Empty;

        if (Main.PlayerStates[seer.PlayerId].Role is not EvilTracker et) return string.Empty;

        var imps = et.ImpostorsId;
        var sb = new StringBuilder(80);
        if (imps.Length > 0)
        {
            sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Impostor)}>");
            foreach (var impostorId in imps)
            {
                sb.Append(TargetArrow.GetArrows(target, impostorId));
            }

            sb.Append("</color>");
        }

        var targetId = et.Target;
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