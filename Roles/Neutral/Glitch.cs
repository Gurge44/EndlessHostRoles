using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;

namespace EHR.Neutral;

public class Glitch : RoleBase
{
    private const int Id = 18125;
    public static List<byte> PlayerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem HackCooldown;
    public static OptionItem HackDuration;
    public static OptionItem MimicCooldown;
    public static OptionItem MimicDuration;
    public static OptionItem CanVent;
    public static OptionItem CanVote;
    private static OptionItem HasImpostorVision;

    private byte GlitchId;

    public int HackCDTimer;
    private bool HasMimiced;
    private bool IsShifted;
    public int KCDTimer;
    public long LastHack;
    public long LastKill;
    public long LastMimic;
    private long LastUpdate;
    public int MimicCDTimer;
    public int MimicDurTimer;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Glitch);

        KillCooldown = new IntegerOptionItem(Id + 10, "KillCooldown", new(0, 180, 1), 25, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        HackCooldown = new IntegerOptionItem(Id + 11, "HackCooldown", new(0, 180, 1), 20, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        HackDuration = new FloatOptionItem(Id + 14, "HackDuration", new(0f, 60f, 1f), 15f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        MimicCooldown = new IntegerOptionItem(Id + 15, "MimicCooldown", new(0, 180, 1), 30, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        MimicDuration = new FloatOptionItem(Id + 16, "MimicDuration", new(0f, 60f, 1f), 10f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 12, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
        CanVote = new BooleanOptionItem(Id + 17, "CanVote", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        GlitchId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        GlitchId = playerId;

        HackCDTimer = 10;
        KCDTimer = 10;
        MimicCDTimer = 10;
        MimicDurTimer = 0;

        IsShifted = false;

        long ts = Utils.TimeStamp;

        LastKill = ts;
        LastHack = ts;
        LastMimic = ts;

        LastUpdate = ts;

        HasMimiced = false;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.SabotageButton?.ToggleVisible(!Main.PlayerStates[id].IsDead);
        hud.SabotageButton?.OverrideText(Translator.GetString("HackButtonText"));
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    private void SendRPCSyncTimers()
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGlitchTimers, SendOption.Reliable);
        writer.Write(GlitchId);
        writer.Write(MimicCDTimer);
        writer.Write(MimicDurTimer);
        writer.Write(HackCDTimer);
        writer.Write(KCDTimer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCSyncTimers(MessageReader reader)
    {
        byte id = reader.ReadByte();
        if (Main.PlayerStates[id].Role is not Glitch gc) return;

        gc.MimicCDTimer = reader.ReadInt32();
        gc.MimicDurTimer = reader.ReadInt32();
        gc.HackCDTimer = reader.ReadInt32();
        gc.KCDTimer = reader.ReadInt32();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = 1f;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    private void Mimic(PlayerControl pc)
    {
        if (pc == null || !pc.Is(CustomRoles.Glitch) || !pc.IsAlive() || MimicCDTimer > 0 || IsShifted) return;

        PlayerControl[] playerlist = Main.AllAlivePlayerControls.Where(a => a.PlayerId != pc.PlayerId).ToArray();

        try
        {
            pc.RpcShapeshift(playerlist.RandomElement(), false);

            IsShifted = true;
            HasMimiced = true;
            LastMimic = Utils.TimeStamp;
            MimicCDTimer = MimicCooldown.GetInt();
            MimicDurTimer = MimicDuration.GetInt();
            SendRPCSyncTimers();
        }
        catch (Exception ex) { Logger.Error(ex.ToString(), "Glitch.Mimic.RpcShapeshift"); }
    }

    public override void OnPet(PlayerControl pc)
    {
        Mimic(pc);
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        Mimic(pc);
        return false;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || (KCDTimer > 0 && HackCDTimer > 0)) return false;

        if (killer.CheckDoubleTrigger(target, () =>
        {
            if (HackCDTimer <= 0)
            {
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                HackCDTimer = HackCooldown.GetInt();
                target.BlockRole(HackDuration.GetFloat());
                LastHack = Utils.TimeStamp;
                SendRPCSyncTimers();
            }
        }))
        {
            if (KCDTimer > 0) return false;

            LastKill = Utils.TimeStamp;
            KCDTimer = KillCooldown.GetInt();
            SendRPCSyncTimers();
            return true;
        }

        return false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        long now = Utils.TimeStamp;
        if (LastUpdate == now) return;
        LastUpdate = now;

        if (HackCDTimer is > 180 or < 0) HackCDTimer = 0;
        if (KCDTimer is > 180 or < 0) KCDTimer = 0;
        if (MimicCDTimer is > 180 or < 0) MimicCDTimer = 0;
        if (MimicDurTimer is > 180 or < 0) MimicDurTimer = 0;

        if (player == null) return;

        if (!player.IsAlive())
        {
            HackCDTimer = 0;
            KCDTimer = 0;
            MimicCDTimer = 0;
            MimicDurTimer = 0;
            return;
        }

        if (MimicDurTimer > 0)
        {
            try { MimicDurTimer = (int)(MimicDuration.GetInt() - (now - LastMimic)); }
            catch { MimicDurTimer = 0; }

            if (MimicDurTimer > 180) MimicDurTimer = 0;
        }

        if ((MimicDurTimer <= 0 || !GameStates.IsInTask) && IsShifted)
        {
            try
            {
                player.RpcShapeshift(player, false);
                IsShifted = false;
            }
            catch (Exception ex) { Logger.Error(ex.ToString(), "Glitch.Mimic.RpcRevertShapeshift"); }

            if (!GameStates.IsInTask) MimicDurTimer = 0;
        }

        if (HackCDTimer <= 0 && KCDTimer <= 0 && MimicCDTimer <= 0 && MimicDurTimer <= 0) return;

        try { HackCDTimer = (int)(HackCooldown.GetInt() - (now - LastHack)); }
        catch { HackCDTimer = 0; }

        if (HackCDTimer is > 180 or < 0) HackCDTimer = 0;

        try { KCDTimer = (int)(KillCooldown.GetInt() - (now - LastKill)); }
        catch { KCDTimer = 0; }

        if (KCDTimer is > 180 or < 0) KCDTimer = 0;

        try { MimicCDTimer = (int)(MimicCooldown.GetInt() + (HasMimiced ? MimicDuration.GetInt() : 0) - (now - LastMimic)); }
        catch { MimicCDTimer = 0; }

        if (MimicCDTimer is > 180 or < 0) MimicCDTimer = 0;

        if (player.IsNonHostModClient())
            SendRPCSyncTimers();

        if (!player.IsModClient()) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer == null || seer.PlayerId != GlitchId || seer.PlayerId != target.PlayerId || !seer.IsAlive() || (seer.IsModClient() && !hud) || meeting) return string.Empty;

        var sb = new StringBuilder();

        if (!hud) sb.Append("<size=70%>");

        if (MimicDurTimer > 0) sb.Append($"{string.Format(Translator.GetString("MimicDur"), MimicDurTimer)}\n");
        if (MimicCDTimer > 0 && MimicDurTimer <= 0) sb.Append($"{string.Format(Translator.GetString("MimicCD"), MimicCDTimer)}\n");
        if (HackCDTimer > 0) sb.Append($"{string.Format(Translator.GetString("HackCD"), HackCDTimer)}\n");
        if (KCDTimer > 0) sb.Append($"{string.Format(Translator.GetString("KCD"), KCDTimer)}\n");

        if (!hud) sb.Append("</size>");

        return sb.ToString();
    }

    public override void AfterMeetingTasks()
    {
        if (Main.PlayerStates[GlitchId].IsDead) return;

        long timestamp = Utils.TimeStamp;
        LastKill = timestamp;
        LastHack = timestamp;
        LastMimic = timestamp;
        KCDTimer = 10;
        HackCDTimer = 10;
        MimicCDTimer = 10;
        SendRPCSyncTimers();
    }

    public override void OnReportDeadBody()
    {
        HasMimiced = false;
    }
}