using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Roles;

public class Entombed : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public static Dictionary<byte, SystemTypes> BlockedRoom = [];
    private static long MeetingEndTS;
    private static float GracePeriodLength;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(658080, CustomRoles.Entombed, canSetNum: true, teamSpawnOptions: true);
    }

    public static void AfterMeeting()
    {
        MeetingEndTS = Utils.TimeStamp;
        GracePeriodLength = 5 + 5 / Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        
        foreach (PlayerControl pc in Main.EnumeratePlayerControls())
            if (pc.Is(CustomRoles.Entombed))
                BlockedRoom[pc.PlayerId] = ShipStatus.Instance.AllRooms.RandomElement().RoomId;
    }

    public static void OnFixedUpdate(PlayerControl pc)
    {
        if (Utils.TimeStamp - MeetingEndTS <= GracePeriodLength || !BlockedRoom.TryGetValue(pc.PlayerId, out SystemTypes blockedRoom)) return;
        
        if (pc.IsInRoom(blockedRoom))
            pc.Suicide();
    }

    public static string GetSelfSuffix(PlayerControl seer)
    {
        if (!BlockedRoom.TryGetValue(seer.PlayerId, out SystemTypes blockedRoom) || !seer.IsAlive()) return string.Empty;
        long elapsed = Utils.TimeStamp - MeetingEndTS;
        return string.Format(Translator.GetString(elapsed <= GracePeriodLength ? "Entombed.SuffixGracePeriod" : "Entombed.SuffixActive"), Translator.GetString(blockedRoom.ToString()), GracePeriodLength - elapsed);
    }
}