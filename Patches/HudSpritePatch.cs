using System;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Neutral;
using EHR.Patches;
using UnityEngine;

namespace EHR;

public static class CustomButton
{
    public static Sprite Get(string name)
    {
        return Utils.LoadSprite($"EHR.Resources.Images.Skills.{name}.png", 115f);
    }
}

//[HarmonyPriority(520)]
//[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HudSpritePatch
{
    public static bool ForceUpdate;
    public static Sprite[] DefaultIcons = [];
    private static long LastErrorTime;

    public static void Postfix(HudManager __instance)
    {
        try
        {
            PlayerControl player = PlayerControl.LocalPlayer;
            if (player == null) return;

            if (!Main.EnableCustomButton.Value || !Main.ProcessShapeshifts || Mastermind.ManipulatedPlayers.ContainsKey(player.PlayerId) || ExileController.Instance || GameStates.IsMeeting) return;
            if ((!SetHudActivePatch.IsActive && !MeetingStates.FirstMeeting) || !player.IsAlive()) return;
            if (!AmongUsClient.Instance.IsGameStarted || !Main.IntroDestroyed || GameStates.IsLobby || GameStates.IsNotJoined || !GameStates.InGame || IntroCutsceneDestroyPatch.PreventKill) return;

            if (DefaultIcons.Length == 0) return;

            Sprite newKillButton = DefaultIcons[0];
            Sprite newAbilityButton = DefaultIcons[1];
            Sprite newVentButton = DefaultIcons[2];
            Sprite newSabotageButton = DefaultIcons[3];
            Sprite newPetButton = DefaultIcons[4];
            Sprite newReportButton = DefaultIcons[5];
            Sprite newSecondaryAbilityButton = DefaultIcons[6];

            bool usesPetInsteadOfKill = player.UsesPetInsteadOfKill();
            bool shapeshifting = player.IsShifted();

            switch (player.GetCustomRole())
            {
                case CustomRoles.SnowdownPlayer when Snowdown.Data.TryGetValue(player.PlayerId, out Snowdown.PlayerData snowdownData) && !snowdownData.InShop:
                {
                    newAbilityButton = CustomButton.Get("Snowdown");
                    break;
                }
                case CustomRoles.CTFPlayer:
                {
                    newAbilityButton = CustomButton.Get("Tag");
                    break;
                }
                case CustomRoles.Enderman:
                {
                    if (Options.UsePhantomBasis.GetBool()) newAbilityButton = CustomButton.Get("abscond");
                    else if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("abscond");
                    else newSabotageButton = CustomButton.Get("abscond");

                    break;
                }
                case CustomRoles.Dreamweaver:
                {
                    newKillButton = CustomButton.Get("Dreamweave");
                    break;
                }
                case CustomRoles.Wizard:
                {
                    newAbilityButton = CustomButton.Get("Up");
                    break;
                }
                case CustomRoles.Socialite:
                {
                    newKillButton = CustomButton.Get("Mark");
                    break;
                }
                case CustomRoles.Hitman:
                case CustomRoles.Augmenter:
                {
                    newAbilityButton = CustomButton.Get("Mark");
                    break;
                }
                case CustomRoles.Echo:
                {
                    newAbilityButton = player.IsShifted() ? newKillButton : CustomButton.Get("Puttpuer");
                    break;
                }
                case CustomRoles.Shifter:
                {
                    newKillButton = CustomButton.Get("Swap");
                    break;
                }
                case CustomRoles.Changeling:
                {
                    newAbilityButton = CustomButton.Get("GlitchMimic");

                    if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("Swap");
                    else newVentButton = CustomButton.Get("Swap");

                    break;
                }
                case CustomRoles.Vulture:
                {
                    newReportButton = CustomButton.Get("Eat");
                    break;
                }
                case CustomRoles.Sentry:
                {
                    newPetButton = CustomButton.Get("Sentry");
                    break;
                }
                case CustomRoles.Commander:
                {
                    newAbilityButton = CustomButton.Get("Commander");
                    break;
                }
                case CustomRoles.Cleaner:
                {
                    newReportButton = CustomButton.Get("Clean");
                    break;
                }
                case CustomRoles.Amnesiac:
                {
                    if (Amnesiac.RememberMode.GetValue() == 1) newKillButton = CustomButton.Get("AmnesiacKill");
                    else newReportButton = CustomButton.Get("AmnesiacReport");

                    break;
                }
                case CustomRoles.Ninja:
                case CustomRoles.Undertaker:
                {
                    if (Main.PlayerStates[player.PlayerId].Role is not Ninja ninja) break;

                    if (Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool())
                    {
                        newKillButton = CustomButton.Get("Mark");
                        if (ninja.MarkedPlayer != byte.MaxValue) newPetButton = CustomButton.Get("Assassinate");
                    }
                    else
                    {
                        if (!shapeshifting)
                        {
                            newKillButton = CustomButton.Get("Mark");
                            if (ninja.MarkedPlayer != byte.MaxValue) newAbilityButton = CustomButton.Get("Assassinate");
                        }
                    }

                    break;
                }
                case CustomRoles.Gaulois:
                {
                    newKillButton = CustomButton.Get("Gaulois");
                    break;
                }
                case CustomRoles.Consort:
                case CustomRoles.Escort:
                {
                    newKillButton = CustomButton.Get("GlitchHack");
                    break;
                }
                case CustomRoles.Glitch when Main.PlayerStates[player.PlayerId].Role is Glitch gc:
                {
                    if (gc.KCDTimer > 0 && gc.HackCDTimer <= 0) newKillButton = CustomButton.Get("GlitchHack");
                    newAbilityButton = CustomButton.Get("GlitchMimic");
                    break;
                }
                case CustomRoles.Jester:
                {
                    newAbilityButton = CustomButton.Get("JesterVent");
                    break;
                }
                case CustomRoles.Transporter when player.GetRoleTypes() == RoleTypes.Shapeshifter:
                case CustomRoles.Swapster:
                {
                    newAbilityButton = CustomButton.Get("Transport");
                    break;
                }
                case CustomRoles.Disperser:
                {
                    if (Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool()) newPetButton = CustomButton.Get("Disperse");
                    else if (!shapeshifting) newAbilityButton = CustomButton.Get("Disperse");

                    break;
                }
                case CustomRoles.Duellist:
                case CustomRoles.SoulCatcher:
                case CustomRoles.Twister:
                {
                    if (player.Is(CustomRoles.Twister) && Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool()) newPetButton = CustomButton.Get("Transport");
                    else if (!shapeshifting) newAbilityButton = CustomButton.Get("Transport");

                    break;
                }
                case CustomRoles.Deputy:
                {
                    newKillButton = CustomButton.Get("Handcuff");
                    break;
                }
                case CustomRoles.Pursuer:
                {
                    newKillButton = CustomButton.Get("Pursuer");
                    break;
                }
                case CustomRoles.Alchemist:
                {
                    if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("Drink");
                    else newAbilityButton = CustomButton.Get("Drink");

                    break;
                }
                case CustomRoles.Jailor:
                {
                    newKillButton = CustomButton.Get("Jail");
                    break;
                }
                case CustomRoles.Penguin:
                {
                    newAbilityButton = CustomButton.Get("Timer");
                    break;
                }
                case CustomRoles.Revolutionist:
                {
                    newKillButton = CustomButton.Get("Tag");
                    break;
                }
                case CustomRoles.DonutDelivery:
                {
                    newKillButton = CustomButton.Get("Donut");
                    break;
                }
                case CustomRoles.Sapper:
                case CustomRoles.Bomber:
                case CustomRoles.Nuker:
                {
                    if (Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool()) newPetButton = CustomButton.Get("Bomb");
                    else newAbilityButton = CustomButton.Get("Bomb");

                    break;
                }
                case CustomRoles.Camouflager:
                {
                    newAbilityButton = CustomButton.Get("Camo");
                    break;
                }
                case CustomRoles.Agitator:
                case CustomRoles.Potato:
                {
                    newKillButton = CustomButton.Get("bombshell");
                    break;
                }
                case CustomRoles.Arsonist:
                {
                    newKillButton = CustomButton.Get("Douse");

                    if (player.IsDouseDone() || (Arsonist.ArsonistCanIgniteAnytime.GetBool() && Utils.GetDousedPlayerCount(player.PlayerId).Item1 >= Arsonist.ArsonistMinPlayersToIgnite.GetInt()))
                    {
                        if (Options.UsePets.GetBool())
                            newPetButton = CustomButton.Get("Ignite");
                        else
                            newVentButton = CustomButton.Get("Ignite");
                    }

                    break;
                }
                case CustomRoles.Pyromaniac:
                {
                    newKillButton = CustomButton.Get("Pyromaniac");
                    break;
                }
                case CustomRoles.Fireworker when Main.PlayerStates[player.PlayerId].Role is Fireworker fw:
                {
                    newAbilityButton = CustomButton.Get(fw.nowFireworksCount == 0 ? "FireworkD" : "FireworkP");
                    break;
                }
                case CustomRoles.Anonymous:
                {
                    newAbilityButton = CustomButton.Get("Hack");
                    break;
                }
                case CustomRoles.Hangman when shapeshifting:
                {
                    newAbilityButton = CustomButton.Get("Hangman");
                    break;
                }
                case CustomRoles.Paranoid:
                {
                    if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("Paranoid");
                    else newAbilityButton = CustomButton.Get("Paranoid");

                    break;
                }
                case CustomRoles.Mayor when Mayor.MayorHasPortableButton.GetBool():
                {
                    if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("EmergencyButton");
                    else newAbilityButton = CustomButton.Get("EmergencyButton");

                    break;
                }
                case CustomRoles.Puppeteer:
                {
                    newKillButton = CustomButton.Get("Puttpuer");
                    break;
                }
                case CustomRoles.Aid:
                case CustomRoles.Medic:
                {
                    newKillButton = CustomButton.Get("Shield");
                    break;
                }
                case CustomRoles.Gangster when Gangster.CanRecruit(player.PlayerId):
                case CustomRoles.Jackal when player.GetAbilityUseLimit() > 0:
                {
                    newKillButton = CustomButton.Get("Sidekick");
                    break;
                }
                case CustomRoles.Cultist:
                {
                    newKillButton = CustomButton.Get("Subbus");
                    break;
                }
                case CustomRoles.Innocent:
                {
                    newKillButton = CustomButton.Get("Suidce");
                    break;
                }
                case CustomRoles.EvilTracker:
                {
                    newAbilityButton = CustomButton.Get("Track");
                    break;
                }
                case CustomRoles.Vampire:
                {
                    newKillButton = CustomButton.Get("Bite");
                    break;
                }
                case CustomRoles.Veteran:
                {
                    if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("Veteran");
                    else newAbilityButton = CustomButton.Get("Veteran");

                    break;
                }
                case CustomRoles.Lighter:
                {
                    if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("Lighter");
                    else newAbilityButton = CustomButton.Get("Lighter");

                    break;
                }
                case CustomRoles.SecurityGuard:
                {
                    if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("BlockSabo");
                    else newAbilityButton = CustomButton.Get("BlockSabo");

                    break;
                }
                case CustomRoles.Ventguard:
                {
                    newAbilityButton = CustomButton.Get("Block");
                    break;
                }
                case CustomRoles.Romantic:
                {
                    newKillButton = CustomButton.Get(!Romantic.HasPickedPartner ? "Romance" : "RomanticProtect");
                    break;
                }
                case CustomRoles.VengefulRomantic:
                {
                    newKillButton = CustomButton.Get("RomanticKill");
                    break;
                }
                case CustomRoles.Miner:
                {
                    if (!Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool()) newAbilityButton = CustomButton.Get("Mine");
                    else newPetButton = CustomButton.Get("Mine");

                    break;
                }
                case CustomRoles.Analyst:
                case CustomRoles.Witness:
                {
                    newKillButton = CustomButton.Get("Examine");
                    break;
                }
                case CustomRoles.Postman:
                {
                    newKillButton = CustomButton.Get("Deliver");
                    break;
                }
                case CustomRoles.Pelican:
                {
                    newKillButton = CustomButton.Get("Vulture");
                    break;
                }
                case CustomRoles.TimeMaster:
                {
                    if (Options.UsePets.GetBool()) newPetButton = CustomButton.Get("TimeMaster");
                    else newAbilityButton = CustomButton.Get("TimeMaster");

                    break;
                }
                case CustomRoles.Sheriff:
                {
                    newKillButton = CustomButton.Get("Kill");
                    break;
                }
                case CustomRoles.Dasher:
                case CustomRoles.Swiftclaw:
                {
                    if (Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool()) newPetButton = CustomButton.Get("Dash");
                    else newAbilityButton = CustomButton.Get("Dash");

                    break;
                }
                case CustomRoles.Swooper:
                case CustomRoles.Chameleon:
                case CustomRoles.Wraith:
                {
                    newAbilityButton = CustomButton.Get("invisible");
                    break;
                }
                case CustomRoles.Visionary:
                {
                    newAbilityButton = CustomButton.Get("prophecies");
                    break;
                }
                case CustomRoles.Escapist:
                {
                    if (Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool()) newPetButton = CustomButton.Get("abscond");
                    else newAbilityButton = CustomButton.Get("abscond");

                    break;
                }
                case CustomRoles.Tunneler:
                {
                    newPetButton = CustomButton.Get("abscond");
                    break;
                }
                case CustomRoles.Investigator:
                {
                    newKillButton = CustomButton.Get("prophecies");
                    break;
                }
                case CustomRoles.Warlock:
                {
                    if (Options.UsePets.GetBool() || !shapeshifting)
                    {
                        newKillButton = CustomButton.Get("Curse");
                        if (Warlock.IsCurseAndKill.TryGetValue(player.PlayerId, out bool curse) && curse)
                            newAbilityButton = CustomButton.Get("CurseKill");
                    }

                    break;
                }
                default:
                {
                    if (ForceUpdate || usesPetInsteadOfKill) break;
                    SetButtonColors();
                    return;
                }
            }

            if (usesPetInsteadOfKill) newPetButton = newKillButton;
            
            SetButtonColors();

            __instance.KillButton.graphic.sprite = newKillButton;
            __instance.AbilityButton.graphic.sprite = newAbilityButton;
            __instance.ImpostorVentButton.graphic.sprite = newVentButton;
            __instance.SabotageButton.graphic.sprite = newSabotageButton;
            __instance.PetButton.graphic.sprite = newPetButton;
            __instance.ReportButton.graphic.sprite = newReportButton;

            new[]
            {
                __instance.KillButton.graphic,
                __instance.AbilityButton.graphic,
                __instance.ImpostorVentButton.graphic,
                __instance.SabotageButton.graphic,
                __instance.PetButton.graphic,
                __instance.ReportButton.graphic,
                __instance.SecondaryAbilityButton.graphic
            }.Do(x => x.SetCooldownNormalizedUvs());
            
            ForceUpdate = false;

            void SetButtonColors()
            {
                var roleColor = Utils.GetRoleColor(player.GetCustomRole());

                foreach (var button in new ActionButton[] { __instance.KillButton, __instance.AbilityButton, __instance.ImpostorVentButton, __instance.SabotageButton, __instance.PetButton, __instance.ReportButton, __instance.SecondaryAbilityButton })
                    button.buttonLabelText.SetOutlineColor(roleColor);
            }
        }
        catch (Exception e)
        {
            if (Utils.TimeStamp - LastErrorTime > 10)
            {
                LastErrorTime = Utils.TimeStamp;
                Utils.ThrowException(e);
            }
        }
    }
}
