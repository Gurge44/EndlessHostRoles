using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using EHR.Patches;
using HarmonyLib;

namespace EHR;

// Partially from https://github.com/eDonnes124/Town-Of-Us-R/tree/master/source/Patches/SubmergedCompatibility.cs

public static class SubmergedCompatibility
{
    /*
        public static class Classes
        {
            public const string ElevatorMover = "ElevatorMover";
        }
    */

    public const string SubmergedGuid = "Submerged";
    private const ShipStatus.MapType SubmergedMapType = (ShipStatus.MapType)6;

    // public static SemanticVersioning.Version Version { get; private set; }
    public static bool Loaded { get; set; }
    private static BasePlugin Plugin { get; set; }
    private static Assembly Assembly { get; set; }
    private static Type[] Types { get; set; }
    // public static Dictionary<string, Type> InjectedTypes { get; private set; }

    /*
        private static MonoBehaviour _submarineStatus;
        public static MonoBehaviour SubmarineStatus
        {
            get
            {
                if (!Loaded) return null;

                if (_submarineStatus is null || _submarineStatus.WasCollected || !_submarineStatus || _submarineStatus == null)
                {
                    if (ShipStatus.Instance is null || ShipStatus.Instance == null || !ShipStatus.Instance || ShipStatus.Instance.WasCollected)
                        return _submarineStatus = null;

                    if (ShipStatus.Instance.Type == SUBMERGED_MAP_TYPE)
                        return _submarineStatus = ShipStatus.Instance.GetComponent(Il2CppType.From(SubmarineStatusType))?.TryCast(SubmarineStatusType) as MonoBehaviour;

                    return _submarineStatus = null;
                }

                return _submarineStatus;
            }
        }
    */

    private static Type SubmarineStatusType;
    // private static MethodInfo CalculateLightRadiusMethod;

    // private static MethodInfo RpcRequestChangeFloorMethod;
    // private static Type FloorHandlerType;
    // private static MethodInfo GetFloorHandlerMethod;

    private static Type SubmarinePlayerFloorSystemType;
    private static MethodInfo SubmarinePlayerFloorSystemInstanceGetter;
    private static MethodInfo ChangePlayerFloorStateMethod;

    private static Type VentPatchDataType;
    private static PropertyInfo InTransitionField;

    // private static Type CustomTaskTypesType;
    // private static FieldInfo RetrieveOxigenMaskField;
    // public static TaskTypes RetrieveOxygenMask;
    // private static Type SubmarineOxygenSystemType;
    // private static PropertyInfo SubmarineOxygenSystemInstanceField;
    // private static MethodInfo RepairDamageMethod;

    private static Type SubmergedExileController;
    private static MethodInfo SubmergedExileWrapUpMethod;

    private static Type SubmarineElevator;

    private static MethodInfo GetInElevator;

    // private static MethodInfo GetMovementStageFromTime;
    private static FieldInfo GetSubElevatorSystem;

    // private static Type ElevatorConsole;
    // private static MethodInfo CanUse;

    private static Type SubmarineElevatorSystem;
    private static FieldInfo UpperDeckIsTargetFloor;

    private static FieldInfo SubmergedInstance;
    private static FieldInfo SubmergedElevators;

