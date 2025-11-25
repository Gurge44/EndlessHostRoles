using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Reaper : Coven
{
    public static bool On;
    private static List<Reaper> Instances = [];

    private static OptionItem AbilityCooldown;
    private static OptionItem SoulsRequired;
    public static OptionItem KillCooldown;
    private static OptionItem CanVent;

    public HashSet<byte> CursedPlayers = [];

    private byte ReaperId;
    private int Souls;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Never;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650040)
            .AutoSetupOption(ref AbilityCooldown, 10f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref SoulsRequired, 3, new IntegerValueRule(1, 10, 1))
            .AutoSetupOption(ref KillCooldown, 15f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, false);
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
        ReaperId = playerId;
        CursedPlayers = [];
        Souls = 0;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        CursedPlayers.Add(target.PlayerId);
        Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        CursedPlayers.Add(reader.ReadByte());
    }

    public static void OnAnyoneDead(PlayerControl target)
    {
        Instances.DoIf(x => x.CursedPlayers.Contains(target.PlayerId) && ++x.Souls >= SoulsRequired.GetInt(), x => x.BecomeDeath());
        Instances.RemoveAll(x => x.Souls >= SoulsRequired.GetInt());

        if (Instances.Count > 0 && Main.AllAlivePlayerControls.All(x => x.GetCountTypes() is CountTypes.OutOfGame or CountTypes.None or CountTypes.Crew || x.Is(CustomRoles.Reaper)))
            Instances.RandomElement().BecomeDeath(true);
    }

    void BecomeDeath(bool removeInstance = false)
    {
        PlayerControl reaperPc = ReaperId.GetPlayer();
        if (reaperPc == null) return;

        reaperPc.RpcSetCustomRole(CustomRoles.Death);
        reaperPc.SetKillCooldown(KillCooldown.GetFloat());
        reaperPc.ResetKillCooldown();

        Utils.NotifyRoles(SpecifySeer: reaperPc);
        Utils.NotifyRoles(SpecifyTarget: reaperPc);

        if (removeInstance) Instances.Remove(this);
    }
}

public class Death : Coven
{
    public static bool On;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Never;

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

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Reaper.KillCooldown.GetFloat();
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return false;
    }
}