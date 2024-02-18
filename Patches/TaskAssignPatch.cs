using AmongUs.GameOptions;
using HarmonyLib;
using System.Collections.Generic;
using TOHE.Roles.AddOns.Crewmate;

namespace TOHE;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.AddTasksFromList))]
class AddTasksFromListPatch
{
    public static void Prefix( /*ShipStatus __instance,*/
        [HarmonyArgument(4)] Il2CppSystem.Collections.Generic.List<NormalPlayerTask> unusedTasks)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!Options.DisableShortTasks.GetBool() && !Options.DisableCommonTasks.GetBool() && !Options.DisableLongTasks.GetBool() && !Options.DisableOtherTasks.GetBool()) return;
        List<NormalPlayerTask> disabledTasks = [];
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
                default:
                    break;
            }
        }

        for (int i = 0; i < disabledTasks.Count; i++)
        {
            NormalPlayerTask task = disabledTasks[i];
            Logger.Msg("Deleted assigned task: " + task.TaskType.ToString(), "AddTask");
            unusedTasks.Remove(task);
        }
    }
}

[HarmonyPatch(typeof(GameData), nameof(GameData.RpcSetTasks))]
class RpcSetTasksPatch
{
    //タスクを割り当ててRPCを送る処理が行われる直前にタスクを上書きするPatch
    //バニラのタスク割り当て処理自体には干渉しない
    public static void Prefix( /*GameData __instance,*/
        [HarmonyArgument(0)] byte playerId,
        [HarmonyArgument(1)] ref Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> taskTypeIds)
    {
        //null対策
        if (Main.RealOptionsData == null)
        {
            Logger.Warn("警告:RealOptionsDataがnullです。", "RpcSetTasksPatch");
            return;
        }

        var pc = Utils.GetPlayerById(playerId);
        CustomRoles? RoleNullable = pc?.GetCustomRole();
        if (RoleNullable == null) return;
        CustomRoles role = RoleNullable.Value;

        //デフォルトのタスク数
        bool hasCommonTasks = true;
        int NumLongTasks = Main.NormalOptions.NumLongTasks;
        int NumShortTasks = Main.NormalOptions.NumShortTasks;

        if (Options.OverrideTasksData.AllData.TryGetValue(role, out var data) && data.doOverride.GetBool())
        {
            hasCommonTasks = data.assignCommonTasks.GetBool(); // コモンタスク(通常タスク)を割り当てるかどうか
            // 割り当てる場合でも再割り当てはされず、他のクルーと同じコモンタスクが割り当てられる。
            NumLongTasks = data.numLongTasks.GetInt(); // 割り当てるロングタスクの数
            NumShortTasks = data.numShortTasks.GetInt(); // 割り当てるショートタスクの数
            // ロングとショートは常時再割り当てが行われる。
        }

        if (pc.Is(CustomRoles.Busy))
        {
            NumLongTasks += Options.BusyLongTasks.GetInt();
            NumShortTasks += Options.BusyShortTasks.GetInt();
        }

        //背叛告密的任务覆盖
        if (pc.Is(CustomRoles.Snitch) && pc.Is(CustomRoles.Madmate))
        {
            hasCommonTasks = false;
            NumLongTasks = 0;
            NumShortTasks = Options.MadSnitchTasks.GetInt();
        }

        //管理员和摆烂人没有任务
        if (pc.Is(CustomRoles.GM) || pc.Is(CustomRoles.Needy) || Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA || pc.Is(CustomRoles.Lazy))
        {
            hasCommonTasks = false;
            NumShortTasks = 0;
            NumLongTasks = 0;
        }

        //加班狂加班咯~
        if (pc.Is(CustomRoles.Workhorse))
            (hasCommonTasks, NumLongTasks, NumShortTasks) = Workhorse.TaskData;

        //资本主义要祸害人咯~
        if (Main.CapitalismAssignTask.ContainsKey(playerId))
        {
            NumShortTasks += Main.CapitalismAssignTask[playerId];
            Main.CapitalismAssignTask.Remove(playerId);
        }

        if (taskTypeIds.Length == 0) hasCommonTasks = false; //タスク再配布時はコモンを0に
        if (!hasCommonTasks && NumLongTasks == 0 && NumShortTasks == 0) NumShortTasks = 1; //タスク0対策
        if (hasCommonTasks && NumLongTasks == Main.NormalOptions.NumLongTasks && NumShortTasks == Main.NormalOptions.NumShortTasks) return; //変更点がない場合

        //割り当て可能なタスクのIDが入ったリスト
        //本来のRpcSetTasksの第二引数のクローン
        Il2CppSystem.Collections.Generic.List<byte> TasksList = new();
        for (int i1 = 0; i1 < taskTypeIds.Count; i1++)
        {
            byte num = taskTypeIds[i1];
            TasksList.Add(num);
        }

        //参考:ShipStatus.Begin
        //不要な割り当て済みのタスクを削除する処理
        //コモンタスクを割り当てる設定ならコモンタスク以外を削除
        //コモンタスクを割り当てない設定ならリストを空にする
        int defaultCommonTasksNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks);
        if (hasCommonTasks) TasksList.RemoveRange(defaultCommonTasksNum, TasksList.Count - defaultCommonTasksNum);
        else TasksList.Clear();

        //割り当て済みのタスクが入れられるHashSet
        //同じタスクが複数割り当てられるのを防ぐ
        Il2CppSystem.Collections.Generic.HashSet<TaskTypes> usedTaskTypes = new();
        int start2 = 0;
        int start3 = 0;

        //割り当て可能なロングタスクのリスト
        Il2CppSystem.Collections.Generic.List<NormalPlayerTask> LongTasks = new();
        foreach (var task in ShipStatus.Instance.LongTasks)
            LongTasks.Add(task);
        Shuffle(LongTasks);

        //割り当て可能なショートタスクのリスト
        Il2CppSystem.Collections.Generic.List<NormalPlayerTask> ShortTasks = new();
        foreach (var task in ShipStatus.Instance.ShortTasks)
            ShortTasks.Add(task);
        Shuffle(ShortTasks);

        //実際にAmong Us側で使われているタスクを割り当てる関数を使う。
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

        //タスクのリストを配列(Il2CppStructArray)に変換する
        taskTypeIds = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(TasksList.Count);
        for (int i = 0; i < TasksList.Count; i++)
        {
            taskTypeIds[i] = TasksList[i];
        }
    }

    public static void Shuffle<T>(Il2CppSystem.Collections.Generic.List<T> list)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            T obj = list[i];
            int rand = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[rand];
            list[rand] = obj;
        }
    }
}