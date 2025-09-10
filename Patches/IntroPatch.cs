using System;
using System.Collections;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

// Patch for non-host modded clients to ensure that the intro cutscene is shown correctly
// and GameStates.InGame is set to true
#if ANDROID
[HarmonyPatch(typeof(IntroCutscene._ShowRole_d__40), nameof(IntroCutscene._ShowRole_d__40.MoveNext))]
static class ShowRoleMoveNextPatchAndroid
{
    public static void Postfix(IntroCutscene._ShowRole_d__40 __instance, ref bool __result)
    {
        if (AmongUsClient.Instance.AmHost || __instance.__1__state != 1 || !__result) return;

        GameStates.InGame = true;
        SetUpRoleTextPatch.Postfix(__instance.__4__this);
    }
}
#else
[HarmonyPatch(typeof(IntroCutscene._ShowRole_d__41), nameof(IntroCutscene._ShowRole_d__41.MoveNext))]
static class ShowRoleMoveNextPatch
{
    public static void Postfix(IntroCutscene._ShowRole_d__41 __instance, ref bool __result)
    {
        if (AmongUsClient.Instance.AmHost || __instance.__1__state != 1 || !__result) return;

        GameStates.InGame = true;
        SetUpRoleTextPatch.Postfix(__instance.__4__this);
    }
}
#endif

// For some reason, IntroCutScene.ShowRole is not called in the base game with this exact same code,
// so we need to patch HudManager.CoShowIntro for the host entirely to ensure that the intro is shown correctly
[HarmonyPatch(typeof(HudManager), nameof(HudManager.CoShowIntro))]
static class CoShowIntroPatch
{
    public static bool IntroStarted;

    public static bool Prefix(HudManager __instance)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsModHost) return true;

        IntroStarted = true;

        Utils.SetupLongRoleDescriptions();

        LateTask.New(() =>
        {
            try
            {
                if (!(AmongUsClient.Instance.IsGameOver || GameStates.IsLobby || GameEndChecker.Ended))
                {
                    ShipStatusBeginPatch.RolesIsAssigned = true;

                    // Assign tasks after assigning all roles, as it should be
                    ShipStatus.Instance.Begin();

                    GameOptionsSender.AllSenders.Clear();
                    foreach (PlayerControl pc in Main.AllPlayerControls) GameOptionsSender.AllSenders.Add(new PlayerGameOptionsSender(pc));
                }
            }
            catch { Logger.Warn($"Game ended? {AmongUsClient.Instance.IsGameOver || GameStates.IsLobby || GameEndChecker.Ended}", "ShipStatus.Begin"); }
        }, 4f, "Assign Tasks");

        Main.Instance.StartCoroutine(CoShowIntro());
        return false;

        IEnumerator CoShowIntro()
        {
            while (!ShipStatus.Instance) yield return null;

            RPC.RpcVersionCheck();
            GameStates.InGame = true;

            __instance.IsIntroDisplayed = true;
            __instance.LobbyTimerExtensionUI.HideAll();
            __instance.SetMapButtonEnabled(false);

            HudManager hudManager = FastDestroyableSingleton<HudManager>.Instance;
            hudManager.FullScreen.transform.localPosition = new Vector3(0.0f, 0.0f, -250f);

            yield return hudManager.ShowEmblem(true);
            yield return CoBegin(Object.Instantiate(__instance.IntroPrefab, __instance.transform));

            PlayerControl.LocalPlayer.SetKillTimer(10f);
            (ShipStatus.Instance.Systems[SystemTypes.Sabotage].CastFast<SabotageSystemType>()).SetInitialSabotageCooldown();

            if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Doors, out ISystemType systemType) && systemType.TryCast<IDoorSystem>() != null)
                (systemType.CastFast<IDoorSystem>()).SetInitialSabotageCooldown();

            yield return ShipStatus.Instance.PrespawnStep();
            PlayerControl.LocalPlayer.AdjustLighting();
            yield return __instance.CoFadeFullScreen(Color.black, Color.clear);
            __instance.FullScreen.transform.localPosition = new Vector3(0.0f, 0.0f, -500f);
            __instance.IsIntroDisplayed = false;
            __instance.SetMapButtonEnabled(true);
            __instance.SetHudActive(true);
            __instance.CrewmatesKilled.gameObject.SetActive(GameManager.Instance.ShowCrewmatesKilled());
            GameManager.Instance.StartGame();
        }

        IEnumerator CoBegin(IntroCutscene introCutscene)
        {
            Logger.Info("IntroCutscene :: CoBegin() :: Starting intro cutscene", "BASE GAME LOGGER");

            SoundManager.Instance.PlaySound(introCutscene.IntroStinger, false);

            introCutscene.LogPlayerRoleData();
            introCutscene.HideAndSeekPanels.SetActive(false);
            introCutscene.CrewmateRules.SetActive(false);
            introCutscene.ImpostorRules.SetActive(false);
            introCutscene.ImpostorName.gameObject.SetActive(false);
            introCutscene.ImpostorTitle.gameObject.SetActive(false);

            List<PlayerControl> show = IntroCutscene.SelectTeamToShow((Func<NetworkedPlayerInfo, bool>)(pcd => !PlayerControl.LocalPlayer.Data.Role.IsImpostor || pcd.Role.TeamType == PlayerControl.LocalPlayer.Data.Role.TeamType));

            if (show == null || show.Count < 1)
            {
                Logger.Error("IntroCutscene :: CoBegin() :: teamToShow is EMPTY or NULL", "BASE GAME LOGGER");
                show = new();
                show.Add(PlayerControl.LocalPlayer);
            }

            if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
                introCutscene.ImpostorText.gameObject.SetActive(false);
            else
            {
                int adjustedNumImpostors = GameManager.Instance.LogicOptions.GetAdjustedNumImpostors(GameData.Instance.PlayerCount);
                introCutscene.ImpostorText.text = adjustedNumImpostors == 1 ? FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.NumImpostorsS) : FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.NumImpostorsP, adjustedNumImpostors);
                introCutscene.ImpostorText.text = introCutscene.ImpostorText.text.Replace("[FF1919FF]", "<color=#FF1919FF>");
                introCutscene.ImpostorText.text = introCutscene.ImpostorText.text.Replace("[]", "</color>");
            }

            yield return introCutscene.ShowTeam(show, 3f);
            yield return introCutscene.ShowRole();

            ShipStatus.Instance.StartSFX();
            Object.Destroy(introCutscene.gameObject);
        }
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole))]
internal static class SetUpRoleTextPatch
{
    public static bool IsInIntro;

