using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public class Vampire : RoleBase
{
    private class BittenInfo(byte vampierId, float killTimer)
    {
        public readonly byte VampireId = vampierId;
        public float KillTimer = killTimer;
    }

    private const int Id = 4500;
    private static readonly List<byte> PlayerIdList = [];
    private static OptionItem OptionKillDelay;
    private static readonly Dictionary<byte, BittenInfo> BittenPlayers = [];

    private float KillCooldown;
    private float KillDelay;
    private bool CanVent;

    private bool IsPoisoner;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Vampire);
        OptionKillDelay = FloatOptionItem.Create(Id + 10, "VampireKillDelay", new(1f, 30f, 1f), 3f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vampire])
            .SetValueFormat(OptionFormat.Seconds);
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
        if (IsPoisoner)
        {
            KillCooldown = Options.DefaultKillCooldown;
            KillDelay = OptionKillDelay.GetFloat();
            CanVent = true;
        }
        else
        {
            KillCooldown = Poisoner.KillCooldown.GetFloat();
            KillDelay = Poisoner.OptionKillDelay.GetFloat();
            CanVent = Poisoner.CanVent.GetBool();
        }

        if (!AmongUsClient.Instance.AmHost || !IsPoisoner) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => PlayerIdList.Count > 0;
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
        if (target.Is(CustomRoles.Opportunist) && target.AllTasksCompleted() && Options.OppoImmuneToAttacksWhenTasksDone.GetBool()) return false;
        if (target.Is(CustomRoles.Veteran) && Main.VeteranInProtect.ContainsKey(target.PlayerId)) return true;
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;

        killer.SetKillCooldown();
        _ = new LateTask(() =>
        {
            if (GameStates.IsInTask)
                killer.SetKillCooldown();
        }, OptionKillDelay.GetFloat());
        killer.RPCPlayCustomSound("Bite");

        if (!BittenPlayers.ContainsKey(target.PlayerId))
        {
            BittenPlayers.Add(target.PlayerId, new(killer.PlayerId, 0f));
        }

        return false;
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

    public static void KillBitten(PlayerControl vampire, PlayerControl target, bool isButton = false)
    {
        if (vampire == null || target == null || target.Data.Disconnected) return;
        if (target.IsAlive())
        {
            target.Suicide(PlayerState.DeathReason.Bite, vampire);
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
        hud.KillButton.OverrideText(GetString("VampireBiteButtonText"));
    }
}