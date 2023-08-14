using HarmonyLib;
using Il2CppSystem.Text;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
class HudManagerPatch
{
    public static bool ShowDebugText = false;
    public static int LastCallNotifyRolesPerSecond = 0;
    public static int NowCallNotifyRolesCount = 0;
    public static int LastSetNameDesyncCount = 0;
    public static int LastFPS = 0;
    public static int NowFrameCount = 0;
    public static float FrameRateTimer = 0.0f;
    public static TMPro.TextMeshPro LowerInfoText;
    public static void Postfix(HudManager __instance)
    {
        if (!GameStates.IsModHost) return;
        var player = PlayerControl.LocalPlayer;
        if (player == null) return;
        //壁抜け
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if ((!AmongUsClient.Instance.IsGameStarted || !GameStates.IsOnlineGame)
                && player.CanMove)
            {
                player.Collider.offset = new Vector2(0f, 127f);
            }
        }
        //壁抜け解除
        if (player.Collider.offset.y == 127f)
        {
            if (!Input.GetKey(KeyCode.LeftControl) || (AmongUsClient.Instance.IsGameStarted && GameStates.IsOnlineGame))
            {
                player.Collider.offset = new Vector2(0f, -0.3636f);
            }
        }
        if (GameStates.IsLobby)
        {
            var POM = GameObject.Find("PlayerOptionsMenu(Clone)");
            __instance.GameSettings.text = POM != null ? "" : OptionShower.GetTextNoFresh();
            __instance.GameSettings.fontSizeMin =
            __instance.GameSettings.fontSizeMax = 1f;
        }
        //ゲーム中でなければ以下は実行されない
        if (!AmongUsClient.Instance.IsGameStarted) return;

        Utils.CountAlivePlayers();

        bool shapeshifting = Main.CheckShapeshift.TryGetValue(player.PlayerId, out bool ss) && ss;

