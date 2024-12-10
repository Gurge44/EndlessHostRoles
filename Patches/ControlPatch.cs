using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using HarmonyLib;
using Rewired;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
internal class ControllerManagerUpdatePatch
{
    private static readonly (int, int)[] Resolutions = [(480, 270), (640, 360), (800, 450), (1280, 720), (1600, 900), (1920, 1080)];
    private static int ResolutionIndex;

    private static bool IsResetting;

    public static void Postfix( /*ControllerManager __instance*/)
    {
        if (GameStates.IsLobby && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            if (Input.GetKeyDown(KeyCode.Tab)) OptionShower.Next();

            for (var i = 0; i < 9; i++)
                if (OrGetKeysDown(KeyCode.Alpha1 + i, KeyCode.Keypad1 + i) && OptionShower.Pages.Count >= i + 1)
                    OptionShower.CurrentPage = i;

            if (KeysDown(KeyCode.Return) && GameSettingMenu.Instance != null && GameSettingMenu.Instance.isActiveAndEnabled)
                GameSettingMenuPatch.SearchForOptionsAction?.Invoke();
        }

        if (KeysDown(KeyCode.LeftShift, KeyCode.LeftControl, KeyCode.X))
            ExileController.Instance?.ReEnableGameplay();

        if (KeysDown(KeyCode.LeftAlt, KeyCode.Return)) LateTask.New(SetResolutionManager.Postfix, 0.01f, "Fix Button Position");

        if (GameStates.IsInGame && (GameStates.IsCanMove || GameStates.IsMeeting) && CustomGameMode.Standard.IsActiveOrIntegrated())
        {
            if (Input.GetKey(KeyCode.F1))
            {
                if (!InGameRoleInfoMenu.Showing) InGameRoleInfoMenu.SetRoleInfoRef(PlayerControl.LocalPlayer);

                InGameRoleInfoMenu.Show();
            }
            else
                InGameRoleInfoMenu.Hide();
        }
        else
            InGameRoleInfoMenu.Hide();

        if (Input.GetKeyDown(KeyCode.F11))
        {
            ResolutionIndex++;
            if (ResolutionIndex >= Resolutions.Length) ResolutionIndex = 0;

            ResolutionManager.SetResolution(Resolutions[ResolutionIndex].Item1, Resolutions[ResolutionIndex].Item2, false);
            SetResolutionManager.Postfix();
        }

        if (KeysDown(KeyCode.F5, KeyCode.T))
        {
            Logger.Info("Reloading Custom Translation File", "KeyCommand");
            LoadLangs();
            Logger.SendInGame("Reloaded Custom Translation File");
        }

        if (KeysDown(KeyCode.F5, KeyCode.X))
        {
            Logger.Info("Exported Custom Translation File", "KeyCommand");
            ExportCustomTranslation();
            Logger.SendInGame("Exported Custom Translation File");
        }

        if (KeysDown(KeyCode.F1, KeyCode.LeftControl))
        {
            Logger.Info("Log dumped", "KeyCommand");
            Utils.DumpLog();
        }

        if (KeysDown(KeyCode.LeftAlt, KeyCode.C) && !Input.GetKey(KeyCode.LeftShift) && !GameStates.IsNotJoined) Utils.CopyCurrentSettings();

        if (!AmongUsClient.Instance.AmHost) return;

        if (KeysDown(KeyCode.Return, KeyCode.C, KeyCode.LeftShift)) HudManager.Instance.Chat.SetVisible(true);

        if (KeysDown(KeyCode.Return, KeyCode.L, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
            GameManager.Instance.LogicFlow.CheckEndCriteria();
        }

        if (KeysDown(KeyCode.Return, KeyCode.M, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            if (GameStates.IsMeeting)
                MeetingHud.Instance.RpcClose();
            else
                PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true);
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && GameStates.IsCountDown && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            Logger.Info("Countdown timer set to 0", "KeyCommand");
            GameStartManager.Instance.countDownTimer = 0;
        }

        if (Input.GetKeyDown(KeyCode.C) && GameStates.IsCountDown && GameStates.IsLobby)
        {
            GameStartManager.Instance.ResetStartState();
            Logger.SendInGame(GetString("CancelStartCountDown"));
        }

        if (KeysDown(KeyCode.N, KeyCode.LeftShift, KeyCode.LeftControl))
        {
            Main.IsChatCommand = true;
            Utils.ShowActiveSettingsHelp();
        }

        if (KeysDown(KeyCode.N, KeyCode.LeftControl) && !Input.GetKey(KeyCode.LeftShift))
        {
            Main.IsChatCommand = true;
            Utils.ShowActiveSettings();
        }

        if (KeysDown(KeyCode.Delete, KeyCode.LeftControl, KeyCode.LeftShift) && !IsResetting)
        {
            IsResetting = true;
            Main.Instance.StartCoroutine(Reset());

            IEnumerator Reset()
            {
                for (var index = 0; index < OptionItem.AllOptions.Count; index++)
                {
                    OptionItem option = OptionItem.AllOptions[index];
                    if (option.Id > 0) option.SetValue(option.DefaultValue);

                    if (index % 100 == 0) yield return null;
                }

                IsResetting = false;
            }
        }

        if (KeysDown(KeyCode.Return, KeyCode.E, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            PlayerControl.LocalPlayer.Data.IsDead = true;
            Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].deathReason = PlayerState.DeathReason.etc;
            PlayerControl.LocalPlayer.RpcExileV2();
            Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
        }

        if (KeysDown(KeyCode.F2, KeyCode.LeftControl))
        {
            Logger.IsAlsoInGame = !Logger.IsAlsoInGame;
            Logger.SendInGame($"In-game output log：{Logger.IsAlsoInGame}");
        }

        if (!Options.NoGameEnd.GetBool()) return;

#if DEBUG

        if (KeysDown(KeyCode.Return, KeyCode.F, KeyCode.LeftShift))
        {
            Utils.FlashColor(new(1f, 0f, 0f, 0.3f));
            if (Constants.ShouldPlaySfx()) RPC.PlaySound(PlayerControl.LocalPlayer.PlayerId, Sounds.KillSound);
        }

        if (KeysDown(KeyCode.Return, KeyCode.G, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            HudManager.Instance.StartCoroutine(HudManager.Instance.CoFadeFullScreen(Color.clear, Color.black));
            HudManager.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro());
        }

        if (KeysDown(KeyCode.Return, KeyCode.V, KeyCode.LeftShift) && GameStates.IsMeeting)
            MeetingHud.Instance.RpcClearVote(AmongUsClient.Instance.ClientId);

        if (KeysDown(KeyCode.Return, KeyCode.D, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 79);
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 80);
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 81);
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 82);
        }

        if (KeysDown(KeyCode.Return, KeyCode.K, KeyCode.LeftShift) && GameStates.IsInGame)
        {
            PlayerControl.LocalPlayer.SetKillTimer(0f);
            PlayerControl.LocalPlayer.SetKillCooldown(0f);
        }

        if (KeysDown(KeyCode.Return, KeyCode.T, KeyCode.LeftShift) && GameStates.IsInGame)
            foreach (PlayerTask task in PlayerControl.LocalPlayer.myTasks)
                PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);

        if (Input.GetKeyDown(KeyCode.Y) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            RPC.SyncCustomSettingsRPC();
            Logger.SendInGame(GetString("SyncCustomSettingsRPC"));
        }

        if (Input.GetKeyDown(KeyCode.Equals) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            Main.VisibleTasksCount = !Main.VisibleTasksCount;
            DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage($"VisibleTaskCount changed to {Main.VisibleTasksCount}.");
        }

        if (Input.GetKeyDown(KeyCode.I) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            Logger.SendInGame(PlayerControl.LocalPlayer.Pos().ToString());
            Logger.SendInGame(PlayerControl.LocalPlayer.GetPlainShipRoom()?.RoomId.ToString() ?? "null");
        }

        if (Input.GetKeyDown(KeyCode.C) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (!pc.AmOwner)
                    pc.MyPhysics.RpcEnterVent(2);
        }

        if (Input.GetKeyDown(KeyCode.V) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            Vector2 pos = PlayerControl.LocalPlayer.NetTransform.transform.position;

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (!pc.AmOwner)
                {
                    pc.TP(pos);
                    pos.x += 0.5f;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.B) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening)
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (!pc.AmOwner)
                    pc.MyPhysics.RpcExitVent(2);
        }

        if (Input.GetKeyDown(KeyCode.N) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening)
            VentilationSystem.Update(VentilationSystem.Operation.StartCleaning, 0);

