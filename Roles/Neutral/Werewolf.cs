using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Roles.Crewmate;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Neutral;

public class Werewolf : RoleBase
{
    private const int Id = 12850;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem HasImpostorVision;
    public static OptionItem RampageCD;
    public static OptionItem RampageDur;
    private static int CD;

    private static long lastFixedTime;
    public long lastTime;

    private long RampageTime;
    private byte WWId;

    public override bool IsEnable => playerIdList.Count > 0;

    bool CanRampage => GameStates.IsInTask && RampageTime == -10 && lastTime == -10;
    bool IsRampaging => RampageTime != -10;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Werewolf);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 3f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);
        HasImpostorVision = BooleanOptionItem.Create(Id + 11, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf]);
        RampageCD = FloatOptionItem.Create(Id + 12, "WWRampageCD", new(0f, 180f, 0.5f), 35f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);
        RampageDur = FloatOptionItem.Create(Id + 13, "WWRampageDur", new(0f, 180f, 1f), 12f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        playerIdList = [];
        RampageTime = -10;
        lastTime = -10;
        CD = 0;
        WWId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        WWId = playerId;

        RampageTime = -10;
        lastTime = -10;
        CD = 0;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanRampage || IsRampaging || pc.inVent;
    public override bool CanUseKillButton(PlayerControl pc) => IsRampaging;

    void SendRPC()
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetWWTimer, SendOption.Reliable);
        writer.Write(WWId);
        writer.Write(RampageTime.ToString());
        writer.Write(lastTime.ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        RampageTime = long.Parse(reader.ReadString());
        lastTime = long.Parse(reader.ReadString());
    }

    public override void AfterMeetingTasks()
    {
        RampageTime = -10;
        lastTime = Utils.TimeStamp;
        SendRPC();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable || player == null) return;

        var now = Utils.TimeStamp;

        if (lastTime != -10)
        {
            if (!player.IsModClient())
            {
                var cooldown = lastTime + (long)RampageCD.GetFloat() - now;
                if ((int)cooldown != CD) player.Notify(string.Format(GetString("CDPT"), cooldown + 1), 1.1f);
                CD = (int)cooldown;
            }

            if (lastTime + (long)RampageCD.GetFloat() < now)
            {
                lastTime = -10;
                if (!player.IsModClient()) player.Notify(GetString("WWCanRampage"));
                SendRPC();
                CD = 0;
            }
        }

        if (lastFixedTime != now && RampageTime != -10)
        {
            lastFixedTime = now;
            bool refresh = false;
            var remainTime = RampageTime + (long)RampageDur.GetFloat() - now;
            switch (remainTime)
            {
                case < 0:
                    lastTime = now;
                    player.Notify(GetString("WWRampageOut"));
                    RampageTime = -10;
                    SendRPC();
                    refresh = true;
                    break;
                case <= 10 when !player.IsModClient():
                    player.Notify(string.Format(GetString("WWRampageCountdown"), remainTime + 1));
                    break;
            }

            if (refresh) SendRPC();
        }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (pc == null) return;

        if (!AmongUsClient.Instance.AmHost || IsRampaging) return;
        _ = new LateTask(() =>
        {
            if (CanRampage)
            {
                RampageTime = Utils.TimeStamp;
                SendRPC();
                pc.Notify(GetString("WWRampaging"), RampageDur.GetFloat());
            }
        }, 0.5f, "Werewolf Vent");
    }

    public static string GetHudText(PlayerControl pc)
    {
        if (pc == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive() || Main.PlayerStates[pc.PlayerId].Role is not Werewolf { IsEnable: true } ww) return string.Empty;
        var str = new StringBuilder();
        if (ww.IsRampaging)
        {
            var remainTime = ww.RampageTime + (long)RampageDur.GetFloat() - Utils.TimeStamp;
            str.Append(string.Format(GetString("WWRampageCountdown"), remainTime + 1));
        }
        else if (ww.lastTime != -10)
        {
            var cooldown = ww.lastTime + (long)RampageCD.GetFloat() - Utils.TimeStamp;
            str.Append(string.Format(GetString("WWCD"), cooldown + 2));
        }
        else
        {
            str.Append(GetString("WWCanRampage"));
        }

        return str.ToString();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return !Medic.ProtectList.Contains(target.PlayerId) && IsRampaging;
    }
}