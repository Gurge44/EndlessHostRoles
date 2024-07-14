using System.Collections.Generic;
using System.Linq;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Impostor;

public class Vampire : RoleBase
{
    private const int Id = 4500;
    private static readonly List<byte> PlayerIdList = [];
    private static readonly Dictionary<byte, BittenInfo> BittenPlayers = [];

    private static OptionItem Cooldown;
    private static OptionItem OptionKillDelay;
    private static OptionItem OptionCanKillNormally;
    private bool CanKillNormally;
    private bool CanVent;

    private bool IsPoisoner;
    private float KillCooldown;
    private float KillDelay;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Vampire);
        Cooldown = new FloatOptionItem(Id + 9, "VampireKillCooldown", new(1f, 30f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vampire])
            .SetValueFormat(OptionFormat.Seconds);
        OptionKillDelay = new FloatOptionItem(Id + 10, "VampireKillDelay", new(1f, 30f, 1f), 3f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vampire])
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanKillNormally = new BooleanOptionItem(Id + 11, "CanKillNormally", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vampire]);
    }

    public override void Init()
    {
        PlayerIdList.Clear();
        BittenPlayers.Clear();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);

        IsPoisoner = Main.PlayerStates[playerId].MainRole == CustomRoles.Poisoner;
        if (!IsPoisoner)
        {
            KillCooldown = Cooldown.GetFloat();
            KillDelay = OptionKillDelay.GetFloat();
            CanVent = true;
            CanKillNormally = OptionCanKillNormally.GetBool();
        }
        else
        {
            KillCooldown = Poisoner.KillCooldown.GetFloat();
            KillDelay = Poisoner.OptionKillDelay.GetFloat();
            CanVent = Poisoner.CanVent.GetBool();
            CanKillNormally = Poisoner.CanKillNormally.GetBool();
        }

        if (!AmongUsClient.Instance.AmHost || !IsPoisoner) return;
        Main.ResetCamPlayerList.Add(playerId);
    }

    public static bool IsThisRole(byte playerId) => PlayerIdList.Contains(playerId);

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsThisRole(killer.PlayerId)) return true;
        if (target.Is(CustomRoles.Bait)) return true;
        if (target.Is(CustomRoles.Pestilence)) return true;
        if (target.Is(CustomRoles.Guardian) && target.AllTasksCompleted()) return true;
        if (target.Is(CustomRoles.Opportunist) && target.AllTasksCompleted() && Opportunist.OppoImmuneToAttacksWhenTasksDone.GetBool()) return true;
        if (target.Is(CustomRoles.Veteran) && Veteran.VeteranInProtect.ContainsKey(target.PlayerId)) return true;
        if (Medic.ProtectList.Contains(target.PlayerId)) return true;

        if (CanKillNormally) return killer.CheckDoubleTrigger(target, Bite);

        Bite();
        return false;

        void Bite()
        {
            killer.SetKillCooldown(KillCooldown + KillDelay);
            killer.RPCPlayCustomSound("Bite");

            if (!BittenPlayers.ContainsKey(target.PlayerId))
            {
                BittenPlayers.Add(target.PlayerId, new(killer.PlayerId, 0f));
            }
        }
    }

    public override void OnFixedUpdate(PlayerControl vampire)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask) return;

        var vampireID = vampire.PlayerId;
        if (!IsThisRole(vampire.PlayerId)) return;

        List<byte> targetList = [.. BittenPlayers.Where(b => b.Value.VampireId == vampireID).Select(b => b.Key)];

        foreach (byte targetId in targetList.ToArray())
        {
            var bitten = BittenPlayers[targetId];
            if (bitten.KillTimer >= KillDelay)
            {
                var target = Utils.GetPlayerById(targetId);
                KillBitten(vampire, target);
                BittenPlayers.Remove(targetId);
            }
            else
            {
                bitten.KillTimer += Time.fixedDeltaTime;
                BittenPlayers[targetId] = bitten;
            }
        }
    }

    void KillBitten(PlayerControl vampire, PlayerControl target, bool isButton = false)
    {
        if (vampire == null || target == null || target.Data.Disconnected) return;
        if (target.IsAlive())
        {
            target.Suicide(IsPoisoner ? PlayerState.DeathReason.Poison : PlayerState.DeathReason.Bite, vampire);
            if (!isButton && vampire.IsAlive())
            {
                RPC.PlaySoundRPC(vampire.PlayerId, Sounds.KillSound);
                if (target.Is(CustomRoles.Trapper))
                    vampire.TrapperKilled(target);
                vampire.Notify(GetString("VampireTargetDead"));
            }
        }
    }

    public override void OnReportDeadBody()
    {
        foreach (var targetId in BittenPlayers.Keys)
        {
            var target = Utils.GetPlayerById(targetId);
            var vampire = Utils.GetPlayerById(BittenPlayers[targetId].VampireId);
            KillBitten(vampire, target);
        }

        BittenPlayers.Clear();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton.OverrideText(GetString(IsPoisoner ? "PoisonerKillButtonText" : "VampireBiteButtonText"));
    }

    private class BittenInfo(byte vampierId, float killTimer)
    {
        public readonly byte VampireId = vampierId;
        public float KillTimer = killTimer;
    }
}