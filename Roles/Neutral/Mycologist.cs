using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Neutral;

internal class Mycologist : RoleBase
{
    private static readonly string[] SpreadMode =
    [
        "VentButtonText", // 0
        "SabotageButtonText", // 1
        "PetButtonText", // 2
        "AbilityButtonText.Phantom", // 3
        "AbilityButtonText.Shapeshifter" // 4
    ];

    private static OptionItem KillCooldown;
    private static OptionItem HasImpostorVision;
    private static OptionItem SpreadAction;
    private static OptionItem CD;
    private static OptionItem InfectRadius;
    private static OptionItem InfectTime;

    public readonly List<byte> InfectedPlayers = [];
    private byte MycologistId = byte.MaxValue;
    private static int Id => 643210;

    private PlayerControl MycologistPC => GetPlayerById(MycologistId);

    public override bool IsEnable => MycologistId != byte.MaxValue;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Mycologist);

        KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
            .SetValueFormat(OptionFormat.Seconds);

        HasImpostorVision = new BooleanOptionItem(Id + 7, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist]);

        SpreadAction = new StringOptionItem(Id + 3, "MycologistAction", SpreadMode, 1, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist]);

        CD = new IntegerOptionItem(Id + 4, "AbilityCooldown", new(1, 90, 1), 15, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
            .SetValueFormat(OptionFormat.Seconds);

        InfectRadius = new FloatOptionItem(Id + 5, "InfectRadius", new(0.1f, 5f, 0.1f), 3f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
            .SetValueFormat(OptionFormat.Multiplier);

        InfectTime = new IntegerOptionItem(Id + 6, "InfectDelay", new(0, 60, 1), 5, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        MycologistId = byte.MaxValue;
        InfectedPlayers.Clear();
    }

    public override void Add(byte playerId)
    {
        MycologistId = playerId;
        InfectedPlayers.Clear();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool()) AURoleOptions.PhantomCooldown = CD.GetInt();
    }

    private void SendRPC()
    {
        if (!IsEnable || !DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncMycologist, SendOption.Reliable);
        writer.Write(MycologistId);
        writer.Write(InfectedPlayers.Count);

        if (InfectedPlayers.Count > 0)
        {
            foreach (byte x in InfectedPlayers)
                writer.Write(x);
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        if (Main.PlayerStates[playerId].Role is not Mycologist mg) return;

        mg.InfectedPlayers.Clear();
        int length = reader.ReadInt32();
        for (var i = 0; i < length; i++) mg.InfectedPlayers.Add(reader.ReadByte());
    }

    public override void OnPet(PlayerControl pc)
    {
        if (SpreadAction.GetValue() == 2) SpreadSpores();
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        if (SpreadAction.GetValue() == 1) SpreadSpores();
        return pc.Is(CustomRoles.Mischievous);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (SpreadAction.GetValue() == 0 || (SpreadAction.GetValue() == 2 && !UsePets.GetBool())) SpreadSpores();
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (SpreadAction.GetValue() == 3) SpreadSpores();

        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (SpreadAction.GetValue() == 4) SpreadSpores();

        return false;
    }

    private void SpreadSpores()
    {
        if (!IsEnable || MycologistPC.HasAbilityCD()) return;

        MycologistPC.AddAbilityCD(CD.GetInt());

        LateTask.New(() =>
        {
            InfectedPlayers.AddRange(GetPlayersInRadius(InfectRadius.GetFloat(), MycologistPC.Pos()).Select(x => x.PlayerId));
            SendRPC();
            NotifyRoles(SpecifySeer: MycologistPC);
        }, InfectTime.GetFloat(), "Mycologist Infect Time");

        MycologistPC.Notify(GetString("MycologistNotify"));
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return IsEnable && target != null && InfectedPlayers.Contains(target.PlayerId);
    }

    public override void AfterMeetingTasks()
    {
        if (Main.PlayerStates[MycologistId].IsDead) return;
        MycologistPC.AddAbilityCD(CD.GetInt());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return true;
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return base.CanUseSabotage(pc) || (SpreadAction.GetValue() == 1 && pc.IsAlive());
    }
}
