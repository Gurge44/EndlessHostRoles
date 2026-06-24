using System.Collections.Generic;
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

    public static void AddSettingsChangeMessage(OptionItem option)
    {
        if (GameSettingMenuPatch.ChangingPreset) return;
        
        string optValue = option.GetString();
        if (optValue == "STRMISS") return;

        string name = option.GetName();
        if (name == "Accept") return;

        string parentName = option.Parent?.GetName() ?? string.Empty;
        if (parentName == "Accept") return;
        
        SendRpc(option.Id);
        
        string str = option.Parent != null
            ? TranslationController.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + parentName + "</font>: <font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + name + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + optValue + "</font>")
            : TranslationController.Instance.GetString(StringNames.LobbyChangeSettingNotification, "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + name + "</font>", "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + optValue + "</font>");

        SettingsChangeMessageLogic(option, str);
    }

    private static void SettingsChangeMessageLogic(OptionItem option, string item)
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

        SoundManager.Instance.PlaySoundImmediate(Instance.settingsChangeSound, false);
    }

    private static readonly HashSet<int> RpcBatch = [];

    private static void SendRpc(int optionId)
    {
        if (!AmongUsClient.Instance.AmHost || Options.HideGameSettings.GetBool() || !Utils.DoRPC) return;
        
        if (RpcBatch.Add(optionId) && RpcBatch.Count >= 5)
            ReleaseRpcs();
    }

    public static void ReleaseRpcs()
    {
        OptionItem.SyncAllOptions();
        
        if (RpcBatch.Count == 0) return;
        
        var msg = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.NotificationPopper, Hazel.SendOption.Reliable);
        msg.WritePacked(RpcBatch.Count);
        RpcBatch.Do(msg.WritePacked);
        AmongUsClient.Instance.FinishRpcImmediately(msg);
        
        RpcBatch.Clear();
    }
}