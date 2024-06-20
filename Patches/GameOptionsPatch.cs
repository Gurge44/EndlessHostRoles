using AmongUs.GameOptions;
using HarmonyLib;

namespace EHR.Patches;

// [HarmonyPatch(typeof(RoleOptionSetting), nameof(RoleOptionSetting.UpdateValuesAndText))]
// class ChanceChangePatch
// {
//     public static void Postfix(RoleOptionSetting __instance)
//     {
//         string DisableText = $" ({GetString("Disabled")})";
//         switch (__instance.Role.Role)
//         {
//             case RoleTypes.Scientist:
//                 __instance.TitleText.color = Utils.GetRoleColor(CustomRoles.Scientist);
//                 break;
//             case RoleTypes.Engineer:
//                 __instance.TitleText.color = Utils.GetRoleColor(CustomRoles.Engineer);
//                 break;
//             case RoleTypes.GuardianAngel:
//             {
//                 var tf = __instance.transform;
//                 tf.Find("Count Plus_TMP").gameObject.active
//                     = tf.Find("Chance Minus_TMP").gameObject.active
//                         = tf.Find("Chance Value_TMP").gameObject.active
//                             = tf.Find("Chance Plus_TMP").gameObject.active
//                                 = tf.Find("More Options").gameObject.active
//                                     = false;
//
//                 if (!__instance.TitleText.text.Contains(DisableText))
//                     __instance.TitleText.text += DisableText;
//                 __instance.TitleText.color = Utils.GetRoleColor(CustomRoles.GuardianAngel);
//                 break;
//             }
//             case RoleTypes.Shapeshifter:
//                 __instance.TitleText.color = Utils.GetRoleColor(CustomRoles.Shapeshifter);
//                 break;
//         }
//
//         __instance.ChanceText.text = DisableText;
//     }
// }

[HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.SwitchGameMode))]
class SwitchGameModePatch
{
    public static void Postfix(GameModes gameMode)
    {
        if (gameMode == GameModes.HideNSeek)
        {
            ErrorText.Instance.HnSFlag = true;
            ErrorText.Instance.AddError(ErrorCode.HnsUnload);
            Harmony.UnpatchAll();
            Main.Instance.Unload();
        }
    }
}