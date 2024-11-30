using System.Collections.Generic;
using static EHR.Translator;

namespace EHR.Crewmate
{
    public class GuessManagerRole : RoleBase
    {
        public static List<byte> PlayerIdList = [];

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(642640, TabGroup.CrewmateRoles, CustomRoles.GuessManagerRole);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
        }

        public static void OnGuess(PlayerControl dp, PlayerControl pc)
        {
            foreach (byte guessManager in PlayerIdList) LateTask.New(() => { Utils.SendMessage(dp == pc ? string.Format(GetString("GuessManagerMessageAboutMisguess"), dp.GetRealName().Replace("\n", " + ")) : string.Format(GetString("GuessManagerMessageAboutGuessedRole"), dp.GetAllRoleName().Replace("\n", " + ")), guessManager); }, 1f, "Guess Manager Messages");
        }
    }
}