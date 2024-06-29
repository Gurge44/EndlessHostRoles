using System.Collections.Generic;
using static EHR.Translator;

namespace EHR.Crewmate
{
    public class GuessManagerRole : RoleBase
    {
        public static List<byte> playerIdList = [];

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption() => Options.SetupRoleOptions(642640, TabGroup.CrewmateRoles, CustomRoles.GuessManagerRole);
        public override void Init() => playerIdList = [];
        public override void Add(byte playerId) => playerIdList.Add(playerId);

        public static void OnGuess(PlayerControl dp, PlayerControl pc)
        {
            foreach (var guessManager in playerIdList)
            {
                LateTask.New(() => { Utils.SendMessage(dp == pc ? string.Format(GetString("GuessManagerMessageAboutMisguess"), dp.GetRealName().Replace("\n", " + ")) : string.Format(GetString("GuessManagerMessageAboutGuessedRole"), dp.GetAllRoleName().Replace("\n", " + ")), guessManager); }, 1f, "Guess Manager Messages");
            }
        }
    }
}