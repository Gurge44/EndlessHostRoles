namespace EHR.GameMode.HideAndSeekRoles
{
    internal interface IHideAndSeekRole
    {
        public Team Team { get; }
        public int Count { get; }
        public int Chance { get; }
        public float RoleSpeed { get; }
        public float RoleVision { get; }
    }

    public class DashStatus
    {
        public long DashEndTime { get; set; } = Utils.TimeStamp;
        public bool IsDashing { get; set; } = false;
        public int Cooldown { get; init; } = 20;
        public int Duration { get; init; } = 5;
    }
}