using EHR.Modules;
using HarmonyLib;
using Il2CppSystem;
using UnityEngine;

namespace EHR.Patches
{
    // By TommyXL

    [HarmonyPatch(typeof(NotificationPopper), nameof(NotificationPopper.Awake))]
    internal static class NotificationPopperAwakePatch
    {
        public static void Prefix(NotificationPopper __instance)
        {
            // Don't use ??= because Unity doesn't support null coalescing assignment
            NotificationPopperPatch.Instance = __instance;
        }
    }

    internal static class NotificationPopperPatch
    {
        public static NotificationPopper Instance;

        public static void AddSettingsChangeMessage(int index, OptionItem key, bool playSound = false)
        {
            SendRpc(0, index, playSound: playSound);

            string str = key.Parent != null
                ? DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + key.Parent.GetName() + "</font>: <font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + key.GetName() + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + key.GetString() + "</font>")
                : DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + key.GetName() + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + key.GetString() + "</font>");

            SettingsChangeMessageLogic(key, str, playSound);
        }

        public static void AddRoleSettingsChangeMessage(int index, OptionItem key, CustomRoles customRole, bool playSound = false)
        {
            SendRpc(1, index, customRole, playSound);
            string str = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + key.GetName() + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + key.GetString() + "</font>");
            SettingsChangeMessageLogic(key, str, playSound);
        }

        private static void SettingsChangeMessageLogic(OptionItem key, string item, bool playSound)
        {
            if (Instance.lastMessageKey == key.Id && Instance.activeMessages.Count > 0)
                Instance.activeMessages[^1].UpdateMessage(item); // False error
            else
            {
                Instance.lastMessageKey = key.Id;
                LobbyNotificationMessage newMessage = Object.Instantiate(Instance.notificationMessageOrigin, Vector3.zero, Quaternion.identity, Instance.transform);
                newMessage.transform.localPosition = new(0f, 0f, -2f);
                newMessage.SetUp(item, Instance.settingsChangeSprite, Instance.settingsChangeColor, (Action)(() => Instance.OnMessageDestroy(newMessage)));
                Instance.ShiftMessages();
                Instance.AddMessageToQueue(newMessage);
            }

            if (playSound) SoundManager.Instance.PlaySoundImmediate(Instance.settingsChangeSound, false);
        }

        private static void SendRpc(byte typeId, int index, CustomRoles customRole = CustomRoles.NotAssigned, bool playSound = true)
        {
            if (Options.HideGameSettings.GetBool()) return;

            Utils.SendRPC(CustomRPC.NotificationPopper, typeId, index, (int)customRole, playSound);
        }
    }
}