using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EHR.Modules;
using HarmonyLib;
using Rewired;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
internal class ControllerManagerUpdatePatch
{
    private static readonly (int, int)[] Resolutions = [(480, 270), (640, 360), (800, 450), (1280, 720), (1600, 900), (1920, 1080)];
    private static int ResolutionIndex;

    private static List<string> AddDes = [];
    private static int AddonIndex = -1;

    public static void Postfix( /*ControllerManager __instance*/)
    {
        if (GameStates.IsLobby)
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                OptionShower.Next();
            }

            for (var i = 0; i < 9; i++)
            {
                if (OrGetKeysDown(KeyCode.Alpha1 + i, KeyCode.Keypad1 + i) && OptionShower.Pages.Count >= i + 1)
                    OptionShower.CurrentPage = i;
            }
        }

        if (GetKeysDown(KeyCode.LeftShift, KeyCode.LeftControl, KeyCode.X))
        {
            ExileController.Instance?.ReEnableGameplay();
        }

        if (GetKeysDown(KeyCode.LeftAlt, KeyCode.Return))
        {
            LateTask.New(SetResolutionManager.Postfix, 0.01f, "Fix Button Position");
        }

        if (Input.GetKeyDown(KeyCode.F1) && GameStates.InGame && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            try
            {
                var lp = PlayerControl.LocalPlayer;
                var role = lp.GetCustomRole();
                HudManager.Instance.ShowPopUp(GetString(role.ToString()) + Utils.GetRoleMode(role) + lp.GetRoleInfo(true));
            }
            catch (Exception ex)
            {
                Utils.ThrowException(ex);
            }
        }

        if (Input.GetKeyDown(KeyCode.F2) && GameStates.InGame && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            try
            {
                var lp = PlayerControl.LocalPlayer;
                if (Main.PlayerStates[lp.PlayerId].SubRoles.Count == 0) return;

                AddDes = [];
                foreach (var subRole in Main.PlayerStates[lp.PlayerId].SubRoles.Where(x => !x.IsConverted()))
                    AddDes.Add(GetString($"{subRole}") + Utils.GetRoleMode(subRole) + GetString($"{subRole}InfoLong"));

                AddonIndex++;
                if (AddonIndex >= AddDes.Count) AddonIndex = 0;
                HudManager.Instance.ShowPopUp(AddDes[AddonIndex]);
            }
            catch (Exception ex)
            {
                Utils.ThrowException(ex);
            }
        }

        if (Input.GetKeyDown(KeyCode.F3) && GameStates.InGame && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            try
            {
                var lp = PlayerControl.LocalPlayer;
                var role = lp.GetCustomRole();
                var sb = new StringBuilder();
                if (Options.CustomRoleSpawnChances.TryGetValue(role, out var soi))
                    Utils.ShowChildrenSettings(soi, ref sb, command: true, disableColor: false);
                HudManager.Instance.ShowPopUp(sb.ToString().Trim());
            }
            catch (Exception ex)
            {
                Utils.ThrowException(ex);
            }
        }

        if (Input.GetKeyDown(KeyCode.F11))
        {
            ResolutionIndex++;
            if (ResolutionIndex >= Resolutions.Length) ResolutionIndex = 0;
            ResolutionManager.SetResolution(Resolutions[ResolutionIndex].Item1, Resolutions[ResolutionIndex].Item2, false);
            SetResolutionManager.Postfix();
        }

        if (GetKeysDown(KeyCode.F5, KeyCode.T))
        {
            Logger.Info("Reloading Custom Translation File", "KeyCommand");
            LoadLangs();
            Logger.SendInGame("Reloaded Custom Translation File");
        }

        if (GetKeysDown(KeyCode.F5, KeyCode.X))
        {
            Logger.Info("Exported Custom Translation File", "KeyCommand");
            ExportCustomTranslation();
            Logger.SendInGame("Exported Custom Translation File");
        }

        if (GetKeysDown(KeyCode.F1, KeyCode.LeftControl))
        {
            Logger.Info("Log dumped", "KeyCommand");
            Utils.DumpLog();
        }

        if (GetKeysDown(KeyCode.LeftAlt, KeyCode.C) && !Input.GetKey(KeyCode.LeftShift) && !GameStates.IsNotJoined)
        {
            Utils.CopyCurrentSettings();
        }

        if (!AmongUsClient.Instance.AmHost) return;

        if (GetKeysDown(KeyCode.Return, KeyCode.C, KeyCode.LeftShift))
        {
            HudManager.Instance.Chat.SetVisible(true);
        }

        if (GetKeysDown(KeyCode.Return, KeyCode.L, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
            GameManager.Instance.LogicFlow.CheckEndCriteria();
        }

        if (GetKeysDown(KeyCode.Return, KeyCode.M, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            if (GameStates.IsMeeting) MeetingHud.Instance.RpcClose();
            else PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true);
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && GameStates.IsCountDown && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            Logger.Info("Countdown timer set to 0", "KeyCommand");
            GameStartManager.Instance.countDownTimer = 0;
        }

        if (Input.GetKeyDown(KeyCode.C) && GameStates.IsCountDown)
        {
            GameStartManager.Instance.ResetStartState();
            Logger.SendInGame(GetString("CancelStartCountDown"));
        }

        if (GetKeysDown(KeyCode.N, KeyCode.LeftShift, KeyCode.LeftControl))
        {
            Main.IsChatCommand = true;
            Utils.ShowActiveSettingsHelp();
        }

        if (GetKeysDown(KeyCode.N, KeyCode.LeftControl) && !Input.GetKey(KeyCode.LeftShift))
        {
            Main.IsChatCommand = true;
            Utils.ShowActiveSettings();
        }

        if (GetKeysDown(KeyCode.Delete, KeyCode.LeftControl, KeyCode.LeftShift))
        {
            OptionItem.AllOptions.Where(x => x.Id > 0).Do(x => x.SetValue(x.DefaultValue));
        }

        if (GetKeysDown(KeyCode.Return, KeyCode.E, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            PlayerControl.LocalPlayer.Data.IsDead = true;
            Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].deathReason = PlayerState.DeathReason.etc;
            PlayerControl.LocalPlayer.RpcExileV2();
            Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
        }

        if (GetKeysDown(KeyCode.F2, KeyCode.LeftControl))
        {
            Logger.IsAlsoInGame = !Logger.IsAlsoInGame;
            Logger.SendInGame($"In-game output log：{Logger.IsAlsoInGame}");
        }

        if (!DebugModeManager.IsDebugMode) return;

        if (GetKeysDown(KeyCode.Return, KeyCode.F, KeyCode.LeftShift))
        {
            Utils.FlashColor(new(1f, 0f, 0f, 0.3f));
            if (Constants.ShouldPlaySfx()) RPC.PlaySound(PlayerControl.LocalPlayer.PlayerId, Sounds.KillSound);
        }

        if (GetKeysDown(KeyCode.Return, KeyCode.G, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            HudManager.Instance.StartCoroutine(HudManager.Instance.CoFadeFullScreen(Color.clear, Color.black));
            HudManager.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro());
        }

        if (GetKeysDown(KeyCode.Return, KeyCode.V, KeyCode.LeftShift) && GameStates.IsMeeting)
        {
            MeetingHud.Instance.RpcClearVote(AmongUsClient.Instance.ClientId);
        }

        if (GetKeysDown(KeyCode.Return, KeyCode.D, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 79);
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 80);
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 81);
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 82);
        }

        if (GetKeysDown(KeyCode.Return, KeyCode.K, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            PlayerControl.LocalPlayer.SetKillTimer(0f);
            PlayerControl.LocalPlayer.SetKillCooldown(0f);
        }

        if (GetKeysDown(KeyCode.Return, KeyCode.T, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            foreach (var task in PlayerControl.LocalPlayer.myTasks)
                PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            RPC.SyncCustomSettingsRPC();
            Logger.SendInGame(GetString("SyncCustomSettingsRPC"));
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            HudManager.Instance.StartCoroutine(HudManager.Instance.CoFadeFullScreen(Color.clear, Color.black));
            HudManager.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro());
        }

        if (Input.GetKeyDown(KeyCode.Equals))
        {
            Main.VisibleTasksCount = !Main.VisibleTasksCount;
            DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage($"VisibleTaskCount changed to {Main.VisibleTasksCount}.");
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            Logger.SendInGame(PlayerControl.LocalPlayer.Pos().ToString());
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (!pc.AmOwner) pc.MyPhysics.RpcEnterVent(2);
            }
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            Vector2 pos = PlayerControl.LocalPlayer.NetTransform.transform.position;
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (!pc.AmOwner)
                {
                    pc.TP(pos);
                    pos.x += 0.5f;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (!pc.AmOwner) pc.MyPhysics.RpcExitVent(2);
            }
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            VentilationSystem.Update(VentilationSystem.Operation.StartCleaning, 0);
        }
    }

    private static bool GetKeysDown(params KeyCode[] keys)
    {
        if (keys.Any(Input.GetKeyDown) && keys.All(Input.GetKey))
        {
            Logger.Info($"Shortcut Key：{keys.Where(Input.GetKeyDown).First()} in [{string.Join(",", keys)}]", "GetKeysDown");
            return true;
        }

        return false;
    }

    private static bool OrGetKeysDown(params KeyCode[] keys) => keys.Any(Input.GetKeyDown);
}

[HarmonyPatch(typeof(ConsoleJoystick), nameof(ConsoleJoystick.HandleHUD))]
internal class ConsoleJoystickHandleHUDPatch
{
    public static void Postfix()
    {
        HandleHUDPatch.Postfix(ConsoleJoystick.player);
    }
}

[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.HandleHud))]
internal class KeyboardJoystickHandleHUDPatch
{
    public static void Postfix()
    {
        HandleHUDPatch.Postfix(KeyboardJoystick.player);
    }
}

internal static class HandleHUDPatch
{
    public static void Postfix(Player player)
    {
        if (player.GetButtonDown(8) && // 8: Kill button actionId
            PlayerControl.LocalPlayer.Data?.Role?.IsImpostor == false &&
            PlayerControl.LocalPlayer.CanUseKillButton())
        {
            DestroyableSingleton<HudManager>.Instance.KillButton.DoClick();
        }

        if (player.GetButtonDown(50) && // 50: Impostor vent button actionId
            PlayerControl.LocalPlayer.Data?.Role?.IsImpostor == false &&
            PlayerControl.LocalPlayer.CanUseImpostorVentButton())
        {
            DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.DoClick();
        }
    }
}