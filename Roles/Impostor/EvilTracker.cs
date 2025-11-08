using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

public class EvilTracker : RoleBase
{
    private const int Id = 500;
    private static List<byte> PlayerIdList = [];

    private static OptionItem OptionCanSeeKillFlash;
    private static OptionItem OptionTargetMode;
    private static OptionItem OptionCanSeeLastRoomInMeeting;

    private static bool CanSeeKillFlash;
    private static TargetMode CurrentTargetMode;
    private static RoleTypes RoleTypes;
    public static bool CanSeeLastRoomInMeeting;

    private static readonly string[] TargetModeText =
    [
        "EvilTrackerTargetMode.Never",
        "EvilTrackerTargetMode.OnceInGame",
        "EvilTrackerTargetMode.EveryMeeting",
        "EvilTrackerTargetMode.Always"
    ];

    public bool CanSetTarget;

    private byte EvilTrackerId;
    public byte Target = byte.MaxValue;
    private byte[] ImpostorsId => Main.AllAlivePlayerControls.Where(x => x.PlayerId != EvilTrackerId && x.Is(CustomRoleTypes.Impostor)).Select(x => x.PlayerId).ToArray();

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.EvilTracker);

        OptionCanSeeKillFlash = new BooleanOptionItem(Id + 10, "EvilTrackerCanSeeKillFlash", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilTracker]);

        OptionTargetMode = new StringOptionItem(Id + 11, "EvilTrackerTargetMode", TargetModeText, 2, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilTracker]);

        OptionCanSeeLastRoomInMeeting = new BooleanOptionItem(Id + 12, "EvilTrackerCanSeeLastRoomInMeeting", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.EvilTracker]);
    }

    public override void Init()
    {
        PlayerIdList = [];
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

        PlayerIdList.Add(playerId);
        Target = byte.MaxValue;
        CanSetTarget = CurrentTargetMode != TargetMode.Never;
        EvilTrackerId = playerId;

        LateTask.New(() =>
        {
            foreach (byte id in ImpostorsId) TargetArrow.Add(playerId, id);
        }, 3f, log: false);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

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

    private bool CanTarget(byte playerId)
    {
        return !Main.PlayerStates[playerId].IsDead && CanSetTarget;
    }

    public static bool IsTrackTarget(PlayerControl seer, PlayerControl target)
    {
        return Main.PlayerStates[seer.PlayerId].Role is EvilTracker et
               && seer.IsAlive() && PlayerIdList.Contains(seer.PlayerId)
               && target.IsAlive() && seer != target
               && (target.Is(CustomRoleTypes.Impostor) || et.Target == target.PlayerId);
    }

    public static void OnAnyoneMurder(PlayerControl killer, PlayerControl target)
    {
        if (CanSeeKillFlash && killer != null && killer.Is(CustomRoleTypes.Impostor) && killer != target && !PlayerIdList.Contains(killer.PlayerId))
            PlayerIdList.ToValidPlayers().ForEach(x => x.KillFlash());
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
            if (CurrentTargetMode == TargetMode.EveryMeeting) SetTarget();

            foreach (byte playerId in PlayerIdList)
            {
                PlayerControl pc = Utils.GetPlayerById(playerId);
                PlayerControl target = Utils.GetPlayerById(Target);

                try
                {
                    if (!pc.IsAlive() || !target.IsAlive()) SetTarget(playerId);
                }
                catch (NullReferenceException) { }

                pc?.SyncSettings();
                pc?.RpcResetAbilityCooldown();
                target?.MarkDirtySettings();
            }
        }
        catch (Exception ex) { Logger.Error(ex.ToString(), "EvilTracker.AfterMeetingTasks"); }
    }

    private void SetTarget(byte trackerId = byte.MaxValue, byte targetId = byte.MaxValue)
    {
        if (trackerId == byte.MaxValue)
            CanSetTarget = true;
        else if (targetId == byte.MaxValue)
            Target = byte.MaxValue;
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

    public override string GetProgressText(byte playerId, bool comms)
    {
        return CanTarget(playerId) ? Utils.ColorString(Palette.ImpostorRed.ShadeColor(0.5f), "◁") : string.Empty;
    }

    public static string GetTargetMark(PlayerControl seer, PlayerControl target)
    {
        return Main.PlayerStates[seer.PlayerId].Role is not EvilTracker et ? string.Empty : et.Target == target.PlayerId ? Utils.ColorString(Palette.ImpostorRed, "◀") : string.Empty;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (meeting || hud) return string.Empty;

        byte trackerId = target.PlayerId;
        if (seer.PlayerId != trackerId || seer.PlayerId != EvilTrackerId) return string.Empty;

        byte[] imps = ImpostorsId;
        var sb = new StringBuilder(80);

        if (imps.Length > 0)
        {
            sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Impostor)}>");
            foreach (byte impostorId in imps) sb.Append(TargetArrow.GetArrows(target, impostorId));
            sb.Append("</color>");
        }

        if (Target != byte.MaxValue) sb.Append(Utils.ColorString(Color.white, TargetArrow.GetArrows(target, Target)));

        return sb.ToString();
    }

    public static string GetArrowAndLastRoom(PlayerControl seer, PlayerControl target)
    {
        string text = Utils.ColorString(Palette.ImpostorRed, TargetArrow.GetArrows(seer, target.PlayerId));
        PlainShipRoom room = Main.PlayerStates[target.PlayerId].LastRoom;

        if (room == null)
            text += Utils.ColorString(Color.gray, "@" + GetString("FailToTrack"));
        else
            text += Utils.ColorString(Palette.ImpostorRed, "@" + GetString(room.RoomId.ToString()));

        return text;
    }

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
}