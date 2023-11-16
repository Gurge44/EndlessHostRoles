using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    public static class GuessManagerRole
    {
        public static List<byte> playerIdList = [];
        public static void SetupCustomOption() => Options.SetupRoleOptions(642640, TabGroup.CrewmateRoles, CustomRoles.GuessManager);
        public static void Init() => playerIdList = [];
        public static void Add(byte playerId) => playerIdList.Add(playerId);
        public static void OnGuess(PlayerControl dp, PlayerControl pc)
        {
            foreach (var guessManager in playerIdList.ToArray())
            {
                _ = new LateTask(() =>
                {
                    if (dp == pc)
                        Utils.SendMessage(string.Format(GetString("GuessManagerMessageAboutMisguess"), dp.GetRealName().Replace("\n", " + ")));
                    else
                        Utils.SendMessage(string.Format(GetString("GuessManagerMessageAboutGuessedRole"), dp.GetAllRoleName().Replace("\n", " + ")));
                }, 1f, "Guess Manager Messages");
            }
        }
    }
}
