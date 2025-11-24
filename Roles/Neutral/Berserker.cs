using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral;

public class Berserker : RoleBase
{
    public static bool On;
    private static List<Berserker> Instances = [];

    public override bool IsEnable => On;
    
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;
    private static OptionItem LowerKillCooldown;

    private byte BerserkerId;
    public int Form;

    public override void SetupCustomOption()
    {
        StartSetup(657100)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true)
            .AutoSetupOption(ref LowerKillCooldown, 10f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Form = 0;
        BerserkerId = playerId;
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        if (Form < 2) base.SetKillCooldown(id);
        else Main.AllPlayerKillCooldown[id] = LowerKillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        switch (Form)
        {
            case 1:
                opt.SetInt(Int32OptionNames.KillDistance, 2);
                break;
            case 3:
            case 4:
                opt.SetVision(true);
                goto case 1;
        }
    }

    public static void OnAnyoneMurder(PlayerControl killer)
    {
        foreach (Berserker instance in Instances)
        {
            if (instance.BerserkerId == killer.PlayerId) instance.Form--;
            else instance.Form++;
            
            if (instance.Form < 0) instance.Form = 0;
            Utils.SendRPC(CustomRPC.SyncRoleData, instance.BerserkerId, instance.Form);
            PlayerControl berserker = instance.BerserkerId.GetPlayer();

            if (instance.Form >= 5) berserker?.Suicide();
            else
            {
                Utils.NotifyRoles(SpecifySeer: berserker, SpecifyTarget: berserker);
                PlayerGameOptionsSender.SetDirty(instance.BerserkerId);
            }
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return Form < 4 && base.OnCheckMurderAsTarget(killer, target);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Form < 4) return base.OnCheckMurder(killer, target);
        killer.Kill(target);
        return false;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return base.GetProgressText(playerId, comms) + Utils.ColorString(Color.Lerp(Color.white, Color.red, Mathf.Clamp01(Form / 5f)), $" {Form}/5");
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Form = reader.ReadPackedInt32();
    }
}