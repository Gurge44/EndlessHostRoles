using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace EHR.Modules;

/// <summary>
///     Reflection-based wrapper for LevelImposter's MapInfo API.
///     Call <see cref="Init"/> once after IL2CPPChainloader finishes loading plugins.
///     <para>See <see href="See: https://github.com/DigiWorm0/LevelImposter/blob/master/LevelImposter/Core/ModCompatibility/MapInfo.cs"/> for the API this wraps.</para>
/// </summary>
public static class LevelImposterCompatibility
{
    private const string LevelImposterGuid = "com.DigiWorm.LevelImposter";
    private const string MapInfoTypeName = "LevelImposter.Core.ModCompatibility.MapInfo";

    private static bool _initialized;
    private static bool _isInstalled;

    private static PropertyInfo _mapIdProp;
    private static PropertyInfo _hasCamsProp;
    private static PropertyInfo _hasBinocularsProp;
    private static PropertyInfo _hasAdminTableProp;
    private static PropertyInfo _hasVitalsProp;
    private static PropertyInfo _hasSporesProp;
    private static PropertyInfo _hasMovingPlatformProp;
    private static PropertyInfo _hasLadderProp;
    private static PropertyInfo _hasDoorsProp;
    private static PropertyInfo _hasVentsProp;
    private static PropertyInfo _hasCustomEjectAnimationProp;
    private static PropertyInfo _hasTeleporterProp;
    private static PropertyInfo _hasDeathTriggerProp;
    private static PropertyInfo _hasDecontaminationProp;
    private static PropertyInfo _hasDoorLogsProp;
    private static PropertyInfo _allCameraPanelsProp;
    private static PropertyInfo _allBinocularsProp;
    private static PropertyInfo _allAdminTablesProp;
    private static PropertyInfo _allVitalsProp;
    private static PropertyInfo _allSporesProp;
    private static PropertyInfo _allMovingPlatformsProp;
    private static PropertyInfo _allLaddersProp;
    private static PropertyInfo _allDoorsProp;
    private static PropertyInfo _allVentsProp;
    private static PropertyInfo _allCustomEjectAnimationsProp;
    private static PropertyInfo _allTeleportersProp;
    private static PropertyInfo _allDeathTriggersProp;
    private static MethodInfo _getDoorTypeMethod;

    /// <summary>Whether LevelImposter is currently loaded.</summary>
    public static bool IsInstalled => _isInstalled;

    /// <summary>
    ///     The current LI map ID, or null if LI is not installed or no map is loaded.
    ///     Note: LI may keep a randomized fallback map loaded at all times for mods that do map
    ///     randomization, so a non-null value here does not mean the player is on a LI map mid-game.
    /// </summary>
    public static string MapID => GetString(_mapIdProp);

    /// <summary>True if a LI map is currently selected (MapID is non-null).</summary>
    public static bool IsLevelImposterMap => MapID != null;

    public static bool HasCams => GetBool(_hasCamsProp);
    public static bool HasBinoculars => GetBool(_hasBinocularsProp);
    public static bool HasAdminTable => GetBool(_hasAdminTableProp);
    public static bool HasVitals => GetBool(_hasVitalsProp);
    public static bool HasSpores => GetBool(_hasSporesProp);
    public static bool HasMovingPlatform => GetBool(_hasMovingPlatformProp);
    public static bool HasLadder => GetBool(_hasLadderProp);
    public static bool HasDoors => GetBool(_hasDoorsProp);
    public static bool HasVents => GetBool(_hasVentsProp);
    public static bool HasCustomEjectAnimation => GetBool(_hasCustomEjectAnimationProp);
    public static bool HasTeleporter => GetBool(_hasTeleporterProp);
    public static bool HasDeathTrigger => GetBool(_hasDeathTriggerProp);
    public static bool HasDecontamination => GetBool(_hasDecontaminationProp);

    /// <summary>Always false - door logs are not implemented in LI.</summary>
    public static bool HasDoorLogs => GetBool(_hasDoorLogsProp);