        if (SetHudActivePatch.IsActive)
        {
            if (player.IsAlive())
            {
                //MOD入り用のボタン下テキスト変更
                switch (player.GetCustomRole())
                {
                    case CustomRoles.Sniper:
                        Sniper.OverrideShapeText(player.PlayerId);
                        break;
                    case CustomRoles.FireWorks:
                        if (FireWorks.nowFireWorksCount[player.PlayerId] == 0)
                            __instance.AbilityButton.OverrideText(GetString("FireWorksExplosionButtonText"));
                        else
                            __instance.AbilityButton.OverrideText(GetString("FireWorksInstallAtionButtonText"));
                        break;
                    case CustomRoles.SerialKiller:
                        SerialKiller.GetAbilityButtonText(__instance, player);
                        break;
                    case CustomRoles.Warlock:
                        bool curse = Main.isCurseAndKill.TryGetValue(player.PlayerId, out bool wcs) && wcs;
                        if (!shapeshifting && !curse)
                            __instance.KillButton.OverrideText(GetString("WarlockCurseButtonText"));
                        else
                            __instance.KillButton.OverrideText(GetString("KillButtonText"));
                        if (!shapeshifting && curse)
                            __instance.AbilityButton.OverrideText(GetString("WarlockShapeshiftButtonText"));
                        break;
                    case CustomRoles.Miner:
                        __instance.AbilityButton.OverrideText(GetString("MinerTeleButtonText"));
                        break;
                    case CustomRoles.Pestilence:
                        __instance.KillButton.OverrideText(GetString("KillButtonText"));
                        break;
                    case CustomRoles.PlagueBearer:
                        __instance.KillButton.OverrideText(GetString("InfectiousKillButtonText"));
                        break;
                    case CustomRoles.Witch:
                        Witch.GetAbilityButtonText(__instance);
                        break;
                    case CustomRoles.HexMaster:
                        HexMaster.GetAbilityButtonText(__instance);
                        break;
                    case CustomRoles.Vampire:
                        Vampire.SetKillButtonText();
                        break;
                    case CustomRoles.Poisoner:
                        Poisoner.SetKillButtonText();
                        break;
                    case CustomRoles.Arsonist:
                        __instance.KillButton.OverrideText(GetString("ArsonistDouseButtonText"));
                        __instance.ImpostorVentButton.buttonLabelText.text = GetString("ArsonistVentButtonText");
                        break;
                    case CustomRoles.Revolutionist:
                        __instance.KillButton.OverrideText(GetString("RevolutionistDrawButtonText"));
                        __instance.ImpostorVentButton.buttonLabelText.text = GetString("RevolutionistVentButtonText");
                        break;
                    case CustomRoles.Farseer:
                        __instance.KillButton.OverrideText(GetString("FarseerKillButtonText"));
                        break;
                    case CustomRoles.Puppeteer:
                        __instance.KillButton.OverrideText(GetString("PuppeteerOperateButtonText"));
                        break;
                    case CustomRoles.NWitch:
                        __instance.KillButton.OverrideText($"{GetString("WitchControlButtonText")}");
                        break;
                    case CustomRoles.BountyHunter:
                        BountyHunter.SetAbilityButtonText(__instance);
                        break;
                    case CustomRoles.EvilTracker:
                        EvilTracker.GetAbilityButtonText(__instance, player.PlayerId);
                        break;
                    case CustomRoles.Innocent:
                        __instance.KillButton.OverrideText(GetString("InnocentButtonText"));
                        break;
                    case CustomRoles.Capitalism:
                        __instance.KillButton.OverrideText(GetString("CapitalismButtonText"));
                        break;
                    case CustomRoles.Pelican:
                        __instance.KillButton.OverrideText(GetString("PelicanButtonText"));
                        break;
                    case CustomRoles.Counterfeiter:
                        __instance.KillButton.OverrideText(GetString("CounterfeiterButtonText"));
                        break;
                    case CustomRoles.Witness:
                        __instance.KillButton.OverrideText(GetString("WitnessButtonText"));
                        break;
                    case CustomRoles.Pursuer:
                        __instance.KillButton.OverrideText(GetString("PursuerButtonText"));
                        break;
                    case CustomRoles.Gangster:
                        Gangster.SetKillButtonText(player.PlayerId);
                        break;
                    case CustomRoles.NSerialKiller:
                    case CustomRoles.RuthlessRomantic:
                    case CustomRoles.Juggernaut:
                    case CustomRoles.Jackal:
                    case CustomRoles.Virus:
                    case CustomRoles.BloodKnight:
                    case CustomRoles.SwordsMan:
                    case CustomRoles.Parasite:
                    case CustomRoles.Refugee:
                    case CustomRoles.Traitor:
                    case CustomRoles.Ritualist:
                    case CustomRoles.Spiritcaller:
                    case CustomRoles.DarkHide:
                    case CustomRoles.Maverick:
                        __instance.KillButton.OverrideText(GetString("KillButtonText"));
                        break;
                    case CustomRoles.Glitch:
                        __instance.SabotageButton.OverrideText(GetString("HackButtonText"));
                        break;
                    case CustomRoles.FFF:
                        __instance.KillButton.OverrideText(GetString("FFFButtonText"));
                        break;
                    case CustomRoles.Medic:
                        __instance.KillButton.OverrideText(GetString("MedicalerButtonText"));
                        break;
                    case CustomRoles.Gamer:
                        __instance.KillButton.OverrideText(GetString("GamerButtonText"));
                        break;
                    case CustomRoles.BallLightning:
                        __instance.KillButton.OverrideText(GetString("BallLightningButtonText"));
                        break;
                    case CustomRoles.Bomber:
                    case CustomRoles.Nuker:
                        __instance.AbilityButton.OverrideText(GetString("BomberShapeshiftText"));
                        break;
                    case CustomRoles.Twister:
                        __instance.AbilityButton.OverrideText(GetString("TwisterButtonText"));
                        break;
                    case CustomRoles.ImperiusCurse:
                        __instance.AbilityButton.OverrideText(GetString("ImperiusCurseButtonText"));
                        break;
                    case CustomRoles.QuickShooter:
                        __instance.AbilityButton.OverrideText(GetString("QuickShooterShapeshiftText"));
                        __instance.AbilityButton.SetUsesRemaining(QuickShooter.ShotLimit.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var qx) ? qx : 0);
                        break;
                    case CustomRoles.Provocateur:
                        __instance.KillButton.OverrideText(GetString("ProvocateurButtonText"));
                        break;
                    case CustomRoles.Camouflager:
                        __instance.AbilityButton.OverrideText(GetString("CamouflagerShapeshiftText"));
                        break;
                    case CustomRoles.OverKiller:
                        __instance.KillButton.OverrideText(GetString("OverKillerButtonText"));
                        break;
                    case CustomRoles.Assassin:
                        Assassin.SetKillButtonText(player.PlayerId);
                        Assassin.GetAbilityButtonText(__instance, player.PlayerId);
                        break;
                    case CustomRoles.Hacker:
                        Hacker.GetAbilityButtonText(__instance, player.PlayerId);
                        break;
                    case CustomRoles.KB_Normal:
                        __instance.KillButton.OverrideText(GetString("GamerButtonText"));
                        break;
                    case CustomRoles.Cleaner:
                        __instance.ReportButton.OverrideText(GetString("CleanerReportButtonText"));
                        break;
                    case CustomRoles.Medusa:
                        __instance.KillButton.OverrideText(GetString("KillButtonText"));
                        __instance.ReportButton.OverrideText(GetString("MedusaReportButtonText"));
                        break;
                    case CustomRoles.Vulture:
                        __instance.ReportButton.OverrideText(GetString("VultureEatButtonText"));
                        break;
                    case CustomRoles.Disperser:
                        __instance.AbilityButton.OverrideText(GetString("DisperserVentButtonText"));
                        break;
                    case CustomRoles.Swooper:
                        __instance.ImpostorVentButton.OverrideText(GetString(Swooper.IsInvis(PlayerControl.LocalPlayer.PlayerId) ? "SwooperRevertVentButtonText" : "SwooperVentButtonText"));
                        __instance.ImpostorVentButton.OverrideText(GetString(Swooper.CanGoInvis(PlayerControl.LocalPlayer.PlayerId) ? "SwooperVentButtonText" : "VentButtonText"));
                        break;
                    case CustomRoles.Wraith:
                        __instance.KillButton.OverrideText(GetString("KillButtonText"));
                        __instance.ImpostorVentButton.OverrideText(GetString(Wraith.IsInvis(PlayerControl.LocalPlayer.PlayerId) ? "WraithRevertVentButtonText" : "WraithVentButtonText"));
                        __instance.ImpostorVentButton.OverrideText(GetString(Swooper.CanGoInvis(PlayerControl.LocalPlayer.PlayerId) ? "WraithVentButtonText" : "VentButtonText"));
                        break;
                    case CustomRoles.Chameleon:
                        __instance.AbilityButton.OverrideText(GetString(Chameleon.IsInvis(PlayerControl.LocalPlayer.PlayerId) ? "ChameleonRevertDisguise" : "ChameleonDisguise"));
                        break;
                    case CustomRoles.Mario:
                        __instance.AbilityButton.buttonLabelText.text = GetString("MarioVentButtonText");
                        __instance.AbilityButton.SetUsesRemaining(Options.MarioVentNumWin.GetInt() - (Main.MarioVentCount.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var mx) ? mx : 0));
                        break;
                    case CustomRoles.Veteran:
                        __instance.AbilityButton.buttonLabelText.text = GetString("VeteranVentButtonText");
                        break;
                    case CustomRoles.TimeMaster:
                        __instance.AbilityButton.buttonLabelText.text = GetString("TimeMasterVentButtonText");
                        break;
                    case CustomRoles.Grenadier:
                        __instance.AbilityButton.buttonLabelText.text = GetString("GrenadierVentButtonText");
                        break;
                    case CustomRoles.Lighter:
                        __instance.AbilityButton.buttonLabelText.text = GetString("LighterVentButtonText");
                        break;
                    case CustomRoles.Mayor:
                        __instance.AbilityButton.buttonLabelText.text = GetString("MayorVentButtonText");
                        break;
                    case CustomRoles.Paranoia:
                        __instance.AbilityButton.buttonLabelText.text = GetString("ParanoiaVentButtonText");
                        break;
                    case CustomRoles.Sheriff:
                        __instance.KillButton.OverrideText(GetString("SheriffKillButtonText"));
                        break;
                    case CustomRoles.Crusader:
                        __instance.KillButton.OverrideText(GetString("CrusaderKillButtonText"));
                        break;
                    case CustomRoles.Totocalcio:
                        __instance.KillButton.OverrideText(GetString("TotocalcioKillButtonText"));
                        break;
                    case CustomRoles.Romantic:
                        __instance.KillButton.OverrideText(GetString("RomanticKillButtonText"));
                        break;
                    case CustomRoles.VengefulRomantic:
                        __instance.KillButton.OverrideText(GetString("VengefulRomanticKillButtonText"));
                        break;
                    case CustomRoles.Succubus:
                        __instance.KillButton.OverrideText(GetString("SuccubusKillButtonText"));
                        break;
                    case CustomRoles.CursedSoul:
                        __instance.KillButton.OverrideText(GetString("CursedSoulKillButtonText"));
                        break;
                    case CustomRoles.Admirer:
                        __instance.KillButton.OverrideText(GetString("AdmireButtonText"));
                        break;
                    case CustomRoles.Amnesiac:
                        __instance.KillButton.OverrideText(GetString("RememberButtonText"));
                        break;
                    case CustomRoles.DovesOfNeace:
                        __instance.AbilityButton.buttonLabelText.text = GetString("DovesOfNeaceVentButtonText");
                        break;
                    case CustomRoles.Infectious:
                        __instance.KillButton.OverrideText(GetString("InfectiousKillButtonText"));
                        break;
                    case CustomRoles.Monarch:
                        __instance.KillButton.OverrideText(GetString("MonarchKillButtonText"));
                        break;
                    case CustomRoles.Deputy:
                        __instance.KillButton.OverrideText(GetString("DeputyHandcuffText"));
                        break;
                    case CustomRoles.Sidekick:
                        __instance.KillButton.OverrideText(GetString("KillButtonText"));
                        __instance.ImpostorVentButton.OverrideText(GetString("ReportButtonText"));
                        __instance.SabotageButton.OverrideText(GetString("SabotageButtonText"));
                        break;
                    case CustomRoles.Addict:
                        __instance.AbilityButton.OverrideText(GetString("AddictVentButtonText"));
                        break;
                    case CustomRoles.Dazzler:
                        __instance.AbilityButton.OverrideText(GetString("DazzleButtonText"));
                        break;
                    case CustomRoles.Deathpact:
                        __instance.AbilityButton.OverrideText(GetString("DeathpactButtonText"));
                        break;
                    case CustomRoles.Devourer:
                        __instance.AbilityButton.OverrideText(GetString("DevourerButtonText"));
                        break;
                }

