using System.Collections.Generic;
using EHR.Modules;
using Hazel;
using static EHR.Options;

namespace EHR.Impostor;

public class Chronomancer : RoleBase
{
    private const int Id = 642100;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KCD;
    private static OptionItem ChargeInterval;
    private static OptionItem ChargeLossInterval;

    private int RampageKills;
    private int ChargePercent;
    private byte ChronomancerId;
    private bool IsRampaging;
    private long LastUpdate;
    
    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Chronomancer);

        KCD = new FloatOptionItem(Id + 11, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
            .SetValueFormat(OptionFormat.Seconds);

        ChargeInterval = new IntegerOptionItem(Id + 12, "ChargeInterval", new(1, 20, 1), 5, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
            .SetValueFormat(OptionFormat.Percent);

        ChargeLossInterval = new IntegerOptionItem(Id + 13, "ChargeLossInterval", new(1, 50, 1), 25, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
            .SetValueFormat(OptionFormat.Percent);
    }

    private void SendRPC()
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncChronomancer, SendOption.Reliable);
        writer.Write(ChronomancerId);
        writer.Write(IsRampaging);
        writer.Write(ChargePercent);
        writer.Write(LastUpdate.ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(bool isRampaging, int chargePercent, long lastUpdate)
    {
        IsRampaging = isRampaging;
        ChargePercent = chargePercent;
        LastUpdate = lastUpdate;
    }

    public override void Init()
    {
        PlayerIdList = [];
        IsRampaging = false;
        ChargePercent = 0;
        LastUpdate = Utils.TimeStamp + 30;
        ChronomancerId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        RampageKills = 0;
        IsRampaging = false;
        ChargePercent = 0;
        LastUpdate = Utils.TimeStamp + 10;
        ChronomancerId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = IsRampaging ? 0.01f : KCD.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (ChargePercent <= 0) return base.OnCheckMurder(killer, target);

        if (!IsRampaging)
        {
            RampageKills = 0;
            IsRampaging = true;
            SendRPC();
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.ImpTransform);
            killer.ResetKillCooldown();
            killer.SyncSettings();
        }

        return base.OnCheckMurder(killer, target);
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (IsRampaging) RampageKills++;
        
        if (killer.AmOwner && RampageKills >= 4)
            Achievements.Type.Massacre.Complete();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc == null) return;

        if (!pc.Is(CustomRoles.Chronomancer)) return;

        if (!GameStates.IsInTask) return;

        if (LastUpdate >= Utils.TimeStamp) return;

        LastUpdate = Utils.TimeStamp;

        var notify = false;
        int beforeCharge = ChargePercent;

        if (IsRampaging)
        {
            ChargePercent -= ChargeLossInterval.GetInt();

            if (ChargePercent <= 0)
            {
                ChargePercent = 0;
                IsRampaging = false;
                RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
                pc.ResetKillCooldown();
                pc.SyncSettings();
                pc.SetKillCooldown();
            }

            notify = true;
        }
        else if (Main.KillTimers[pc.PlayerId] <= 0 && !MeetingStates.FirstMeeting)
        {
            ChargePercent += ChargeInterval.GetInt();
            if (ChargePercent > 100) ChargePercent = 100;

            notify = true;
        }

        if (notify && !pc.IsModdedClient()) pc.Notify(string.Format(Translator.GetString("ChronomancerPercent"), ChargePercent), 300f);

        if (beforeCharge != ChargePercent && pc.IsModdedClient() && !pc.IsHost()) SendRPC();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || seer.PlayerId != ChronomancerId) return string.Empty;

        return ChargePercent > 0 ? string.Format(Translator.GetString("ChronomancerPercent"), ChargePercent) : string.Empty;
    }

    public override void OnReportDeadBody()
    {
        LastUpdate = Utils.TimeStamp;
        ChargePercent = 0;
        IsRampaging = false;
        SendRPC();
    }

    public override void AfterMeetingTasks()
    {
        OnReportDeadBody();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.ToggleVisible(false);
    }
}
