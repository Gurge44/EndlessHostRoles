using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Crewmate;

internal class Transporter : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public static Dictionary<byte, byte> FirstSwapTarget = [];

    private static OptionItem AbilityCooldown;

    public override void SetupCustomOption()
    {
        StartSetup(6200)
            .AutoSetupOption(ref AbilityCooldown, 15, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .CreateOverrideTasksData();
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
        FirstSwapTarget = [];
    }

    public override void Remove(byte playerId)
    {
        FirstSwapTarget.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        try
        {
            opt.SetVision(false);
            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (shapeshifter == null || target == null || shapeshifter == target || !shapeshifter.IsAlive() || !target.IsAlive())
            return false;

        if (shapeshifter.GetAbilityUseLimit() < 1f)
            return false;

        if (FirstSwapTarget.TryGetValue(shapeshifter.PlayerId, out byte firstTargetId))
        {
            if (target.PlayerId == firstTargetId)
            {
                FirstSwapTarget[shapeshifter.PlayerId] = shapeshifter.PlayerId;
                return false;
            }
            
            PlayerControl firstTarget = firstTargetId.GetPlayer();

            if (firstTarget == null || !firstTarget.IsAlive())
            {
                FirstSwapTarget.Remove(shapeshifter.PlayerId);
                shapeshifter.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), Translator.GetString("ErrorTeleport")));
                return false;
            }

            Vector2 pos = firstTarget.Pos();
            firstTarget.TP(target);
            target.TP(pos);

            firstTarget.RPCPlayCustomSound("Teleport");
            target.RPCPlayCustomSound("Teleport");

            firstTarget.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Transporter), string.Format(Translator.GetString("TeleportedByTransporter"), target.GetRealName())));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Transporter), string.Format(Translator.GetString("TeleportedByTransporter"), firstTarget.GetRealName())));

            FirstSwapTarget.Remove(shapeshifter.PlayerId);
            shapeshifter.RpcRemoveAbilityUse();
        }
        else
            FirstSwapTarget[shapeshifter.PlayerId] = target.PlayerId;

        return false;
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (completedTaskCount + 1 >= totalTaskCount)
            pc.RpcChangeRoleBasis(CustomRoles.Glitch);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(Translator.GetString("BountyHunterChangeButtonText"));
    }
}