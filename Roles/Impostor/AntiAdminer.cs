using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Roles.Crewmate;
using EHR.Roles.Neutral;
using UnityEngine;

namespace EHR.Roles.Impostor;

// Reference: https://github.com/ykundesu/SuperNewRoles/blob/master/SuperNewRoles/Mode/SuperHostRoles/BlockTool.cs
// Reference：https://github.com/Yumenopai/TownOfHost_Y/tree/AntiAdminer
internal class AntiAdminer : RoleBase
{
    private const int Id = 2300;
    private static List<byte> playerIdList = [];

    private static OptionItem CanCheckCamera;
    public static OptionItem EnableExtraAbility;
    private static OptionItem CanOnlyUseWhileAnyWatch;
    private static OptionItem Delay;

    public static bool IsAdminWatch;
    public static bool IsVitalWatch;
    public static bool IsDoorLogWatch;
    public static bool IsCameraWatch;
    public static List<byte> PlayersNearDevices = [];

    private int Count;
    private long ExtraAbilityStartTimeStamp;

    private bool IsMonitor;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.AntiAdminer);
        CanCheckCamera = new BooleanOptionItem(Id + 10, "CanCheckCamera", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.AntiAdminer]);
        EnableExtraAbility = new BooleanOptionItem(Id + 11, "EnableExtraAbility", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.AntiAdminer]);
        CanOnlyUseWhileAnyWatch = new BooleanOptionItem(Id + 12, "CanOnlyUseWhileAnyWatch", true, TabGroup.ImpostorRoles)
            .SetParent(EnableExtraAbility);
        Delay = new FloatOptionItem(Id + 13, "AADelay", new(0f, 20f, 0.5f), 5f, TabGroup.ImpostorRoles)
            .SetParent(EnableExtraAbility)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        playerIdList = [];
        PlayersNearDevices = [];
        IsAdminWatch = false;
        IsVitalWatch = false;
        IsDoorLogWatch = false;
        IsCameraWatch = false;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        IsMonitor = Main.PlayerStates[playerId].MainRole == CustomRoles.Monitor;
        ExtraAbilityStartTimeStamp = 0;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        if (IsMonitor || !EnableExtraAbility.GetBool() || ExtraAbilityStartTimeStamp > 0 || (CanOnlyUseWhileAnyWatch.GetBool() && !IsAdminWatch && !IsVitalWatch && !IsDoorLogWatch && !IsCameraWatch)) return false;

        ExtraAbilityStartTimeStamp = Utils.TimeStamp;
        shapeshifter.RpcResetAbilityCooldown();
        Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: shapeshifter);

        foreach (var pc in PlayersNearDevices.Select(x => Utils.GetPlayerById(x)).Where(x => x != null && x.IsAlive()))
        {
            pc.Notify(Translator.GetString("AAWarning"), Delay.GetFloat());
        }

        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (!IsMonitor)
        {
            if (EnableExtraAbility.GetBool())
            {
                AURoleOptions.ShapeshifterCooldown = Math.Clamp(Options.DefaultKillCooldown - Delay.GetFloat() - 2f, Delay.GetFloat() + 1f, Options.DefaultKillCooldown);
                AURoleOptions.ShapeshifterDuration = 1f;
            }

            return;
        }

        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!IsEnable) return;

        bool notify = false;

        if (!IsMonitor && ExtraAbilityStartTimeStamp > 0 && EnableExtraAbility.GetBool())
        {
            if (ExtraAbilityStartTimeStamp + Delay.GetInt() < Utils.TimeStamp)
            {
                ExtraAbilityStartTimeStamp = 0;
                player.RpcResetAbilityCooldown();
                player.Notify(Translator.GetString("AADone"));

                foreach (var pc in PlayersNearDevices.Select(x => Utils.GetPlayerById(x)).Where(x => x != null && x.IsAlive() && player.RpcCheckAndMurder(x, check: true)))
                {
                    pc.Suicide(realKiller: player);
                }
            }
            else
            {
                notify = true;
                foreach (var pc in PlayersNearDevices.Select(x => Utils.GetPlayerById(x)).Where(x => x != null && x.IsAlive()))
                {
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                }
            }
        }

        Count--;
        if (Count > 0) return;
        Count = notify || ExtraAbilityStartTimeStamp > 0 ? 1 : 5;

        PlayersNearDevices = [];
        bool Admin = false, Camera = false, DoorLog = false, Vital = false;
        float usableDistance = DisableDevice.UsableDistance();
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (Pelican.IsEaten(pc.PlayerId) || pc.inVent || pc.GetCustomRole().IsImpostor()) continue;
            try
            {
                Vector2 PlayerPos = pc.Pos();
                bool isNearDevice = false;

                switch (Main.NormalOptions.MapId)
                {
                    case 0:
                        if (!Options.DisableSkeldAdmin.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["SkeldAdmin"]) <= usableDistance;
                            Admin |= near;
                            isNearDevice |= near;
                        }

                        if (!Options.DisableSkeldCamera.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["SkeldCamera"]) <= usableDistance;
                            Camera |= near;
                            isNearDevice |= near;
                        }

                        break;
                    case 1:
                        if (!Options.DisableMiraHQAdmin.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["MiraHQAdmin"]) <= usableDistance;
                            Admin |= near;
                            isNearDevice |= near;
                        }

                        if (!Options.DisableMiraHQDoorLog.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["MiraHQDoorLog"]) <= usableDistance;
                            DoorLog |= near;
                            isNearDevice |= near;
                        }

                        break;
                    case 2:
                        if (!Options.DisablePolusAdmin.GetBool())
                        {
                            bool nearLeft = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["PolusLeftAdmin"]) <= usableDistance;
                            Admin |= nearLeft;
                            isNearDevice |= nearLeft;
                            bool nearRight = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["PolusRightAdmin"]) <= usableDistance;
                            Admin |= nearRight;
                            isNearDevice |= nearRight;
                        }

                        if (!Options.DisablePolusCamera.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["PolusCamera"]) <= usableDistance;
                            Camera |= near;
                            isNearDevice |= near;
                        }

                        if (!Options.DisablePolusVital.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["PolusVital"]) <= usableDistance;
                            Vital |= near;
                            isNearDevice |= near;
                        }

                        break;
                    case 3:
                        if (!Options.DisableSkeldAdmin.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["DleksAdmin"]) <= usableDistance;
                            Admin |= near;
                            isNearDevice |= near;
                        }

                        if (!Options.DisableSkeldCamera.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["DleksCamera"]) <= usableDistance;
                            Camera |= near;
                            isNearDevice |= near;
                        }

                        break;
                    case 4:
                        if (!Options.DisableAirshipCockpitAdmin.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["AirshipCockpitAdmin"]) <= usableDistance;
                            Admin |= near;
                            isNearDevice |= near;
                        }

                        if (!Options.DisableAirshipRecordsAdmin.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["AirshipRecordsAdmin"]) <= usableDistance;
                            Admin |= near;
                            isNearDevice |= near;
                        }

                        if (!Options.DisableAirshipCamera.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["AirshipCamera"]) <= usableDistance;
                            Camera |= near;
                            isNearDevice |= near;
                        }

                        if (!Options.DisableAirshipVital.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["AirshipVital"]) <= usableDistance;
                            Vital |= near;
                            isNearDevice |= near;
                        }

                        break;
                    case 5:
                        if (!Options.DisableFungleCamera.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["FungleCamera"]) <= usableDistance;
                            Camera |= near;
                            isNearDevice |= near;
                        }

                        if (!Options.DisableFungleVital.GetBool())
                        {
                            bool near = Vector2.Distance(PlayerPos, DisableDevice.DevicePos["FungleVital"]) <= usableDistance;
                            Vital |= near;
                            isNearDevice |= near;
                        }

                        break;
                }

                if (isNearDevice)
                {
                    PlayersNearDevices.Add(pc.PlayerId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "AntiAdminer");
            }
        }

        var isChange = false;

        isChange |= IsAdminWatch != Admin;
        IsAdminWatch = Admin;
        isChange |= IsVitalWatch != Vital;
        IsVitalWatch = Vital;
        isChange |= IsDoorLogWatch != DoorLog;
        IsDoorLogWatch = DoorLog;
        if (IsMonitor ? Monitor.CanCheckCamera.GetBool() : CanCheckCamera.GetBool())
        {
            isChange |= IsCameraWatch != Camera;
            IsCameraWatch = Camera;
        }

        if (notify || isChange)
        {
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
        }

        if (isChange)
        {
            FixedUpdatePatch.DoPostfix(player);
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl _, bool h = false, bool m = false)
    {
        if (Main.PlayerStates[seer.PlayerId].Role is AntiAdminer self && seer.PlayerId == _.PlayerId)
        {
            return self.ExtraAbilityStartTimeStamp > 0
                ? $"<#ffffff>▩ {Delay.GetInt() - (Utils.TimeStamp - self.ExtraAbilityStartTimeStamp):N0}</color>"
                : string.Empty;
        }

        if (!PlayersNearDevices.Contains(seer.PlayerId)) return string.Empty;

        AntiAdminer aa = null;
        foreach (byte id in playerIdList)
        {
            if (Main.PlayerStates[id].Role is not AntiAdminer { IsEnable: true, IsMonitor: false } x || x.ExtraAbilityStartTimeStamp == 0) continue;
            if (aa != null && x.ExtraAbilityStartTimeStamp >= aa.ExtraAbilityStartTimeStamp) continue;
            aa = x;
        }

        return aa == null ? string.Empty : $"<#ffff00>\u26a0 {Delay.GetInt() - (Utils.TimeStamp - aa.ExtraAbilityStartTimeStamp):N0}</color>";
    }
}