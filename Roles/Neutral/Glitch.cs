using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class Glitch
{
    private static readonly int Id = 18125;
    public static List<byte> playerIdList = new();

    public static Dictionary<byte, long> hackedIdList = new();

    private static OptionItem KillCooldown;
    public static OptionItem HackCooldown;
    public static OptionItem HackDuration;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    public static int HackCDTimer;
    public static int KCDTimer;
    public static long LastHack;
    public static long LastKill;
    //    public static OptionItem CanUseSabotage;

    public static void SetupCustomOption()
    {
        //Glitchは1人固定
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Glitch, 1, zeroOne: false);
        KillCooldown = IntegerOptionItem.Create(Id + 10, "KillCooldown", new(0, 180, 1), 30, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);
        HackCooldown = IntegerOptionItem.Create(Id + 11, "HackCooldown", new(0, 180, 1), 20, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);
        HackDuration = FloatOptionItem.Create(Id + 14, "HackDuration", new(0f, 60f, 1f), 30f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 12, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
        //     CanUseSabotage = BooleanOptionItem.Create(Id + 15, "CanUseSabotage", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
    }
    public static void Init()
    {
        playerIdList = new();
        hackedIdList = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        HackCDTimer = 10;
        KCDTimer = 10;
        LastKill = Utils.GetTimeStamp() - 9 + StartingKillCooldown.GetInt(); // Starting Kill Cooldown is synced with the setting
        LastHack = Utils.GetTimeStamp() - 9 + 7; // Starting Hack Cooldown is 7s

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static void SetHudActive(HudManager __instance, bool isActive)
    {
        __instance.SabotageButton.ToggleVisible(isActive);
    }

    public static bool IsEnable => playerIdList.Any();
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = 1f;
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;
        if (target == null) return false;

        if (KCDTimer > 0 && HackCDTimer > 0) return false;

        if (killer.CheckDoubleTrigger(target, () =>
        {
            if (HackCDTimer <= 0)
            {
                Utils.NotifyRoles(SpecifySeer: killer);
                HackCDTimer = HackCooldown.GetInt();
                hackedIdList.TryAdd(target.PlayerId, Utils.GetTimeStamp());
                LastHack = Utils.GetTimeStamp();
            }
        }))
        {
            if (KCDTimer > 0) return false;
            LastKill = Utils.GetTimeStamp();
            KCDTimer = KillCooldown.GetInt();
            return true;
        }
        else return false;
    }
    public static void UpdateHackCooldown(PlayerControl player)
    {
        if (HackCDTimer > 180 || HackCDTimer < 0) HackCDTimer = 0;
        if (KCDTimer > 180 || KCDTimer < 0) KCDTimer = 0;

        bool change = false;
        foreach (var pc in hackedIdList)
        {
            if (pc.Value + HackDuration.GetInt() < Utils.GetTimeStamp())
            {
                hackedIdList.Remove(pc.Key);
                change = true;
            }
        }
        if (change) { Utils.NotifyRoles(SpecifySeer: player); }

        if (player == null) return;
        if (!player.Is(CustomRoles.Glitch)) return;
        if (!player.IsAlive())
        {
            HackCDTimer = 0;
            KCDTimer = 0;
            return;
        }
        if (HackCDTimer <= 0 && KCDTimer <= 0) return;

        try { HackCDTimer = (int)(HackCooldown.GetInt() - (Utils.GetTimeStamp() - LastHack)); }
        catch { HackCDTimer = 0; }
        if (HackCDTimer > 180 || HackCDTimer < 0) HackCDTimer = 0;

        try { KCDTimer = (int)(KillCooldown.GetInt() - (Utils.GetTimeStamp() - LastKill)); }
        catch { KCDTimer = 0; }
        if (KCDTimer > 180 || KCDTimer < 0) KCDTimer = 0;

        if (!player.IsModClient())
        {
            if (HackCDTimer > 0 && KCDTimer > 0) player.Notify($"\n{string.Format(Translator.GetString("HackCD"), HackCDTimer)}\n{string.Format(Translator.GetString("KCD"), KCDTimer)}");
            if (HackCDTimer > 0 && KCDTimer <= 0) player.Notify($"{string.Format(Translator.GetString("HackCD"), HackCDTimer)}");
            if (HackCDTimer <= 0 && KCDTimer > 0) player.Notify($"{string.Format(Translator.GetString("KCD"), KCDTimer)}");
        }
    }
    public static string GetHudText(PlayerControl player)
    {
        if (player == null) return string.Empty;
        if (!player.Is(CustomRoles.Glitch)) return string.Empty;
        if (!player.IsAlive()) return string.Empty;

        if (HackCDTimer > 0 && KCDTimer > 0) return $"{string.Format(Translator.GetString("HackCD"), HackCDTimer)}\n{string.Format(Translator.GetString("KCD"), KCDTimer)}";
        if (HackCDTimer > 0 && KCDTimer <= 0) return $"{string.Format(Translator.GetString("HackCD"), HackCDTimer)}";
        if (HackCDTimer <= 0 && KCDTimer > 0) return $"{string.Format(Translator.GetString("KCD"), KCDTimer)}";

        return string.Empty;
    }
}