#endif
    }

    private static bool KeysDown(params KeyCode[] keys)
    {
        if (keys.Any(Input.GetKeyDown) && keys.All(Input.GetKey))
        {
            Logger.Info($"Shortcut Key：{keys.Where(Input.GetKeyDown).First()} in [{string.Join(",", keys)}]", "GetKeysDown");
            return true;
        }

        return false;
    }

    private static bool OrGetKeysDown(params KeyCode[] keys)
    {
        return keys.Any(Input.GetKeyDown);
    }
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
            DestroyableSingleton<HudManager>.Instance.KillButton.DoClick();

        if (player.GetButtonDown(50) && // 50: Impostor vent button actionId
            PlayerControl.LocalPlayer.Data?.Role?.IsImpostor == false &&
            PlayerControl.LocalPlayer.CanUseImpostorVentButton())
            DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.DoClick();
    }
}

// Credit: https://github.com/KARPED1EM/TownOfNext/blob/57c675c0f43cb714801d475b8e1722373f711f10/TONX/Modules/InGameRoleInfoMenu.cs#L8
public static class InGameRoleInfoMenu
{
    private static GameObject Fill;

    private static GameObject Menu;

    private static GameObject MainInfo;
    private static GameObject AddonsInfo;
    public static bool Showing => Fill != null && Fill.active && Menu != null && Menu.active;
    private static SpriteRenderer FillSp => Fill.GetComponent<SpriteRenderer>();
    private static TextMeshPro MainInfoTMP => MainInfo.GetComponent<TextMeshPro>();
    private static TextMeshPro AddonsInfoTMP => AddonsInfo.GetComponent<TextMeshPro>();

