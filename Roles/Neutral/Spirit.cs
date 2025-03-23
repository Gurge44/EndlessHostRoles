using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Spirit : RoleBase
{
    public static bool On;
    private static List<Spirit> Instances = [];

    private static OptionItem ShapeshiftCooldown;

    private byte SpiritID;
    public (byte, byte) Targets;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645600)
            .AutoSetupOption(ref ShapeshiftCooldown, 15f, new FloatValueRule(0.5f, 60f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        SpiritID = playerId;
        playerId.SetAbilityUseLimit(2);
        Targets = (byte.MaxValue, byte.MaxValue);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        bool firstIsSet = Targets.Item1 != byte.MaxValue;
        bool secondIsSet = Targets.Item2 != byte.MaxValue;

        float cd;

        if (firstIsSet ^ secondIsSet) cd = 1f;
        else if (firstIsSet) cd = 300f;
        else cd = ShapeshiftCooldown.GetFloat();

        AURoleOptions.ShapeshifterCooldown = cd;
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        bool firstIsSet = Targets.Item1 != byte.MaxValue;
        bool secondIsSet = Targets.Item2 != byte.MaxValue;

        switch (firstIsSet)
        {
            case true when secondIsSet:
                return false;
            case true:
                Targets.Item2 = target.PlayerId;
                break;
            case false:
                Targets.Item1 = target.PlayerId;
                break;
        }

        shapeshifter.RpcRemoveAbilityUse();
        shapeshifter.SyncSettings();
        target.Notify(string.Format(Translator.GetString("SpiritTarget"), CustomRoles.Spirit.ToColoredString()));
        Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: target);
        Utils.SendRPC(CustomRPC.SyncRoleData, SpiritID, Targets.Item1, Targets.Item2);
        return false;
    }

    public override void AfterMeetingTasks()
    {
        Targets = (byte.MaxValue, byte.MaxValue);
        SpiritID.SetAbilityUseLimit(2);
        Utils.SendRPC(CustomRPC.SyncRoleData, SpiritID, Targets.Item1, Targets.Item2);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Targets = (reader.ReadByte(), reader.ReadByte());
    }

    public static bool TryGetSwapTarget(PlayerControl originalTarget, out PlayerControl newTarget)
    {
        newTarget = null;

        if (!Instances.FindFirst(x => x.Targets.Item1 == originalTarget.PlayerId || x.Targets.Item2 == originalTarget.PlayerId, out var spirit))
            return false;

        if (spirit.Targets.Item1 == originalTarget.PlayerId)
            newTarget = spirit.Targets.Item2.GetPlayer();
        else if (spirit.Targets.Item2 == originalTarget.PlayerId)
            newTarget = spirit.Targets.Item1.GetPlayer();

        return newTarget != null;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        byte[] tuple = [Targets.Item1, Targets.Item2];
        if (tuple.All(x => x == byte.MaxValue)) return;

        var sewed = tuple.ToValidPlayers().ToList();

        if (sewed.Count != 2 || sewed.Exists(x => !x.IsAlive()))
        {
            Targets = (byte.MaxValue, byte.MaxValue);
            Utils.SendRPC(CustomRPC.SyncRoleData, SpiritID, Targets.Item1, Targets.Item2);
            pc.SetAbilityUseLimit(2);
            pc.RpcResetAbilityCooldown();

            if (sewed.Count > 0 && sewed.FindFirst(x => x.IsAlive(), out var alive))
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: alive);
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != SpiritID || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;

        bool firstIsSet = Targets.Item1 != byte.MaxValue;
        bool secondIsSet = Targets.Item2 != byte.MaxValue;

        if (firstIsSet && secondIsSet) return string.Format(Translator.GetString("SpiritTargetBoth"), Targets.Item1.ColoredPlayerName(), Targets.Item2.ColoredPlayerName());
        if (firstIsSet ^ secondIsSet) return string.Format(Translator.GetString("SpiritTargetOne"), (firstIsSet ? Targets.Item1 : Targets.Item2).ColoredPlayerName());
        return Translator.GetString("SpiritTargetNone");
    }
}