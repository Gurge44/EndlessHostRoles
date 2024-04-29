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
}