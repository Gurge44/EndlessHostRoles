using System.Collections.Generic;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor;

public class Mastermind : RoleBase
{
    private const int Id = 640600;

    private static List<byte> PlayerIdList = [];

    public static Dictionary<byte, long> ManipulatedPlayers = [];
    public static Dictionary<byte, long> ManipulateDelays = [];
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
        if (killer == null) return false;
        if (target == null) return false;
        
        if (Thanos.IsImmune(target)) return false;

        return killer.CheckDoubleTrigger(target, () =>
        {
            killer.RPCPlayCustomSound("Line");
            killer.SetKillCooldown(ManipulateCD);
            ManipulateDelays.TryAdd(target.PlayerId, TimeStamp);
            NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        });
    }

    public override void OnFixedUpdate(PlayerControl _)
    {
        if (!IsEnable || GameStates.IsMeeting || (ManipulatedPlayers.Count == 0 && ManipulateDelays.Count == 0)) return;

        foreach (KeyValuePair<byte, long> x in ManipulateDelays)
        {
            PlayerControl pc = GetPlayerById(x.Key);

            if (!pc.IsAlive())
            {
                ManipulateDelays.Remove(x.Key);
                continue;
            }

            if (x.Value + Delay.GetInt() < TimeStamp)
            {
                ManipulateDelays.Remove(x.Key);
                ManipulatedPlayers.TryAdd(x.Key, TimeStamp);

                if (pc.HasKillButton()) TempKCDs.TryAdd(pc.PlayerId, Main.KillTimers[pc.PlayerId]);
                else pc.RpcChangeRoleBasis(CustomRoles.SerialKiller);
                
                pc.SetKillCooldown(1f);
                NotifyRoles(SpecifySeer: MastermindPC, SpecifyTarget: pc);
            }
        }

        foreach (KeyValuePair<byte, long> x in ManipulatedPlayers)
        {
            PlayerControl player = GetPlayerById(x.Key);

            if (!player.IsAlive())
            {
                ManipulatedPlayers.Remove(x.Key);
                TempKCDs.Remove(x.Key);
                continue;
            }

            if (x.Value + TimeLimit.GetInt() < TimeStamp)
            {
                ManipulatedPlayers.Remove(x.Key);
                if (!TempKCDs.Remove(x.Key)) player.RpcChangeRoleBasis(player.GetCustomRole());
                player.Suicide(realKiller: MastermindPC);
                RPC.PlaySoundRPC(MastermindId, Sounds.KillSound);

                if (player.AmOwner)
                    Achievements.Type.OutOfTime.Complete();
            }

            long time = TimeLimit.GetInt() - (TimeStamp - x.Value);

            player.Notify(string.Format(GetString("ManipulateNotify"), time), 3f, true);
        }
    }

    public override void OnReportDeadBody()
    {
        if (!IsEnable) return;

        foreach (KeyValuePair<byte, long> x in ManipulatedPlayers)
        {
            PlayerControl pc = GetPlayerById(x.Key);
            if (pc.IsAlive()) pc.Suicide(realKiller: MastermindPC);
        }

        ManipulateDelays.Clear();
        ManipulatedPlayers.Clear();
        TempKCDs.Clear();
    }

    public static bool ForceKillForManipulatedPlayer(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;
        if (target == null) return false;

        ManipulatedPlayers.Remove(killer.PlayerId);

        foreach (byte id in PlayerIdList) (Main.PlayerStates[id].Role as Mastermind)?.NotifyMastermindTargetSurvived();

        if (target.Is(CustomRoles.Pestilence) || Veteran.VeteranInProtect.ContainsKey(target.PlayerId) || target.Is(CustomRoles.Mastermind))
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
