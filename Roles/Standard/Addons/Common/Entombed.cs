using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Entombed : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public static Dictionary<byte, SystemTypes> BlockedRoom = [];
    private static long LastNotifyTS;
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

        if (Utils.DoRPC)
        {
            MessageWriter writer = Utils.CreateRPC(CustomRPC.Entombed);
            writer.Write(MeetingEndTS.ToString());
            writer.Write(GracePeriodLength);
            writer.Write(BlockedRoom.Count);

            foreach ((byte key, SystemTypes value) in BlockedRoom)
            {
                writer.Write(key);
                writer.Write((byte)value);
            }
            
            Utils.EndRPC(writer);
        }
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        MeetingEndTS = long.Parse(reader.ReadString());
        GracePeriodLength = reader.ReadSingle();
        Loop.Times(reader.ReadInt32(), _ => BlockedRoom[reader.ReadByte()] = (SystemTypes)reader.ReadByte());
    }

    public static void OnFixedUpdate(PlayerControl pc)
    {
        if (!BlockedRoom.TryGetValue(pc.PlayerId, out SystemTypes blockedRoom)) return;

        long now = Utils.TimeStamp;
        
        if (now - MeetingEndTS <= GracePeriodLength)
        {
            if (LastNotifyTS != now)
            {
                LastNotifyTS = now;
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }
            
            return;
        }

        if (pc.IsInRoom(blockedRoom))
            pc.Suicide();
    }

    public static string GetSelfSuffix(PlayerControl seer)
    {
        if (!BlockedRoom.TryGetValue(seer.PlayerId, out SystemTypes blockedRoom) || !seer.IsAlive()) return string.Empty;
        long elapsed = Utils.TimeStamp - MeetingEndTS;
        return string.Format(Translator.GetString(elapsed < GracePeriodLength ? "Entombed.SuffixGracePeriod" : "Entombed.SuffixActive"), Translator.GetString(blockedRoom.ToString()), GracePeriodLength - elapsed);
    }
}