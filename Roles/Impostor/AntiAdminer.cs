﻿using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using Monitor = EHR.Crewmate.Monitor;

namespace EHR.Impostor;

// Reference: https://github.com/ykundesu/SuperNewRoles/blob/master/SuperNewRoles/Mode/SuperHostRoles/BlockTool.cs
// Reference: https://github.com/Yumenopai/TownOfHost_Y/tree/AntiAdminer
internal class AntiAdminer : RoleBase
{
    public enum Device
    {
        Admin,
        Vitals,
        DoorLog,
        Camera
    }

    private const int Id = 2300;
    private static List<byte> PlayerIdList = [];

    private static OptionItem CanCheckCamera;
    public static OptionItem EnableExtraAbility;
    private static OptionItem CanOnlyUseWhileAnyWatch;
    private static OptionItem Delay;

    public static bool IsAdminWatch;
    public static bool IsVitalWatch;
    public static bool IsDoorLogWatch;
    public static bool IsCameraWatch;
    public static Dictionary<byte, HashSet<Device>> PlayersNearDevices = [];
    private byte AntiAdminerId;

    private int Count;
    private long ExtraAbilityStartTimeStamp;

    private bool IsMonitor;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
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
        PlayerIdList = [];
        PlayersNearDevices = [];
        IsAdminWatch = false;
        IsVitalWatch = false;
        IsDoorLogWatch = false;
        IsCameraWatch = false;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        IsMonitor = Main.PlayerStates[playerId].MainRole == CustomRoles.Monitor;
        ExtraAbilityStartTimeStamp = 0;
        AntiAdminerId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (IsMonitor || !EnableExtraAbility.GetBool() || ExtraAbilityStartTimeStamp > 0 || (CanOnlyUseWhileAnyWatch.GetBool() && !IsAdminWatch && !IsVitalWatch && !IsDoorLogWatch && !IsCameraWatch)) return false;

        ExtraAbilityStartTimeStamp = Utils.TimeStamp;

        var sender = CustomRpcSender.Create("AntiAdminer.OnShapeshift", SendOption.Reliable);
        var hasValue = false;

        hasValue |= sender.RpcResetAbilityCooldown(shapeshifter);
        hasValue |= sender.NotifyRolesSpecific(shapeshifter, shapeshifter, out sender, out bool cleared);
        if (cleared) hasValue = false;

        hasValue = PlayersNearDevices.Keys.ToValidPlayers().Where(x => x.IsAlive()).Aggregate(hasValue, (current, pc) => current || sender.Notify(pc, Translator.GetString("AAWarning"), Delay.GetFloat()));

        sender.SendMessage(!hasValue);
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

        var notify = false;

        if (!IsMonitor && ExtraAbilityStartTimeStamp > 0 && EnableExtraAbility.GetBool())
        {
            if (ExtraAbilityStartTimeStamp + Delay.GetInt() < Utils.TimeStamp)
            {
                ExtraAbilityStartTimeStamp = 0;
                player.RpcResetAbilityCooldown();
                player.Notify(Translator.GetString("AADone"));

                foreach (PlayerControl pc in PlayersNearDevices.Keys.ToValidPlayers().Where(x => x.IsAlive() && player.RpcCheckAndMurder(x, true)))
                    pc.Suicide(realKiller: player);
            }
            else
            {
                notify = true;

                foreach (PlayerControl pc in PlayersNearDevices.Keys.ToValidPlayers().Where(x => x.IsAlive()))
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }
        }

        Count--;
        if (Count > 0) return;

        Count = notify || ExtraAbilityStartTimeStamp > 0 ? 1 : 5;

