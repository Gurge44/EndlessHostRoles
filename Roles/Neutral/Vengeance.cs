using System.Collections.Generic;
using AmongUs.GameOptions;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class Vengeance : RoleBase
{
    private const int Id = 12820;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem RevengeTime;

    private bool IsRevenge;
    private int Timer;
    private bool Success;
    private byte Killer;
    private float tempKillTimer;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Vengeance);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance])
            .SetValueFormat(OptionFormat.Seconds);
        RevengeTime = IntegerOptionItem.Create(Id + 11, "VengeanceRevengeTime", new(0, 30, 1), 15, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 12, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance]);
    }

    public override void Init()
    {
        playerIdList = [];
        IsRevenge = false;
        Success = false;
        Killer = byte.MaxValue;
        tempKillTimer = 0;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        Timer = RevengeTime.GetInt();

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return true;
        if (IsRevenge) return true;

        _ = new LateTask(() => { target.TPtoRndVent(); }, 0.01f);

        Timer = RevengeTime.GetInt();
        Countdown(Timer, target);
        IsRevenge = true;
        killer.SetKillCooldown();
        tempKillTimer = Main.KillTimers[target.PlayerId];
        target.SetKillCooldown(time: 1f);
        Killer = killer.PlayerId;

        return false;
    }

    void Countdown(int seconds, PlayerControl player)
    {
        if (!player.IsAlive()) return;

        if (Success)
        {
            Timer = RevengeTime.GetInt();
            Success = false;
            return;
        }

        if ((seconds <= 0 || GameStates.IsMeeting) && player.IsAlive())
        {
            player.Kill(player);
            return;
        }

        player.Notify(string.Format(GetString("VengeanceRevenge"), seconds), 1.1f);
        Timer = seconds;

        _ = new LateTask(() => { Countdown(seconds - 1, player); }, 1.01f);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;
        if (target == null) return false;

        if (!IsRevenge) return true;
        if (target.PlayerId == Killer)
        {
            Success = true;
            killer.Notify(GetString("VengeanceSuccess"));
            killer.SetKillCooldown(KillCooldown.GetFloat() + tempKillTimer);
            IsRevenge = false;
            return true;
        }

        killer.Kill(killer);
        return false;
    }
}