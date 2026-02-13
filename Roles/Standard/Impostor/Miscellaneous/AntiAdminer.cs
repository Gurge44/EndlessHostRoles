using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;

namespace EHR.Roles;

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
    public static OptionItem AbilityCooldown;
    private static OptionItem CanOnlyUseWhileAnyWatch;
    private static OptionItem Delay;

    public static bool IsAdminWatch;
    public static bool IsVitalWatch;
    public static bool IsDoorLogWatch;
    public static bool IsCameraWatch;
    public static Dictionary<byte, HashSet<Device>> PlayersNearDevices = [];
    private byte AntiAdminerId;

    private int Count;
    private CountdownTimer ExtraAbilityTimer;

    private bool IsTelecommunication;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.AntiAdminer);

        CanCheckCamera = new BooleanOptionItem(Id + 10, "CanCheckCamera", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.AntiAdminer]);

        AbilityCooldown = new FloatOptionItem(Id + 11, "AbilityCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.AntiAdminer])
            .SetValueFormat(OptionFormat.Seconds);

        CanOnlyUseWhileAnyWatch = new BooleanOptionItem(Id + 12, "CanOnlyUseWhileAnyWatch", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.AntiAdminer]);

        Delay = new FloatOptionItem(Id + 13, "AADelay", new(0f, 20f, 0.5f), 5f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.AntiAdminer])
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
        IsTelecommunication = Main.PlayerStates[playerId].MainRole == CustomRoles.Telecommunication;
        ExtraAbilityTimer = null;
        AntiAdminerId = playerId;
        if (IsTelecommunication && Main.CurrentMap != MapNames.MiraHQ) playerId.SetAbilityUseLimit(Telecommunication.UseLimitOpt.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (IsTelecommunication || ExtraAbilityTimer != null || (CanOnlyUseWhileAnyWatch.GetBool() && !IsAdminWatch && !IsVitalWatch && !IsDoorLogWatch && !IsCameraWatch)) return false;

        ExtraAbilityTimer = new CountdownTimer(Delay.GetInt(), () =>
        {
            ExtraAbilityTimer = null;
            pc.RpcResetAbilityCooldown();
            pc.Notify(Translator.GetString("AADone"));

            foreach (PlayerControl player in PlayersNearDevices.Keys.ToValidPlayers().Where(x => x.IsAlive() && pc.RpcCheckAndMurder(x, true)))
                player.Suicide(realKiller: pc);
        }, onTick: () =>
        {
            foreach (PlayerControl player in PlayersNearDevices.Keys.ToValidPlayers().Where(x => x.IsAlive()))
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
        }, onCanceled: () => ExtraAbilityTimer = null);

        pc.RpcResetAbilityCooldown();
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

        PlayersNearDevices.Keys.ToValidPlayers().Where(x => x.IsAlive()).NotifyPlayers(Translator.GetString("AAWarning"), Delay.GetFloat());
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        OnVanish(shapeshifter);
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (!IsTelecommunication)
        {
            if (Options.UsePhantomBasis.GetBool())
            {
                AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
                AURoleOptions.PhantomDuration = 0.1f;
            }
            else
            {
                AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
                AURoleOptions.ShapeshifterDuration = 0.1f;
            }

            return;
        }

        bool canVent = Telecommunication.CanVent.GetBool();

        AURoleOptions.EngineerCooldown = canVent ? 0f : Telecommunication.VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = canVent ? 0f : 1f;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!IsEnable) return;


        Count--;
        if (Count > 0) return;
        Count = ExtraAbilityTimer != null ? 1 : 5;

        var notify = false;
        Dictionary<byte, HashSet<Device>> oldPlayersNearDevices = PlayersNearDevices.ToDictionary(x => x.Key, x => x.Value);
        PlayersNearDevices = [];
        bool admin = false, camera = false, doorLog = false, vital = false;
        float usableDistance = DisableDevice.UsableDistance;

        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
        {
            if (pc.inVent || (pc.IsImpostor() && !IsTelecommunication)) continue;

            try
            {
                Vector2 playerPos = pc.Pos();

                switch (Main.NormalOptions.MapId)
                {
                    case 0:
                    {
                        if (!Options.DisableSkeldAdmin.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["SkeldAdmin"], usableDistance))
                        {
                            admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableSkeldCamera.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["SkeldCamera"], usableDistance))
                        {
                            camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        break;
                    }
                    case 1:
                    {
                        if (!Options.DisableMiraHQAdmin.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["MiraHQAdmin"], usableDistance))
                        {
                            admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableMiraHQDoorLog.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["MiraHQDoorLog"], usableDistance))
                        {
                            doorLog = true;
                            AddDeviceUse(pc.PlayerId, Device.DoorLog);
                        }

                        break;
                    }
                    case 2:
                    {
                        if (!Options.DisablePolusAdmin.GetBool() && (FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["PolusLeftAdmin"], usableDistance) || FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["PolusRightAdmin"], usableDistance)))
                        {
                            admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisablePolusCamera.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["PolusCamera"], usableDistance))
                        {
                            camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        if (!Options.DisablePolusVital.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["PolusVital"], usableDistance))
                        {
                            vital = true;
                            AddDeviceUse(pc.PlayerId, Device.Vitals);
                        }

                        break;
                    }
                    case 3:
                    {
                        if (!Options.DisableSkeldAdmin.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["DleksAdmin"], usableDistance))
                        {
                            admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableSkeldCamera.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["DleksCamera"], usableDistance))
                        {
                            camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        break;
                    }
                    case 4:
                    {
                        if (!Options.DisableAirshipCockpitAdmin.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["AirshipCockpitAdmin"], usableDistance))
                        {
                            admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableAirshipRecordsAdmin.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["AirshipRecordsAdmin"], usableDistance))
                        {
                            admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (!Options.DisableAirshipCamera.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["AirshipCamera"], usableDistance))
                        {
                            camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        if (!Options.DisableAirshipVital.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["AirshipVital"], usableDistance))
                        {
                            vital = true;
                            AddDeviceUse(pc.PlayerId, Device.Vitals);
                        }

                        break;
                    }
                    case 5:
                    {
                        if (!Options.DisableFungleCamera.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["FungleCamera"], usableDistance))
                        {
                            camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        if (!Options.DisableFungleVital.GetBool() && FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["FungleVital"], usableDistance))
                        {
                            vital = true;
                            AddDeviceUse(pc.PlayerId, Device.Vitals);
                        }

                        break;
                    }
                    case 6 when SubmergedCompatibility.IsSubmerged():
                    {
                        if (FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["SubmergedLeftAdmin"], usableDistance) || FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["SubmergedRightAdmin"], usableDistance))
                        {
                            admin = true;
                            AddDeviceUse(pc.PlayerId, Device.Admin);
                        }

                        if (FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["SubmergedCamera"], usableDistance))
                        {
                            camera = true;
                            AddDeviceUse(pc.PlayerId, Device.Camera);
                        }

                        if (FastVector2.DistanceWithinRange(playerPos, DisableDevice.DevicePos["SubmergedVital"], usableDistance))
                        {
                            vital = true;
                            AddDeviceUse(pc.PlayerId, Device.Vitals);
                        }

                        break;
                    }
                }
            }
            catch (Exception ex) { Logger.Error(ex.ToString(), "AntiAdminer"); }
        }

        var change = false;

        change |= IsAdminWatch != admin;
        IsAdminWatch = admin;
        change |= IsVitalWatch != vital;
        IsVitalWatch = vital;
        change |= IsDoorLogWatch != doorLog;
        IsDoorLogWatch = doorLog;

        if (IsTelecommunication ? Telecommunication.CanCheckCamera.GetBool() : CanCheckCamera.GetBool())
        {
            change |= IsCameraWatch != camera;
            IsCameraWatch = camera;
        }

        if (IsTelecommunication) notify |= oldPlayersNearDevices.Count != PlayersNearDevices.Count || oldPlayersNearDevices.Any(x => !PlayersNearDevices.TryGetValue(x.Key, out HashSet<Device> c) || !x.Value.SetEquals(c));

        if (notify || change) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);

        return;

        void AddDeviceUse(byte id, Device device)
        {
            if (Main.PlayerStates[id].MainRole is CustomRoles.Telecommunication or CustomRoles.AntiAdminer) return;

            if (!PlayersNearDevices.TryGetValue(id, out HashSet<Device> devices))
                PlayersNearDevices[id] = [device];
            else devices.Add(device);
        }
    }

    public override void OnPet(PlayerControl pc)
    {
        if (IsTelecommunication) OpenDoors(pc);
        else OnVanish(pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Options.UsePets.GetBool()) return;
        if (!IsTelecommunication) return;
        OpenDoors(pc);
    }

    private void OpenDoors(PlayerControl pc)
    {
        if (!IsTelecommunication) return;
        if (pc == null) return;
        if (Main.CurrentMap == MapNames.MiraHQ) return;
        
        if (pc.GetAbilityUseLimit() >= 1)
        {
            pc.RpcRemoveAbilityUse();
            DoorsReset.OpenAllDoors();
        }
        else
            pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !pc.Is(CustomRoles.Telecommunication) || pc.Is(CustomRoles.Nimble) || Telecommunication.CanVent.GetBool() || pc.GetClosestVent()?.Id == ventId;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (Main.PlayerStates[seer.PlayerId].Role is AntiAdminer self && seer.PlayerId == target.PlayerId && self.AntiAdminerId == seer.PlayerId)
        {
            return self.ExtraAbilityTimer != null
                ? $"<#ffffff>▩ {(int)Math.Ceiling(self.ExtraAbilityTimer.Remaining.TotalSeconds)}</color>"
                : string.Empty;
        }

        if (!PlayersNearDevices.ContainsKey(seer.PlayerId)) return string.Empty;

        AntiAdminer aa = null;

        foreach (byte id in PlayerIdList)
        {
            if (Main.PlayerStates[id].Role is not AntiAdminer { IsEnable: true, IsTelecommunication: false } x || x.ExtraAbilityTimer == null) continue;

            if (aa != null && x.ExtraAbilityTimer.Remaining.TotalSeconds >= aa.ExtraAbilityTimer.Remaining.TotalSeconds) continue;

            aa = x;
        }

        return aa == null ? string.Empty : $"<#ffff00>\u26a0 {(int)aa.ExtraAbilityTimer.Remaining.TotalSeconds}</color>";
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool())
            hud.PetButton?.OverrideText(Translator.GetString("VeteranVentButtonText"));
        else
            hud.AbilityButton?.OverrideText(Translator.GetString("VeteranVentButtonText"));
    }

}
