using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOHE.Roles.Impostor;

public static class SerialKiller
{
    private static readonly int Id = 1700;
    public static List<byte> playerIdList = new();

    private static OptionItem KillCooldown;
    public static OptionItem TimeLimit;
    public static OptionItem WaitFor1Kill;

    private static int Timer;

    public static Dictionary<byte, float> SuicideTimer = new();

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
        playerIdList = new();
        SuicideTimer = new();
    }
    public static void Add(byte serial)
    {
        playerIdList.Add(serial);
        Timer = TimeLimit.GetInt();
    }
    public static bool IsEnable() => playerIdList.Any();
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
        Timer = TimeLimit.GetInt();
        if (CanMurder)
            killer.MarkDirtySettings();
    }
    public static void OnReportDeadBody()
    {
        SuicideTimer.Clear();
        Timer = TimeLimit.GetInt();
    }
    public static void FixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !CustomRoles.SerialKiller.IsEnable()) return;
        if (!HasKilled(player))
        {
            SuicideTimer.Remove(player.PlayerId);
            Timer = TimeLimit.GetInt();
            return;
        }
        if (!SuicideTimer.ContainsKey(player.PlayerId)) //タイマーがない
        {
            SuicideTimer[player.PlayerId] = 0f;
            //player.RpcResetAbilityCooldown();
        }
        else if (SuicideTimer[player.PlayerId] >= TimeLimit.GetFloat())
        {
            //自爆時間が来たとき
            Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Suicide;//死因：自殺
            player.RpcMurderPlayerV3(player);//自殺させる
            SuicideTimer.Remove(player.PlayerId);
            Timer = TimeLimit.GetInt();
        }
        else
        {
            SuicideTimer[player.PlayerId] += Time.fixedDeltaTime;
            int tempTimer = Timer;
            Timer = TimeLimit.GetInt() - (int)SuicideTimer[player.PlayerId];
            if (Timer != tempTimer && Timer <= 20 && !player.IsModClient()) Utils.NotifyRoles(SpecifySeer: player);
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
        for (int i = 0; i < playerIdList.Count; i++)
        {
            byte id = playerIdList[i];
            if (!Main.PlayerStates[id].IsDead)
            {
                var pc = Utils.GetPlayerById(id);
                //pc?.RpcResetAbilityCooldown();
                if (HasKilled(pc))
                {
                    SuicideTimer[id] = 0f;
                    Timer = TimeLimit.GetInt();
                }

            }
        }
    }
}