    public static void Postfix(IntroCutscene __instance)
    {
        IsInIntro = false;

        PlayerControl lp = PlayerControl.LocalPlayer;

        LateTask.New(() =>
        {
            Main.Instance.StartCoroutine(LogGameInfo());

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                {
                    Color color = ColorUtility.TryParseHtmlString("#f55252", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("SoloKombat");
                    __instance.RoleText.color = Utils.GetRoleColor(lp.GetCustomRole());
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = lp.GetRoleInfo();
                    break;
                }
                case CustomGameMode.FFA:
                {
                    Color color = ColorUtility.TryParseHtmlString("#00ffff", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("Killer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("KillerInfo");
                    break;
                }
                case CustomGameMode.MoveAndStop:
                {
                    Color color = ColorUtility.TryParseHtmlString("#00ffa5", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("MoveAndStop");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("TaskerInfo");
                    break;
                }
                case CustomGameMode.HotPotato:
                {
                    Color color = ColorUtility.TryParseHtmlString("#e8cd46", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("HotPotato");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("PotatoInfo");
                    break;
                }
                case CustomGameMode.Speedrun:
                {
                    Color color = Utils.GetRoleColor(CustomRoles.Speedrunner);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("Runner");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("RunnerInfo");
                    break;
                }
                case CustomGameMode.CaptureTheFlag:
                {
                    Color color = ColorUtility.TryParseHtmlString("#1313c2", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("CTFPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("CTFPlayerInfo");
                    break;
                }
                case CustomGameMode.NaturalDisasters:
                {
                    Color color = ColorUtility.TryParseHtmlString("#03fc4a", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("NDPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("NDPlayerInfo");
                    break;
                }
                case CustomGameMode.RoomRush:
                {
                    Color color = ColorUtility.TryParseHtmlString("#ffab1b", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("RRPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("RRPlayerInfo");
                    break;
                }
                case CustomGameMode.KingOfTheZones:
                {
                    Color color = ColorUtility.TryParseHtmlString("#ff0000", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("KOTZPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("KOTZPlayerInfo");
                    break;
                }
                case CustomGameMode.Quiz:
                {
                    Color color = Utils.GetRoleColor(CustomRoles.QuizMaster);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("QuizPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("QuizPlayerInfo");
                    break;
                }
                case CustomGameMode.TheMindGame:
                {
                    Color color = Color.yellow;
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("TMGPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("TMGPlayerInfo");
                    break;
                }
                case CustomGameMode.BedWars:
                {
                    Color color = Utils.GetRoleColor(CustomRoles.BedWarsPlayer);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("BedWarsPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("BedWarsPlayerInfo");
                    break;
                }
                default:
                {
                    CustomRoles role = lp.GetCustomRole();

                    if (!role.IsVanilla())
                    {
                        __instance.YouAreText.color = Utils.GetRoleColor(role);
                        __instance.RoleText.text = Utils.GetRoleName(role);
                        __instance.RoleText.color = Utils.GetRoleColor(role);
                        __instance.RoleBlurbText.color = Utils.GetRoleColor(role);
                        __instance.RoleBlurbText.text = "<size=50%>" + lp.GetRoleInfo() + "</size>";
                    }

                    foreach (CustomRoles subRole in Main.PlayerStates[lp.PlayerId].SubRoles)
                    {
                        if (role is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor && subRole == CustomRoles.Lovers) continue;

                        __instance.RoleBlurbText.text += "\n<size=30%>" + Utils.ColorString(Utils.GetRoleColor(subRole), GetString($"{subRole}Info"));
                    }

                    __instance.RoleText.text += Utils.GetSubRolesText(lp.PlayerId, false, true);
                    break;
                }
            }
        }, 0f, "Override Role Text");

        if (!AmongUsClient.Instance.AmHost)
        {
            LateTask.New(() =>
            {
                if (AmongUsClient.Instance.IsGameOver || GameStates.IsLobby || lp == null) return;
                lp.SetName(Main.AllPlayerNames[lp.PlayerId]);
            }, 1f, "Reset Name For Modded Client");
        }
    }

    private static IEnumerator LogGameInfo()
    {
        StringBuilder sb = new("\n");

        yield return null;

        sb.Append("------------Display Names------------\n");

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            sb.Append($"{(pc.AmOwner ? "[*]" : string.Empty),-3}{pc.PlayerId,-2}:{pc.name.PadRightV2(20)}:{pc.cosmetics.nameText.text.Trim()} ({Palette.ColorNames[pc.Data.DefaultOutfit.ColorId].ToString().Replace("Color", string.Empty)})\n");
            pc.cosmetics.nameText.text = pc.name;
        }

        yield return null;

        sb.Append("------------Roles------------\n");

        foreach (PlayerControl pc in Main.AllPlayerControls)
            sb.Append($"{(pc.AmOwner ? "[*]" : string.Empty),-3}{pc.PlayerId,-2}:{pc.Data?.PlayerName?.PadRightV2(20)}:{pc.GetAllRoleName().RemoveHtmlTags().Replace("\n", " + ")}\n");

        yield return null;

        sb.Append("------------Platforms------------\n");

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            try
            {
                string text = pc.AmOwner ? "[*]" : "   ";
                text += $"{pc.PlayerId,-2}:{pc.Data?.PlayerName?.PadRightV2(20)}:{pc.GetClient()?.PlatformData?.Platform.ToString().Replace("Standalone", string.Empty),-11}";

                if (Main.PlayerVersion.TryGetValue(pc.PlayerId, out PlayerVersion pv))
                    text += $":Mod({pv.forkId}/{pv.version}:{pv.tag})";
                else
                    text += ":Vanilla";

                sb.Append(text + "\n");
            }
            catch (Exception ex) { Logger.Exception(ex, "Platform"); }
        }

        yield return null;

        sb.Append("------------Vanilla Settings------------\n");

        foreach (string t in GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split("\r\n")[1..])
            sb.Append(t + "\n");

        yield return null;

        sb.Append("------------Modded Settings------------\n");

        string disabledRoleStr = GetString("Rate0");
        var i = 0;

        foreach ((TabGroup tab, OptionItem[] options) in Options.GroupedOptions)
        {
            sb.Append($"\n----{GetString($"TabGroup.{tab}")}----\n");

            foreach (OptionItem o in options)
            {
                if (!o.IsCurrentlyHidden() && (o.Parent == null ? !o.GetString().Equals(disabledRoleStr) : AllParentsEnabled(o)))
                    sb.Append($"{(o.Parent == null ? o.GetName(true, true).RemoveHtmlTags().PadRightV2(40) : $"┗ {o.GetName(true, true).RemoveHtmlTags()}".PadRightV2(41))}:{o.GetString().RemoveHtmlTags()}\n");

                if (i++ > 20)
                {
                    yield return null;
                    i = 0;
                }

                continue;

                bool AllParentsEnabled(OptionItem oi)
                {
                    if (oi.Parent == null) return true;
                    return oi.Parent.GetBool() && AllParentsEnabled(oi.Parent);
                }
            }
        }

        sb.Append("-------------Other Information-------------\n");
        sb.Append($"Number of players: {Main.AllPlayerControls.Length}\n");
        sb.Append($"Game mode: {GetString(Options.CurrentGameMode.ToString())}\n");
        sb.Append($"Map: {Main.CurrentMap}\n");
        sb.Append($"Server: {Utils.GetRegionName()}");

        yield return null;

        Logger.Info(sb.ToString(), "GameInfo", multiLine: true);
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
internal static class BeginCrewmatePatch
{
    public static bool Prefix(IntroCutscene __instance, ref List<PlayerControl> teamToDisplay)
    {
        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

        if (PlayerControl.LocalPlayer.Is(CustomRoleTypes.Neutral) && !role.IsMadmate())
        {
            teamToDisplay = new();
            teamToDisplay.Add(PlayerControl.LocalPlayer);

            byte id = PlayerControl.LocalPlayer.PlayerId;

            switch (Main.PlayerStates[id].Role)
            {
                case Lawyer:
                    teamToDisplay.Add(Utils.GetPlayerById(Lawyer.Target[id]));
                    break;
                case Executioner:
                    teamToDisplay.Add(Utils.GetPlayerById(Executioner.Target[id]));
                    break;
            }
        }
        else if (PlayerControl.LocalPlayer.IsMadmate())
        {
            teamToDisplay = new();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            __instance.BeginImpostor(teamToDisplay);
            __instance.BackgroundBar.material.color = Palette.ImpostorRed;
            return false;
        }
        else if (role.IsCoven())
        {
            teamToDisplay = new();

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (pc.Is(Team.Coven))
                    teamToDisplay.Add(pc);
            }
        }

        if (role == CustomRoles.LovingCrewmate || PlayerControl.LocalPlayer.Is(CustomRoles.Lovers))
        {
            teamToDisplay = new();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            teamToDisplay.Add(Main.LoversPlayers.FirstOrDefault(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId));
        }
        else if (role == CustomRoles.LovingImpostor) teamToDisplay.Add(Main.LoversPlayers.FirstOrDefault(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId));

        if (CustomTeamManager.EnabledCustomTeams.Count > 0)
        {
            CustomTeamManager.CustomTeam team = CustomTeamManager.GetCustomTeam(PlayerControl.LocalPlayer.PlayerId);

            if (team != null)
            {
                teamToDisplay = new();

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (CustomTeamManager.AreInSameCustomTeam(pc.PlayerId, PlayerControl.LocalPlayer.PlayerId))
                        teamToDisplay.Add(pc);
                }
            }
        }

        if (Options.CurrentGameMode == CustomGameMode.FFA && FreeForAll.FFATeamMode.GetBool() && FreeForAll.PlayerTeams.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out int ffaTeam))
        {
            teamToDisplay = new();

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (FreeForAll.PlayerTeams.TryGetValue(pc.PlayerId, out int team) && team == ffaTeam)
                    teamToDisplay.Add(pc);
            }
        }

        return true;
    }

    public static void Postfix(IntroCutscene __instance, ref List<PlayerControl> teamToDisplay)
    {
        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

        __instance.ImpostorText.gameObject.SetActive(false);

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Bloodlust))
        {
            __instance.TeamTitle.text = GetString("TeamCrewmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Bloodlust);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Bloodlust");
        }
        else
        {
            switch (role)
            {
                case CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor:
                {
                    __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
                    PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(role.GetRoleTypes());
                    byte otherLoverId = Main.LoversPlayers.First(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId).PlayerId;
                    __instance.ImpostorText.gameObject.SetActive(true);
                    __instance.ImpostorText.text = string.Format(GetString($"SubText.{role}"), otherLoverId.ColoredPlayerName());
                    break;
                }
                case CustomRoles.DoubleAgent:
                {
                    __instance.TeamTitle.text = GetString("TeamImpostor");
                    __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(140, 255, 255, byte.MaxValue);
                    PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                    __instance.ImpostorText.gameObject.SetActive(true);
                    __instance.ImpostorText.text = GetString("SubText.Crewmate");
                    break;
                }
                default:
                {
                    switch (role.GetCustomRoleTypes())
                    {
                        case CustomRoleTypes.Impostor:
                        {
                            __instance.TeamTitle.text = GetString("TeamImpostor");
                            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
                            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
                            __instance.ImpostorText.gameObject.SetActive(true);
                            __instance.ImpostorText.text = GetString("SubText.Impostor");
                            break;
                        }
                        case CustomRoleTypes.Crewmate:
                        {
                            __instance.TeamTitle.text = GetString("TeamCrewmate");
                            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(140, 255, 255, byte.MaxValue);
                            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Crewmate);
                            __instance.ImpostorText.gameObject.SetActive(true);
                            __instance.ImpostorText.text = GetString("SubText.Crewmate");
                            break;
                        }
                        case CustomRoleTypes.Neutral:
                        {
                            if (Options.UniqueNeutralRevealScreen.GetBool())
                            {
                                __instance.TeamTitle.text = GetString($"{role}");
                                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
                                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                                __instance.ImpostorText.gameObject.SetActive(true);
                                __instance.ImpostorText.text = GetString($"{role}Info");
                            }
                            else
                            {
                                __instance.TeamTitle.text = GetString("TeamNeutral");
                                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 171, 27, byte.MaxValue);
                                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                                __instance.ImpostorText.gameObject.SetActive(true);
                                __instance.ImpostorText.text = GetString("SubText.Neutral");
                            }

                            break;
                        }

                        case CustomRoleTypes.Coven:
                        {
                            __instance.TeamTitle.text = GetString("TeamCoven");
                            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Team.Coven.GetColor();
                            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Phantom);
                            __instance.ImpostorText.gameObject.SetActive(true);
                            __instance.ImpostorText.text = GetString("SubText.Coven");
                            break;
                        }
                    }

                    if (Main.LoversPlayers.Count == 2 && Main.LoversPlayers.Exists(x => x.IsLocalPlayer()))
                    {
                        __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Lovers);
                        byte otherLoverId = Main.LoversPlayers.First(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId).PlayerId;
                        __instance.ImpostorText.gameObject.SetActive(true);
                        __instance.ImpostorText.DestroyTranslator();
                        __instance.ImpostorText.text = string.Format(GetString("SubText.LovingCrewmate"), otherLoverId.ColoredPlayerName());
                    }

                    break;
                }
            }
        }

        try
        {
            PlayerControl.LocalPlayer.Data.Role.IntroSound = role switch
            {
                CustomRoles.Bomber or
                    CustomRoles.Nuker or
                    CustomRoles.Sapper or
                    CustomRoles.Terrorist or
                    CustomRoles.Battery
                    => ShipStatus.Instance.CommonTasks.FirstOrDefault(task => task.TaskType == TaskTypes.FixWiring)?.MinigamePrefab.OpenSound,

                CustomRoles.Dictator or
                    CustomRoles.Mayor or
                    CustomRoles.NiceSwapper
                    => FastDestroyableSingleton<HudManager>.Instance.Chat.messageSound,

                CustomRoles.AntiAdminer or
                    CustomRoles.Monitor
                    => FastDestroyableSingleton<HudManager>.Instance.Chat.warningSound,

                CustomRoles.GM or
                    CustomRoles.Snitch or
                    CustomRoles.Speedrunner or
                    CustomRoles.Workaholic
                    => FastDestroyableSingleton<HudManager>.Instance.TaskCompleteSound,

                CustomRoles.Helper or
                    CustomRoles.TaskManager
                    => FastDestroyableSingleton<HudManager>.Instance.TaskUpdateSound,

                CustomRoles.Inhibitor or
                    CustomRoles.SabotageMaster or
                    CustomRoles.Saboteur or
                    CustomRoles.SecurityGuard or
                    CustomRoles.Provocateur
                    => ShipStatus.Instance.SabotageSound,

                CustomRoles.KillingMachine or
                    CustomRoles.NiceGuesser or
                    CustomRoles.SwordsMan or
                    CustomRoles.Veteran
                    => PlayerControl.LocalPlayer.KillSfx,

                CustomRoles.Chameleon or
                    CustomRoles.Drainer or
                    CustomRoles.Swooper or
                    CustomRoles.Wraith
                    => PlayerControl.LocalPlayer.MyPhysics.ImpostorDiscoveredSound,

                CustomRoles.Addict or
                    CustomRoles.Ventguard
                    => ShipStatus.Instance.VentEnterSound,

                CustomRoles.NiceEraser or
                    CustomRoles.ParityCop or
                    CustomRoles.TimeManager
                    => MeetingHud.Instance.VoteLockinSound,

                CustomRoles.Demolitionist or
                    CustomRoles.Disperser or
                    CustomRoles.Grenadier or
                    CustomRoles.Miner or
                    CustomRoles.TimeMaster
                    => ShipStatus.Instance.VentMoveSounds.FirstOrDefault(),

                CustomRoles.Chronomancer or
                    CustomRoles.Tremor
                    => FastDestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherImpostorTransformSfx,

                CustomRoles.Deputy or
                    CustomRoles.Sheriff
                    => FastDestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherYeehawSfx,

                CustomRoles.FFF or
                    CustomRoles.Opportunist or
                    CustomRoles.Revolutionist
                    => GetIntroSound(RoleTypes.Crewmate),

                CustomRoles.Curser or
                    CustomRoles.Nightmare
                    => GetIntroSound(RoleTypes.Impostor),

                CustomRoles.Beacon or
                    CustomRoles.DovesOfNeace or
                    CustomRoles.Mediumshiper or
                    CustomRoles.Observer or
                    CustomRoles.Spiritcaller or
                    CustomRoles.Spiritualist
                    => GetIntroSound(RoleTypes.GuardianAngel),

                CustomRoles.Engineer or
                    CustomRoles.EngineerEHR or
                    CustomRoles.Dealer or
                    CustomRoles.Detour or
                    CustomRoles.Investor or
                    CustomRoles.Sentinel or
                    CustomRoles.Sentry
                    => GetIntroSound(RoleTypes.Engineer),

                CustomRoles.Scientist or
                    CustomRoles.ScientistEHR or
                    CustomRoles.Aid or
                    CustomRoles.Doctor or
                    CustomRoles.Medic
                    => GetIntroSound(RoleTypes.Scientist),

                CustomRoles.Tracker
                    or CustomRoles.TrackerEHR
                    or CustomRoles.Bloodhound
                    or CustomRoles.EvilTracker
                    or CustomRoles.Scout
                    => GetIntroSound(RoleTypes.Tracker),
                
                CustomRoles.Viper
                    or CustomRoles.ViperEHR
                    or CustomRoles.Beehive
                    or CustomRoles.Pelican
                    or CustomRoles.Scavenger
                    or CustomRoles.Vampire
                    or CustomRoles.Vulture
                    or CustomRoles.Wasp
                    => GetIntroSound(RoleTypes.Viper),
                
                CustomRoles.Detective
                    or CustomRoles.DetectiveEHR
                    or CustomRoles.Analyst
                    or CustomRoles.Divinator
                    or CustomRoles.Farseer
                    or CustomRoles.Forensic
                    or CustomRoles.Insight
                    or CustomRoles.Oracle
                    => GetIntroSound(RoleTypes.Detective),

                CustomRoles.Noisemaker
                    or CustomRoles.NoisemakerEHR
                    or CustomRoles.SuperStar
                    or CustomRoles.DarkHide
                    or CustomRoles.Specter
                    => GetIntroSound(RoleTypes.Noisemaker),

                CustomRoles.Phantom
                    or CustomRoles.PhantomEHR
                    or CustomRoles.DarkHide
                    or CustomRoles.ImperiusCurse
                    or CustomRoles.SoulHunter
                    => GetIntroSound(RoleTypes.Phantom),

                CustomRoles.Shapeshifter
                    or CustomRoles.ShapeshifterEHR
                    or CustomRoles.Gambler
                    or CustomRoles.Mastermind
                    or CustomRoles.Randomizer
                    or CustomRoles.Shiftguard
                    or CustomRoles.Wizard
                    => GetIntroSound(RoleTypes.Shapeshifter),

                _ => GetAudioClipFromCustomRoleType()
            };
        }
        catch (Exception ex)
        {
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetAudioClipFromCustomRoleType();
            Logger.Warn($"Could not set intro sound\n{ex}", "IntroSound");
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
        {
            __instance.TeamTitle.text = Utils.GetRoleName(role);
            __instance.TeamTitle.color = Utils.GetRoleColor(role);
            __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.GM");
        }

        if (PlayerControl.LocalPlayer.IsMadmate())
        {
            __instance.TeamTitle.text = GetString("TeamMadmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Madmate");
        }

        if (CustomTeamManager.EnabledCustomTeams.Count > 0)
        {
            CustomTeamManager.CustomTeam team = CustomTeamManager.GetCustomTeam(PlayerControl.LocalPlayer.PlayerId);

            if (team != null)
            {
                if (team.RoleRevealScreenTitle != "*") __instance.TeamTitle.text = team.RoleRevealScreenTitle;

                if (team.RoleRevealScreenBackgroundColor != "*" && ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out Color bgColor)) __instance.TeamTitle.color = __instance.BackgroundBar.material.color = bgColor;

                __instance.ImpostorText.gameObject.SetActive(team.RoleRevealScreenSubtitle != "*");
                __instance.ImpostorText.text = team.RoleRevealScreenSubtitle;

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (CustomTeamManager.AreInSameCustomTeam(pc.PlayerId, PlayerControl.LocalPlayer.PlayerId))
                        teamToDisplay.Add(pc);
                }
            }
        }

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
            {
                Color color = ColorUtility.TryParseHtmlString("#f55252", out Color c) ? c : new(255, 255, 255, 255);
                __instance.TeamTitle.text = Utils.GetRoleName(role);
                __instance.TeamTitle.color = Utils.GetRoleColor(role);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("ModeSoloKombat");
                __instance.BackgroundBar.material.color = color;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = FastDestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherImpostorTransformSfx;
                break;
            }
            case CustomGameMode.FFA:
            {
                __instance.TeamTitle.text = GetString("Killer");
                Color color = FreeForAll.PlayerTeams.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out int team) && FreeForAll.TeamColors.TryGetValue(team, out var teamColorHex) && ColorUtility.TryParseHtmlString(teamColorHex, out Color teamColor) ? teamColor : new(0, 255, 255, byte.MaxValue);
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = color;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("KillerInfo");
                break;
            }
            case CustomGameMode.MoveAndStop:
            {
                __instance.TeamTitle.text = GetString("MoveAndStop");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(0, 255, 165, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("TaskerInfo");
                break;
            }
            case CustomGameMode.HotPotato:
            {
                __instance.TeamTitle.text = GetString("HotPotato");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(232, 205, 70, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Viper);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("PotatoInfo");
                break;
            }
            case CustomGameMode.Speedrun:
            {
                __instance.TeamTitle.text = GetString("Runner");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Speedrunner);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Crewmate);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("RunnerInfo");
                break;
            }
            case CustomGameMode.HideAndSeek:
            {
                __instance.TeamTitle.text = GetString("HideAndSeek");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(52, 94, 235, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Phantom);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("SubText.HideAndSeek");
                break;
            }
            case CustomGameMode.CaptureTheFlag:
            {
                __instance.TeamTitle.text = $"<size=70%>{GetString("CTFPlayer")}</size>";
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(19, 19, 194, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Engineer);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("CTFPlayerInfo");
                break;
            }
            case CustomGameMode.NaturalDisasters:
            {
                __instance.TeamTitle.text = $"<size=70%>{GetString("NDPlayer")}</size>";
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(3, 252, 74, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Scientist);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("NDPlayerInfo");
                break;
            }
            case CustomGameMode.RoomRush:
            {
                __instance.TeamTitle.text = GetString("RRPlayer");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 171, 27, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("RRPlayerInfo");
                break;
            }
            case CustomGameMode.KingOfTheZones:
            {
                __instance.TeamTitle.text = $"<size=70%>{GetString("KOTZPlayer")}</size>";
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 0, 0, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("KOTZPlayerInfo");
                break;
            }
            case CustomGameMode.Quiz:
            {
                __instance.TeamTitle.text = GetString("QuizPlayer");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.QuizMaster);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Phantom);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("QuizPlayerInfo");
                break;
            }
            case CustomGameMode.TheMindGame:
            {
                __instance.TeamTitle.text = GetString("TMGPlayer");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Color.yellow;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("TMGPlayerInfo");
                break;
            }
            case CustomGameMode.BedWars:
            {
                __instance.TeamTitle.text = GetString("BedWarsPlayer");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.BedWarsPlayer);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Engineer);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("BedWarsPlayerInfo");
                break;
            }
        }

        return;

        AudioClip GetAudioClipFromCustomRoleType() =>
            PlayerControl.LocalPlayer.GetCustomRoleTypes() switch
            {
                CustomRoleTypes.Impostor => GetIntroSound(RoleTypes.Impostor),
                CustomRoleTypes.Crewmate => GetIntroSound(RoleTypes.Crewmate),
                CustomRoleTypes.Neutral => GetIntroSound(RoleTypes.Shapeshifter),
                CustomRoleTypes.Coven => GetIntroSound(RoleTypes.Phantom),
                _ => GetIntroSound(RoleTypes.Crewmate)
            };
    }

    private static AudioClip GetIntroSound(RoleTypes roleType)
    {
        return RoleManager.Instance.AllRoles.Find((Il2CppSystem.Predicate<RoleBehaviour>)(role => role.Role == roleType))?.IntroSound;
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
internal static class BeginImpostorPatch
{
    public static bool Prefix(IntroCutscene __instance, ref List<PlayerControl> yourTeam)
    {
        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

        if (PlayerControl.LocalPlayer.IsImpostor() && Options.ImpKnowWhosMadmate.GetBool())
        {
            foreach (var pc in Main.AllPlayerControls)
            {
                if (pc.IsMadmate() && !pc.IsLocalPlayer())
                    yourTeam.Add(pc);
            }
        }

        if (PlayerControl.LocalPlayer.IsMadmate())
        {
            yourTeam = new();
            yourTeam.Add(PlayerControl.LocalPlayer);

            if (Options.MadmateKnowWhosImp.GetBool())
            {
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.IsImpostor() && !pc.IsLocalPlayer())
                        yourTeam.Add(pc);
                }
            }

            if (Options.MadmateKnowWhosMadmate.GetBool())
            {
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.IsMadmate() && !pc.IsLocalPlayer())
                        yourTeam.Add(pc);
                }
            }

            __instance.BackgroundBar.material.color = Palette.ImpostorRed;
            return true;
        }

        if (PlayerControl.LocalPlayer.IsCrewmate() && role.IsDesyncRole())
        {
            yourTeam = new();
            yourTeam.Add(PlayerControl.LocalPlayer);
            foreach (PlayerControl pc in Main.AllPlayerControls.Where(x => !x.AmOwner)) yourTeam.Add(pc);

            __instance.BeginCrewmate(yourTeam);
            __instance.BackgroundBar.material.color = Palette.CrewmateBlue;
            return false;
        }

        if (role.IsNeutral() || PlayerControl.LocalPlayer.Is(CustomRoles.Bloodlust))
        {
            yourTeam = new();
            yourTeam.Add(PlayerControl.LocalPlayer);
            foreach (PlayerControl pc in Main.AllPlayerControls.Where(x => !x.AmOwner)) yourTeam.Add(pc);

            __instance.BeginCrewmate(yourTeam);
            __instance.BackgroundBar.material.color = new Color32(255, 171, 27, byte.MaxValue);
            return false;
        }

        BeginCrewmatePatch.Prefix(__instance, ref yourTeam);
        return true;
    }

    public static void Postfix(IntroCutscene __instance, ref List<PlayerControl> yourTeam)
    {
        BeginCrewmatePatch.Postfix(__instance, ref yourTeam);
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.OnGameStart))]
internal static class IntroCutsceneDestroyPatch
{
    public static void Postfix( /*IntroCutscene __instance*/)
    {
        if (!GameStates.IsInGame) return;

        Main.IntroDestroyed = true;

        // Set roleAssigned as false for overriding roles for modded players
        // for vanilla clients we use "Data.Disconnected"
        Main.AllPlayerControls.Do(x => x.roleAssigned = false);

        PlayerControl[] aapc = Main.AllAlivePlayerControls;

        Utils.NumSnapToCallsThisRound = aapc.Length;

        if (AmongUsClient.Instance.AmHost)
        {
            if (Main.NormalOptions.MapId != 4)
            {
                foreach (PlayerControl pc in aapc)
                {
                    pc.SyncSettings();
                    pc.RpcResetAbilityCooldown();

                    if (pc.GetCustomRole().UsesPetInsteadOfKill())
                        pc.AddAbilityCD(10);
                    else
                        pc.AddAbilityCD(false);
                }

                if (Options.CurrentGameMode == CustomGameMode.Standard)
                {
                    int kcd = Options.StartingKillCooldown.GetInt();

                    if (kcd is not 10 and > 0)
                        LateTask.New(() => aapc.Do(x => x.SetKillCooldown(kcd - 2)), 2f, "FixKillCooldownTask");
                    else if (Options.FixFirstKillCooldown.GetBool())
                    {
                        LateTask.New(() =>
                        {
                            aapc.Do(x =>
                            {
                                x.ResetKillCooldown(false);

                                if (Main.AllPlayerKillCooldown.TryGetValue(x.PlayerId, out float kc) && kc - 2f > 0f)
                                    x.SetKillCooldown(kc - 2f);
                            });
                        }, 2f, "FixKillCooldownTask");
                    }
                }
            }

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.FFA when FreeForAll.FFAChatDuringGame.GetBool():
                case CustomGameMode.Quiz when Quiz.Chat:
                    Utils.SetChatVisibleForAll();
                    break;
            }

            // LateTask.New(() => Main.AllPlayerControls.Do(pc ⇒ pc.RpcSetRoleDesync(RoleTypes.Shapeshifter, -3)), 2f, "SetImpostorForServer");

            PlayerControl lp = PlayerControl.LocalPlayer;

            LateTask.New(() =>
            {
                lp.RpcChangeRoleBasis(lp.GetCustomRole());

                LateTask.New(() =>
                {
                    lp.RpcResetAbilityCooldown();
                    lp.SetKillCooldown(10f);
                }, 0.2f, log: false);

                StartGameHostPatch.RpcSetRoleReplacer.SetActualSelfRolesAfterOverride();
            }, 0.1f, log: false);

            if (Options.UsePets.GetBool() && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek or CustomGameMode.CaptureTheFlag or CustomGameMode.BedWars)
            {
                void GrantPetForEveryone()
                {
                    foreach (PlayerControl pc in aapc)
                    {
                        if (pc.Is(CustomRoles.GM)) continue;

                        string petId = PetsHelper.GetPetId();
                        PetsHelper.SetPet(pc, petId);
                        Logger.Info($"{pc.GetNameWithRole()} => {GetString(petId)} Pet", "PetAssign");
                    }
                }

                Main.ProcessShapeshifts = false;

                LateTask.New(GrantPetForEveryone, 0.7f, "Grant Pet For Everyone");

                LateTask.New(() =>
                {
                    lp.Notify(GetString("GLHF"), 2f);

                    foreach (PlayerControl pc in aapc)
                    {
                        if (pc.IsHost()) continue; // Skip the host

                        try
                        {
                            var sender = CustomRpcSender.Create("Shapeshift After Pet Assign On Game Start", SendOption.Reliable);
                            
                            if (AmongUsClient.Instance.AmClient)
                                pc.Shapeshift(pc, false);

                            sender.AutoStartRpc(pc.NetId, 46);
                            sender.WriteNetObject(pc);
                            sender.Write(false);
                            sender.EndRpc();

                            sender.Notify(pc, GetString("GLHF"), 2f);

                            sender.SendMessage();
                        }
                        catch (Exception ex) { Logger.Fatal(ex.ToString(), "IntroPatch.RpcShapeshift"); }
                    }
                }, 1.4f, "Show Pet For Everyone");

                LateTask.New(() => Main.ProcessShapeshifts = true, 2f, "Enable SS Processing");
            }

            try
            {
                System.Collections.Generic.List<PlayerControl> spectators = ChatCommands.Spectators.ToList().ToValidPlayers();
                if (Main.GM.Value) spectators.Add(PlayerControl.LocalPlayer);

                spectators.ForEach(x =>
                {
                    x.RpcExileV2();
                    Main.PlayerStates[x.PlayerId].SetDead();
                });
            }
            catch (Exception e) { Utils.ThrowException(e); }

            if (Options.RandomSpawn.GetBool() && Main.CurrentMap != MapNames.Airship && AmongUsClient.Instance.AmHost && Options.CurrentGameMode is not CustomGameMode.CaptureTheFlag and not CustomGameMode.KingOfTheZones and not CustomGameMode.BedWars)
            {
                var map = RandomSpawn.SpawnMap.GetSpawnMap();
                aapc.Do(map.RandomTeleport);
            }

            try
            {
                if (lp.HasDesyncRole())
                {
                    lp.Data.Role.AffectedByLightAffectors = false;

                    foreach (PlayerControl target in Main.AllPlayerControls)
                    {
                        try
                        {
                            // Set all players as killable players
                            target.Data.Role.CanBeKilled = true;

                            // When target is impostor, set name color as white
                            target.cosmetics.SetNameColor(Color.white);
                            target.Data.Role.NameColor = Color.white;
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.KingOfTheZones:
                    Main.Instance.StartCoroutine(KingOfTheZones.GameStart());
                    break;
                case CustomGameMode.HotPotato:
                    HotPotato.OnGameStart();
                    break;
                case CustomGameMode.Quiz:
                    Main.Instance.StartCoroutine(Quiz.OnGameStart());
                    break;
                case CustomGameMode.MoveAndStop:
                    MoveAndStop.OnGameStart();
                    break;
                case CustomGameMode.TheMindGame:
                    Main.Instance.StartCoroutine(TheMindGame.OnGameStart());
                    break;
                case CustomGameMode.CaptureTheFlag:
                    Main.Instance.StartCoroutine(CaptureTheFlag.OnGameStart());
                    break;
                case CustomGameMode.RoomRush:
                    Main.Instance.StartCoroutine(RoomRush.GameStartTasks());
                    break;
                case CustomGameMode.BedWars:
                    Main.Instance.StartCoroutine(BedWars.OnGameStart());
                    break;
            }

            Utils.CheckAndSetVentInteractions();

            if (AFKDetector.ActivateOnStart.GetBool()) LateTask.New(() => aapc.Do(AFKDetector.RecordPosition), 1f, log: false);

            LateTask.New(() => Main.Instance.StartCoroutine(Utils.NotifyEveryoneAsync()), 3f, "NotifyEveryoneAsync On Game Start");
            LateTask.New(Utils.SyncAllSettings, 0.5f, "SyncAllSettings On Game Start");
        }
        else
        {
            foreach (PlayerControl player in Main.AllPlayerControls)
                Main.PlayerStates[player.PlayerId].InitTask(player);
        }

        Logger.Info("OnDestroy", "IntroCutscene");

        LateTask.New(() =>
        {
            if (SubmergedCompatibility.IsSubmerged())
            {
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    PlainShipRoom room = pc.GetPlainShipRoom();

                    if (room == null || room.RoomId is not ((SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerCentral or (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral))
                        pc.TP(new Vector2(3.32f, -26.57f));
                }
            }
            
            HudManager hud = FastDestroyableSingleton<HudManager>.Instance;

            HudSpritePatch.DefaultIcons =
            [
                hud.KillButton.graphic.sprite,
                hud.AbilityButton.graphic.sprite,
                hud.ImpostorVentButton.graphic.sprite,
                hud.SabotageButton.graphic.sprite,
                hud.PetButton.graphic.sprite,
                hud.ReportButton.graphic.sprite
            ];
        }, 1f, log: false);

        LateTask.New(() =>
        {
            if (Main.CurrentMap == MapNames.Airship && Vector2.Distance(PlayerControl.LocalPlayer.Pos(), new Vector2(-25f, 40f)) < 8f && PlayerControl.LocalPlayer.Is(CustomRoles.GM))
                PlayerControl.LocalPlayer.NetTransform.SnapTo(new(15.5f, 0.0f), (ushort)(PlayerControl.LocalPlayer.NetTransform.lastSequenceId + 8));
        }, 4f, "Airship Spawn FailSafe");
    }

}
