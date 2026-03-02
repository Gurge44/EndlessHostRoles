using EHR.Modules.Extensions;

namespace EHR.Roles;

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
    public bool IsDashing { get; set; }
    public int Cooldown { get; init; } = 20;
    public int Duration { get; init; } = 5;
}