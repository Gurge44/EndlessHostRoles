namespace TOHE.Roles.Crewmate
{
    internal class Mathematician : RoleBase
    {
        private static int Id => 643370;
        public static (bool AskedQuestion, int Answer, byte ProtectedPlayerId, byte MathematicianPlayerId) State = (false, int.MaxValue, byte.MaxValue, byte.MaxValue);
        public static void SetupCustomOption() => Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mathematician, 1);
        public static bool On;

        public override void Init()
        {
            On = false;
            State = (false, int.MaxValue, byte.MaxValue, byte.MaxValue);
        }

        public override void Add(byte playerId)
        {
            On = true;
            State = (false, int.MaxValue, byte.MaxValue, byte.MaxValue);
        }

        public override bool IsEnable => On;

        public static void Ask(PlayerControl pc, string num1Str, string num2Str)
        {
            if (pc == null || !pc.IsAlive() || !GameStates.IsMeeting || State.AskedQuestion || !int.TryParse(num1Str, out var num1) || !int.TryParse(num2Str, out var num2)) return;

            State.AskedQuestion = true;
            State.Answer = num1 + num2;
            State.MathematicianPlayerId = pc.PlayerId;

            string question = string.Format(Translator.GetString("MathematicianQuestionString"), num1, num2);
            Utils.SendMessage(question, title: Translator.GetString("Mathematician"));
        }
        public static void Reply(PlayerControl pc, string answerStr)
        {
            if (pc == null || !pc.IsAlive() || !GameStates.IsMeeting || !State.AskedQuestion || State.MathematicianPlayerId == pc.PlayerId || !int.TryParse(answerStr, out var answer)) return;

            if (answer == State.Answer)
            {
                State.ProtectedPlayerId = pc.PlayerId;
                Utils.SendMessage(string.Format(Translator.GetString("MathematicianAnsweredString"), pc.GetRealName(), answer), title: Translator.GetString("Mathematician"));
                State.AskedQuestion = false;
            }
        }

        public override void OnReportDeadBody()
        {
            State = (false, int.MaxValue, byte.MaxValue, byte.MaxValue);
        }
    }
}