        Dictionary<byte, HashSet<Device>> oldPlayersNearDevices = PlayersNearDevices.ToDictionary(x => x.Key, x => x.Value);
        PlayersNearDevices = [];
        bool Admin = false, Camera = false, DoorLog = false, Vital = false;
        float usableDistance = DisableDevice.UsableDistance;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.inVent || (pc.IsImpostor() && !IsMonitor)) continue;

            try
            {
                Vector2 PlayerPos = pc.Pos();

                switch (Main.NormalOptions.MapId)
                {
                    case 0:
                        if (!Options.DisableSkeldAdmin.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["SkeldAdmin"]) <= usableDistance)
                        {
                            Admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableSkeldCamera.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["SkeldCamera"]) <= usableDistance)
                        {
                            Camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        break;
                    case 1:
                        if (!Options.DisableMiraHQAdmin.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["MiraHQAdmin"]) <= usableDistance)
                        {
                            Admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableMiraHQDoorLog.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["MiraHQDoorLog"]) <= usableDistance)
                        {
                            DoorLog = true;
                            AddDeviceUse(pc.PlayerId, Device.DoorLog);
                        }

                        break;
                    case 2:
                        if (!Options.DisablePolusAdmin.GetBool() && (Vector2.Distance(PlayerPos, DisableDevice.DevicePos["PolusLeftAdmin"]) <= usableDistance || Vector2.Distance(PlayerPos, DisableDevice.DevicePos["PolusRightAdmin"]) <= usableDistance))
                        {
                            Admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisablePolusCamera.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["PolusCamera"]) <= usableDistance)
                        {
                            Camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        if (!Options.DisablePolusVital.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["PolusVital"]) <= usableDistance)
                        {
                            Vital = true;
                            AddDeviceUse(pc.PlayerId, Device.Vitals);
                        }

                        break;
                    case 3:
                        if (!Options.DisableSkeldAdmin.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["DleksAdmin"]) <= usableDistance)
                        {
                            Admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableSkeldCamera.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["DleksCamera"]) <= usableDistance)
                        {
                            Camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        break;
                    case 4:
                        if (!Options.DisableAirshipCockpitAdmin.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["AirshipCockpitAdmin"]) <= usableDistance)
                        {
                            Admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableAirshipRecordsAdmin.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["AirshipRecordsAdmin"]) <= usableDistance)
                        {
                            Admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableAirshipCamera.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["AirshipCamera"]) <= usableDistance)
                        {
                            Camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        if (!Options.DisableAirshipVital.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["AirshipVital"]) <= usableDistance)
                        {
                            Vital = true;
                            AddDeviceUse(pc.PlayerId, Device.Vitals);
                        }

                        break;
                    case 5:
                        if (!Options.DisableFungleCamera.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["FungleCamera"]) <= usableDistance)
                        {
                            Camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        if (!Options.DisableFungleVital.GetBool() && Vector2.Distance(PlayerPos, DisableDevice.DevicePos["FungleVital"]) <= usableDistance)
                        {
                            Vital = true;
                            AddDeviceUse(pc.PlayerId, Device.Vitals);
                        }

                        break;
                }
            }
            catch (Exception ex) { Logger.Error(ex.ToString(), "AntiAdminer"); }
        }

        var change = false;

        change |= IsAdminWatch != Admin;
        IsAdminWatch = Admin;
        change |= IsVitalWatch != Vital;
        IsVitalWatch = Vital;
        change |= IsDoorLogWatch != DoorLog;
        IsDoorLogWatch = DoorLog;

        if (IsMonitor ? Monitor.CanCheckCamera.GetBool() : CanCheckCamera.GetBool())
        {
            change |= IsCameraWatch != Camera;
            IsCameraWatch = Camera;
        }

        if (IsMonitor) notify |= oldPlayersNearDevices.Count != PlayersNearDevices.Count || oldPlayersNearDevices.Any(x => !PlayersNearDevices.TryGetValue(x.Key, out HashSet<Device> c) || !x.Value.SetEquals(c));

        if (notify || change) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);

        return;

        void AddDeviceUse(byte id, Device device)
        {
            if (Main.PlayerStates[id].MainRole is CustomRoles.Monitor or CustomRoles.AntiAdminer) return;

            if (!PlayersNearDevices.TryGetValue(id, out HashSet<Device> devices))
                PlayersNearDevices[id] = [device];
            else devices.Add(device);
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (Main.PlayerStates[seer.PlayerId].Role is AntiAdminer self && seer.PlayerId == target.PlayerId && self.AntiAdminerId == seer.PlayerId)
        {
            return self.ExtraAbilityStartTimeStamp > 0
                ? $"<#ffffff>▩ {Delay.GetInt() - (Utils.TimeStamp - self.ExtraAbilityStartTimeStamp):N0}</color>"
                : string.Empty;
        }

        if (!PlayersNearDevices.ContainsKey(seer.PlayerId)) return string.Empty;

        AntiAdminer aa = null;

        foreach (byte id in PlayerIdList)
        {
            if (Main.PlayerStates[id].Role is not AntiAdminer { IsEnable: true, IsMonitor: false } x || x.ExtraAbilityStartTimeStamp == 0) continue;

            if (aa != null && x.ExtraAbilityStartTimeStamp >= aa.ExtraAbilityStartTimeStamp) continue;

            aa = x;
        }

        return aa == null ? string.Empty : $"<#ffff00>\u26a0 {Delay.GetInt() - (Utils.TimeStamp - aa.ExtraAbilityStartTimeStamp):N0}</color>";
    }
}