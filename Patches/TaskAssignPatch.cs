using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.AddOns.Crewmate;
using EHR.AddOns.GhostRoles;
using EHR.Impostor;
using EHR.Modules;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Random = UnityEngine.Random;

namespace EHR
{
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.AddTasksFromList))]
    internal static class AddTasksFromListPatch
    {
        public static Dictionary<TaskTypes, OptionItem> DisableTasksSettings = [];

        public static void Prefix( /*ShipStatus __instance,*/
            [HarmonyArgument(4)] Il2CppSystem.Collections.Generic.List<NormalPlayerTask> unusedTasks)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            if (DisableTasksSettings.Count == 0)
            {
                DisableTasksSettings = new()
                {
                    [TaskTypes.SwipeCard] = Options.DisableSwipeCard,
                    [TaskTypes.SubmitScan] = Options.DisableSubmitScan,
                    [TaskTypes.UnlockSafe] = Options.DisableUnlockSafe,
                    [TaskTypes.UploadData] = Options.DisableUploadData,
                    [TaskTypes.StartReactor] = Options.DisableStartReactor,
                    [TaskTypes.ResetBreakers] = Options.DisableResetBreaker,
                    [TaskTypes.VentCleaning] = Options.DisableCleanVent,
                    [TaskTypes.CalibrateDistributor] = Options.DisableCalibrateDistributor,
                    [TaskTypes.ChartCourse] = Options.DisableChartCourse,
                    [TaskTypes.StabilizeSteering] = Options.DisableStabilizeSteering,
                    [TaskTypes.CleanO2Filter] = Options.DisableCleanO2Filter,
                    [TaskTypes.UnlockManifolds] = Options.DisableUnlockManifolds,
                    [TaskTypes.PrimeShields] = Options.DisablePrimeShields,
                    [TaskTypes.MeasureWeather] = Options.DisableMeasureWeather,
                    [TaskTypes.BuyBeverage] = Options.DisableBuyBeverage,
                    [TaskTypes.AssembleArtifact] = Options.DisableAssembleArtifact,
                    [TaskTypes.SortSamples] = Options.DisableSortSamples,
                    [TaskTypes.ProcessData] = Options.DisableProcessData,
                    [TaskTypes.RunDiagnostics] = Options.DisableRunDiagnostics,
                    [TaskTypes.RepairDrill] = Options.DisableRepairDrill,
                    [TaskTypes.AlignTelescope] = Options.DisableAlignTelescope,
                    [TaskTypes.RecordTemperature] = Options.DisableRecordTemperature,
                    [TaskTypes.FillCanisters] = Options.DisableFillCanisters,
                    [TaskTypes.MonitorOxygen] = Options.DisableMonitorTree,
                    [TaskTypes.StoreArtifacts] = Options.DisableStoreArtifacts,
                    [TaskTypes.PutAwayPistols] = Options.DisablePutAwayPistols,
                    [TaskTypes.PutAwayRifles] = Options.DisablePutAwayRifles,
                    [TaskTypes.MakeBurger] = Options.DisableMakeBurger,
                    [TaskTypes.CleanToilet] = Options.DisableCleanToilet,
                    [TaskTypes.Decontaminate] = Options.DisableDecontaminate,
                    [TaskTypes.SortRecords] = Options.DisableSortRecords,
                    [TaskTypes.FixShower] = Options.DisableFixShower,
                    [TaskTypes.PickUpTowels] = Options.DisablePickUpTowels,
                    [TaskTypes.PolishRuby] = Options.DisablePolishRuby,
                    [TaskTypes.DressMannequin] = Options.DisableDressMannequin,
                    [TaskTypes.AlignEngineOutput] = Options.DisableAlignEngineOutput,
                    [TaskTypes.InspectSample] = Options.DisableInspectSample,
                    [TaskTypes.EmptyChute] = Options.DisableEmptyChute,
                    [TaskTypes.ClearAsteroids] = Options.DisableClearAsteroids,
                    [TaskTypes.WaterPlants] = Options.DisableWaterPlants,
                    [TaskTypes.OpenWaterways] = Options.DisableOpenWaterways,
                    [TaskTypes.ReplaceWaterJug] = Options.DisableReplaceWaterJug,
                    [TaskTypes.RebootWifi] = Options.DisableRebootWifi,
                    [TaskTypes.DevelopPhotos] = Options.DisableDevelopPhotos,
                    [TaskTypes.RewindTapes] = Options.DisableRewindTapes,
                    [TaskTypes.StartFans] = Options.DisableStartFans,
                    [TaskTypes.FixWiring] = Options.DisableFixWiring,
                    [TaskTypes.EnterIdCode] = Options.DisableEnterIdCode,
                    [TaskTypes.InsertKeys] = Options.DisableInsertKeys,
                    [TaskTypes.ScanBoardingPass] = Options.DisableScanBoardingPass,
                    [TaskTypes.EmptyGarbage] = Options.DisableEmptyGarbage,
                    [TaskTypes.FuelEngines] = Options.DisableFuelEngines,
                    [TaskTypes.DivertPower] = Options.DisableDivertPower,
                    [TaskTypes.FixWeatherNode] = Options.DisableActivateWeatherNodes,
                    [TaskTypes.RoastMarshmallow] = Options.DisableRoastMarshmallow,
                    [TaskTypes.CollectSamples] = Options.DisableCollectSamples,
                    [TaskTypes.ReplaceParts] = Options.DisableReplaceParts,
                    [TaskTypes.CollectVegetables] = Options.DisableCollectVegetables,
                    [TaskTypes.MineOres] = Options.DisableMineOres,
                    [TaskTypes.ExtractFuel] = Options.DisableExtractFuel,
                    [TaskTypes.CatchFish] = Options.DisableCatchFish,
                    [TaskTypes.PolishGem] = Options.DisablePolishGem,
                    [TaskTypes.HelpCritter] = Options.DisableHelpCritter,
                    [TaskTypes.HoistSupplies] = Options.DisableHoistSupplies,
                    [TaskTypes.FixAntenna] = Options.DisableFixAntenna,
                    [TaskTypes.BuildSandcastle] = Options.DisableBuildSandcastle,
                    [TaskTypes.CrankGenerator] = Options.DisableCrankGenerator,
                    [TaskTypes.MonitorMushroom] = Options.DisableMonitorMushroom,
                    [TaskTypes.PlayVideogame] = Options.DisablePlayVideoGame,
                    [TaskTypes.TuneRadio] = Options.DisableFindSignal,
                    [TaskTypes.TestFrisbee] = Options.DisableThrowFisbee,
                    [TaskTypes.LiftWeights] = Options.DisableLiftWeights,
                    [TaskTypes.CollectShells] = Options.DisableCollectShells
                };
            }

            if (!Options.DisableShortTasks.GetBool() && !Options.DisableCommonTasks.GetBool() && !Options.DisableLongTasks.GetBool() && !Options.DisableOtherTasks.GetBool()) return;

            List<NormalPlayerTask> disabledTasks = [];

            foreach (NormalPlayerTask task in unusedTasks)
                if (DisableTasksSettings.TryGetValue(task.TaskType, out OptionItem setting) && setting.GetBool())
                    disabledTasks.Add(task);

            foreach (NormalPlayerTask task in disabledTasks)
            {
                Logger.Msg("Deleted assigned task: " + task.TaskType, "AddTask");
                unusedTasks.Remove(task);
            }
        }
    }

    [HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.RpcSetTasks))]
    internal class RpcSetTasksPatch
    {
        // Patch that overwrites the task just before assigning the task and sending the RPC
        // Do not interfere with vanilla task allocation process itself
        public static void Prefix(NetworkedPlayerInfo __instance, [HarmonyArgument(0)] ref Il2CppStructArray<byte> taskTypeIds)
        {
            // Null measures
            if (Main.RealOptionsData == null)
            {
                Logger.Warn("Warning: RealOptionsData is null.", "RpcSetTasksPatch");
                return;
            }

            PlayerControl pc = __instance.Object;
            if (pc == null) return;

            CustomRoles role = GhostRolesManager.AssignedGhostRoles.TryGetValue(pc.PlayerId, out (CustomRoles Role, IGhostRole Instance) gr) && gr.Instance is Specter or Haunter ? gr.Role : pc.GetCustomRole();

            // Default number of tasks
            var hasCommonTasks = true;
            int NumLongTasks = Main.NormalOptions.NumLongTasks;
            int NumShortTasks = Main.NormalOptions.NumShortTasks;

            if (Options.OverrideTasksData.AllData.TryGetValue(role, out Options.OverrideTasksData data) && data.DoOverride.GetBool())
            {
                hasCommonTasks = data.AssignCommonTasks.GetBool(); // Whether to assign common tasks (regular tasks)
                // Even if assigned, it will not be reassigned and will be assigned the same common tasks as other crews.
                NumLongTasks = data.NumLongTasks.GetInt(); // Number of long tasks to allocate
                NumShortTasks = data.NumShortTasks.GetInt(); // Number of short tasks to allocate
                // Longs and shorts are constantly reallocated.
                if (role is CustomRoles.Specter or CustomRoles.Haunter) Main.PlayerStates[pc.PlayerId].TaskState.AllTasksCount = NumLongTasks + NumShortTasks;
            }

            if (pc.Is(CustomRoles.Busy))
            {
                NumLongTasks += Options.BusyLongTasks.GetInt();
                NumShortTasks += Options.BusyShortTasks.GetInt();
            }

            // Mad Snitch mission coverage
            if (pc.Is(CustomRoles.Snitch) && pc.Is(CustomRoles.Madmate))
            {
                hasCommonTasks = false;
                NumLongTasks = 0;
                NumShortTasks = Options.MadSnitchTasks.GetInt();
            }

            // GM and Lazy Guy have no tasks
            if (pc.Is(CustomRoles.GM) || pc.Is(CustomRoles.Needy) || Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.HotPotato or CustomGameMode.NaturalDisasters)
            {
                hasCommonTasks = false;
                NumShortTasks = 0;
                NumLongTasks = 0;
            }

            // Workhorse task assignment
            if (pc.Is(CustomRoles.Workhorse)) (hasCommonTasks, NumLongTasks, NumShortTasks) = Workhorse.TaskData;

            // Capitalism is going to harm people~
            if (Capitalism.CapitalismAssignTask.ContainsKey(pc.PlayerId))
            {
                NumShortTasks += Capitalism.CapitalismAssignTask[pc.PlayerId];
                Capitalism.CapitalismAssignTask.Remove(pc.PlayerId);
            }

            if (taskTypeIds.Length == 0) hasCommonTasks = false; // Set common to 0 when redistributing tasks

            switch (hasCommonTasks)
            {
                case false when NumLongTasks == 0 && NumShortTasks == 0:
                    NumShortTasks = 1; // Task 0 measures
                    break;
                case true when NumLongTasks == Main.NormalOptions.NumLongTasks && NumShortTasks == Main.NormalOptions.NumShortTasks:
                    return; // If there are no changes
            }

            // List containing IDs of assignable tasks
            // Clone of the second argument of the original RpcSetTasks
            Il2CppSystem.Collections.Generic.List<byte> TasksList = new();
            foreach (byte num in taskTypeIds) TasksList.Add(num);

            // Reference: ShipStatus.Begin
            // Processing to delete unnecessary assigned tasks
            // If the setting is to assign common tasks, delete all other than common tasks
            // Empty the list if no common tasks are assigned
            int defaultCommonTasksNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks);

            if (hasCommonTasks)
                TasksList.RemoveRange(defaultCommonTasksNum, TasksList.Count - defaultCommonTasksNum);
            else
                TasksList.Clear();

            // HashSet where assigned tasks will be placed
            // Prevent multiple assignments of the same task
            Il2CppSystem.Collections.Generic.HashSet<TaskTypes> usedTaskTypes = new();
            var start2 = 0;
            var start3 = 0;

            // List of assignable long tasks
            Il2CppSystem.Collections.Generic.List<NormalPlayerTask> LongTasks = new();
            foreach (NormalPlayerTask task in ShipStatus.Instance.LongTasks) LongTasks.Add(task);

            Shuffle(LongTasks);

            // List of assignable short tasks
            Il2CppSystem.Collections.Generic.List<NormalPlayerTask> ShortTasks = new();
            foreach (NormalPlayerTask task in ShipStatus.Instance.ShortTasks) ShortTasks.Add(task);

            Shuffle(ShortTasks);

            // Use the task assignment function actually used on the Among Us side.
            ShipStatus.Instance.AddTasksFromList(
                ref start2,
                NumLongTasks,
                TasksList,
                usedTaskTypes,
                LongTasks
            );

            ShipStatus.Instance.AddTasksFromList(
                ref start3,
                NumShortTasks,
                TasksList,
                usedTaskTypes,
                ShortTasks
            );

            // Convert list of tasks to array (Il2CppStructArray)
            taskTypeIds = new(TasksList.Count);
            for (var i = 0; i < TasksList.Count; i++) taskTypeIds[i] = TasksList[i];
        }

        private static void Shuffle<T>(Il2CppSystem.Collections.Generic.List<T> list)
        {
            for (var i = 0; i < list.Count - 1; i++)
            {
                T obj = list[i];
                int rand = Random.Range(i, list.Count);
                list[i] = list[rand];
                list[rand] = obj;
            }
        }
    }
}