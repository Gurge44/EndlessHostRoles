using System;
using System.Linq;
using EHR.Modules;
using EHR.Neutral;

namespace EHR.Impostor;

public class Loner : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public byte PickedPlayer;
    public CustomRoles PickedRole;

    public override void SetupCustomOption()
    {
        StartSetup(655300);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        PickedPlayer = byte.MaxValue;
        PickedRole = default;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return;
        if (shapeshifter == null || target == null || shapeshifter.PlayerId == target.PlayerId || PickedPlayer != byte.MaxValue) return;

        PickedPlayer = target.PlayerId;
        PickedRole = Enum.GetValues<CustomRoles>().Where(x => x.IsImpostor() && !x.IsVanilla() && !CustomRoleSelector.RoleResult.ContainsValue(x) && x.GetMode() != 0).RandomElement();

        Utils.SendMessage("\n", shapeshifter.PlayerId, string.Format(Translator.GetString("Loner.Picked"), PickedPlayer.ColoredPlayerName(), PickedRole.ToColoredString()));
    }
}