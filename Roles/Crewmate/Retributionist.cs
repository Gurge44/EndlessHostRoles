using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;

namespace EHR.Crewmate;

public class Retributionist : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public static OptionItem ResetCampedPlayerAfterEveryMeeting;
    public static OptionItem UsePet;
    public static OptionItem CancelVote;

    public byte Camping;
    public bool Notified;
    private PlayerControl RetributionistPC;

    public override void SetupCustomOption()
    {
        StartSetup(653200)
            .AutoSetupOption(ref ResetCampedPlayerAfterEveryMeeting, false)
            .CreatePetUseSetting(ref UsePet)
            .CreateVoteCancellingUseSetting(ref CancelVote);
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

    public override bool OnVote(PlayerControl voter, PlayerControl target)
    {
        if (!CancelVote.GetBool()) return false;
        if (Starspawn.IsDayBreak) return false;
        if (target == null || voter == null || voter.PlayerId == target.PlayerId || Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;

        if (!Notified || Camping == byte.MaxValue) return false;

        PlayerControl campTarget = Utils.GetPlayerById(Camping);
        if (campTarget == null || campTarget.IsAlive() || !Main.PlayerStates.TryGetValue(campTarget.PlayerId, out PlayerState campState)) return false;

        byte realKiller = campState.GetRealKiller();

        if (realKiller != target.PlayerId)
        {
            Notified = false;
            Utils.SendMessage("\n", voter.PlayerId, Translator.GetString("Retributionist.Fail"));
        }
        else
        {
            PlayerControl killer = Utils.GetPlayerById(realKiller);

            if (killer == null || !killer.IsAlive())
            {
                Notified = false;
                Utils.SendMessage("\n", voter.PlayerId, Translator.GetString("Retributionist.KillerDead"));
            }
            else
            {
                killer.SetRealKiller(voter);
                PlayerState killerState = Main.PlayerStates[killer.PlayerId];
                killerState.deathReason = PlayerState.DeathReason.Retribution;
                killerState.SetDead();
                Medic.IsDead(killer);
                killer.RpcExileV2();
                Utils.AfterPlayerDeathTasks(killer, true);
                Utils.SendMessage("\n", title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Retributionist), string.Format(Translator.GetString("Retributionist.SuccessOthers"), target.PlayerId.ColoredPlayerName(), CustomRoles.Retributionist.ToColoredString())));
                Utils.SendMessage("\n", voter.PlayerId, Translator.GetString("Retributionist.Success"));
            }
        }

        MeetingManager.SendCommandUsedMessage("/retribute");

        Main.DontCancelVoteList.Add(voter.PlayerId);
        return true;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Camping = reader.ReadByte();
    }
}