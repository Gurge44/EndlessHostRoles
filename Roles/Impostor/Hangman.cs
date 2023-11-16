using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Crewmate;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public static class Hangman
{
    private static readonly int Id = 1400;
    private static List<byte> playerIdList = [];
    public static Dictionary<byte, float> HangLimit = [];

    private static OptionItem ShapeshiftCooldown;
    public static OptionItem ShapeshiftDuration;
    private static OptionItem KCD;
    private static OptionItem HangmanLimitOpt;
    public static OptionItem HangmanAbilityUseGainWithEachKill;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Hangman);
        ShapeshiftCooldown = FloatOptionItem.Create(Id + 2, "ShapeshiftCooldown", new(1f, 60f, 1f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftDuration = FloatOptionItem.Create(Id + 3, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
            .SetValueFormat(OptionFormat.Seconds);
        KCD = FloatOptionItem.Create(Id + 4, "KillCooldownOnStrangle", new(1f, 90f, 1f), 40f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
            .SetValueFormat(OptionFormat.Seconds);
        HangmanLimitOpt = IntegerOptionItem.Create(Id + 5, "AbilityUseLimit", new(0, 5, 1), 0, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
            .SetValueFormat(OptionFormat.Times);
        HangmanAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 6, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
            .SetValueFormat(OptionFormat.Times);
    }
    public static void Init()
    {
        playerIdList = [];
        HangLimit = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        HangLimit.Add(playerId, HangmanLimitOpt.GetInt());
    }
    public static bool IsEnable => playerIdList.Any();
    public static void ApplyGameOptions()
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = ShapeshiftDuration.GetFloat();
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        //    if (target.Is(CustomRoles.Bait)) return true;
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;

        //禁止内鬼刀叛徒
        if (target.Is(CustomRoles.Madmate) && !ImpCanKillMadmate.GetBool())
            return false;

        if (HangLimit[killer.PlayerId] < 1)
        {
            if (Main.CheckShapeshift.TryGetValue(killer.PlayerId, out var ss) && ss) return false;
        };

        if (Main.CheckShapeshift.TryGetValue(killer.PlayerId, out var s) && s)
        {
            if (target.Is(CustomRoles.Pestilence)) return false;
            if (target.Is(CustomRoles.Veteran) && Main.VeteranInProtect.ContainsKey(target.PlayerId)) return false;
            HangLimit[killer.PlayerId] -= 1;
            target.Data.IsDead = true;
            target.SetRealKiller(killer);
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.LossOfHead;
            target.RpcExileV2();
            Main.PlayerStates[target.PlayerId].SetDead();
            target.SetRealKiller(killer);
            killer.SetKillCooldown(time: KCD.GetFloat());
            return false;
        }
        return true;
    }
}