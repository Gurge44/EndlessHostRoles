using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Gatekeeper : RoleBase
{
    public static bool On;

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;
    private static OptionItem MaxRoomsMarkedAtOnce;

    public override bool IsEnable => On;

    private Dictionary<SystemTypes, int> MarkedRooms = [];
    private StringBuilder Suffix;
    private byte GatekeeperId;

    public override void SetupCustomOption()
    {
        StartSetup(659200)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 1f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times)
            .AutoSetupOption(ref MaxRoomsMarkedAtOnce, 5, new IntegerValueRule(1, 30, 1));
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Suffix = new();
        MarkedRooms = [];
        GatekeeperId = playerId;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        MarkRoom(shapeshifter);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        MarkRoom(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        MarkRoom(pc);
    }

    private void MarkRoom(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1f) return;
        
        var room = pc.GetPlainShipRoom();
        
        if (room && MarkedRooms.TryAdd(room.RoomId, -1))
        {
            pc.RpcRemoveAbilityUse();
            
            if (MarkedRooms.Count > MaxRoomsMarkedAtOnce.GetInt())
                MarkedRooms.Remove(MarkedRooms.Keys.First());
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || !PerSecondUpdateScheduler.ShouldRunUpdate(pc.PlayerId)) return;

        bool first = true;
        bool changed = false;
        var aapc = Main.CachedAlivePlayerControls();
        
        Suffix.Clear();
        Suffix.Append("<size=80%>");

        foreach ((SystemTypes markedRoom, int oldCount) in MarkedRooms)
        {
            int newCount = aapc.Count(x => x.IsInRoom(markedRoom));
            MarkedRooms[markedRoom] = newCount;
            if (oldCount != newCount) changed = true;

            if (!first) Suffix.Append('\n');
            Suffix.Append("<#00a5ff>");
            Suffix.Append(Translator.GetString(markedRoom));
            Suffix.Append("</color>");
            Suffix.Append(' ');
            Suffix.Append('-');
            Suffix.Append(' ');
            Suffix.Append("<#00ffff>");
            Suffix.Append(newCount);
            Suffix.Append("</color>");
            first = false;
        }
        
        if (changed)
        {
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            if (pc.IsNonHostModdedClient()) Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, MarkedRooms.Count > 5 ? Suffix.ToString().RemoveHtmlTags() : Suffix.ToString());
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Suffix.Clear();
        Suffix.Append(reader.ReadString());
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != GatekeeperId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;
        return Suffix.ToString();
    }
}