    private static void Init()
    {
        Transform DOBScreen = AccountManager.Instance.transform.FindChild("DOBEnterScreen");

        Fill = new("EHR Role Info Menu Fill") { layer = 5 };
        Fill.transform.SetParent(HudManager.Instance.transform.parent, true);
        Fill.transform.localPosition = new(0f, 0f, -980f);
        Fill.transform.localScale = new(20f, 10f, 1f);
        Fill.AddComponent<SpriteRenderer>().sprite = DOBScreen.FindChild("Fill").GetComponent<SpriteRenderer>().sprite;
        FillSp.color = new(0f, 0f, 0f, 0.75f);

        Menu = Object.Instantiate(DOBScreen.FindChild("InfoPage").gameObject, HudManager.Instance.transform.parent);
        Menu.name = "EHR Role Info Menu Page";
        Menu.transform.SetLocalZ(-990f);

        Object.Destroy(Menu.transform.FindChild("Title Text").gameObject);
        Object.Destroy(Menu.transform.FindChild("BackButton").gameObject);
        Object.Destroy(Menu.transform.FindChild("EvenMoreInfo").gameObject);

        MainInfo = Menu.transform.FindChild("InfoText_TMP").gameObject;
        MainInfo.name = "Main Role Info";
        MainInfo.DestroyTranslator();
        MainInfo.transform.localPosition = new(-2.3f, 0.8f, 4f);
        MainInfo.GetComponent<RectTransform>().sizeDelta = new(4.5f, 10f);
        MainInfoTMP.alignment = TextAlignmentOptions.Left;
        MainInfoTMP.fontSize = MainInfoTMP.fontSizeMax = MainInfoTMP.fontSizeMin = 1.75f;

        AddonsInfo = Object.Instantiate(MainInfo, MainInfo.transform.parent);
        AddonsInfo.name = "Addons Info";
        AddonsInfo.DestroyTranslator();
        AddonsInfo.transform.SetLocalX(2.3f);
        AddonsInfo.transform.localScale = new(0.7f, 0.7f, 0.7f);
    }

    public static void SetRoleInfoRef(PlayerControl player)
    {
        if (player == null) return;

        if (!Fill || !Menu) Init();

        CustomRoles role = player.GetCustomRole();
        StringBuilder sb = new();
        StringBuilder titleSb = new();
        StringBuilder settings = new();
        StringBuilder addons = new();
        settings.Append("<size=75%>");
        titleSb.Append($"{role.ToColoredString()} {Utils.GetRoleMode(role)}");
        sb.Append("<size=90%>");
        sb.Append(player.GetRoleInfo(true).TrimStart());
        if (Options.CustomRoleSpawnChances.TryGetValue(role, out StringOptionItem opt)) Utils.ShowChildrenSettings(opt, ref settings, f1: true, disableColor: false);

        settings.Append("</size>");
        if (settings.Length > 0) addons.Append($"{settings}\n\n");

        if (role.PetActivatedAbility()) sb.Append($"<size=80%>{GetString("SupportsPetMessage")}</size>");

        string searchStr = GetString(role.ToString());
        sb.Replace(searchStr, role.ToColoredString());
        sb.Replace(searchStr.ToLower(), role.ToColoredString());
        sb.Append("</size>");
        List<CustomRoles> subRoles = Main.PlayerStates[player.PlayerId].SubRoles;
        if (subRoles.Count > 0) addons.Append(GetString("AddonListTitle"));

        addons.Append("<size=75%>");

        subRoles.ForEach(subRole =>
        {
            addons.Append($"\n\n{subRole.ToColoredString()} {Utils.GetRoleMode(subRole)} {GetString($"{subRole}InfoLong")}");
            string searchSubStr = GetString(subRole.ToString());
            addons.Replace(searchSubStr, subRole.ToColoredString());
            addons.Replace(searchSubStr.ToLower(), subRole.ToColoredString());
        });

        addons.Append("</size>");
        if (role.UsesPetInsteadOfKill()) sb.Append($"\n\n<size=85%>{GetString("UsesPetInsteadOfKillNotice")}</size>");

        sb.Insert(0, $"{titleSb}\n");

        MainInfoTMP.text = sb.ToString();
        AddonsInfoTMP.text = addons.ToString();
    }

    public static void Show()
    {
        if (!Fill || !Menu) Init();

        if (!Showing)
        {
            Fill?.SetActive(true);
            Menu?.SetActive(true);
        }
    }

    public static void Hide()
    {
        if (Showing)
        {
            Fill?.SetActive(false);
            Menu?.SetActive(false);
        }
    }
}