using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using static EHR.Translator;

namespace EHR.Roles;

public class Vampire : RoleBase
{
    private const int Id = 4500;
    private static readonly List<byte> PlayerIdList = [];
    private static readonly HashSet<byte> BittenPlayers = [];

    private static OptionItem Cooldown;
    private static OptionItem OptionKillDelay;
    public static OptionItem OptionCanKillNormally;

    private bool CanKillNormally;
    private bool CanVent;
    private bool IsPoisoner;
    private float KillCooldown;
    private float KillDelay;

    private byte VampireId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Vampire);

        Cooldown = new FloatOptionItem(Id + 9, "VampireKillCooldown", new(1f, 120f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Vampire])
            .SetValueFormat(OptionFormat.Seconds);

        OptionKillDelay = new FloatOptionItem(Id + 10, "VampireKillDelay", new(1f, 120f, 1f), 3f, TabGroup.ImpostorRoles)
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

        VampireId = playerId;
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
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (IsPoisoner) opt.SetVision(Poisoner.ImpostorVision.GetBool());
    }

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
        if (target.Is(CustomRoles.Bait)) return true;
        if (target.Is(CustomRoles.Pestilence)) return true;
        if (target.Is(CustomRoles.Guardian) && target.AllTasksCompleted()) return true;
        if (target.Is(CustomRoles.Opportunist) && target.AllTasksCompleted() && Opportunist.OppoImmuneToAttacksWhenTasksDone.GetBool()) return true;
        if (target.Is(CustomRoles.Veteran) && Veteran.VeteranInProtect.Contains(target.PlayerId)) return true;
        if (Medic.ProtectList.Contains(target.PlayerId)) return true;

        if (CanKillNormally) return killer.CheckDoubleTrigger(target, Bite);

        Bite();
        return false;

        void Bite()
        {
            killer.SetKillCooldown(KillCooldown + KillDelay);
            killer.RPCPlayCustomSound("Bite");
            
            BittenPlayers.Add(target.PlayerId);

            _ = new CountdownTimer(KillDelay, () =>
            {
                if (target == null || !target.IsAlive()) return;
                KillBitten(killer, target);
                BittenPlayers.Remove(target.PlayerId);
            }, onCanceled: BittenPlayers.Clear);
        }
    }

    private void KillBitten(PlayerControl vampire, PlayerControl target, bool meeting = false)
    {
        if (vampire == null || target == null || target.Data.Disconnected) return;

        if (target.IsAlive())
        {
            target.Suicide(IsPoisoner ? PlayerState.DeathReason.Poison : PlayerState.DeathReason.Bite, vampire);
            RPC.PlaySoundRPC(vampire.PlayerId, Sounds.KillSound);

            if (!meeting && vampire.IsAlive())
            {
                if (target.Is(CustomRoles.Beartrap)) vampire.BeartrapKilled(target);
                vampire.Notify(GetString("VampireTargetDead"));
            }
        }
    }

    public override void OnReportDeadBody()
    {
        try
        {
            foreach (byte targetId in BittenPlayers)
            {
                try
                {
                    PlayerControl target = Utils.GetPlayerById(targetId);
                    PlayerControl vampire = Utils.GetPlayerById(VampireId);
                    KillBitten(vampire, target, meeting: true);
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        BittenPlayers.Clear();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton.OverrideText(GetString(IsPoisoner ? "PoisonerKillButtonText" : "VampireBiteButtonText"));
    }
}
