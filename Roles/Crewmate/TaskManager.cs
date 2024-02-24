using System.Text;

namespace TOHE.Roles.Crewmate
{
    internal class TaskManager : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            string totalCompleted = comms ? "?" : $"{GameData.Instance.CompletedTasks}";
            ProgressText.Append(Utils.GetTaskCount(playerId, comms));
            ProgressText.Append($" <color=#777777>-</color> <color=#00ffa5>{totalCompleted}</color><color=#ffffff>/{GameData.Instance.TotalTasks}</color>");
        }
    }
}