    public static void Initialize()
    {
        Loaded = IL2CPPChainloader.Instance.Plugins.TryGetValue(SubmergedGuid, out PluginInfo plugin);
        if (!Loaded) return;

        Plugin = plugin!.Instance as BasePlugin;
        // Version = plugin.Metadata.Version;

        Assembly = Plugin!.GetType().Assembly;
        Types = AccessTools.GetTypesFromAssembly(Assembly);

        AccessTools.PropertyGetter(Types.FirstOrDefault(t => t.Name == "ComponentExtensions"), "RegisteredTypes")
            .Invoke(null, []);

        SubmarineStatusType = Types.First(t => t.Name == "SubmarineStatus");
        SubmergedInstance = AccessTools.Field(SubmarineStatusType, "instance");
        SubmergedElevators = AccessTools.Field(SubmarineStatusType, "elevators");

        // CalculateLightRadiusMethod = AccessTools.Method(SubmarineStatusType, "CalculateLightRadius");

        // FloorHandlerType = Types.First(t => t.Name == "FloorHandler");
        // GetFloorHandlerMethod = AccessTools.Method(FloorHandlerType, "GetFloorHandler", [typeof(PlayerControl)]);
        // RpcRequestChangeFloorMethod = AccessTools.Method(FloorHandlerType, "RpcRequestChangeFloor");

        SubmarinePlayerFloorSystemType = Types.First(t => t.Name == "SubmarinePlayerFloorSystem");
        SubmarinePlayerFloorSystemInstanceGetter = AccessTools.PropertyGetter(SubmarinePlayerFloorSystemType, "Instance");
        ChangePlayerFloorStateMethod = AccessTools.Method(SubmarinePlayerFloorSystemType, "ChangePlayerFloorState");

        VentPatchDataType = Types.First(t => t.Name == "VentPatchData");
        InTransitionField = AccessTools.Property(VentPatchDataType, "InTransition");

        // CustomTaskTypesType = Types.First(t => t.Name == "CustomTaskTypes");
        // RetrieveOxigenMaskField = AccessTools.Field(CustomTaskTypesType, "RetrieveOxygenMask");
        // var retTaskType = AccessTools.Field(CustomTaskTypesType, "taskType");
        // RetrieveOxygenMask = (TaskTypes)retTaskType.GetValue(RetrieveOxigenMaskField.GetValue(null))!;

        // SubmarineOxygenSystemType = Types.First(t => t.Name == "SubmarineOxygenSystem");
        // SubmarineOxygenSystemInstanceField = AccessTools.Property(SubmarineOxygenSystemType, "Instance");
        // RepairDamageMethod = AccessTools.Method(SubmarineOxygenSystemType, "RepairDamage");
        SubmergedExileController = Types.First(t => t.Name == "SubmergedExileController");
        SubmergedExileWrapUpMethod = AccessTools.Method(SubmergedExileController, "WrapUpAndSpawn");

        SubmarineElevator = Types.First(t => t.Name == "SubmarineElevator");
        GetInElevator = AccessTools.Method(SubmarineElevator, "GetInElevator", [typeof(PlayerControl)]);
        // GetMovementStageFromTime = AccessTools.Method(SubmarineElevator, "GetMovementStageFromTime");
        GetSubElevatorSystem = AccessTools.Field(SubmarineElevator, "system");

        // ElevatorConsole = Types.First(t => t.Name == "ElevatorConsole");
        // CanUse = AccessTools.Method(ElevatorConsole, "CanUse");

        SubmarineElevatorSystem = Types.First(t => t.Name == "SubmarineElevatorSystem");
        UpperDeckIsTargetFloor = AccessTools.Field(SubmarineElevatorSystem, "upperDeckIsTargetFloor");
        var harmony = new Harmony("ehr.submerged.patch");
        MethodInfo exilerolechangePostfix = SymbolExtensions.GetMethodInfo(() => WrapUpPatch());
        harmony.Patch(SubmergedExileWrapUpMethod, null, new HarmonyMethod(exilerolechangePostfix));
        /*
        var canusePrefix = SymbolExtensions.GetMethodInfo(() => CanUsePrefix());
        var canusePostfix = SymbolExtensions.GetMethodInfo(() => CanUsePostfix());
        _harmony.Patch(CanUse, new HarmonyMethod(canusePrefix), new HarmonyMethod(canusePostfix));
        */
    }

    private static void WrapUpPatch()
    {
        try { ExileControllerWrapUpPatch.WrapUpPostfix(ExileControllerWrapUpPatch.LastExiled); }
        finally { ExileControllerWrapUpPatch.WrapUpFinalizer(); }
    }

    public static void CheckOutOfBoundsElevator(PlayerControl player)
    {
        if (!Loaded) return;
        if (!IsSubmerged()) return;

        Tuple<bool, object> elevator = GetPlayerElevator(player);
        if (!elevator.Item1) return;
        var currentFloor = (bool)UpperDeckIsTargetFloor.GetValue(GetSubElevatorSystem.GetValue(elevator.Item2))!; //true is top, false is bottom
        bool playerFloor = player.transform.position.y > -7f; //true is top, false is bottom

        if (currentFloor != playerFloor)
            ChangeFloor(player.PlayerId, currentFloor);
    }

