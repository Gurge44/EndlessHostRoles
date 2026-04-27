using EHR.Modules;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR.Roles;

public class PortalMaker : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private byte PortalMakerId;
    private List<Vector2> Marks;
    private Dictionary<byte, long> LastTP;

    public static OptionItem AbilityCooldown;
    private static OptionItem PetToRemovePortals;

    public override void SetupCustomOption()
    {
        StartSetup(652800)
            .AutoSetupOption(ref AbilityCooldown, 5, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref PetToRemovePortals, true);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        PortalMakerId = playerId;
        Marks = new(2);
        LastTP = Main.PlayerStates.Keys.ToDictionary(x => x, _ => Utils.TimeStamp);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (Marks.Count == 2)
        {
            if (!PetToRemovePortals.GetBool()) return;
            Marks.Clear();
            CustomNetObject.AllObjects.OfType<Portal>().Do(x => x.Despawn());
            pc.Notify(Translator.GetString("MarksCleared"));
            return;
        }

        Vector2 pos = pc.Pos();
        if (Marks.Count == 1 && FastVector2.DistanceWithinRange(Marks[0], pos, 4f)) return;
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

        long now = Utils.TimeStamp;
        if (!LastTP.TryGetValue(pc.PlayerId, out long lastTP) || lastTP + 5 > now) return;

        Vector2 pos = pc.Pos();
        int nearIndex = -1;
        Vector2 mark0 = Marks[0];
        if (FastVector2.DistanceWithinRange(mark0, pos, 1f)) nearIndex = 0;
        else
        {
            Vector2 mark1 = Marks[1];
            if (FastVector2.DistanceWithinRange(mark1, pos, 1f))
                nearIndex = 1;
        }
        if (nearIndex == -1) return;

        Vector2 target = Marks[1 - nearIndex];
        pc.TP(target);
        LastTP[pc.PlayerId] = now;
        
        if (PortalMakerId == PlayerControl.LocalPlayer.PlayerId && pc.Is(CustomRoles.RiftMaker))
            Achievements.Type.YouCopycat.CompleteAfterGameEnd();
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Marks.Add(reader.ReadVector2());
    }

    public override void GetProgressText(byte playerId, bool comms, StringBuilder resultText)
    {
        base.GetProgressText(playerId, comms, resultText);
        Color32 color = Marks.Count == 2 ? Color.green : Color.red;
        resultText
            .Append(Utils.ColorPrefix(color))
            .Append(' ')
            .Append(Marks.Count)
            .Append("/2</color>");
    }
}