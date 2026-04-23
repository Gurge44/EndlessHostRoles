using AmongUs.GameOptions;
using System.Collections.Generic;
using EHR.Modules.Extensions;

namespace EHR.Roles;

public class Shadow : CovenBase
{
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem SelfCamoCooldown;
    private static OptionItem SelfCamoDuration;
    private static OptionItem CanCamoOthersWithKillButton;
    private static OptionItem OthersCooldown;
    private static OptionItem OthersCamoDuration;
    private static OptionItem InvisDuration;
    private static OptionItem SwitchCamoAbilityToInvisWithNecronomicon;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;
    
    public override bool IsEnable => On;

    private byte ShadowId;
    private NetworkedPlayerInfo.PlayerOutfit OriginalOutfit;
    private CountdownTimer SelfTimer;
    private byte TargetId;
    private NetworkedPlayerInfo.PlayerOutfit OriginalTargetOutfit;
    private CountdownTimer OthersCamoTimer;

    public override void SetupCustomOption()
    {
        StartSetup(658700)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref SelfCamoCooldown, 15f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref SelfCamoDuration, 7f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanCamoOthersWithKillButton, true)
            .AutoSetupOption(ref OthersCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanCamoOthersWithKillButton)
            .AutoSetupOption(ref OthersCamoDuration, 7f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanCamoOthersWithKillButton)
            .AutoSetupOption(ref InvisDuration, 5f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref SwitchCamoAbilityToInvisWithNecronomicon, true)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        ShadowId = playerId;
        OriginalOutfit = null;
        SelfTimer = null;
        TargetId = byte.MaxValue;
        OriginalTargetOutfit = null;
        OthersCamoTimer = null;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = SelfCamoCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return HasNecronomicon || CanCamoOthersWithKillButton.GetBool();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = HasNecronomicon switch
        {
            false when !CanCamoOthersWithKillButton.GetBool() => 300f,
            false => OthersCooldown.GetFloat(),
            _ => KillCooldown.GetFloat()
        };
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (SelfTimer != null) return false;
        
        if (HasNecronomicon && SwitchCamoAbilityToInvisWithNecronomicon.GetBool())
        {
            MakeSelfInvisible(pc);
            return false;
        }
        
        if (Camouflage.IsCamouflage) return false;

        OriginalOutfit = Camouflage.PlayerSkins[pc.PlayerId];
        Utils.RpcChangeSkin(pc, new NetworkedPlayerInfo.PlayerOutfit().Set(Main.AllPlayerNames.GetValueOrDefault(pc.PlayerId, pc.GetRealName()), 15, "", "", "", "", ""));
        Utils.NotifyRoles(SpecifyTarget: pc);

        SelfTimer = new CountdownTimer(SelfCamoDuration.GetFloat(), () =>
        {
            SelfTimer = null;
            if (Camouflage.IsCamouflage || !pc) return;
            Utils.RpcChangeSkin(pc, OriginalOutfit);
            Utils.NotifyRoles(SpecifyTarget: pc);
            pc.RpcResetAbilityCooldown();
        }, cancelOnMeeting: false, onCanceled: () => SelfTimer = null);

        return false;
    }

    private void MakeSelfInvisible(PlayerControl pc)
    {
        pc.RpcMakeInvisible();
        SelfTimer = new CountdownTimer(InvisDuration.GetFloat(), () =>
        {
            SelfTimer = null;
            if (!pc) return;
            pc.RpcMakeVisible();
            pc.RpcResetAbilityCooldown();
        }, onCanceled: () => SelfTimer = null);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!HasNecronomicon)
        {
            UseAbility();
            return false;
        }

        if (!CanCamoOthersWithKillButton.GetBool() || killer.CheckDoubleTrigger(target, UseAbility))
        {
            if (SelfTimer == null) MakeSelfInvisible(killer);
            return true;
        }

        return false;
        
        void UseAbility()
        {
            if (OthersCamoTimer != null || !target.Is(Team.Coven)) return;

            if (HasNecronomicon && SwitchCamoAbilityToInvisWithNecronomicon.GetBool())
            {
                target.RpcMakeInvisible();
                OthersCamoTimer = new CountdownTimer(InvisDuration.GetFloat(), () =>
                {
                    OthersCamoTimer = null;
                    if (!target) return;
                    target.RpcMakeVisible();
                }, onCanceled: () => OthersCamoTimer = null);
                return;
            }
            
            if (Camouflage.IsCamouflage) return;
            
            killer.SetKillCooldown(OthersCooldown.GetFloat());

            TargetId = target.PlayerId;
            OriginalTargetOutfit = Camouflage.PlayerSkins[target.PlayerId];
            Utils.RpcChangeSkin(target, new NetworkedPlayerInfo.PlayerOutfit().Set(Main.AllPlayerNames.GetValueOrDefault(target.PlayerId, target.GetRealName()), 15, "", "", "", "", ""));
            Utils.NotifyRoles(SpecifyTarget: target);

            OthersCamoTimer = new CountdownTimer(OthersCamoDuration.GetFloat(), () =>
            {
                OthersCamoTimer = null;
                if (Camouflage.IsCamouflage || !target) return;
                Utils.RpcChangeSkin(target, OriginalTargetOutfit);
                Utils.NotifyRoles(SpecifyTarget: target);
            }, cancelOnMeeting: false, onCanceled: () => OthersCamoTimer = null);
        }
    }

    public override void OnReportDeadBody()
    {
        if (HasNecronomicon && SwitchCamoAbilityToInvisWithNecronomicon.GetBool()) return;
        
        if (SelfTimer != null)
        {
            SelfTimer.Dispose();
            SelfTimer = null;
            PlayerControl pc = ShadowId.GetPlayer();
            if (Camouflage.IsCamouflage || !pc) return;
            Utils.RpcChangeSkin(pc, OriginalOutfit);
        }
        
        if (OthersCamoTimer != null)
        {
            OthersCamoTimer.Dispose();
            OthersCamoTimer = null;
            PlayerControl pc = TargetId.GetPlayer();
            if (Camouflage.IsCamouflage || !pc) return;
            Utils.RpcChangeSkin(pc, OriginalOutfit);
        }
    }
}