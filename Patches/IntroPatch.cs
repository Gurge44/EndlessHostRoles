using AmongUs.GameOptions;
using HarmonyLib;
using System;
using System.Linq;
using System.Threading.Tasks;
using TOHE.Roles.Impostor;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole))]
class SetUpRoleTextPatch
{
    public static void Postfix(IntroCutscene __instance)
    {
        if (!GameStates.IsModHost) return;
        _ = new LateTask(() =>
        {
            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                    {
                        var color = ColorUtility.TryParseHtmlString("#f55252", out var c) ? c : new(255, 255, 255, 255);
                        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();
                        __instance.YouAreText.color = color;
                        __instance.RoleText.text = Utils.GetRoleName(role);
                        __instance.RoleText.color = Utils.GetRoleColor(role);
                        __instance.RoleBlurbText.color = color;
                        __instance.RoleBlurbText.text = PlayerControl.LocalPlayer.GetRoleInfo();
                        break;
                    }
                case CustomGameMode.FFA:
                    {
                        var color = ColorUtility.TryParseHtmlString("#00ffff", out var c) ? c : new(255, 255, 255, 255);
                        __instance.YouAreText.transform.gameObject.SetActive(false);
                        __instance.RoleText.text = "FREE FOR ALL";
                        __instance.RoleText.color = color;
                        __instance.RoleBlurbText.color = color;
                        __instance.RoleBlurbText.text = "KILL EVERYONE TO WIN";
                        break;
                    }
                case CustomGameMode.MoveAndStop:
                    {
                        var color = ColorUtility.TryParseHtmlString("#00ffa5", out var c) ? c : new(255, 255, 255, 255);
                        __instance.YouAreText.transform.gameObject.SetActive(false);
                        __instance.RoleText.text = "MOVE AND STOP";
                        __instance.RoleText.color = color;
                        __instance.RoleBlurbText.color = color;
                        __instance.RoleBlurbText.text = "FINISH TASKS FIRST TO WIN";
                        break;
                    }
                default:
                    {
                        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();
                        if (!role.IsVanilla())
                        {
                            __instance.YouAreText.color = Utils.GetRoleColor(role);
                            __instance.RoleText.text = Utils.GetRoleName(role);
                            __instance.RoleText.color = Utils.GetRoleColor(role);
                            __instance.RoleBlurbText.color = Utils.GetRoleColor(role);
                            __instance.RoleBlurbText.text = "<size=50%>" + PlayerControl.LocalPlayer.GetRoleInfo() + "</size>";
                        }
                        foreach (CustomRoles subRole in Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SubRoles.ToArray())
                        {
                            __instance.RoleBlurbText.text += "\n<size=30%>" + Utils.ColorString(Utils.GetRoleColor(subRole), GetString($"{subRole}Info"));
                        }

                        //if (!PlayerControl.LocalPlayer.Is(CustomRoles.Lovers) && !PlayerControl.LocalPlayer.Is(CustomRoles.Ntr) && CustomRolesHelper.RoleExist(CustomRoles.Ntr))
                        //    __instance.RoleBlurbText.text += "\n" + Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), GetString($"{CustomRoles.Lovers}Info"));
                        __instance.RoleText.text += Utils.GetSubRolesText(PlayerControl.LocalPlayer.PlayerId, false, true);
                        break;
                    }
            }
        }, 0.01f, "Override Role Text");
    }
}
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
class CoBeginPatch
{
    public static void Prefix()
    {
        var logger = Logger.Handler("Info");
        logger.Info("------------Display Names------------");
        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            logger.Info($"{(pc.AmOwner ? "[*]" : string.Empty),-3}{pc.PlayerId,-2}:{pc.name.PadRightV2(20)}:{pc.cosmetics.nameText.text} ({Palette.ColorNames[pc.Data.DefaultOutfit.ColorId].ToString().Replace("Color", string.Empty)})");
            pc.cosmetics.nameText.text = pc.name;
        }
        logger.Info("------------Roles------------");
        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            logger.Info($"{(pc.AmOwner ? "[*]" : string.Empty),-3}{pc.PlayerId,-2}:{pc?.Data?.PlayerName?.PadRightV2(20)}:{pc.GetAllRoleName().RemoveHtmlTags().Replace("\n", " + ")}");
        }
        logger.Info("------------Platforms------------");
        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            try
            {
                var text = pc.AmOwner ? "[*]" : "   ";
                text += $"{pc.PlayerId,-2}:{pc.Data?.PlayerName?.PadRightV2(20)}:{pc.GetClient()?.PlatformData?.Platform.ToString()?.Replace("Standalone", string.Empty),-11}";
                if (Main.playerVersion.TryGetValue(pc.PlayerId, out PlayerVersion pv))
                    text += $":Mod({pv.forkId}/{pv.version}:{pv.tag})";
                else
                    text += ":Vanilla";
                logger.Info(text);
            }
            catch (Exception ex) { Logger.Exception(ex, "Platform"); }
        }
        logger.Info("------------Vanilla Settings------------");
        var tmp = GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split("\r\n").Skip(1);
        foreach (var t in tmp) logger.Info(t);
        logger.Info("------------Modded Settings------------");
        foreach (OptionItem o in OptionItem.AllOptions.ToArray())
        {
            if (!o.IsHiddenOn(Options.CurrentGameMode) && (o.Parent == null ? !o.GetString().Equals("0%") : o.Parent.GetBool()))
                logger.Info($"{(o.Parent == null ? o.GetName(true, true).RemoveHtmlTags().PadRightV2(40) : $"┗ {o.GetName(true, true).RemoveHtmlTags()}".PadRightV2(41))}:{o.GetString().RemoveHtmlTags()}");
        }

        logger.Info("-------------Other Information-------------");
        logger.Info($"Number of players: {Main.AllPlayerControls.Length}");
        Main.AllPlayerControls.Do(x => Main.PlayerStates[x.PlayerId].InitTask(x));
        GameData.Instance.RecomputeTaskCounts();
        TaskState.InitialTotalTasks = GameData.Instance.TotalTasks;

        Utils.NotifyRoles(ForceLoop: true);

        GameStates.InGame = true;
    }
}
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
class BeginCrewmatePatch
{
    public static bool Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
    {
        if (PlayerControl.LocalPlayer.Is(CustomRoleTypes.Neutral) && !PlayerControl.LocalPlayer.Is(CustomRoles.Parasite))
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
        }
        if (PlayerControl.LocalPlayer.Is(CustomRoleTypes.Neutral) && !PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate))
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            __instance.BeginImpostor(teamToDisplay);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return false;
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            __instance.BeginImpostor(teamToDisplay);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return false;
        }
        else if (PlayerControl.LocalPlayer.GetCustomRole().IsMadmate())
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            __instance.BeginImpostor(teamToDisplay);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return false;
        }

        return true;
    }
    public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
    {
        //チーム表示変更
        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

        __instance.ImpostorText.gameObject.SetActive(false);
        switch (role.GetCustomRoleTypes())
        {
            case CustomRoleTypes.Impostor:
                __instance.TeamTitle.text = GetString("TeamImpostor");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("SubText.Impostor");
                break;
            case CustomRoleTypes.Crewmate:
                __instance.TeamTitle.text = GetString("TeamCrewmate");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(140, 255, 255, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Crewmate);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("SubText.Crewmate");
                break;
            case CustomRoleTypes.Neutral:
                __instance.TeamTitle.text = GetString("TeamNeutral");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(127, 140, 141, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("SubText.Neutral");
                break;
        }
        try
        {
            PlayerControl.LocalPlayer.Data.Role.IntroSound = role switch
            {
                CustomRoles.Terrorist or
                CustomRoles.Sapper or
                CustomRoles.Bomber or
                CustomRoles.Nuker
                => ShipStatus.Instance.CommonTasks.FirstOrDefault(task => task.TaskType == TaskTypes.FixWiring).MinigamePrefab.OpenSound,

                CustomRoles.Sheriff or
                CustomRoles.Veteran
                => ShipStatus.Instance.ShortTasks.FirstOrDefault(task => task.TaskType == TaskTypes.PutAwayPistols).MinigamePrefab.OpenSound,

                CustomRoles.Cleaner or
                CustomRoles.Cleanser
                => ShipStatus.Instance.ShortTasks.FirstOrDefault(task => task.TaskType == TaskTypes.PolishRuby).MinigamePrefab.OpenSound,

                CustomRoles.Dictator or
                CustomRoles.Lawyer or
                CustomRoles.Judge or
                CustomRoles.Mayor
                => ShipStatus.Instance.ShortTasks.FirstOrDefault(task => task.TaskType == TaskTypes.FixShower).MinigamePrefab.OpenSound,

                CustomRoles.Monitor or
                CustomRoles.AntiAdminer
                => ShipStatus.Instance.LongTasks.FirstOrDefault(task => task.TaskType == TaskTypes.ResetBreakers).MinigamePrefab.OpenSound,

                CustomRoles.EvilTracker or
                CustomRoles.Tracefinder or
                CustomRoles.Tracker or
                CustomRoles.Bloodhound or
                CustomRoles.Mortician or
                CustomRoles.Lighter
                => ShipStatus.Instance.ShortTasks.FirstOrDefault(task => task.TaskType == TaskTypes.DivertPower).MinigamePrefab.OpenSound,

                CustomRoles.Oracle or
                CustomRoles.Divinator or
                CustomRoles.Mediumshiper or
                CustomRoles.DovesOfNeace or
                CustomRoles.Spiritualist or
                CustomRoles.Spiritcaller or
                CustomRoles.Farseer
                => GetIntroSound(RoleTypes.GuardianAngel),

                CustomRoles.Alchemist
                => ShipStatus.Instance.LongTasks.FirstOrDefault(task => task.TaskType == TaskTypes.DevelopPhotos).MinigamePrefab.OpenSound,

                CustomRoles.Deputy or
                CustomRoles.Jailor
                => ShipStatus.Instance.LongTasks.FirstOrDefault(task => task.TaskType == TaskTypes.UnlockSafe).MinigamePrefab.OpenSound,

                CustomRoles.Workaholic or
                CustomRoles.Speedrunner or
                CustomRoles.Snitch
                => DestroyableSingleton<HudManager>.Instance.TaskCompleteSound,

                CustomRoles.TaskManager
                => DestroyableSingleton<HudManager>.Instance.TaskUpdateSound,

                CustomRoles.Opportunist or
                CustomRoles.FFF or
                CustomRoles.Revolutionist
                => GetIntroSound(RoleTypes.Crewmate),

                CustomRoles.Nightmare
                => GetIntroSound(RoleTypes.Impostor),

                CustomRoles.SabotageMaster or
                CustomRoles.Engineer or
                CustomRoles.EngineerTOHE or
                CustomRoles.Inhibitor or
                CustomRoles.Saboteur or
                CustomRoles.SecurityGuard or
                CustomRoles.Provocateur
                => ShipStatus.Instance.SabotageSound,

                CustomRoles.Mastermind or
                CustomRoles.Gambler
                => GetIntroSound(RoleTypes.Shapeshifter),

                CustomRoles.Doctor or
                CustomRoles.Medic
                => GetIntroSound(RoleTypes.Scientist),

                CustomRoles.GM
                => DestroyableSingleton<HudManager>.Instance.TaskCompleteSound,

                CustomRoles.SwordsMan or
                CustomRoles.Minimalism or
                CustomRoles.NiceGuesser
                => PlayerControl.LocalPlayer.KillSfx,

                CustomRoles.Swooper or
                CustomRoles.Wraith or
                CustomRoles.Chameleon or
                CustomRoles.Drainer
                => PlayerControl.LocalPlayer.MyPhysics.ImpostorDiscoveredSound,

                CustomRoles.Addict or
                CustomRoles.Ventguard
                => ShipStatus.Instance.VentEnterSound,

                CustomRoles.ParityCop or
                CustomRoles.NiceEraser or
                CustomRoles.TimeManager
                => MeetingHud.Instance.VoteLockinSound,

                CustomRoles.Demolitionist or
                CustomRoles.TimeMaster or
                CustomRoles.Grenadier or
                CustomRoles.Miner or
                CustomRoles.Disperser
                => ShipStatus.Instance.VentMoveSounds.FirstOrDefault(),

                _ => PlayerControl.LocalPlayer.Is(RoleTypes.Impostor) ? PlayerControl.LocalPlayer.Is(RoleTypes.Shapeshifter) ? GetIntroSound(RoleTypes.Shapeshifter) : GetIntroSound(RoleTypes.Impostor) : GetIntroSound(RoleTypes.Crewmate),
            };
        }
        catch (Exception ex)
        {
            switch (role.GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
                    break;
                case CustomRoleTypes.Crewmate:
                    PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Crewmate);
                    break;
                case CustomRoleTypes.Neutral:
                    PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                    break;
            }
            Logger.Warn($"Could not set intro sound\n{ex}", "IntroSound");
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
        {
            __instance.TeamTitle.text = Utils.GetRoleName(role);
            __instance.TeamTitle.color = Utils.GetRoleColor(role);
            __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
            __instance.ImpostorText.gameObject.SetActive(false);
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate))
        {
            __instance.TeamTitle.text = GetString("TeamMadmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Madmate");
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Parasite))
        {
            __instance.TeamTitle.text = GetString("TeamMadmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Madmate");
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            __instance.TeamTitle.text = GetString("TeamMadmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Madmate");
        }

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                {
                    var color = ColorUtility.TryParseHtmlString("#f55252", out var c) ? c : new(255, 255, 255, 255);
                    __instance.TeamTitle.text = Utils.GetRoleName(role);
                    __instance.TeamTitle.color = Utils.GetRoleColor(role);
                    __instance.ImpostorText.gameObject.SetActive(true);
                    __instance.ImpostorText.text = GetString("ModeSoloKombat");
                    __instance.BackgroundBar.material.color = color;
                    PlayerControl.LocalPlayer.Data.Role.IntroSound = DestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherImpostorTransformSfx;
                    break;
                }
            case CustomGameMode.FFA:
                __instance.TeamTitle.text = "FREE FOR ALL";
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(0, 255, 255, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = "KILL EVERYONE TO WIN";
                break;
            case CustomGameMode.MoveAndStop:
                __instance.TeamTitle.text = "MOVE AND STOP";
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(0, 255, 160, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = "FINISH TASKS FIRST TO WIN";
                break;
        }

        if (Input.GetKey(KeyCode.RightShift))
        {
            __instance.TeamTitle.text = "明天就跑路啦";
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = "嘿嘿嘿嘿嘿嘿";
            __instance.TeamTitle.color = Color.cyan;
            StartFadeIntro(__instance, Color.cyan, Color.yellow);
        }
        if (Input.GetKey(KeyCode.RightControl))
        {
            __instance.TeamTitle.text = "警告";
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = "请远离无知的玩家";
            __instance.TeamTitle.color = Color.magenta;
            StartFadeIntro(__instance, Color.magenta, Color.magenta);
        }
    }
    public static AudioClip GetIntroSound(RoleTypes roleType)
    {
        return RoleManager.Instance.AllRoles.FirstOrDefault((role) => role.Role == roleType).IntroSound;
    }
    private static async void StartFadeIntro(IntroCutscene __instance, Color start, Color end)
    {
        await Task.Delay(1000);
        int milliseconds = 0;
        while (true)
        {
            await Task.Delay(20);
            milliseconds += 20;
            float time = milliseconds / (float)500;
            Color LerpingColor = Color.Lerp(start, end, time);
            if (__instance == null || milliseconds > 500)
            {
                Logger.Info("ループを終了します", "StartFadeIntro");
                break;
            }
            __instance.BackgroundBar.material.color = LerpingColor;
        }
    }
}
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
class BeginImpostorPatch
{
    public static bool Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
    {
        var role = PlayerControl.LocalPlayer.GetCustomRole();
        if (role is CustomRoles.Crewpostor)
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate))
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Parasite))
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }
        else if (role is CustomRoles.Sheriff or CustomRoles.Jailor or CustomRoles.SwordsMan or CustomRoles.Medic/* or CustomRoles.Counterfeiter*/ or CustomRoles.Witness or CustomRoles.Analyzer or CustomRoles.Aid or CustomRoles.Escort or CustomRoles.DonutDelivery or CustomRoles.Gaulois or CustomRoles.Monarch or CustomRoles.Farseer or CustomRoles.Admirer or CustomRoles.Deputy)
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            foreach (var pc in Main.AllPlayerControls.Where(x => !x.AmOwner).ToArray()) yourTeam.Add(pc);
            __instance.BeginCrewmate(yourTeam);
            __instance.overlayHandle.color = Palette.CrewmateBlue;
            return false;
        }
        else if (role is CustomRoles.Romantic or CustomRoles.RuthlessRomantic or CustomRoles.VengefulRomantic or CustomRoles.Agitater or CustomRoles.Doppelganger or CustomRoles.NSerialKiller or CustomRoles.PlagueDoctor or CustomRoles.Postman or CustomRoles.Magician or CustomRoles.WeaponMaster or CustomRoles.Reckless or CustomRoles.Eclipse or CustomRoles.Pyromaniac or CustomRoles.HeadHunter or CustomRoles.Vengeance or CustomRoles.Imitator or CustomRoles.Werewolf or CustomRoles.Jackal or CustomRoles.CursedSoul or CustomRoles.Amnesiac or CustomRoles.Arsonist or CustomRoles.Sidekick or CustomRoles.Innocent or CustomRoles.Pelican or CustomRoles.Pursuer or CustomRoles.Revolutionist or CustomRoles.FFF or CustomRoles.Gamer or CustomRoles.Glitch or CustomRoles.Juggernaut or CustomRoles.DarkHide or CustomRoles.Provocateur or CustomRoles.BloodKnight or CustomRoles.NSerialKiller or CustomRoles.Maverick/* or CustomRoles.NWitch*/ or CustomRoles.Totocalcio or CustomRoles.Succubus or CustomRoles.Pelican or CustomRoles.Infectious or CustomRoles.Virus or CustomRoles.Pickpocket or CustomRoles.Traitor or CustomRoles.PlagueBearer or CustomRoles.Pestilence or CustomRoles.Spiritcaller)
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            foreach (var pc in Main.AllPlayerControls.Where(x => !x.AmOwner).ToArray()) yourTeam.Add(pc);
            __instance.BeginCrewmate(yourTeam);
            __instance.overlayHandle.color = new Color32(127, 140, 141, byte.MaxValue);
            return false;
        }
        BeginCrewmatePatch.Prefix(__instance, ref yourTeam);
        return true;
    }
    public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
    {
        BeginCrewmatePatch.Postfix(__instance, ref yourTeam);
    }
}
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
class IntroCutsceneDestroyPatch
{
    public static void Postfix(IntroCutscene __instance)
    {
        if (!GameStates.IsInGame) return;
        Main.introDestroyed = true;
        if (AmongUsClient.Instance.AmHost)
        {
            if (Main.NormalOptions.MapId != 4)
            {
                Main.AllPlayerControls.Do(pc => pc.RpcResetAbilityCooldown());
                if (Options.StartingKillCooldown.GetInt() != 10 && Options.StartingKillCooldown.GetInt() > 0)
                {
                    _ = new LateTask(() =>
                    {
                        Main.AllPlayerControls.Do(x => x.ResetKillCooldown());
                        Main.AllPlayerControls.Where(x => Main.AllPlayerKillCooldown[x.PlayerId] != 7f && Main.AllPlayerKillCooldown[x.PlayerId] != 7.5f).Do(pc => pc.SetKillCooldown(Options.StartingKillCooldown.GetInt() - 2));
                    }, 2f, "FixKillCooldownTask");
                }
                else if (Options.FixFirstKillCooldown.GetBool() && Options.CurrentGameMode is not CustomGameMode.SoloKombat and not CustomGameMode.FFA and not CustomGameMode.MoveAndStop)
                {
                    _ = new LateTask(() =>
                    {
                        Main.AllPlayerControls.Do(x => x.ResetKillCooldown());
                        Main.AllPlayerControls.Where(x => (Main.AllPlayerKillCooldown[x.PlayerId] - 2f) > 0f).Do(pc => pc.SetKillCooldown(Main.AllPlayerKillCooldown[pc.PlayerId] - 2f));
                    }, 2f, "FixKillCooldownTask");
                }
            }
            _ = new LateTask(() => Main.AllPlayerControls.Do(pc => pc.RpcSetRoleDesync(RoleTypes.Shapeshifter, -3)), 2f, "SetImpostorForServer");

            if (Options.UsePets.GetBool())
            {
                Main.ProcessShapeshifts = false;

                string[] pets = Options.PetToAssign;
                string pet = pets[Options.PetToAssignToEveryone.GetValue()];

                var r = IRandom.Instance;

                _ = new LateTask(() =>
                {
                    foreach (var pc in Main.AllAlivePlayerControls)
                    {
                        if (pc.Is(CustomRoles.GM)) continue;
                        string petId = pet == "pet_RANDOM_FOR_EVERYONE" ? pets[r.Next(0, pets.Length)] : pet;
                        PetsPatch.SetPet(pc, petId, true);
                        Logger.Info($"{pc.GetNameWithRole()} => {GetString(petId)} Pet", "PetAssign");
                    }
                }, 0.3f, "Grant Pet For Everyone");
                try
                {
                    _ = new LateTask(() =>
                    {
                        try
                        {
                            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                            {
                                if (pc.PlayerId == 0) continue; // Skip the host
                                if (pc != null)
                                {
                                    try
                                    {
                                        pc.RpcShapeshift(pc, false);
                                        pc.Notify("Good Luck & Have Fun!", 1f);
                                    }
                                    catch (Exception ex) { Logger.Fatal(ex.ToString(), "IntroPatch.RpcShapeshift"); }
                                }
                            }
                        }
                        catch (Exception ex) { Logger.Fatal(ex.ToString(), "IntroPatch.RpcShapeshift.forCycle"); }
                    }, 0.4f, "Show Pet For Everyone");
                }
                catch { }
                _ = new LateTask(() => Main.ProcessShapeshifts = true, 1f, "Enable SS Processing");
            }

            if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
            {
                PlayerControl.LocalPlayer.RpcExile();
                Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            }
            if (Options.RandomSpawn.GetBool() || Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop)
            {
                RandomSpawn.SpawnMap map;
                switch (Main.NormalOptions.MapId)
                {
                    case 0:
                        map = new RandomSpawn.SkeldSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 1:
                        map = new RandomSpawn.MiraHQSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 2:
                        map = new RandomSpawn.PolusSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 5:
                        map = new RandomSpawn.FungleSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                }
            }

            if (Main.NormalOptions.MapId == 4)
            {
                Penguin.OnSpawnAirship();
            }

            var amDesyncImpostor = Main.ResetCamPlayerList.Contains(PlayerControl.LocalPlayer.PlayerId);
            if (amDesyncImpostor)
            {
                PlayerControl.LocalPlayer.Data.Role.AffectedByLightAffectors = false;
            }
        }
        Logger.Info("OnDestroy", "IntroCutscene");
    }
}