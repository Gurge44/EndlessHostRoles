using System.Collections.Generic;
using UnityEngine;

namespace TOHE.Roles.Impostor;

public static class SerialKiller
{
    private static readonly int Id = 1700;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem TimeLimit;
    public static OptionItem WaitFor1Kill;

    private static Dictionary<byte, int> Timer;

    public static Dictionary<byte, float> SuicideTimer = [];

    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.SerialKiller, 1);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller])
            .SetValueFormat(OptionFormat.Seconds);
        TimeLimit = FloatOptionItem.Create(Id + 11, "SerialKillerLimit", new(5f, 180f, 5f), 80f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller])
            .SetValueFormat(OptionFormat.Seconds);
        WaitFor1Kill = BooleanOptionItem.Create(Id + 12, "WaitFor1Kill", true, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller]);
    }
    public static void Init()
    {
        playerIdList = [];
        SuicideTimer = [];
        Timer = [];
    }
    public static void Add(byte serial)
    {
        playerIdList.Add(serial);
        Timer.Add(serial, TimeLimit.GetInt());
    }
    public static bool IsEnable() => playerIdList.Count > 0;
    public static void ApplyKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    //public static void ApplyGameOptions(PlayerControl pc)
    //{
    //    AURoleOptions.ShapeshifterCooldown = HasKilled(pc) ? TimeLimit.GetFloat() : 255f;
    //    AURoleOptions.ShapeshifterDuration = 1f;
    //}
    ///<summary>
    ///シリアルキラー＋生存＋一人以上キルしている
    ///</summary>
    public static bool HasKilled(PlayerControl pc)
        => pc != null && pc.Is(CustomRoles.SerialKiller) && pc.IsAlive() && (Main.PlayerStates[pc.PlayerId].GetKillCount(true) > 0 || !WaitFor1Kill.GetBool());
    public static void OnCheckMurder(PlayerControl killer, bool CanMurder = true)
    {
        if (!killer.Is(CustomRoles.SerialKiller)) return;
        SuicideTimer.Remove(killer.PlayerId);
        Timer[killer.PlayerId] = TimeLimit.GetInt();
        if (CanMurder) killer.MarkDirtySettings();
    }
    public static void OnReportDeadBody()
    {
        SuicideTimer.Clear();
        foreach (var kvp in Timer) Timer[kvp.Key] = TimeLimit.GetInt();
    }
    public static void FixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !CustomRoles.SerialKiller.IsEnable()) return;
        if (!HasKilled(player))
        {
            SuicideTimer.Remove(player.PlayerId);
            Timer[player.PlayerId] = TimeLimit.GetInt();
            return;
        }

        if (SuicideTimer.TryAdd(player.PlayerId, 0f)) //タイマーがない
        {
            //player.RpcResetAbilityCooldown();
        }
        else if (SuicideTimer[player.PlayerId] >= TimeLimit.GetFloat())
        {
            //自爆時間が来たとき //死因：自殺
            player.Suicide(); //自殺させる
            SuicideTimer.Remove(player.PlayerId);
            Timer[player.PlayerId] = TimeLimit.GetInt();
        }
        else
        {
            SuicideTimer[player.PlayerId] += Time.fixedDeltaTime;
            int tempTimer = Timer[player.PlayerId];
            Timer[player.PlayerId] = TimeLimit.GetInt() - (int)SuicideTimer[player.PlayerId];
            if (Timer[player.PlayerId] != tempTimer && Timer[player.PlayerId] <= 20 && !player.IsModClient()) Utils.NotifyRoles(SpecifySeer: player);
        }
        //時間をカウント
    }
    //public static void GetAbilityButtonText(HudManager __instance, PlayerControl pc)
    //{
    //    __instance.AbilityButton.ToggleVisible(pc.IsAlive() && HasKilled(pc));
    //    __instance.AbilityButton.OverrideText(GetString("SerialKillerSuicideButtonText"));
    //}
    public static void AfterMeetingTasks()
    {
        foreach (byte id in playerIdList.ToArray())
        {
            if (!Main.PlayerStates[id].IsDead)
            {
                var pc = Utils.GetPlayerById(id);
                //pc?.RpcResetAbilityCooldown();
                if (HasKilled(pc))
                {
                    SuicideTimer[id] = 0f;
                    Timer[id] = TimeLimit.GetInt();
                }

            }
        }
    }
}