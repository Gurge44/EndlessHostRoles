using EHR.Modules;
using HarmonyLib;
using Il2CppSystem;
using UnityEngine;

namespace EHR.Patches;
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
        string optValue = key.GetString();
        if (optValue == "STRMISS") return;

        SendRpc(0, index, playSound: playSound);

        string name = key.GetName();
        if (name == "Accept") return;

        string parentName = key.Parent?.GetName() ?? string.Empty;
        if (parentName == "Accept") return;
        
        string str = key.Parent != null
            ? TranslationController.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + parentName + "</font>: <font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + name + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + optValue + "</font>")
            : TranslationController.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + name + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + optValue + "</font>");

        SettingsChangeMessageLogic(key, str, playSound);
    }

    public static void AddRoleSettingsChangeMessage(int index, OptionItem key, CustomRoles customRole, bool playSound = false)
    {
        string optValue = key.GetString();
        if (optValue == "STRMISS") return;

        string name = key.GetName();
        if (name == "Accept") return;

        SendRpc(1, index, customRole, playSound);
        string str = TranslationController.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + name + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + optValue + "</font>");
        SettingsChangeMessageLogic(key, str, playSound);
    }

    private static void SettingsChangeMessageLogic(OptionItem key, string item, bool playSound)
    {
        if (Instance.lastMessageKey == key.Id && Instance.activeMessages.Count > 0)
            Instance.activeMessages[^1].UpdateMessage(item);
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
