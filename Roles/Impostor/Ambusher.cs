using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Impostor;

public class Ambusher : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem KillCooldown;
    private static OptionItem FollowDuration;
    private static OptionItem FollowRadius;
    private static OptionItem InvisDurAfterSuccessfulAmbush;
    private static OptionItem FragileDuration;

    private float AbilityEndTimer;
    private byte TargetId;
    private float TargetTimer;
    private bool DontCheck;
    private int Count;
    private long LastRPCTS;
    private byte AmbusherId;

    public static Dictionary<byte, long> FragilePlayers = [];

    public override void SetupCustomOption()
    {
        StartSetup(655200)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 20f, new FloatValueRule(0.5f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref FollowDuration, 5f, new FloatValueRule(0.5f, 30f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref FollowRadius, 1.5f, new FloatValueRule(0.1f, 10f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref InvisDurAfterSuccessfulAmbush, 5f, new FloatValueRule(0.5f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref FragileDuration, 40, new IntegerValueRule(1, 300, 1), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        FragilePlayers = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        AbilityEndTimer = AbilityDuration.GetFloat();
        TargetId = byte.MaxValue;
        TargetTimer = FollowDuration.GetFloat();
        DontCheck = false;
        Count = 0;
        LastRPCTS = 0;
        AmbusherId = playerId;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool())
        {
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.PhantomDuration = 1f;
        }
        else
        {
            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return !Main.Invisible.Contains(pc.PlayerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        killer.RpcResetAbilityCooldown();
    }

    public override bool OnVanish(PlayerControl pc)
    {
        pc.RpcMakeInvisible(true);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        shapeshifter.RpcMakeInvisible(true);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        pc.RpcMakeInvisible(true);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || !Main.IntroDestroyed || ExileController.Instance || AntiBlackout.SkipTasks || !Main.Invisible.Contains(pc.PlayerId)) return;

        long now = Utils.TimeStamp;

        if (!DontCheck)
        {
            Vector2 pos = pc.Pos();
            float radius = FollowRadius.GetFloat();
            (PlayerControl pc, float distance)[] nearPlayers = Main.AllAlivePlayerControls.Without(pc).Select(x => (pc: x, distance: Vector2.Distance(x.Pos(), pos))).Where(x => x.distance <= radius).ToArray();
            PlayerControl closestPlayer = nearPlayers.Length == 0 ? null : nearPlayers.MinBy(x => x.distance).pc;

            if (closestPlayer == null)
            {
                TargetId = byte.MaxValue;
                TargetTimer = FollowDuration.GetFloat();
            }
            else if (closestPlayer.PlayerId != TargetId)
            {
                TargetId = closestPlayer.PlayerId;
                TargetTimer = FollowDuration.GetFloat();
            }
            else
            {
                TargetTimer -= Time.fixedDeltaTime;

                if (TargetTimer <= 0)
                {
                    DontCheck = true;
                    AbilityEndTimer = InvisDurAfterSuccessfulAmbush.GetFloat();
                    FragilePlayers[TargetId] = now + FragileDuration.GetInt();
                    Utils.SendRPC(CustomRPC.SyncRoleData, AmbusherId, 3, TargetId);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: TargetId.GetPlayer());
                    
                    if (Main.PlayerStates.TryGetValue(TargetId, out var s) && s.Player != null && s.Player.AmOwner && s.SubRoles.Contains(CustomRoles.Fragile))
                        Achievements.Type.Collapse.CompleteAfterGameEnd();
                    
                    TargetId = byte.MaxValue;
                    TargetTimer = FollowDuration.GetFloat();
                }
            }
        }
        
        AbilityEndTimer -= Time.fixedDeltaTime;

        if (AbilityEndTimer <= 0)
        {
            pc.RpcMakeVisible(true);
            pc.SetKillCooldown(KillCooldown.GetFloat());
            AbilityEndTimer = AbilityDuration.GetFloat();
            LastRPCTS = 0;
            Count = 30;
            DontCheck = false;
        }

        if (LastRPCTS != now)
        {
            pc.RpcResetAbilityCooldown();
            Utils.SendRPC(CustomRPC.SyncRoleData, AmbusherId, 1, TargetId, TargetTimer, AbilityEndTimer, DontCheck);
            LastRPCTS = now;
        }

        if (Count++ < (DontCheck ? 30 : 10)) return;
        Count = 0;
        
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        if (pc.AmOwner) Utils.DirtyName.Add(pc.PlayerId);
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (!lowLoad && FragilePlayers.TryGetValue(pc.PlayerId, out long endTS) && Utils.TimeStamp >= endTS)
        {
            FragilePlayers.Remove(pc.PlayerId);
            Utils.SendRPC(CustomRPC.SyncRoleData, AmbusherId, 2, pc.PlayerId);
            Utils.NotifyRoles(SpecifySeer: AmbusherId.GetPlayer(), SpecifyTarget: pc);
        }
    }

    public override void OnReportDeadBody()
    {
        TargetId = byte.MaxValue;
        TargetTimer = FollowDuration.GetFloat();
        AbilityEndTimer = AbilityDuration.GetFloat();
        DontCheck = false;
        Utils.SendRPC(CustomRPC.SyncRoleData, AmbusherId, 1, TargetId, TargetTimer, AbilityEndTimer, DontCheck);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        TargetId = reader.ReadByte();
        TargetTimer = reader.ReadSingle();
        AbilityEndTimer = reader.ReadSingle();
        DontCheck = reader.ReadBoolean();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != AmbusherId || seer.PlayerId != target.PlayerId || hud || meeting || !Main.Invisible.Contains(AmbusherId)) return string.Empty;
        string str = DontCheck ? Translator.GetString("Ambusher.Success") : TargetId != byte.MaxValue ? string.Format(Translator.GetString("Ambusher.InProgress"), TargetId.ColoredPlayerName(), Math.Round(TargetTimer, 1)) : string.Empty;
        return $"{str}\n<size=80%>{string.Format(Translator.GetString("Ambusher.InvisTimeLeft"), (int)Math.Round(AbilityEndTimer))}</size>";
    }
}