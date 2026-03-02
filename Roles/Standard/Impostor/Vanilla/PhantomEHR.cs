using AmongUs.GameOptions;

namespace EHR.Roles;

public class PhantomEHR : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption() { }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        pc.RpcMakeInvisible(phantom: true);
        LateTask.New(() =>
        {
            if (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting) return;
            pc.RpcMakeVisible(phantom: true);
            pc.RpcResetAbilityCooldown();
        }, Main.RealOptionsData.GetFloat(FloatOptionNames.PhantomDuration), "PhantomEHR Appear");
        return false;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return !Main.Invisible.Contains(pc.PlayerId);
    }
}