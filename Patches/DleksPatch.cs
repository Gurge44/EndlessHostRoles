using System;
using EHR.Patches;
using HarmonyLib;
using UnityEngine;

namespace EHR
{
    [HarmonyPatch(typeof(GameStartManager))]
    internal static class AllMapIconsPatch
    {
        [HarmonyPatch(nameof(GameStartManager.Start))]
        [HarmonyPostfix]
        public static void Postfix_AllMapIcons(GameStartManager __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                LateTask.New(() =>
                {
                    if (Main.NormalOptions.MapId == 3)
                    {
                        Main.NormalOptions.MapId = 0;
                        __instance.UpdateMapImage(MapNames.Skeld);

                        if (!Options.RandomMapsMode.GetBool())
                        {
                            CreateOptionsPickerPatch.SetDleks = true;
                        }
                    }
                }, AmongUsClient.Instance.AmHost ? 1f : 4f, "Set Skeld Icon For Dleks Map");

                MapIconByName dleksIcon = Object.Instantiate(__instance, __instance.gameObject.transform).AllMapIcons[0];
                dleksIcon.Name = MapNames.Dleks;
                dleksIcon.MapImage = Utils.LoadSprite("EHR.Resources.Images.DleksBanner.png", 100f);
                dleksIcon.NameImage = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 100f);
                __instance.AllMapIcons.Add(dleksIcon);
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
        }
    }

    [HarmonyPatch(typeof(AmongUsClient._CoStartGameHost_d__32), nameof(AmongUsClient._CoStartGameHost_d__32.MoveNext))]
    public static class DleksPatch
    {
        public static bool Prefix(AmongUsClient._CoStartGameHost_d__32 __instance, ref bool __result)
        {
            if (__instance.__1__state != 0)
            {
                return true;
            }

            __instance.__1__state = -1;
            if (LobbyBehaviour.Instance)
            {
                LobbyBehaviour.Instance.Despawn();
            }

            if (ShipStatus.Instance)
            {
                __instance.__2__current = null;
                __instance.__1__state = 2;
                __result = true;
                return false;
            }

            // removed dleks check as it's always false
            int num2 = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.MapId, 0, Constants.MapNames.Length - 1);
            __instance.__2__current = __instance.__4__this.ShipLoadingAsyncHandle = __instance.__4__this.ShipPrefabs[num2].InstantiateAsync();
            __instance.__1__state = 1;

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Vent), nameof(Vent.SetButtons))]
    public static class VentSetButtonsPatch
    {
        public static bool ShowButtons;

        // Fix arrows buttons in vent on Dleks map and "Index was outside the bounds of the array" errors
        private static bool Prefix( /*Vent __instance,*/ [HarmonyArgument(0)] ref bool enabled)
        {
            if (Main.CurrentMap == MapNames.Dleks && Main.IntroDestroyed)
            {
                enabled = false;
                if (GameStates.IsMeeting)
                {
                    ShowButtons = false;
                }
            }

            return true;
        }

        public static void Postfix(Vent __instance, [HarmonyArgument(0)] bool enabled)
        {
            if (Main.CurrentMap != MapNames.Dleks)
            {
                return;
            }

            if (enabled || !Main.IntroDestroyed)
            {
                return;
            }

            bool setActive = ShowButtons || (!PlayerControl.LocalPlayer.inVent && !GameStates.IsMeeting);
            switch (__instance.Id)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 5:
                case 6:
                    __instance.Buttons[0].gameObject.SetActive(setActive);
                    __instance.Buttons[1].gameObject.SetActive(setActive);
                    break;
                case 7:
                case 12:
                case 13:
                    __instance.Buttons[0].gameObject.SetActive(setActive);
                    break;
                case 4:
                case 8:
                case 9:
                case 10:
                case 11:
                    __instance.Buttons[1].gameObject.SetActive(setActive);
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(Vent), nameof(Vent.TryMoveToVent))]
    internal class VentTryMoveToVentPatch
    {
        // Update arrows buttons when player move to vents
        private static void Postfix(Vent __instance, [HarmonyArgument(0)] Vent otherVent)
        {
            if (__instance == null || otherVent == null || Main.CurrentMap != MapNames.Dleks)
            {
                return;
            }

            VentSetButtonsPatch.ShowButtons = true;
            VentSetButtonsPatch.Postfix(otherVent, false);
            VentSetButtonsPatch.ShowButtons = false;
        }
    }

    [HarmonyPatch(typeof(Vent), nameof(Vent.UpdateArrows))]
    internal class VentUpdateArrowsPatch
    {
        // Fixes "Index was outside the bounds of the array" errors when arrows updates in vent on Dleks map
        private static bool Prefix()
        {
            return Main.CurrentMap != MapNames.Dleks;
        }
    }

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Start))]
    public static class AutoselectDleksPatch
    {
        public static void Postfix(StringOption __instance)
        {
            if (__instance.Title == StringNames.GameMapName)
            {
                // vanilla clamps this to not autoselect dleks
                __instance.Value = GameOptionsManager.Instance.CurrentGameOptions.MapId;
            }
        }
    }
}