    /*
        public static void MoveDeadPlayerElevator(PlayerControl player)
        {
            if (!isSubmerged()) return;
            Tuple<bool, object> elevator = GetPlayerElevator(player);
            if (!elevator.Item1) return;

            int MovementStage = (int)GetMovementStageFromTime.Invoke(elevator.Item2, null)!;
            if (MovementStage >= 5)
            {
                //Fade to clear
                bool topfloortarget = (bool)UpperDeckIsTargetFloor.GetValue(getSubElevatorSystem.GetValue(elevator.Item2))!; //true is top, false is bottom
                bool topintendedtarget = player.transform.position.y > -7f; //true is top, false is bottom
                if (topfloortarget != topintendedtarget)
                {
                    ChangeFloor(!topintendedtarget);
                }
            }
        }
    */

    private static Tuple<bool, object> GetPlayerElevator(PlayerControl player)
    {
        if (!IsSubmerged()) return Tuple.Create(false, (object)null);
        // ReSharper disable once RedundantAssignment
        IList elevatorlist = CreateList(SubmarineElevator);
        elevatorlist = (IList)SubmergedElevators.GetValue(SubmergedInstance.GetValue(null))!;
        foreach (object elevator in elevatorlist)
            if ((bool)GetInElevator.Invoke(elevator, [player])!)
                return Tuple.Create(true, elevator);

        return Tuple.Create(false, (object)null);
    }

    public static void ChangeFloor(byte playerId, bool toUpper)
    {
        if (!Loaded) return;
        ChangePlayerFloorStateMethod.Invoke(SubmarinePlayerFloorSystemInstanceGetter.Invoke(null, null), [playerId, toUpper]);
    }

    public static bool GetInTransition()
    {
        if (!Loaded) return false;
        return (bool)InTransitionField.GetValue(null)!;
    }


    /*
    public static void RepairOxygen()
    {
        if (!Loaded) return;
        try
        {
            ShipStatus.Instance.RpcUpdateSystem((SystemTypes)130, 64);
            RepairDamageMethod.Invoke(SubmarineOxygenSystemInstanceField.GetValue(null), new object[] { PlayerControl.LocalPlayer, 64 });
        }
        catch (System.NullReferenceException)
        {

        }

    }
*/

    public static bool IsSubmerged()
    {
        return Loaded && ShipStatus.Instance && ShipStatus.Instance.Type == SubmergedMapType;
    }

    /*
        private static object TryCast(this Il2CppObjectBase self, Type type)
        {
            return AccessTools.Method(self.GetType(), nameof(Il2CppObjectBase.TryCast)).MakeGenericMethod(type).Invoke(self, []);
        }
    */

    private static IList CreateList(Type myType)
    {
        Type genericListType = typeof(List<>).MakeGenericType(myType);
        return (IList)Activator.CreateInstance(genericListType);
    }

    public static bool IsSupported(CustomGameMode mode)
    {
        return mode is CustomGameMode.Standard or CustomGameMode.SoloPVP or CustomGameMode.FFA or CustomGameMode.StopAndGo or CustomGameMode.HotPotato or CustomGameMode.HideAndSeek or CustomGameMode.Speedrun or CustomGameMode.CaptureTheFlag or CustomGameMode.RoomRush or CustomGameMode.KingOfTheZones or CustomGameMode.Quiz or CustomGameMode.TheMindGame or CustomGameMode.BedWars;
    }

    public enum SubmergedSystemTypes : byte
    {
        Research = 0x80, // 128
        Observatory = 0x81, // 129
        UpperCentral = 0x82, // 130 (SubmarineOxygenSystem)
        UpperLobby = 0x83, // 131
        Filtration = 0x84, // 132
        Ballast = 0x85, // 133
        LowerCentral = 0x86, // 134
        LowerLobby = 0x87, // 135
        ElevatorWestLeft = 0x88, // 136 (SubmarineElevatorSystem (HallwayLeft))
        ElevatorWestRight = 0x89, // 137 (SubmarineElevatorSystem (HallwayRight))
        ElevatorEastLeft = 0x8a, // 138 (SubmarineElevatorSystem (LobbyLeft))
        ElevatorEastRight = 0x8b, // 139 (SubmarineElevatorSystem (LobbyRight))
        ElevatorService = 0x8c, // 140 (SubmarineElevatorSystem (Service))

        SubmarinePlayerFloorSystem = 0x8d, // 141
        SubmarineSecuritySabotageSystem = 0x8e, // 142
        SubmarineSpawnInSystem = 0x8f, // 143
        SubmarineBoxCatSystem = 0x90 // 144
    }
}