                //バウンティハンターのターゲットテキスト
                if (LowerInfoText == null)
                {
                    LowerInfoText = Object.Instantiate(__instance.KillButton.buttonLabelText);
                    LowerInfoText.transform.parent = __instance.transform;
                    LowerInfoText.transform.localPosition = new Vector3(0, -2f, 0);
                    LowerInfoText.alignment = TMPro.TextAlignmentOptions.Center;
                    LowerInfoText.overflowMode = TMPro.TextOverflowModes.Overflow;
                    LowerInfoText.enableWordWrapping = false;
                    LowerInfoText.color = Palette.EnabledColor;
                    LowerInfoText.fontSizeMin = 2.0f;
                    LowerInfoText.fontSizeMax = 2.0f;
                }

                if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                {
                    LowerInfoText.text = SoloKombatManager.GetHudText();
                }
                else if (player.Is(CustomRoles.BountyHunter))
                {
                    LowerInfoText.text = BountyHunter.GetTargetText(player, true);
                }
                else if (player.Is(CustomRoles.Witch))
                {
                    LowerInfoText.text = Witch.GetSpellModeText(player, true);
                }
                else if (player.Is(CustomRoles.HexMaster))
                {
                    LowerInfoText.text = HexMaster.GetHexModeText(player, true);
                }
                else if (player.Is(CustomRoles.FireWorks))
                {
                    var stateText = FireWorks.GetStateText(player);
                    LowerInfoText.text = stateText;
                }
                else if (player.Is(CustomRoles.Swooper))
                {
                    LowerInfoText.text = Swooper.GetHudText(player);
                }
                else if (player.Is(CustomRoles.Wraith))
                {
                    LowerInfoText.text = Wraith.GetHudText(player);
                }
                else if (player.Is(CustomRoles.Chameleon))
                {
                    LowerInfoText.text = Chameleon.GetHudText(player);
                }
                else if (player.Is(CustomRoles.BloodKnight))
                {
                    LowerInfoText.text = BloodKnight.GetHudText(player);
                }
                else if (player.Is(CustomRoles.Wildling))
                {
                    LowerInfoText.text = Wildling.GetHudText(player);
                }
                else
                {
                    LowerInfoText.text = "";
                }
                LowerInfoText.enabled = LowerInfoText.text != "";

