using AmongUs.GameOptions;
using EHR.AddOns.Crewmate;
using EHR.AddOns.GhostRoles;
using EHR.Impostor;
using EHR.Modules;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using Random = UnityEngine.Random;

namespace EHR;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.AddTasksFromList))]
class AddTasksFromListPatch
{
    public static void Prefix( /*ShipStatus __instance,*/
        [HarmonyArgument(4)] List<NormalPlayerTask> unusedTasks)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!Options.DisableShortTasks.GetBool() && !Options.DisableCommonTasks.GetBool() && !Options.DisableLongTasks.GetBool() && !Options.DisableOtherTasks.GetBool()) return;
        System.Collections.Generic.List<NormalPlayerTask> disabledTasks = [];
        for (var i = 0; i < unusedTasks.Count; i++)
        {
            var task = unusedTasks[i];
            switch (task.TaskType)
            {
                case TaskTypes.SwipeCard when Options.DisableSwipeCard.GetBool():
                case TaskTypes.SubmitScan when Options.DisableSubmitScan.GetBool():
                case TaskTypes.UnlockSafe when Options.DisableUnlockSafe.GetBool():
                case TaskTypes.UploadData when Options.DisableUploadData.GetBool():
                case TaskTypes.StartReactor when Options.DisableStartReactor.GetBool():
                case TaskTypes.ResetBreakers when Options.DisableResetBreaker.GetBool():
                case TaskTypes.VentCleaning when Options.DisableCleanVent.GetBool():
                case TaskTypes.CalibrateDistributor when Options.DisableCalibrateDistributor.GetBool():
                case TaskTypes.ChartCourse when Options.DisableChartCourse.GetBool():
                case TaskTypes.StabilizeSteering when Options.DisableStabilizeSteering.GetBool():
                case TaskTypes.CleanO2Filter when Options.DisableCleanO2Filter.GetBool():
                case TaskTypes.UnlockManifolds when Options.DisableUnlockManifolds.GetBool():
                case TaskTypes.PrimeShields when Options.DisablePrimeShields.GetBool():
                case TaskTypes.MeasureWeather when Options.DisableMeasureWeather.GetBool():
                case TaskTypes.BuyBeverage when Options.DisableBuyBeverage.GetBool():
                case TaskTypes.AssembleArtifact when Options.DisableAssembleArtifact.GetBool():
                case TaskTypes.SortSamples when Options.DisableSortSamples.GetBool():
                case TaskTypes.ProcessData when Options.DisableProcessData.GetBool():
                case TaskTypes.RunDiagnostics when Options.DisableRunDiagnostics.GetBool():
                case TaskTypes.RepairDrill when Options.DisableRepairDrill.GetBool():
                case TaskTypes.AlignTelescope when Options.DisableAlignTelescope.GetBool():
                case TaskTypes.RecordTemperature when Options.DisableRecordTemperature.GetBool():
                case TaskTypes.FillCanisters when Options.DisableFillCanisters.GetBool():
                case TaskTypes.MonitorOxygen when Options.DisableMonitorTree.GetBool():
                case TaskTypes.StoreArtifacts when Options.DisableStoreArtifacts.GetBool():
                case TaskTypes.PutAwayPistols when Options.DisablePutAwayPistols.GetBool():
                case TaskTypes.PutAwayRifles when Options.DisablePutAwayRifles.GetBool():
                case TaskTypes.MakeBurger when Options.DisableMakeBurger.GetBool():
                case TaskTypes.CleanToilet when Options.DisableCleanToilet.GetBool():
                case TaskTypes.Decontaminate when Options.DisableDecontaminate.GetBool():
                case TaskTypes.SortRecords when Options.DisableSortRecords.GetBool():
                case TaskTypes.FixShower when Options.DisableFixShower.GetBool():
                case TaskTypes.PickUpTowels when Options.DisablePickUpTowels.GetBool():
                case TaskTypes.PolishRuby when Options.DisablePolishRuby.GetBool():
                case TaskTypes.DressMannequin when Options.DisableDressMannequin.GetBool():
                case TaskTypes.AlignEngineOutput when Options.DisableAlignEngineOutput.GetBool():
                case TaskTypes.InspectSample when Options.DisableInspectSample.GetBool():
                case TaskTypes.EmptyChute when Options.DisableEmptyChute.GetBool():
                case TaskTypes.ClearAsteroids when Options.DisableClearAsteroids.GetBool():
                case TaskTypes.WaterPlants when Options.DisableWaterPlants.GetBool():
                case TaskTypes.OpenWaterways when Options.DisableOpenWaterways.GetBool():
                case TaskTypes.ReplaceWaterJug when Options.DisableReplaceWaterJug.GetBool():
                case TaskTypes.RebootWifi when Options.DisableRebootWifi.GetBool():
                case TaskTypes.DevelopPhotos when Options.DisableDevelopPhotos.GetBool():
                case TaskTypes.RewindTapes when Options.DisableRewindTapes.GetBool():
                case TaskTypes.StartFans when Options.DisableStartFans.GetBool():
                case TaskTypes.FixWiring when Options.DisableFixWiring.GetBool():
                case TaskTypes.EnterIdCode when Options.DisableEnterIdCode.GetBool():
                case TaskTypes.InsertKeys when Options.DisableInsertKeys.GetBool():
                case TaskTypes.ScanBoardingPass when Options.DisableScanBoardingPass.GetBool():
                case TaskTypes.EmptyGarbage when Options.DisableEmptyGarbage.GetBool():
                case TaskTypes.FuelEngines when Options.DisableFuelEngines.GetBool():
                case TaskTypes.DivertPower when Options.DisableDivertPower.GetBool():
                case TaskTypes.FixWeatherNode when Options.DisableActivateWeatherNodes.GetBool():
                case TaskTypes.RoastMarshmallow when Options.DisableRoastMarshmallow.GetBool():
                case TaskTypes.CollectSamples when Options.DisableCollectSamples.GetBool():
                case TaskTypes.ReplaceParts when Options.DisableReplaceParts.GetBool():
                case TaskTypes.CollectVegetables when Options.DisableCollectVegetables.GetBool():
                case TaskTypes.MineOres when Options.DisableMineOres.GetBool():
                case TaskTypes.ExtractFuel when Options.DisableExtractFuel.GetBool():
                case TaskTypes.CatchFish when Options.DisableCatchFish.GetBool():
                case TaskTypes.PolishGem when Options.DisablePolishGem.GetBool():
                case TaskTypes.HelpCritter when Options.DisableHelpCritter.GetBool():
                case TaskTypes.HoistSupplies when Options.DisableHoistSupplies.GetBool():
                case TaskTypes.FixAntenna when Options.DisableFixAntenna.GetBool():
                case TaskTypes.BuildSandcastle when Options.DisableBuildSandcastle.GetBool():
                case TaskTypes.CrankGenerator when Options.DisableCrankGenerator.GetBool():
                case TaskTypes.MonitorMushroom when Options.DisableMonitorMushroom.GetBool():
                case TaskTypes.PlayVideogame when Options.DisablePlayVideoGame.GetBool():
                case TaskTypes.TuneRadio when Options.DisableFindSignal.GetBool():
                case TaskTypes.TestFrisbee when Options.DisableThrowFisbee.GetBool():
                case TaskTypes.LiftWeights when Options.DisableLiftWeights.GetBool():
                case TaskTypes.CollectShells when Options.DisableCollectShells.GetBool():
                    disabledTasks.Add(task);
                    break;
            }
        }

        foreach (var task in disabledTasks)
        {
            Logger.Msg("Deleted assigned task: " + task.TaskType, "AddTask");
            unusedTasks.Remove(task);
        }
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.RpcSetTasks))]
class RpcSetTasksPatch
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

        var pc = __instance.Object;
        if (pc == null) return;
        CustomRoles role = GhostRolesManager.AssignedGhostRoles.TryGetValue(pc.PlayerId, out var gr) && gr.Instance is Specter or Haunter ? gr.Role : pc.GetCustomRole();

        // Default number of tasks
        bool hasCommonTasks = true;
        int NumLongTasks = Main.NormalOptions.NumLongTasks;
        int NumShortTasks = Main.NormalOptions.NumShortTasks;

        if (Options.OverrideTasksData.AllData.TryGetValue(role, out var data) && data.DoOverride.GetBool())
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
        if (pc.Is(CustomRoles.Workhorse))
            (hasCommonTasks, NumLongTasks, NumShortTasks) = Workhorse.TaskData;

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
        List<byte> TasksList = new();
        foreach (var num in taskTypeIds)
        {
            TasksList.Add(num);
        }

        // Reference: ShipStatus.Begin
        // Processing to delete unnecessary assigned tasks
        // If the setting is to assign common tasks, delete all other than common tasks
        // Empty the list if no common tasks are assigned
        int defaultCommonTasksNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks);
        if (hasCommonTasks) TasksList.RemoveRange(defaultCommonTasksNum, TasksList.Count - defaultCommonTasksNum);
        else TasksList.Clear();

        // HashSet where assigned tasks will be placed
        // Prevent multiple assignments of the same task
        HashSet<TaskTypes> usedTaskTypes = new();
        int start2 = 0;
        int start3 = 0;

        // List of assignable long tasks
        List<NormalPlayerTask> LongTasks = new();
        foreach (var task in ShipStatus.Instance.LongTasks)
            LongTasks.Add(task);
        Shuffle(LongTasks);

        // List of assignable short tasks
        List<NormalPlayerTask> ShortTasks = new();
        foreach (var task in ShipStatus.Instance.ShortTasks)
            ShortTasks.Add(task);
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
        for (int i = 0; i < TasksList.Count; i++)
        {
            taskTypeIds[i] = TasksList[i];
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            T obj = list[i];
            int rand = Random.Range(i, list.Count);
            list[i] = list[rand];
            list[rand] = obj;
        }
    }
}