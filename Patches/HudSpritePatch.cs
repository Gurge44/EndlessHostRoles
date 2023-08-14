using HarmonyLib;
using TOHE.Modules;
using TOHE.Roles.Impostor;
using UnityEngine;

namespace TOHE;

public static class CustomButton
{
    public static Sprite Get(string name) => Utils.LoadSprite($"TOHE.Resources.Images.Skills.{name}.png", 115f);
}

[HarmonyPriority(520)]
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HudSpritePatch
{
    private static Sprite Kill;
    private static Sprite Ability;
    private static Sprite Vent;
    public static void Postfix(HudManager __instance)
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null || !GameStates.IsModHost) return;
        if (!SetHudActivePatch.IsActive || !player.IsAlive()) return;
        if (!AmongUsClient.Instance.IsGameStarted || !Main.introDestroyed)
        {
            Kill = null;
            Ability = null;
            Vent = null;
            return;
        }

        bool shapeshifting = Main.CheckShapeshift.TryGetValue(player.PlayerId, out bool ss) && ss;

        if (!Kill) Kill = __instance.KillButton.graphic.sprite;
        if (!Ability) Ability = __instance.AbilityButton.graphic.sprite;
        if (!Vent) Vent = __instance.ImpostorVentButton.graphic.sprite;

        Sprite newKillButton = Kill;
        Sprite newAbilityButton = Ability;
        Sprite newVentButton = Vent;

        if (!Main.EnableCustomButton.Value) goto EndOfSelectImg;

        switch (player.GetCustomRole())
        {
            case CustomRoles.Assassin:
                if (!shapeshifting)
                {
                    newKillButton = CustomButton.Get("Mark");
                    if (Assassin.MarkedPlayer.ContainsKey(player.PlayerId))
                        newAbilityButton = CustomButton.Get("Assassinate");
                }
                break;
            case CustomRoles.Disperser:
                if (!shapeshifting)
                {
                    newAbilityButton = CustomButton.Get("Disperse");
                }
                break;
            case CustomRoles.BountyHunter:
                if (!shapeshifting)
                {
                    newAbilityButton = CustomButton.Get("Swap");
                }
                break;
            case CustomRoles.ImperiusCurse:
            case CustomRoles.Twister:
                if (!shapeshifting)
                {
                    newAbilityButton = CustomButton.Get("Transport");
                }
                break;
            case CustomRoles.Deputy:
                newKillButton = CustomButton.Get("Handcuff");
                break;
            case CustomRoles.Bomber:
            case CustomRoles.Nuker:
                newAbilityButton = CustomButton.Get("Bomb");
                break;
            case CustomRoles.Camouflager:
                newAbilityButton = CustomButton.Get("Camo");
                break;
            case CustomRoles.Arsonist:
                newKillButton = CustomButton.Get("Douse");
                if (player.IsDouseDone()) newVentButton = CustomButton.Get("Ignite");
                break;
            case CustomRoles.FireWorks:
                if (FireWorks.nowFireWorksCount[player.PlayerId] == 0)
                    newAbilityButton = CustomButton.Get("FireworkD");
                else
                    newAbilityButton = CustomButton.Get("FireworkP");
                break;
            case CustomRoles.Hacker:
                newAbilityButton = CustomButton.Get("Hack");
                break;
            case CustomRoles.Hangman:
                if (shapeshifting) newAbilityButton = CustomButton.Get("Hangman");
                break;
            case CustomRoles.Paranoia:
                newAbilityButton = CustomButton.Get("Paranoid");
                break;
            case CustomRoles.Mayor:
                newAbilityButton = CustomButton.Get("Button");
                break;
            case CustomRoles.Puppeteer:
                newKillButton = CustomButton.Get("Puttpuer");
                break;
            case CustomRoles.Medic:
                newKillButton = CustomButton.Get("Shield");
                break;
            case CustomRoles.Gangster:
                if (Gangster.CanRecruit(player.PlayerId)) newKillButton = CustomButton.Get("Sidekick");
                break;
            case CustomRoles.Succubus:
                newKillButton = CustomButton.Get("Subbus");
                break;
            case CustomRoles.Innocent:
                newKillButton = CustomButton.Get("Suidce");
                break;
            case CustomRoles.EvilTracker:
                newAbilityButton = CustomButton.Get("Track");
                break;
            case CustomRoles.Vampire:
                newKillButton = CustomButton.Get("Bite");
                break;
            case CustomRoles.Veteran:
                newAbilityButton = CustomButton.Get("Veteran");
                break;
            case CustomRoles.Lighter:
                newAbilityButton = CustomButton.Get("Lighter");
                break;
            case CustomRoles.Pelican:
                newKillButton = CustomButton.Get("Vulture");
                break;
            case CustomRoles.CursedSoul:
                newKillButton = CustomButton.Get("Soul");
                break;
            case CustomRoles.Warlock:
                if (!shapeshifting)
                {
                    newKillButton = CustomButton.Get("Curse");
                    if (Main.isCurseAndKill.TryGetValue(player.PlayerId, out bool curse) && curse)
                        newAbilityButton = CustomButton.Get("CurseKill");
                }
                break;
        }

    EndOfSelectImg:

        __instance.KillButton.graphic.sprite = newKillButton;
        __instance.AbilityButton.graphic.sprite = newAbilityButton;
        __instance.ImpostorVentButton.graphic.sprite = newVentButton;

    }
}
