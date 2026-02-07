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

    public static void AddSettingsChangeMessage(OptionItem option, bool playSound = false)
    {
        string optValue = option.GetString();
        if (optValue == "STRMISS") return;

        SendRpc(0, option.Id, playSound: playSound);

        string name = option.GetName();
        if (name == "Accept") return;

        string parentName = option.Parent?.GetName() ?? string.Empty;
        if (parentName == "Accept") return;
        
        string str = option.Parent != null
            ? TranslationController.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + parentName + "</font>: <font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + name + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + optValue + "</font>")
            : TranslationController.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + name + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + optValue + "</font>");

        SettingsChangeMessageLogic(option, str, playSound);
    }

    public static void AddRoleSettingsChangeMessage(OptionItem option, CustomRoles customRole, bool playSound = false)
    {
        string optValue = option.GetString();
        if (optValue == "STRMISS") return;

        string name = option.GetName();
        if (name == "Accept") return;

        SendRpc(1, option.Id, customRole, playSound);
        string str = TranslationController.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + name + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + optValue + "</font>");
        SettingsChangeMessageLogic(option, str, playSound);
    }

    private static void SettingsChangeMessageLogic(OptionItem option, string item, bool playSound)
    {
        if (Instance.lastMessageKey == option.Id && Instance.activeMessages.Count > 0)
            Instance.activeMessages[^1].UpdateMessage(item);
        else
        {
            Instance.lastMessageKey = option.Id;
            LobbyNotificationMessage newMessage = Object.Instantiate(Instance.notificationMessageOrigin, Vector3.zero, Quaternion.identity, Instance.transform);
            newMessage.transform.localPosition = new(0f, 0f, -2f);
            newMessage.SetUp(item, Instance.settingsChangeSprite, Instance.settingsChangeColor, (Action)(() => Instance.OnMessageDestroy(newMessage)));
            Instance.ShiftMessages();
            Instance.AddMessageToQueue(newMessage);
        }

        if (playSound) SoundManager.Instance.PlaySoundImmediate(Instance.settingsChangeSound, false);
    }

    private static void SendRpc(byte typeId, int optionId, CustomRoles customRole = CustomRoles.NotAssigned, bool playSound = true)
    {
        if (Options.HideGameSettings.GetBool()) return;
        Utils.SendRPC(CustomRPC.NotificationPopper, typeId, optionId, (int)customRole, playSound);
    }
}