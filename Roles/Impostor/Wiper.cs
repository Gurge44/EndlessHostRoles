using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Impostor;

public class Wiper : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    private static OptionItem KillOtherImpostors;
    private static OptionItem CanVent;
    private PlainShipRoom LastRoom;

    private byte WiperID;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651200)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref KillOtherImpostors, false)
            .AutoSetupOption(ref CanVent, false);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        LastRoom = null;
        WiperID = playerId;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return false;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetInt();
    }

    public override void OnPet(PlayerControl pc)
    {
        WipeOutEveryoneInRoom(pc);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        WipeOutEveryoneInRoom(shapeshifter);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        WipeOutEveryoneInRoom(pc);
        return false;
    }

    private static bool IsInvalidRoom(PlainShipRoom room)
    {
        if (room == null) return true;

        switch (Main.CurrentMap)
        {
            case MapNames.Skeld or MapNames.Dleks when room.RoomId == SystemTypes.Cafeteria:
                return true;
            case MapNames.MiraHQ:
                switch (MeetingStates.FirstMeeting, room.RoomId)
                {
                    case (true, SystemTypes.Launchpad):
                    case (false, SystemTypes.Cafeteria):
                        return true;
                }

                break;
            case MapNames.Polus:
                switch (MeetingStates.FirstMeeting, room.RoomId)
                {
                    case (true, SystemTypes.Dropship):
                    case (false, SystemTypes.Office):
                        return true;
                }

                break;
            case MapNames.Fungle when !MeetingStates.FirstMeeting && room.RoomId == SystemTypes.MeetingRoom:
                return true;
            default:
                if (SubmergedCompatibility.IsSubmerged() && room.RoomId is (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerCentral or (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral)
                    return true;

                break;
        }

        return room.RoomId is SystemTypes.Outside or SystemTypes.Hallway or SystemTypes.Ventilation || room.RoomId.ToString().Contains("Decontamination");
    }

    private static void WipeOutEveryoneInRoom(PlayerControl pc)
    {
        if (Utils.IsAnySabotageActive()) return;

        PlainShipRoom room = pc.GetPlainShipRoom();
        if (IsInvalidRoom(room)) return;

        Main.AllAlivePlayerControls.Without(pc).DoIf(x => x.IsInRoom(room) && pc.RpcCheckAndMurder(x, true), x => x.Suicide(PlayerState.DeathReason.WipedOut, pc));
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || !Main.IntroDestroyed) return;

        PlainShipRoom room = pc.GetPlainShipRoom();

        if (room != LastRoom)
        {
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            LastRoom = room;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != WiperID || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;

        if (Utils.IsAnySabotageActive())
            return Utils.ColorString(Color.red, Translator.GetString("Wiper.CannotUseAbilityDuringSabotage"));

        PlainShipRoom room = seer.GetPlainShipRoom();

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (IsInvalidRoom(room)) return Utils.ColorString(Color.red, Translator.GetString("Wiper.MustBeInRoomToUseAbility"));
        return string.Format(Translator.GetString("Wiper.CurrentRoom"), Translator.GetString($"{room.RoomId}"));
    }
}