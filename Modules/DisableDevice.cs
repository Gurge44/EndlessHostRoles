using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;

namespace EHR;

//参考元 : https://github.com/ykundesu/SuperNewRoles/blob/master/SuperNewRoles/Mode/SuperHostRoles/BlockTool.cs
class DisableDevice
{
    private static readonly List<byte> DesyncComms = [];
    private static int frame;

    public static readonly Dictionary<string, Vector2> DevicePos = new()
    {
        ["SkeldAdmin"] = new(3.48f, -8.62f),
        ["SkeldCamera"] = new(-13.06f, -2.45f),
        ["MiraHQAdmin"] = new(21.02f, 19.09f),
        ["MiraHQDoorLog"] = new(16.22f, 5.82f),
        ["PolusLeftAdmin"] = new(22.80f, -21.52f),
        ["PolusRightAdmin"] = new(24.66f, -21.52f),
        ["PolusCamera"] = new(2.96f, -12.74f),
        ["PolusVital"] = new(26.70f, -15.94f),
        ["DleksAdmin"] = new(-3.48f, -8.62f),
        ["DleksCamera"] = new(13.06f, -2.45f),
        ["AirshipCockpitAdmin"] = new(-22.32f, 0.91f),
        ["AirshipRecordsAdmin"] = new(19.89f, 12.60f),
        ["AirshipCamera"] = new(8.10f, -9.63f),
        ["AirshipVital"] = new(25.24f, -7.94f),
        ["FungleCamera"] = new(6.20f, 0.10f),
        ["FungleVital"] = new(-2.50f, -9.80f)
    };

    public static bool DoDisable => Options.DisableDevices.GetBool();

    public static float UsableDistance => Main.CurrentMap switch
    {
        MapNames.Skeld => 1.8f,
        MapNames.Mira => 2.4f,
        MapNames.Polus => 1.8f,
        MapNames.Dleks => 1.5f,
        MapNames.Airship => 1.8f,
        MapNames.Fungle => 1.8f,
        _ => 0.0f
    };

    public static void FixedUpdate()
    {
        frame = frame == 3 ? 0 : ++frame;
        if (frame != 0) return;

        var rogueForce = Rogue.On && Main.PlayerStates.Values.Any(x => x.Role is Rogue { DisableDevices: true });

        if (!DoDisable && !rogueForce) return;
        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            try
            {
                if (pc.IsModClient()) continue;

                bool doComms = false;
                Vector2 PlayerPos = pc.Pos();
                bool ignore = (Options.DisableDevicesIgnoreImpostors.GetBool() && pc.Is(CustomRoleTypes.Impostor)) ||
                              (Options.DisableDevicesIgnoreNeutrals.GetBool() && pc.Is(CustomRoleTypes.Neutral)) ||
                              (Options.DisableDevicesIgnoreCrewmates.GetBool() && pc.Is(CustomRoleTypes.Crewmate)) ||
                              (Options.DisableDevicesIgnoreAfterAnyoneDied.GetBool() && GameStates.AlreadyDied);
                ignore &= !rogueForce;

                if (pc.IsAlive() && !Utils.IsActive(SystemTypes.Comms))
                {
                    switch (Main.NormalOptions.MapId)
                    {
                        case 0:
                            if (Options.DisableSkeldAdmin.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["SkeldAdmin"]) <= UsableDistance;
                            if (Options.DisableSkeldCamera.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["SkeldCamera"]) <= UsableDistance;
                            break;
                        case 1:
                            if (Options.DisableMiraHQAdmin.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["MiraHQAdmin"]) <= UsableDistance;
                            if (Options.DisableMiraHQDoorLog.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["MiraHQDoorLog"]) <= UsableDistance;
                            break;
                        case 2:
                            if (Options.DisablePolusAdmin.GetBool() || rogueForce)
                            {
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["PolusLeftAdmin"]) <= UsableDistance;
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["PolusRightAdmin"]) <= UsableDistance;
                            }

                            if (Options.DisablePolusCamera.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["PolusCamera"]) <= UsableDistance;
                            if (Options.DisablePolusVital.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["PolusVital"]) <= UsableDistance;
                            break;
                        case 3:
                            if (Options.DisableSkeldAdmin.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["DleksAdmin"]) <= UsableDistance;
                            if (Options.DisableSkeldCamera.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["DleksCamera"]) <= UsableDistance;
                            break;
                        case 4:
                            if (Options.DisableAirshipCockpitAdmin.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["AirshipCockpitAdmin"]) <= UsableDistance;
                            if (Options.DisableAirshipRecordsAdmin.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["AirshipRecordsAdmin"]) <= UsableDistance;
                            if (Options.DisableAirshipCamera.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["AirshipCamera"]) <= UsableDistance;
                            if (Options.DisableAirshipVital.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["AirshipVital"]) <= UsableDistance;
                            break;
                        case 5:
                            if (Options.DisableFungleCamera.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["FungleCamera"]) <= UsableDistance;
                            if (Options.DisableFungleVital.GetBool() || rogueForce)
                                doComms |= Vector2.Distance(PlayerPos, DevicePos["FungleVital"]) <= UsableDistance;
                            break;
                    }
                }

