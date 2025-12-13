using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

public class Swooper : RoleBase
{
    private const int Id = 4200;
    private static List<byte> PlayerIdList = [];

    private static OptionItem SwooperCooldown;
    private static OptionItem SwooperDuration;
    private static OptionItem SwooperVentNormallyOnCooldown;
    private static OptionItem SwooperCanVent;
    private static OptionItem SwooperLimitOpt;
    public static OptionItem SwooperAbilityUseGainWithEachKill;

    private int CD;
    private float Cooldown;
    private int Count;
    private float Duration;
    private long InvisTime;
    private bool CanVent;
    
    private long lastFixedTime;
    private long lastTime;
    
    private byte SwooperId;

    private CustomRoles UsedRole;
    private int ventedId;
    private bool VentNormallyOnCooldown;

    public override bool IsEnable => PlayerIdList.Count > 0;

    private bool CanGoInvis => GameStates.IsInTask && InvisTime == -10 && lastTime == -10;
    private bool IsInvis => InvisTime != -10;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swooper);

        SwooperCooldown = new FloatOptionItem(Id + 2, "SwooperCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Seconds);

        SwooperDuration = new FloatOptionItem(Id + 3, "SwooperDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Seconds);

        SwooperVentNormallyOnCooldown = new BooleanOptionItem(Id + 4, "SwooperVentNormallyOnCooldown", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper]);
        
        SwooperCanVent = new BooleanOptionItem(Id + 7, "CanVent", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper]);

        SwooperLimitOpt = new IntegerOptionItem(Id + 5, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Times);

        SwooperAbilityUseGainWithEachKill = new FloatOptionItem(Id + 6, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.7f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        SwooperId = playerId;

        InvisTime = -10;
        lastTime = -10;
        ventedId = -10;
        CD = 0;

        UsedRole = Main.PlayerStates[playerId].MainRole;

        switch (UsedRole)
        {
            case CustomRoles.Swooper:
                playerId.SetAbilityUseLimit(SwooperLimitOpt.GetFloat());
                Cooldown = SwooperCooldown.GetFloat();
                Duration = SwooperDuration.GetFloat();
                VentNormallyOnCooldown = SwooperVentNormallyOnCooldown.GetBool();
                CanVent = SwooperCanVent.GetBool();
                break;
            case CustomRoles.Chameleon:
                playerId.SetAbilityUseLimit(Chameleon.UseLimitOpt.GetFloat());
                Cooldown = Chameleon.ChameleonCooldown.GetFloat();
                Duration = Chameleon.ChameleonDuration.GetFloat();
                VentNormallyOnCooldown = true;
                CanVent = true;
                break;
            case CustomRoles.Wraith:
                Cooldown = Wraith.WraithCooldown.GetFloat();
                Duration = Wraith.WraithDuration.GetFloat();
                VentNormallyOnCooldown = Wraith.WraithVentNormallyOnCooldown.GetBool();
                CanVent = Wraith.WraithCanVent.GetBool();
                break;
        }
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        try
        {
            if (UsedRole == CustomRoles.Chameleon)
                AURoleOptions.EngineerCooldown = Cooldown + 2f;
            else if (UsePhantomBasis.GetBool() && (UsedRole == CustomRoles.Swooper || UsePhantomBasisForNKs.GetBool()))
            {
                AURoleOptions.PhantomCooldown = Cooldown + 2f;
                AURoleOptions.PhantomDuration = 0.1f;
            }
            
            if (UsedRole == CustomRoles.Wraith)
                opt.SetVision(Wraith.ImpostorVision.GetBool());
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public override void SetKillCooldown(byte id)
    {
        if (UsedRole != CustomRoles.Wraith) base.SetKillCooldown(id);
        else Main.AllPlayerKillCooldown[id] = Wraith.KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return UsedRole != CustomRoles.Chameleon && CanVent;
    }

    private void SendRPC()
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetSwooperTimer, SendOption.Reliable);
        writer.Write(SwooperId);
        writer.Write(InvisTime.ToString());
        writer.Write(lastTime.ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        InvisTime = long.Parse(reader.ReadString());
        lastTime = long.Parse(reader.ReadString());
    }

    public override void AfterMeetingTasks()
    {
        InvisTime = -10;
        lastTime = Utils.TimeStamp;
        SendRPC();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable || Main.HasJustStarted || ExileController.Instance || player == null) return;

        if (Count++ < 30) return;

        Count = 0;

        long now = Utils.TimeStamp;

        if (lastTime != -10)
        {
            if (!player.IsModdedClient() && UsedRole != CustomRoles.Chameleon)
            {
                long cooldown = lastTime + (long)Cooldown - now;

                if ((int)cooldown != CD && !(UsedRole != CustomRoles.Chameleon && UsePhantomBasis.GetBool() && (UsedRole != CustomRoles.Wraith || UsePhantomBasisForNKs.GetBool())))
                    player.Notify(string.Format(GetString("CDPT"), cooldown + 1), 3f, true);

                CD = (int)cooldown;
            }

            if (lastTime + (long)Cooldown < now)
            {
                lastTime = -10;

                if (!player.IsModdedClient() && !(UsedRole != CustomRoles.Chameleon && UsePhantomBasis.GetBool() && (UsedRole != CustomRoles.Wraith || UsePhantomBasisForNKs.GetBool())))
                    player.Notify(GetString("SwooperCanVent"), 10f);

                SendRPC();
                CD = 0;
            }
        }

        if (lastFixedTime != now && IsInvis)
        {
            lastFixedTime = now;
            long remainTime = InvisTime + (long)Duration - now;

            switch (remainTime)
            {
                case < 0:
                    lastTime = now;

                    if (UsedRole == CustomRoles.Chameleon)
                    {
                        int ventId = ventedId == -10 ? Main.LastEnteredVent[player.PlayerId].Id : ventedId;
                        Main.AllPlayerControls.Without(player).Do(x => player.MyPhysics.RpcExitVentDesync(ventId, x));
                    }
                    else
                        player.RpcMakeVisible();

                    player.Notify(GetString("SwooperInvisStateOut"));
                    player.RpcResetAbilityCooldown();
                    InvisTime = -10;
                    SendRPC();
                    break;
                case <= 10 when !player.IsModdedClient():
                    player.Notify(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1), overrideAll: true);
                    break;
            }
        }
    }

    bool OnCoEnterVent(PlayerPhysics __instance, int ventId)
    {
        if (!AmongUsClient.Instance.AmHost || IsInvis || (UsedRole != CustomRoles.Chameleon && UsePhantomBasis.GetBool() && (UsedRole != CustomRoles.Wraith || UsePhantomBasisForNKs.GetBool()))) return false;

        PlayerControl pc = __instance.myPlayer;

        float limit = pc.GetAbilityUseLimit();
        bool wraith = UsedRole == CustomRoles.Wraith;

        if (CanGoInvis && (wraith || limit >= 1))
        {
            __instance.RpcExitVentDesync(ventId, pc);

            ventedId = ventId;
            InvisTime = Utils.TimeStamp;
            if (!wraith) pc.RpcRemoveAbilityUse();

            SendRPC();
            pc.Notify(GetString("SwooperInvisState"), Duration);
            return true;
        }

        if (!VentNormallyOnCooldown)
        {
            __instance.RpcExitVent(ventId);
            pc.Notify(GetString("SwooperInvisInCooldown"));
            return true;
        }

        return false;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (OnCoEnterVent(pc.MyPhysics, vent.Id)) return;

        if (!IsInvis || InvisTime == Utils.TimeStamp) return;

        InvisTime = -10;
        lastTime = Utils.TimeStamp;
        SendRPC();

        pc.Notify(GetString("SwooperInvisStateOut"), 10f);
        pc.RpcResetAbilityCooldown();
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (IsInvis) return false;

        float limit = pc.GetAbilityUseLimit();
        bool wraith = UsedRole == CustomRoles.Wraith;

        if (CanGoInvis && (wraith || limit >= 1))
        {
            pc.RpcMakeInvisible();
            
            InvisTime = Utils.TimeStamp;
            if (!wraith) pc.RpcRemoveAbilityUse();

            SendRPC();
            pc.Notify(GetString("SwooperInvisState"), Duration);
        }

        return false;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || seer == null || seer.PlayerId != SwooperId || !GameStates.IsInTask || ExileController.Instance || !seer.IsAlive()) return string.Empty;

        var str = new StringBuilder();

        if (IsInvis)
        {
            long remainTime = InvisTime + (long)Duration - Utils.TimeStamp;
            str.Append(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1));
        }
        else if (!(UsedRole != CustomRoles.Chameleon && UsePhantomBasis.GetBool() && (UsedRole != CustomRoles.Wraith || UsePhantomBasisForNKs.GetBool())))
        {
            if (lastTime != -10)
            {
                long cooldown = lastTime + (long)Cooldown - Utils.TimeStamp;
                str.Append(string.Format(GetString("SwooperInvisCooldownRemain"), cooldown + 1));
            }
            else
                str.Append(GetString("SwooperCanVent"));
        }

        return str.ToString();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (target.Is(CustomRoles.Bait)) return true;
        if (!IsInvis) return true;
        if (!killer.RpcCheckAndMurder(target, true)) return false;

        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        target.Suicide(PlayerState.DeathReason.Swooped, killer);
        killer.SetKillCooldown();
        return false;
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || UsedRole != CustomRoles.Chameleon || pc.GetClosestVent()?.Id == ventId;
    }
}
