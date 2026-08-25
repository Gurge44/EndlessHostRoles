using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;

namespace EHR.Roles;

public class Transposer : RoleBase
{
    public static bool On;

    public static Dictionary<byte, byte> FirstSwapTarget = [];

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(659600)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 15f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        FirstSwapTarget = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        FirstSwapTarget.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool OnShapeshift(PlayerControl transposer, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (FirstSwapTarget.TryGetValue(transposer.PlayerId, out byte firstTargetId))
        {
            PlayerControl firstTarget = Utils.GetPlayerById(firstTargetId);

            if (firstTarget && firstTarget.IsAlive() && target.IsAlive())
            {
                transposer.RpcRemoveAbilityUse(notify: false);
                
                CustomRpcSender sender = CustomRpcSender.Create("Transposer", SendOption.Reliable);
                sender.StartMessage();
                sender.StartRpc(target.NetId, RpcCalls.Shapeshift)
                    .WriteNetObject(firstTarget)
                    .Write(false)
                    .EndRpc();
                sender.StartRpc(firstTarget.NetId, RpcCalls.Shapeshift)
                    .WriteNetObject(target)
                    .Write(false)
                    .EndRpc();
                sender.SendMessage();

                try
                {
                    Main.CheckShapeshift[firstTargetId] = true;
                    Main.CheckShapeshift[target.PlayerId] = true;
                    firstTarget.Shapeshift(target, false);
                    target.Shapeshift(firstTarget, false);
                }
                catch { }

                LateTask.New(() =>
                {
                    if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || ReportDeadBodyPatch.MeetingStarted) return;

                    if (transposer && transposer.IsAlive())
                        transposer.RpcResetAbilityCooldown();
                    
                    bool hasValue = false;
                    CustomRpcSender writer = CustomRpcSender.Create("Transposer Revert", SendOption.Reliable);
                    writer.StartMessage();

                    if (firstTarget && firstTarget.IsAlive() && firstTarget.IsShifted())
                    {
                        hasValue = true;
                        try { firstTarget.Shapeshift(firstTarget, false); } catch { }
                        writer.StartRpc(firstTarget.NetId, RpcCalls.Shapeshift)
                            .WriteNetObject(firstTarget)
                            .Write(false)
                            .EndRpc();
                    }

                    if (target && target.IsAlive() && target.IsShifted())
                    {
                        hasValue = true;
                        try { target.Shapeshift(target, false); } catch { }
                        writer.StartRpc(target.NetId, RpcCalls.Shapeshift)
                            .WriteNetObject(target)
                            .Write(false)
                            .EndRpc();
                    }
                    
                    writer.SendMessage(dispose: !hasValue);

                }, AbilityDuration.GetFloat(), "Transposer ability");
            }
            
            FirstSwapTarget.Remove(transposer.PlayerId);
        }
        else
        {
            if (transposer.GetAbilityUseLimit() < 1f) return false;
            FirstSwapTarget[transposer.PlayerId] = target.PlayerId;
        }

        return false;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(Translator.GetString("BountyHunterChangeButtonText"));
    }
}