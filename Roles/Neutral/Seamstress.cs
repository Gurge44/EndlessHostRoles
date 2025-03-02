using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Seamstress : RoleBase
{
    public static bool On;
    private static List<Seamstress> Instances = [];

    public override bool IsEnable => On;

    private static OptionItem ShapeshiftCooldown;

    private byte SeamstressID;
    public (byte, byte) SewedPlayers;

    public override void SetupCustomOption()
    {
        StartSetup(645800)
            .AutoSetupOption(ref ShapeshiftCooldown, 15f, new FloatValueRule(0.5f, 60f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        SeamstressID = playerId;
        SewedPlayers = (byte.MaxValue, byte.MaxValue);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        bool firstIsSet = SewedPlayers.Item1 != byte.MaxValue;
        bool secondIsSet = SewedPlayers.Item2 != byte.MaxValue;

        float cd;
        if (!firstIsSet && !secondIsSet) cd = ShapeshiftCooldown.GetFloat();
        else if (firstIsSet ^ secondIsSet) cd = 1f;
        else cd = 300f;
        
        AURoleOptions.ShapeshifterCooldown = cd;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        bool firstIsSet = SewedPlayers.Item1 != byte.MaxValue;
        bool secondIsSet = SewedPlayers.Item2 != byte.MaxValue;
        
        switch (firstIsSet)
        {
            case true when secondIsSet:
                return false;
            case true:
                SewedPlayers.Item2 = target.PlayerId;
                break;
            default:
                SewedPlayers.Item1 = target.PlayerId;
                break;
        }
        
        shapeshifter.SyncSettings();
        target.Notify(string.Format(Translator.GetString("SewedBySeamstress"), CustomRoles.Seamstress.ToColoredString()));
        Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: target);
        Utils.SendRPC(CustomRPC.SyncRoleData, SeamstressID, SewedPlayers.Item1, SewedPlayers.Item2);
        return false;
    }

    public override void AfterMeetingTasks()
    {
        SewedPlayers = (byte.MaxValue, byte.MaxValue);
        Utils.SendRPC(CustomRPC.SyncRoleData, SeamstressID, SewedPlayers.Item1, SewedPlayers.Item2);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        SewedPlayers = (reader.ReadByte(), reader.ReadByte());
    }

    public static void OnAnyoneCheckMurder(PlayerControl killer, PlayerControl target)
    {
        foreach (Seamstress instance in Instances)
        {
            if (instance.SewedPlayers.Item1 == target.PlayerId)
                Main.PlayerStates[instance.SewedPlayers.Item2].Role.OnCheckMurder(killer, instance.SewedPlayers.Item2.GetPlayer());
            else if (instance.SewedPlayers.Item2 == target.PlayerId)
                Main.PlayerStates[instance.SewedPlayers.Item1].Role.OnCheckMurder(killer, instance.SewedPlayers.Item1.GetPlayer());
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        byte[] tuple = [SewedPlayers.Item1, SewedPlayers.Item2];
        if (tuple.All(x => x == byte.MaxValue)) return;
        
        var sewed = tuple.ToValidPlayers().ToList();

        if (sewed.Count != 2 || sewed.Exists(x => !x.IsAlive()))
        {
            SewedPlayers = (byte.MaxValue, byte.MaxValue);
            Utils.SendRPC(CustomRPC.SyncRoleData, SeamstressID, SewedPlayers.Item1, SewedPlayers.Item2);
            pc.SyncSettings();
            pc.RpcResetAbilityCooldown();
            
            if (sewed.Count > 0 && sewed.FindFirst(x => x.IsAlive(), out var alive))
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: alive);
        }
    }
}