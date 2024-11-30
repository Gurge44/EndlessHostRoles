using HarmonyLib;

namespace EHR.Patches
{
    [HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.AddVote))]
    internal class VoteBanSystemPatch
    {
        public static bool Prefix()
        {
            return !AmongUsClient.Instance.AmHost || !Options.DisableVoteBan.GetBool();
        }
    }

    [HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.CmdAddVote))]
    internal class VoteBanSystemPatchCmd
    {
        public static bool Prefix()
        {
            return !AmongUsClient.Instance.AmHost || !Options.DisableVoteBan.GetBool();
        }
    }
}