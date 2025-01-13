using System;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(TaskAdderGame), nameof(TaskAdderGame.ShowFolder))]
internal static class ShowFolderPatch
{
    private static TaskFolder CustomRolesFolder;

    public static void Prefix(TaskAdderGame __instance, [HarmonyArgument(0)] TaskFolder taskFolder)
    {
        if (__instance.Root == taskFolder && CustomRolesFolder == null)
        {
            TaskFolder rolesFolder = Object.Instantiate(
                __instance.RootFolderPrefab,
                __instance.transform
            );

            rolesFolder.gameObject.SetActive(false);
            rolesFolder.FolderName = Main.ModName;
            CustomRolesFolder = rolesFolder;
            __instance.Root.SubFolders.Add(rolesFolder);
        }
    }

    public static void Postfix(TaskAdderGame __instance, [HarmonyArgument(0)] TaskFolder taskFolder)
    {
        Logger.Info("Opened " + taskFolder.FolderName, "TaskFolder");
        var xCursor = 0f;
        var yCursor = 0f;
        var maxHeight = 0f;

        if (CustomRolesFolder != null && CustomRolesFolder.FolderName == taskFolder.FolderName)
        {
            CustomRoles[] list = Enum.GetValues<CustomRoles>();

            foreach (CustomRoles cRoleID in list)
            {
                TaskAddButton button = Object.Instantiate(__instance.RoleButton);
                button.Text.text = Utils.GetRoleName(cRoleID);
                __instance.AddFileAsChild(CustomRolesFolder, button, ref xCursor, ref yCursor, ref maxHeight);

                var roleBehaviour = new RoleBehaviour
                {
                    Role = (RoleTypes)cRoleID + 1000
                };

                button.Role = roleBehaviour;

                Color roleColor = Utils.GetRoleColor(cRoleID);

                button.FileImage.color = roleColor;
                button.RolloverHandler.OutColor = roleColor;
                button.RolloverHandler.OverColor = new(roleColor.r * 0.5f, roleColor.g * 0.5f, roleColor.b * 0.5f);
            }
        }
    }
}

[HarmonyPatch(typeof(TaskAddButton), nameof(TaskAddButton.Update))]
internal class TaskAddButtonUpdatePatch
{
    public static bool Prefix(TaskAddButton __instance)
    {
        try
        {
            if ((int)__instance.Role.Role >= 1000)
            {
                CustomRoles PlayerCustomRole = PlayerControl.LocalPlayer.GetCustomRole();
                CustomRoles FileCustomRole = (CustomRoles)__instance.Role.Role - 1000;
                __instance.Overlay.enabled = PlayerCustomRole == FileCustomRole;
            }
        }
        catch { }

        return true;
    }
}

[HarmonyPatch(typeof(TaskAddButton), nameof(TaskAddButton.AddTask))]
internal static class AddTaskButtonPatch
{
    public static bool Prefix(TaskAddButton __instance)
    {
        try
        {
            if ((int)__instance.Role.Role >= 1000)
            {
                CustomRoles FileCustomRole = (CustomRoles)__instance.Role.Role - 1000;
                PlayerControl.LocalPlayer.RpcSetCustomRole(FileCustomRole);
                PlayerControl.LocalPlayer.RpcSetRole(FileCustomRole.GetRoleTypes(), true);
                return false;
            }
        }
        catch { }

        return true;
    }
}