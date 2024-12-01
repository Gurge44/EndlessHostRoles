using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

public class Werewolf : RoleBase
{
    private const int Id = 12850;
    private static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem HasImpostorVision;
    private static OptionItem RampageCD;
    private static OptionItem RampageDur;
    private static int CD;

    private static long LastFixedTime;
    private long lastTime;

    private long RampageTime;
    private byte WWId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    private bool CanRampage => GameStates.IsInTask && RampageTime == -10 && lastTime == -10;
    private bool IsRampaging => RampageTime != -10;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Werewolf);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 3f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);

        HasImpostorVision = new BooleanOptionItem(Id + 11, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf]);

        RampageCD = new FloatOptionItem(Id + 12, "WWRampageCD", new(0f, 180f, 0.5f), 35f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);

        RampageDur = new FloatOptionItem(Id + 13, "WWRampageDur", new(0f, 180f, 1f), 12f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        RampageTime = -10;
        lastTime = -10;
        CD = 0;
        WWId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        WWId = playerId;

        RampageTime = -10;
        lastTime = -10;
        CD = StartingKillCooldown.GetInt();

        LateTask.New(() =>
        {
            if (UseUnshiftTrigger.GetBool() && UseUnshiftTriggerForNKs.GetBool())
                Utils.GetPlayerById(playerId).RpcResetAbilityCooldown();
        }, 9f, log: false);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return (CanRampage && (!UseUnshiftTrigger.GetBool() || !UseUnshiftTriggerForNKs.GetBool())) || IsRampaging || pc.inVent;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return IsRampaging;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool()) AURoleOptions.PhantomCooldown = 1f;

        if (UseUnshiftTrigger.GetBool() && UseUnshiftTriggerForNKs.GetBool()) AURoleOptions.ShapeshifterCooldown = RampageDur.GetFloat() + 0.5f;

        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    private void SendRPC()
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetWwTimer, SendOption.Reliable);
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
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || !IsEnable || Main.HasJustStarted || player == null) return;

        long now = Utils.TimeStamp;

        if (lastTime != -10)
        {
            if (!player.IsModClient())
            {
                long cooldown = lastTime + (long)RampageCD.GetFloat() - now;
                if ((int)cooldown != CD) player.Notify(string.Format(GetString("CDPT"), cooldown + 1), 1.1f, overrideAll: true);

                CD = (int)cooldown;
            }

            if (lastTime + (long)RampageCD.GetFloat() < now)
            {
                lastTime = -10;
                bool unshift = UseUnshiftTrigger.GetBool() && UseUnshiftTriggerForNKs.GetBool();
                if (!player.IsModClient()) player.Notify(GetString(unshift ? "WWCanRampageUnshift" : "WWCanRampage"));

                if (!player.IsModClient()) player.RpcChangeRoleBasis(unshift ? CustomRoles.Werewolf : CustomRoles.EngineerEHR);
                SendRPC();
                CD = 0;
            }
        }

        if (LastFixedTime != now && RampageTime != -10)
        {
            LastFixedTime = now;
            var refresh = false;
            long remainTime = RampageTime + (long)RampageDur.GetFloat() - now;

            switch (remainTime)
            {
                case < 0:
                    lastTime = now;
                    player.Notify(GetString("WWRampageOut"));
                    if (!player.IsModClient()) player.RpcChangeRoleBasis(CustomRoles.CrewmateEHR);
                    RampageTime = -10;
                    SendRPC();
                    refresh = true;
                    break;
                case <= 10 when !player.IsModClient():
                    player.Notify(string.Format(GetString("WWRampageCountdown"), remainTime + 1), overrideAll: true);
                    break;
            }

            if (refresh) SendRPC();
        }
    }

    public override void OnExitVent(PlayerControl pc, Vent vent)
    {
        if (pc == null) return;

        Rampage(pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        Rampage(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        Rampage(pc);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;

        Rampage(shapeshifter);
        return false;
    }

    private void Rampage(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost || IsRampaging) return;

        LateTask.New(() =>
        {
            if (CanRampage)
            {
                RampageTime = Utils.TimeStamp;
                SendRPC();
                pc.Notify(GetString("WWRampaging"), RampageDur.GetFloat());
                if (!pc.IsModClient()) pc.RpcChangeRoleBasis(CustomRoles.Werewolf);
            }
        }, 0.5f, "Werewolf Vent");
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || seer == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive() || Main.PlayerStates[seer.PlayerId].Role is not Werewolf { IsEnable: true } ww) return string.Empty;

        var str = new StringBuilder();

        if (ww.IsRampaging)
        {
            long remainTime = ww.RampageTime + (long)RampageDur.GetFloat() - Utils.TimeStamp;
            str.Append(string.Format(GetString("WWRampageCountdown"), remainTime + 1));
        }
        else if (ww.lastTime != -10)
        {
            long cooldown = ww.lastTime + (long)RampageCD.GetFloat() - Utils.TimeStamp;
            str.Append(string.Format(GetString("WWCD"), cooldown + 1));
        }
        else
            str.Append(GetString(UseUnshiftTrigger.GetBool() && UseUnshiftTriggerForNKs.GetBool() ? "WWCanRampageUnshift" : "WWCanRampage"));

        return str.ToString();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return !Medic.ProtectList.Contains(target.PlayerId) && IsRampaging;
    }
}