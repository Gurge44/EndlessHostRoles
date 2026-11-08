using EHR.Gamemodes;

namespace EHR.Roles;

internal class Seeker : RoleBase, IHideAndSeekRole
{
    public static bool On;

    public const int StartId = 69_211_201;

    public static OptionItem Vision;
    public static OptionItem Speed;
    public static OptionItem CanVent;
    public static OptionItem BlindTime;
    public static OptionItem KillCooldown;

    public override bool IsEnable => On;
    public Team Team => Team.Impostor;
    public int Chance => 100;
    public int Count => CustomHnS.SeekerNum;
    public float RoleSpeed => Speed.GetFloat();
    public float RoleVision => Vision.GetFloat();

    public override void SetupCustomOption()
    {
        Vision = new FloatOptionItem(StartId, "SeekerVision", new(0.05f, 5f, 0.05f), 0.25f, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(255, 25, 25, byte.MaxValue));

        Speed = new FloatOptionItem(StartId + 1, "SeekerSpeed", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(255, 25, 25, byte.MaxValue));

        CanVent = new BooleanOptionItem(StartId + 3, "CanVent", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(255, 25, 25, byte.MaxValue));

        BlindTime = new FloatOptionItem(StartId + 5, "BlindTime", new(0f, 60f, 1f), 10f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(255, 25, 25, byte.MaxValue));

        KillCooldown = new IntegerOptionItem(StartId + 6, "KillCooldown", new(0, 60, 1), 10, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(255, 25, 25, byte.MaxValue));
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return true;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }
}