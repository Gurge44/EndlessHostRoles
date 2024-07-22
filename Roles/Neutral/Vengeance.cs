using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

public class Vengeance : RoleBase
{
    private const int Id = 12820;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem RevengeTime;

    private bool IsRevenge;
    private byte Killer;
    private bool Success;
    private float tempKillTimer;
    private int Timer;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Vengeance);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance])
            .SetValueFormat(OptionFormat.Seconds);
        RevengeTime = new IntegerOptionItem(Id + 11, "VengeanceRevengeTime", new(0, 30, 1), 15, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 12, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance]);
        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance]);
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

        IsRevenge = false;
        Success = false;
        Killer = byte.MaxValue;
        tempKillTimer = 0;
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return true;
        if (IsRevenge) return true;

        LateTask.New(() => { target.TPtoRndVent(); }, 0.01f, log: false);

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

        if (seconds <= 0 || GameStates.IsMeeting)
        {
            player.Kill(player);
            return;
        }

        player.Notify(string.Format(GetString("VengeanceRevenge"), seconds), 1.1f);
        Timer = seconds;

        LateTask.New(() => { Countdown(seconds - 1, player); }, 1.01f, log: false);
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