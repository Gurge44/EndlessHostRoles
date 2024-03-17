using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;

namespace TOHE
{
    internal static class CustomHideAndSeekManager
    {
        public static void SetupCustomOption()
        {
        }

        public static void Init()
        {
        }

        public static void AssignRoles(ref Dictionary<PlayerControl, CustomRoles> result)
        {
        }

        public static void ApplyGameOptions(IGameOptions opt, PlayerControl pc)
        {
        }

        public static bool KnowTargetRoleColor(PlayerControl seer, PlayerControl target, ref string color)
        {
        }

        public static bool HasTasks(GameData.PlayerInfo playerInfo)
        {
        }

        public static bool IsRoleTextEnabled(PlayerControl seer, PlayerControl target)
        {
        }

        public static string GetSuffixText(PlayerControl seer, PlayerControl target, bool isHUD = false)
        {
        }

        public static string GetRoleInfoText(PlayerControl seer)
        {
        }

        public static bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
        }

        public static string GetTaskBarText()
        {
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
        }

        public static void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
        }
    }
}