    /// <summary>These return empty until after ShipStatus.Awake.</summary>
    public static IEnumerable<GameObject> AllCameraPanels => GetObjects(_allCameraPanelsProp);
    public static IEnumerable<GameObject> AllBinoculars => GetObjects(_allBinocularsProp);
    public static IEnumerable<GameObject> AllAdminTables => GetObjects(_allAdminTablesProp);
    public static IEnumerable<GameObject> AllVitals => GetObjects(_allVitalsProp);
    public static IEnumerable<GameObject> AllSpores => GetObjects(_allSporesProp);
    public static IEnumerable<GameObject> AllMovingPlatforms => GetObjects(_allMovingPlatformsProp);
    public static IEnumerable<GameObject> AllLadders => GetObjects(_allLaddersProp);
    public static IEnumerable<GameObject> AllDoors => GetObjects(_allDoorsProp);
    public static IEnumerable<GameObject> AllVents => GetObjects(_allVentsProp);
    public static IEnumerable<GameObject> AllCustomEjectAnimations => GetObjects(_allCustomEjectAnimationsProp);
    public static IEnumerable<GameObject> AllTeleporters => GetObjects(_allTeleportersProp);
    public static IEnumerable<GameObject> AllDeathTriggers => GetObjects(_allDeathTriggersProp);

    /// <summary>Fired when LI calls its OnMapChange hook, i.e. when the selected map changes in the lobby.</summary>
    public static event Action OnMapChanged;

    /// <summary>
    ///     Returns the door type used by the current LI map.
    ///     Possible values: "none", "skeld" (auto timer), "polus" (manual switches), "airship" (card swipe).
    ///     Returns "none" if LI is not installed.
    /// </summary>
    public static string GetDoorType()
    {
        if (!_isInstalled || _getDoorTypeMethod == null)
            return "none";

        try
        {
            return (string)_getDoorTypeMethod.Invoke(null, null) ?? "none";
        }
        catch (Exception ex)
        {
            Logger.Error($"[LI] GetDoorType failed: {ex}", "LI");
            return "none";
        }
    }

    /// <summary>Called by our Harmony postfix on LI's OnMapChange stub.</summary>
    internal static void HandleMapChange()
    {
        try
        {
            OnMapChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error($"[LI] OnMapChanged handler threw: {ex}", "LI");
        }
    }

    /// <summary>True if the map has cameras or binoculars.</summary>
    public static bool HasAnySurveillance => HasCams || HasBinoculars;

    /// <summary>True if the map has doors or spores.</summary>
    public static bool HasSabotageFeatures => HasDoors || HasSpores;

    /// <summary>
    ///     Returns all vents on the current map.
    ///     Falls back to vanilla ShipStatus vents if LI is not installed or has no LI vents.
    /// </summary>
    public static IEnumerable<Vent> GetAllVents()
    {
        if (_isInstalled && AllVents.Any())
            return AllVents.Select(go => go.GetComponent<Vent>()).Where(v => v != null);

        return ShipStatus.Instance != null ? ShipStatus.Instance.AllVents : [];
    }

    /// <summary>Logs all detected LI map features to the console. Useful for debugging role enable logic on custom maps.</summary>
    public static void LogMapFeatures()
    {
        if (!_isInstalled)
        {
            Logger.Info("[LI] Not installed", "LI");
            return;
        }

        Logger.Info($"[LI] Map: {MapID ?? "(none)"}", "LI");
        Logger.Info($"[LI] Cams: {HasCams}, Binoculars: {HasBinoculars}, Admin: {HasAdminTable}, Vitals: {HasVitals}", "LI");
        Logger.Info($"[LI] Spores: {HasSpores}, Platform: {HasMovingPlatform}, Ladder: {HasLadder}", "LI");
        Logger.Info($"[LI] Doors: {HasDoors} ({GetDoorType()}), Vents: {HasVents}", "LI");
        Logger.Info($"[LI] CustomEject: {HasCustomEjectAnimation}, Teleporter: {HasTeleporter}, DeathTrigger: {HasDeathTrigger}, Decontam: {HasDecontamination}", "LI");
    }

    /// <summary>
    ///     Initializes the LI compatibility layer.
    ///     Call this once after IL2CPPChainloader finishes loading plugins, same as SubmergedCompatibility.Initialize().
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        if (!IL2CPPChainloader.Instance.Plugins.TryGetValue(LevelImposterGuid, out PluginInfo plugin))
        {
            Logger.Info("[LI] Not found, skipping", "LI");
            return;
        }

        // Try LI's own assembly first, then fall back to scanning everything
        Type mapInfoType = plugin.Instance?.GetType().Assembly.GetType(MapInfoTypeName, throwOnError: false);