                doComms &= !ignore;
                if (doComms && !pc.inVent)
                {
                    if (!DesyncComms.Contains(pc.PlayerId))
                        DesyncComms.Add(pc.PlayerId);

                    pc.RpcDesyncRepairSystem(SystemTypes.Comms, 128);
                }
                else if (!Utils.IsActive(SystemTypes.Comms) && DesyncComms.Contains(pc.PlayerId))
                {
                    DesyncComms.Remove(pc.PlayerId);
                    pc.RpcDesyncRepairSystem(SystemTypes.Comms, 16);

                    if (Main.NormalOptions.MapId is 1 or 5) // Mira HQ or The Fungle
                        pc.RpcDesyncRepairSystem(SystemTypes.Comms, 17);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "DisableDevice");
            }
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
public class RemoveDisableDevicesPatch
{
    public static void Postfix()
    {
        var rogueForce = Rogue.On && Main.PlayerStates.Values.Any(x => x.Role is Rogue { DisableDevices: true });
        if (!Options.DisableDevices.GetBool() && !rogueForce) return;
        UpdateDisableDevices();
    }

    public static void UpdateDisableDevices()
    {
        var player = PlayerControl.LocalPlayer;
        var rogueForce = Rogue.On && Main.PlayerStates.Values.Any(x => x.Role is Rogue { DisableDevices: true });
        bool ignore = player.Is(CustomRoles.GM) ||
                      !player.IsAlive() ||
                      (Options.DisableDevicesIgnoreImpostors.GetBool() && player.Is(CustomRoleTypes.Impostor)) ||
                      (Options.DisableDevicesIgnoreNeutrals.GetBool() && player.Is(CustomRoleTypes.Neutral)) ||
                      (Options.DisableDevicesIgnoreCrewmates.GetBool() && player.Is(CustomRoleTypes.Crewmate)) ||
                      (Options.DisableDevicesIgnoreAfterAnyoneDied.GetBool() && GameStates.AlreadyDied);
        ignore &= !rogueForce;
        var admins = Object.FindObjectsOfType<MapConsole>(true);
        var consoles = Object.FindObjectsOfType<SystemConsole>(true);
        if (admins == null || consoles == null) return;
        switch (Main.NormalOptions.MapId)
        {
            case 3:
            case 0:
                if (Options.DisableSkeldAdmin.GetBool() || rogueForce)
                    admins[0].gameObject.GetComponent<CircleCollider2D>().enabled = ignore;
                if (Options.DisableSkeldCamera.GetBool() || rogueForce)
                    consoles.DoIf(x => x.name == "SurvConsole", x => x.gameObject.GetComponent<PolygonCollider2D>().enabled = ignore, fast: true);
                break;
            case 1:
                if (Options.DisableMiraHQAdmin.GetBool() || rogueForce)
                    admins[0].gameObject.GetComponent<CircleCollider2D>().enabled = ignore;
                if (Options.DisableMiraHQDoorLog.GetBool() || rogueForce)
                    consoles.DoIf(x => x.name == "SurvLogConsole", x => x.gameObject.GetComponent<BoxCollider2D>().enabled = ignore, fast: true);
                break;
            case 2:
                if (Options.DisablePolusAdmin.GetBool() || rogueForce)
                    admins.Do(x => x.gameObject.GetComponent<BoxCollider2D>().enabled = ignore);
                if (Options.DisablePolusCamera.GetBool() || rogueForce)
                    consoles.DoIf(x => x.name == "Surv_Panel", x => x.gameObject.GetComponent<BoxCollider2D>().enabled = ignore, fast: true);
                if (Options.DisablePolusVital.GetBool() || rogueForce)
                    consoles.DoIf(x => x.name == "panel_vitals", x => x.gameObject.GetComponent<BoxCollider2D>().enabled = ignore, fast: true);
                break;
            case 4:
                admins.Do(x =>
                {
                    if ((Options.DisableAirshipCockpitAdmin.GetBool() && x.name == "panel_cockpit_map") ||
                        (Options.DisableAirshipRecordsAdmin.GetBool() && x.name == "records_admin_map") || rogueForce)
                        x.gameObject.GetComponent<BoxCollider2D>().enabled = ignore;
                });
                if (Options.DisableAirshipCamera.GetBool() || rogueForce)
                    consoles.DoIf(x => x.name == "task_cams", x => x.gameObject.GetComponent<BoxCollider2D>().enabled = ignore, fast: true);
                if (Options.DisableAirshipVital.GetBool() || rogueForce)
                    consoles.DoIf(x => x.name == "panel_vitals", x => x.gameObject.GetComponent<CircleCollider2D>().enabled = ignore, fast: true);
                break;
            case 5:
                if (Options.DisableFungleCamera.GetBool() || rogueForce)
                    consoles.DoIf(x => x.name == "BinocularsSecurityConsole", x => x.gameObject.GetComponent<PolygonCollider2D>().enabled = ignore, fast: true);
                if (Options.DisableFungleCamera.GetBool() || rogueForce)
                    consoles.DoIf(x => x.name == "VitalsConsole", x => x.gameObject.GetComponent<BoxCollider2D>().enabled = ignore, fast: true);
                break;
        }
    }
}