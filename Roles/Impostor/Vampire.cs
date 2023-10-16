using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public static class Vampire
{
    private class BittenInfo
    {
        public byte VampireId;
        public float KillTimer;

        public BittenInfo(byte vampierId, float killTimer)
        {
            VampireId = vampierId;
            KillTimer = killTimer;
        }
    }

    private static readonly int Id = 4500;
    private static readonly List<byte> PlayerIdList = new();
    private static OptionItem OptionKillDelay;
    private static float KillDelay;
    private static readonly Dictionary<byte, BittenInfo> BittenPlayers = new();
    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Vampire);
        OptionKillDelay = FloatOptionItem.Create(Id + 10, "VampireKillDelay", new(1f, 30f, 1f), 3f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vampire])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        IsEnable = false;
        PlayerIdList.Clear();
        BittenPlayers.Clear();

        KillDelay = OptionKillDelay.GetFloat();
    }

    public static void Add(byte playerId)
    {
        IsEnable = true;
        PlayerIdList.Add(playerId);
    }

    public static bool IsEnable;
    public static bool IsThisRole(byte playerId) => PlayerIdList.Contains(playerId);

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsThisRole(killer.PlayerId)) return true;
        if (target.Is(CustomRoles.Bait)) return true;
        if (target.Is(CustomRoles.Pestilence)) return true;
        if (target.Is(CustomRoles.Guardian) && target.AllTasksCompleted()) return true;
        if (target.Is(CustomRoles.Opportunist) && target.AllTasksCompleted() && Options.OppoImmuneToAttacksWhenTasksDone.GetBool()) return false;
        if (target.Is(CustomRoles.Veteran) && Main.VeteranInProtect.ContainsKey(target.PlayerId)) return true;
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;

        killer.SetKillCooldown();
        _ = new LateTask(() => { killer.SetKillCooldown(); }, OptionKillDelay.GetFloat());
        killer.RPCPlayCustomSound("Bite");

        //誰かに噛まれていなければ登録
        if (!BittenPlayers.ContainsKey(target.PlayerId))
        {
            BittenPlayers.Add(target.PlayerId, new(killer.PlayerId, 0f));
        }
        return false;
    }

    public static void OnFixedUpdate(PlayerControl vampire)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask) return;

        var vampireID = vampire.PlayerId;
        if (!IsThisRole(vampire.PlayerId)) return;

        List<byte> targetList = new(BittenPlayers.Where(b => b.Value.VampireId == vampireID).Select(b => b.Key));

        for (int i = 0; i < targetList.Count; i++)
        {
            byte targetId = targetList[i];
            var bitten = BittenPlayers[targetId];
            if (bitten.KillTimer >= KillDelay)
            {
                var target = Utils.GetPlayerById(targetId);
                KillBitten(vampire, target);
                _ = BittenPlayers.Remove(targetId);
            }
            else
            {
                bitten.KillTimer += Time.fixedDeltaTime;
                BittenPlayers[targetId] = bitten;
            }
        }
    }
    public static void KillBitten(PlayerControl vampire, PlayerControl target, bool isButton = false)
    {
        if (vampire == null || target == null || target.Data.Disconnected) return;
        if (target.IsAlive())
        {
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Bite;
            target.SetRealKiller(vampire);
            target.RpcMurderPlayerV3(target);
            Medic.IsDead(target);
            Logger.Info($"Vampireに噛まれている{target.name}を自爆させました。", "Vampire");
            if (!isButton && vampire.IsAlive())
            {
                RPC.PlaySoundRPC(vampire.PlayerId, Sounds.KillSound);
                if (target.Is(CustomRoles.Trapper))
                    vampire.TrapperKilled(target);
                vampire.Notify(GetString("VampireTargetDead"));
            }
        }
        else
        {
            Logger.Info("Vampireに噛まれている" + target.name + "はすでに死んでいました。", "Vampire");
        }
    }

    public static void OnStartMeeting()
    {
        foreach (var targetId in BittenPlayers.Keys)
        {
            var target = Utils.GetPlayerById(targetId);
            var vampire = Utils.GetPlayerById(BittenPlayers[targetId].VampireId);
            KillBitten(vampire, target);
        }
        BittenPlayers.Clear();
    }
    public static void SetKillButtonText()
    {
        HudManager.Instance.KillButton.OverrideText(GetString("VampireBiteButtonText"));
    }
}