        if (mapInfoType == null)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    mapInfoType = assembly.GetType(MapInfoTypeName, throwOnError: false);
                    if (mapInfoType != null) break;
                }
                catch { }
            }
        }

        if (mapInfoType == null)
        {
            Logger.Warn("[LI] Plugin found but MapInfo type is missing", "LI");
            return;
        }

        _isInstalled = true;
        Logger.Info("[LI] Loaded, binding MapInfo API", "LI");

        var flags = BindingFlags.Public | BindingFlags.Static;

        _mapIdProp = mapInfoType.GetProperty("MapID", flags);
        _hasCamsProp = mapInfoType.GetProperty("HasCams", flags);
        _hasBinocularsProp = mapInfoType.GetProperty("HasBinoculars", flags);
        _hasAdminTableProp = mapInfoType.GetProperty("HasAdminTable", flags);
        _hasVitalsProp = mapInfoType.GetProperty("HasVitals", flags);
        _hasSporesProp = mapInfoType.GetProperty("HasSpores", flags);
        _hasMovingPlatformProp = mapInfoType.GetProperty("HasMovingPlatform", flags);
        _hasLadderProp = mapInfoType.GetProperty("HasLadder", flags);
        _hasDoorsProp = mapInfoType.GetProperty("HasDoors", flags);
        _hasVentsProp = mapInfoType.GetProperty("HasVents", flags);
        _hasCustomEjectAnimationProp = mapInfoType.GetProperty("HasCustomEjectAnimation", flags);
        _hasTeleporterProp = mapInfoType.GetProperty("HasTeleporter", flags);
        _hasDeathTriggerProp = mapInfoType.GetProperty("HasDeathTrigger", flags);
        _hasDecontaminationProp = mapInfoType.GetProperty("HasDecontamination", flags);
        _hasDoorLogsProp = mapInfoType.GetProperty("HasDoorLogs", flags);
        _allCameraPanelsProp = mapInfoType.GetProperty("AllCameraPanels", flags);
        _allBinocularsProp = mapInfoType.GetProperty("AllBinoculars", flags);
        _allAdminTablesProp = mapInfoType.GetProperty("AllAdminTables", flags);
        _allVitalsProp = mapInfoType.GetProperty("AllVitals", flags);
        _allSporesProp = mapInfoType.GetProperty("AllSpores", flags);
        _allMovingPlatformsProp = mapInfoType.GetProperty("AllMovingPlatforms", flags);
        _allLaddersProp = mapInfoType.GetProperty("AllLadders", flags);
        _allDoorsProp = mapInfoType.GetProperty("AllDoors", flags);
        _allVentsProp = mapInfoType.GetProperty("AllVents", flags);
        _allCustomEjectAnimationsProp = mapInfoType.GetProperty("AllCustomEjectAnimations", flags);
        _allTeleportersProp = mapInfoType.GetProperty("AllTeleporters", flags);
        _allDeathTriggersProp = mapInfoType.GetProperty("AllDeathTriggers", flags);
        _getDoorTypeMethod = mapInfoType.GetMethod("GetDoorType", flags);

        // LI calls OnMapChange() internally whenever the map changes. It's an empty stub
        // designed for other mods to patch into. We hook it here to fire our OnMapChanged event.
        MethodInfo onMapChange = mapInfoType.GetMethod("OnMapChange", flags);
        if (onMapChange != null)
        {
            var harmony = new Harmony("ehr.levelimposter.patch");
            harmony.Patch(onMapChange, postfix: new HarmonyMethod(typeof(LevelImposterCompatibility), nameof(HandleMapChange)));
        }
    }

    private static bool GetBool(PropertyInfo prop)
    {
        if (!_isInstalled || prop == null) return false;

        try
        {
            return (bool)prop.GetValue(null);
        }
        catch (Exception ex)
        {
            Logger.Error($"[LI] Failed to read {prop.Name}: {ex}", "LI");
            return false;
        }
    }

    private static string GetString(PropertyInfo prop)
    {
        if (!_isInstalled || prop == null) return null;

        try
        {
            return prop.GetValue(null) as string;
        }
        catch (Exception ex)
        {
            Logger.Error($"[LI] Failed to read {prop.Name}: {ex}", "LI");
            return null;
        }
    }

    private static IEnumerable<GameObject> GetObjects(PropertyInfo prop)
    {
        if (!_isInstalled || prop == null) return [];

        try
        {
            // Direct IEnumerable<GameObject> cast fails across IL2CPP assembly boundaries even
            // when the objects are valid GameObjects, so go through non-generic IEnumerable instead.
            var value = prop.GetValue(null);
            if (value is IEnumerable enumerable)
                return enumerable.Cast<object>().OfType<GameObject>();

            return [];
        }
        catch (Exception ex)
        {
            Logger.Error($"[LI] Failed to read {prop.Name}: {ex}", "LI");
            return [];
        }
    }
}
