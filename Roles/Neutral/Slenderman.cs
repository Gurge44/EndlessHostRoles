using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Slenderman : RoleBase
{
    public static bool On;
    private static List<Slenderman> Instances = [];

    public override bool IsEnable => On;

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;
    private static OptionItem BlindRange;
    private static OptionItem AfterMeetingBlindCooldown;

    private HashSet<byte> Blinded;
    private PlayerControl SlendermanPC;
    private long MeetingCooldownEndTS;

    public override void SetupCustomOption()
    {
        StartSetup(652900)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true)
            .AutoSetupOption(ref BlindRange, 4f, new FloatValueRule(0.25f, 10f, 0.25f), OptionFormat.Multiplier)
            .AutoSetupOption(ref AfterMeetingBlindCooldown, 8, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Blinded = [];
        SlendermanPC = playerId.GetPlayer();
        MeetingCooldownEndTS = Utils.TimeStamp + 10 + AfterMeetingBlindCooldown.GetInt();
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public static bool IsBlinded(byte id)
    {
        return On && Instances.Exists(x => x.Blinded.Contains(id));
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (MeetingCooldownEndTS > Utils.TimeStamp || pc.Is(CustomRoles.Slenderman) || SlendermanPC == null || !SlendermanPC.IsAlive()) return;

        bool inRange = Vector2.Distance(pc.Pos(), SlendermanPC.Pos()) <= BlindRange.GetFloat();

        if ((inRange && Blinded.Add(pc.PlayerId)) || (!inRange && Blinded.Remove(pc.PlayerId)))
        {
            pc.MarkDirtySettings();
            Utils.NotifyRoles(SpecifySeer: SlendermanPC, SpecifyTarget: pc, SendOption: SendOption.None);

            if (Utils.DoRPC)
            {
                MessageWriter w = Utils.CreateRPC(CustomRPC.SyncRoleData);
                w.Write(SlendermanPC.PlayerId);
                w.WritePacked(Blinded.Count);
                Blinded.Do(x => w.Write(x));
                Utils.EndRPC(w);
            }
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Blinded.Clear();
        Loop.Times(reader.ReadPackedInt32(), _ => Blinded.Add(reader.ReadByte()));
    }

    public override void AfterMeetingTasks()
    {
        MeetingCooldownEndTS = Utils.TimeStamp + AfterMeetingBlindCooldown.GetInt();
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return base.GetProgressText(playerId, comms) + $" ({Blinded.Count})";
    }
}