                if ((!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay) || GameStates.IsMeeting)
                {
                    LowerInfoText.enabled = false;
                }

                if (player.CanUseKillButton())
                {
                    __instance.KillButton.ToggleVisible(player.IsAlive() && GameStates.IsInTask);
                    player.Data.Role.CanUseKillButton = true;
                }
                else
                {
                    __instance.KillButton.SetDisabled();
                    __instance.KillButton.ToggleVisible(false);
                }

                bool CanUseVent = player.CanUseImpostorVentButton();
                __instance.ImpostorVentButton.ToggleVisible(CanUseVent);
                player.Data.Role.CanVent = CanUseVent;
            }
            else
            {
                __instance.ReportButton.Hide();
                __instance.ImpostorVentButton.Hide();
                __instance.KillButton.Hide();
                __instance.AbilityButton.Show();
                __instance.AbilityButton.OverrideText(GetString(StringNames.HauntAbilityName));
            }
        }


        if (Input.GetKeyDown(KeyCode.Y) && AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
        {
            __instance.ToggleMapVisible(new MapOptions()
            {
                Mode = MapOptions.Modes.Sabotage,
                AllowMovementWhileMapOpen = true
            });
            if (player.AmOwner)
            {
                player.MyPhysics.inputHandler.enabled = true;
                ConsoleJoystick.SetMode_Task();
            }
        }

        if (AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame) RepairSender.enabled = false;
        if (Input.GetKeyDown(KeyCode.RightShift) && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
        {
            RepairSender.enabled = !RepairSender.enabled;
            RepairSender.Reset();
        }
        if (RepairSender.enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0)) RepairSender.Input(0);
            if (Input.GetKeyDown(KeyCode.Alpha1)) RepairSender.Input(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RepairSender.Input(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) RepairSender.Input(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) RepairSender.Input(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) RepairSender.Input(5);
            if (Input.GetKeyDown(KeyCode.Alpha6)) RepairSender.Input(6);
            if (Input.GetKeyDown(KeyCode.Alpha7)) RepairSender.Input(7);
            if (Input.GetKeyDown(KeyCode.Alpha8)) RepairSender.Input(8);
            if (Input.GetKeyDown(KeyCode.Alpha9)) RepairSender.Input(9);
            if (Input.GetKeyDown(KeyCode.Return)) RepairSender.InputEnter();
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ToggleHighlight))]
class ToggleHighlightPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] bool active, [HarmonyArgument(1)] RoleTeamTypes team)
    {
        var player = PlayerControl.LocalPlayer;
        if (!GameStates.IsInTask) return;

        if (player.CanUseKillButton())
        {
            __instance.cosmetics.currentBodySprite.BodySprite.material.SetColor("_OutlineColor", Utils.GetRoleColor(player.GetCustomRole()));
        }
    }
}
[HarmonyPatch(typeof(Vent), nameof(Vent.SetOutline))]
class SetVentOutlinePatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(1)] ref bool mainTarget)
    {
        var player = PlayerControl.LocalPlayer;
        Color color = PlayerControl.LocalPlayer.GetRoleColor();
        __instance.myRend.material.SetColor("_OutlineColor", color);
        __instance.myRend.material.SetColor("_AddColor", mainTarget ? color : Color.clear);
    }
}
[HarmonyPatch(typeof(HudManager), nameof(HudManager.SetHudActive), new System.Type[] { typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool) })]
class SetHudActivePatch
{
    public static bool IsActive = false;
    public static void Prefix(HudManager __instance, [HarmonyArgument(2)] ref bool isActive)
    {
        isActive &= !GameStates.IsMeeting;
        return;
    }
    public static void Postfix(HudManager __instance, [HarmonyArgument(2)] bool isActive)
    {
        __instance.ReportButton.ToggleVisible(!GameStates.IsLobby && isActive);
        if (!GameStates.IsModHost) return;
        IsActive = isActive;
        if (!isActive) return;

        var player = PlayerControl.LocalPlayer;
        if (player == null) return;
        switch (player.GetCustomRole())
        {
            case CustomRoles.Sheriff:
            case CustomRoles.SwordsMan:
            case CustomRoles.Deputy:
            case CustomRoles.Monarch:
            case CustomRoles.Arsonist:
            case CustomRoles.NWitch:
            case CustomRoles.Innocent:
            case CustomRoles.Reverie:
            case CustomRoles.Pelican:
            case CustomRoles.Revolutionist:
            case CustomRoles.FFF:
            case CustomRoles.Medic:
            case CustomRoles.Gamer:
            case CustomRoles.DarkHide:
            case CustomRoles.Provocateur:
            case CustomRoles.Farseer:
            case CustomRoles.Crusader:
                __instance.SabotageButton.ToggleVisible(false);
                __instance.AbilityButton.ToggleVisible(false);
                __instance.ImpostorVentButton.ToggleVisible(false);
                break;

            case CustomRoles.Minimalism:
            case CustomRoles.KB_Normal:
                __instance.SabotageButton.ToggleVisible(false);
                __instance.AbilityButton.ToggleVisible(false);
                __instance.ReportButton.ToggleVisible(false);
                break;
            case CustomRoles.Parasite:
            case CustomRoles.Refugee:
                __instance.SabotageButton.ToggleVisible(true);
                break;
            case CustomRoles.Jackal:
                Jackal.SetHudActive(__instance, isActive);
                break;
            case CustomRoles.Sidekick:
                Sidekick.SetHudActive(__instance, isActive);
                break;
            case CustomRoles.Traitor:
                Traitor.SetHudActive(__instance, isActive);
                break;
            case CustomRoles.Glitch:
                Glitch.SetHudActive(__instance, isActive);
                break;
            
        }

        foreach (var subRole in Main.PlayerStates[player.PlayerId].SubRoles)
        {
            switch (subRole)
            {
                case CustomRoles.Oblivious:
                    __instance.ReportButton.ToggleVisible(false);
                    break;
                case CustomRoles.Mare:
                    if (!Utils.IsActive(SystemTypes.Electrical))
                    __instance.KillButton.ToggleVisible(false);
                    break;
            }
        }
        __instance.KillButton.ToggleVisible(player.CanUseKillButton());
        __instance.ImpostorVentButton.ToggleVisible(player.CanUseImpostorVentButton());
    }
}
[HarmonyPatch(typeof(VentButton), nameof(VentButton.DoClick))]
class VentButtonDoClickPatch
{
    public static bool Prefix(VentButton __instance)
    {
        var pc = PlayerControl.LocalPlayer;
        {
            if (!pc.Is(CustomRoles.Swooper) || !pc.Is(CustomRoles.Wraith) || !pc.Is(CustomRoles.Chameleon) || pc.inVent || __instance.currentTarget == null || !pc.CanMove || !__instance.isActiveAndEnabled) return true;
            pc?.MyPhysics?.RpcEnterVent(__instance.currentTarget.Id);
            return false;
        }
    }
}
[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Show))]
class MapBehaviourShowPatch
{
    public static void Prefix(MapBehaviour __instance, ref MapOptions opts)
    {
        if (GameStates.IsMeeting) return;

        if (opts.Mode is MapOptions.Modes.Normal or MapOptions.Modes.Sabotage)
        {
            var player = PlayerControl.LocalPlayer;
            if (player.Is(CustomRoleTypes.Impostor) || (player.Is(CustomRoles.Parasite)) || (player.Is(CustomRoles.Glitch)) || (player.Is(CustomRoles.Refugee)) || (player.Is(CustomRoles.Jackal) && Jackal.CanUseSabotage.GetBool()) || (player.Is(CustomRoles.Traitor) && Traitor.CanUseSabotage.GetBool()))
                opts.Mode = MapOptions.Modes.Sabotage;
            else
                opts.Mode = MapOptions.Modes.Normal;
        }
    }
}
[HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
class TaskPanelBehaviourPatch
{
    // タスク表示の文章が更新・適用された後に実行される
    public static void Postfix(TaskPanelBehaviour __instance)
    {
        if (!GameStates.IsModHost) return;
        PlayerControl player = PlayerControl.LocalPlayer;

        var taskText = __instance.taskText.text;
        if (taskText == "None") return;

        // 役職説明表示
        if (!player.GetCustomRole().IsVanilla())
        {
            var RoleWithInfo = $"{player.GetDisplayRoleName()}:\r\n";
            RoleWithInfo += player.GetRoleInfo();

            var AllText = Utils.ColorString(player.GetRoleColor(), RoleWithInfo);

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.Standard:

                    var lines = taskText.Split("\r\n</color>\n")[0].Split("\r\n\n")[0].Split("\r\n");
                    StringBuilder sb = new();
                    foreach (var eachLine in lines)
                    {
                        var line = eachLine.Trim();
                        if ((line.StartsWith("<color=#FF1919FF>") || line.StartsWith("<color=#FF0000FF>")) && sb.Length < 1 && !line.Contains('(')) continue;
                        sb.Append(line + "\r\n");
                    }
                    if (sb.Length > 1)
                    {
                        var text = sb.ToString().TrimEnd('\n').TrimEnd('\r');
                        if (!Utils.HasTasks(player.Data, false) && sb.ToString().Count(s => (s == '\n')) >= 2)
                            text = $"{ Utils.ColorString(Utils.GetRoleColor(player.GetCustomRole()).ShadeColor(0.2f), GetString("FakeTask"))}\r\n{text}";
                        AllText += $"\r\n\r\n<size=85%>{text}</size>";
                    }

                    if (MeetingStates.FirstMeeting)
                    {
                        AllText += $"\r\n\r\n</color><size=70%>{GetString("PressF1ShowMainRoleDes")}";
                        if (Main.PlayerStates.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var ps) && ps.SubRoles.Count >= 1)
                            AllText += $"\r\n{GetString("PressF2ShowAddRoleDes")}";
                        AllText += "</size>";
                    }

                    break;

                case CustomGameMode.SoloKombat:

                    var lpc = PlayerControl.LocalPlayer;

                    AllText += "\r\n";
                    AllText += $"\r\n{GetString("PVP.ATK")}: {lpc.ATK()}";
                    AllText += $"\r\n{GetString("PVP.DF")}: {lpc.DF()}";
                    AllText += $"\r\n{GetString("PVP.RCO")}: {lpc.HPRECO()}";
                    AllText += "\r\n";

                    Dictionary<byte, string> SummaryText = new();
                    foreach (var id in Main.PlayerStates.Keys)
                    {
                        string name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
                        string summary = $"{Utils.GetProgressText(id)}  {Utils.ColorString(Main.PlayerColors[id], name)}";
                        if (Utils.GetProgressText(id).Trim() == "") continue;
                        SummaryText[id] = summary;
                    }

                    List<(int, byte)> list = new();
                    foreach (var id in Main.PlayerStates.Keys) list.Add((SoloKombatManager.GetRankOfScore(id), id));
                    list.Sort();
                    foreach (var id in list.Where(x => SummaryText.ContainsKey(x.Item2))) AllText += "\r\n" + SummaryText[id.Item2];

                    AllText = $"<size=80%>{AllText}</size>";

                    break;
            }

            __instance.taskText.text = AllText;
        }

        // RepairSenderの表示
        if (RepairSender.enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            __instance.taskText.text = RepairSender.GetText();
    }
}

class RepairSender
{
    public static bool enabled = false;
    public static bool TypingAmount = false;

    public static int SystemType;
    public static int amount;

    public static void Input(int num)
    {
        if (!TypingAmount)
        {
            //SystemType入力中
            SystemType *= 10;
            SystemType += num;
        }
        else
        {
            //Amount入力中
            amount *= 10;
            amount += num;
        }
    }
    public static void InputEnter()
    {
        if (!TypingAmount)
        {
            //SystemType入力中
            TypingAmount = true;
        }
        else
        {
            //Amount入力中
            Send();
        }
    }
    public static void Send()
    {
        ShipStatus.Instance.RpcRepairSystem((SystemTypes)SystemType, amount);
        Reset();
    }
    public static void Reset()
    {
        TypingAmount = false;
        SystemType = 0;
        amount = 0;
    }
    public static string GetText()
    {
        return SystemType.ToString() + "(" + ((SystemTypes)SystemType).ToString() + ")\r\n" + amount;
    }
}