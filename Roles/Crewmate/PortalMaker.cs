using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using Color = UnityEngine.Color;

namespace EHR.Crewmate;

public class PortalMaker : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private List<Vector2> Marks;
    private Dictionary<byte, long> LastTP;

    public override void SetupCustomOption()
    {
        StartSetup(652800);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Marks = new(2);
        LastTP = Main.PlayerStates.Keys.ToDictionary(x => x, _ => Utils.TimeStamp);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (Marks.Count == 2) return;
        
        Vector2 pos = pc.Pos();
        if (Marks.Count == 1 && Vector2.Distance(Marks[0], pos) < 4f) return;
        Marks.Add(pos);
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, pos);
        
        if (Marks.Count == 2)
        {
            LastTP[pc.PlayerId] = Utils.TimeStamp;
            Marks.ForEach(x => _ = new Portal(x));
        }
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (Marks.Count != 2) return;
        
        var now = Utils.TimeStamp;
        if (!LastTP.TryGetValue(pc.PlayerId, out var lastTP) || lastTP + 5 > now) return;

        var pos = pc.Pos();
        if (!Marks.FindFirst(x => Vector2.Distance(x, pos) <= 1f, out var nearMark)) return;
        
        int index = Marks.IndexOf(nearMark);
        Vector2 target = Marks[1 - index];
        pc.TP(target);
        LastTP[pc.PlayerId] = now;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Marks.Add(reader.ReadVector2());
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return base.GetProgressText(playerId, comms) + Utils.ColorString(Marks.Count == 2 ? Color.green : Color.red, $" {Marks.Count}/2");
    }
}