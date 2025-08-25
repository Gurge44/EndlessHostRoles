using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Retributionist : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public static OptionItem ResetCampedPlayerAfterEveryMeeting;
    public static OptionItem UsePet;

    public byte Camping;
    public bool Notified;
    private PlayerControl RetributionistPC;

    public override void SetupCustomOption()
    {
        StartSetup(653200)
            .AutoSetupOption(ref ResetCampedPlayerAfterEveryMeeting, false)
            .CreatePetUseSetting(ref UsePet);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Camping = byte.MaxValue;
        Notified = false;
        RetributionistPC = playerId.GetPlayer();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetRoleMap().CustomRole == CustomRoles.Retributionist;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = 5f;
    }

    public override void AfterMeetingTasks()
    {
        if (RetributionistPC == null || !RetributionistPC.IsAlive() || CanUseKillButton(RetributionistPC)) return;

        PlayerControl campTarget = Camping.GetPlayer();

        if (ResetCampedPlayerAfterEveryMeeting.GetBool() || campTarget == null || !campTarget.IsAlive())
        {
            Notified = false;
            Camping = byte.MaxValue;
            Utils.SendRPC(CustomRPC.SyncRoleData, RetributionistPC.PlayerId, Camping);
            RetributionistPC.RpcChangeRoleBasis(CustomRoles.Retributionist);
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        Camping = target.PlayerId;
        Utils.SendRPC(CustomRPC.SyncRoleData, RetributionistPC.PlayerId, Camping);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        RetributionistPC.RpcChangeRoleBasis(CustomRoles.CrewmateEHR);
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (Camping == byte.MaxValue || Notified || !pc.IsAlive()) return;

        PlayerControl campTarget = Camping.GetPlayer();

        if (campTarget == null)
        {
            if (!Notified)
            {
                Camping = byte.MaxValue;
                Utils.SendRPC(CustomRPC.SyncRoleData, RetributionistPC.PlayerId, Camping);
            }

            return;
        }

        if (!campTarget.IsAlive())
        {
            pc.ReactorFlash();
            pc.Notify(Translator.GetString("Retributionist.TargetDead"), 15f);
            Notified = true;
        }
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        var command = $"/retribute {target.PlayerId}";
        ChatCommands.RetributeCommand(shapeshifter, command, command.Split(' '));
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Camping = reader.ReadByte();
    }
}