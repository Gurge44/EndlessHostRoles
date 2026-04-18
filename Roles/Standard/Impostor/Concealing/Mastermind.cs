using System.Collections.Generic;
using EHR.Modules;
using EHR.Modules.Extensions;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Roles;

public class Mastermind : RoleBase
{
    private const int Id = 640600;

    private static List<byte> PlayerIdList = [];

    public static Dictionary<byte, CountdownTimer> ManipulatedPlayers = [];
    public static Dictionary<byte, CountdownTimer> ManipulateDelays = [];
    private static Dictionary<byte, float> TempKCDs = [];

    private static OptionItem KillCooldown;
    private static OptionItem TimeLimit;
    private static OptionItem Delay;

    private static float ManipulateCD;
    private byte MastermindId = byte.MaxValue;

    private PlayerControl MastermindPC => GetPlayerById(MastermindId);

    public override bool IsEnable => MastermindId != byte.MaxValue || Randomizer.Exists;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Mastermind);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
            .SetValueFormat(OptionFormat.Seconds);

        // Manipulation Cooldown = Kill Cooldown + Delay + Time Limit
        TimeLimit = new IntegerOptionItem(Id + 12, "MastermindTimeLimit", new(1, 60, 1), 20, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
            .SetValueFormat(OptionFormat.Seconds);

        Delay = new IntegerOptionItem(Id + 13, "MastermindDelay", new(0, 30, 1), 7, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        MastermindId = byte.MaxValue;
        ManipulatedPlayers = [];
        ManipulateDelays = [];
        TempKCDs = [];
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        MastermindId = playerId;
        ManipulateCD = KillCooldown.GetFloat() + TimeLimit.GetFloat() + Delay.GetFloat();
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable) return false;
        if (Thanos.IsImmune(target)) return false;

        return killer.CheckDoubleTrigger(target, () =>
        {
            killer.RPCPlayCustomSound("Line");
            killer.SetKillCooldown(ManipulateCD);

            byte targetId = target.PlayerId;
            ManipulateDelays.TryAdd(targetId, new CountdownTimer(Delay.GetInt(), () =>
            {
                ManipulateDelays.Remove(targetId);
                if (target == null || !target.IsAlive()) return;
                
                ManipulatedPlayers.TryAdd(targetId, new CountdownTimer(TimeLimit.GetInt(), () =>
                {
                    ManipulatedPlayers.Remove(targetId);
                    
                    if (target == null || !target.IsAlive())
                    {
                        TempKCDs.Remove(targetId);
                        return;
                    }

                    if (!TempKCDs.Remove(targetId)) target.RpcChangeRoleBasis(target.GetCustomRole());
                    LateTask.New(() => target.Suicide(realKiller: killer), 0.2f);
                    RPC.PlaySoundRPC(MastermindId, Sounds.KillSound);

                    if (target.AmOwner)
                        Achievements.Type.OutOfTime.Complete();
                }, onTick: () =>
                {
                    if (target == null || !target.IsAlive() || !ManipulatedPlayers.TryGetValue(targetId, out var timer))
                    {
                        ManipulatedPlayers.Remove(targetId);
                        TempKCDs.Remove(targetId);
                        return;
                    }
                    
                    target.Notify(string.Format(GetString("ManipulateNotify"), (int)timer.Remaining.TotalSeconds), 3f, true);
                }, onCanceled: () =>
                {
                    ManipulatedPlayers.Remove(targetId);
                    TempKCDs.Remove(targetId);
                }));

                if (target.HasKillButton()) TempKCDs.TryAdd(targetId, Main.KillTimers[targetId]);
                else target.RpcChangeRoleBasis(CustomRoles.SerialKiller);
                
                target.SetKillCooldown(1f);
                NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            }, onCanceled: () => ManipulateDelays.Remove(targetId)));
            
            NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        });
    }

    public override void OnReportDeadBody()
    {
        if (!IsEnable) return;

        foreach (KeyValuePair<byte, CountdownTimer> x in ManipulatedPlayers)
        {
            x.Value.Dispose();
            PlayerControl pc = GetPlayerById(x.Key);
            if (pc != null && pc.IsAlive()) pc.Suicide(realKiller: MastermindPC);
        }

        ManipulateDelays.Values.Do(x => x.Dispose());
        ManipulateDelays.Clear();
        ManipulatedPlayers.Clear();
        TempKCDs.Clear();
    }

    public static bool ForceKillForManipulatedPlayer(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;
        if (target == null) return false;

        ManipulatedPlayers[killer.PlayerId].Dispose();
        ManipulatedPlayers.Remove(killer.PlayerId);

        foreach (byte id in PlayerIdList) (Main.PlayerStates[id].Role as Mastermind)?.NotifyMastermindTargetSurvived();

        if (target.Is(CustomRoles.Pestilence) || Veteran.VeteranInProtect.Contains(target.PlayerId) || target.Is(CustomRoles.Mastermind))
        {
            Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
            Main.PlayerStates[killer.PlayerId].SetDead();
            target.Kill(killer);
            TempKCDs.Remove(killer.PlayerId);

            if (target.AmOwner)
                Achievements.Type.YoureTooLate.Complete();

            return false;
        }

        killer.Kill(target);
        killer.Notify(GetString("MastermindTargetSurvived"));
        killer.RpcChangeRoleBasis(killer.GetCustomRole());

        LateTask.New(() =>
        {
            float kcd = TempKCDs.TryGetValue(killer.PlayerId, out float cd) ? cd : Main.AllPlayerKillCooldown.GetValueOrDefault(killer.PlayerId, AdjustedDefaultKillCooldown);
            killer.SetKillCooldown(kcd);
            TempKCDs.Remove(killer.PlayerId);
        }, 0.1f, "Set KCD for Manipulated Kill");

        return false;
    }

    private void NotifyMastermindTargetSurvived()
    {
        if (!IsEnable) return;

        MastermindPC.Notify(GetString("ManipulatedKilled"));
        if (Main.KillTimers[MastermindId] > KillCooldown.GetFloat()) MastermindPC.SetKillCooldown(KillCooldown.GetFloat());
    }
}
