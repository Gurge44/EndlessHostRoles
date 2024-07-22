using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

internal class Assassin : RoleBase
{
    private const int Id = 700;
    public static List<byte> playerIdList = [];

    private static OptionItem MarkCooldownOpt;
    public static OptionItem AssassinateCooldownOpt;
    private static OptionItem CanKillAfterAssassinateOpt;
    private float AssassinateCooldown;
    private bool CanKillAfterAssassinate;
    private bool IsUndertaker;

    private float MarkCooldown;

    public byte MarkedPlayer;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Assassin);
        MarkCooldownOpt = new FloatOptionItem(Id + 10, "AssassinMarkCooldown", new(0f, 180f, 0.5f), 1f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Assassin])
            .SetValueFormat(OptionFormat.Seconds);
        AssassinateCooldownOpt = new FloatOptionItem(Id + 11, "AssassinAssassinateCooldown", new(0f, 180f, 0.5f), 18.5f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Assassin])
            .SetValueFormat(OptionFormat.Seconds);
        CanKillAfterAssassinateOpt = new BooleanOptionItem(Id + 12, "AssassinCanKillAfterAssassinate", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Assassin]);
    }

    public override void Init()
    {
        playerIdList = [];
        MarkedPlayer = byte.MaxValue;
        IsUndertaker = false;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        MarkedPlayer = byte.MaxValue;
        IsUndertaker = Main.PlayerStates[playerId].MainRole == CustomRoles.Undertaker;

        if (IsUndertaker)
        {
            MarkCooldown = Undertaker.UndertakerMarkCooldown.GetFloat();
            AssassinateCooldown = Undertaker.UndertakerAssassinateCooldown.GetFloat();
            CanKillAfterAssassinate = Undertaker.UndertakerCanKillAfterAssassinate.GetBool();
        }
        else
        {
            MarkCooldown = MarkCooldownOpt.GetFloat();
            AssassinateCooldown = AssassinateCooldownOpt.GetFloat();
            CanKillAfterAssassinate = CanKillAfterAssassinateOpt.GetBool();
        }
    }

    void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMarkedPlayer, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(MarkedPlayer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        byte targetId = reader.ReadByte();

        if (Main.PlayerStates[playerId].Role is not Assassin assassin) return;
        assassin.MarkedPlayer = targetId;
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = id.IsPlayerShifted() ? DefaultKillCooldown : MarkCooldown;

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = AssassinateCooldown;
        else
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.ShapeshifterCooldown = AssassinateCooldown;
        }
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return false;
        return CanKillAfterAssassinate || !pc.IsShifted();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.IsShifted())
        {
            killer.ResetKillCooldown();
            killer.SyncSettings();
            return CanUseKillButton(killer);
        }

        MarkedPlayer = target.PlayerId;
        SendRPC(killer.PlayerId);
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        if (killer.IsModClient()) killer.RpcResetAbilityCooldown();
        killer.SyncSettings();
        killer.RPCPlayCustomSound("Clothe");
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        OnShapeshift(pc, null, true);
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl t, bool shapeshifting)
    {
        if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return false;
        if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;

        Take(pc);

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return false;
        Take(pc);
        return false;
    }

    private void Take(PlayerControl pc)
    {
        if (MarkedPlayer != byte.MaxValue)
        {
            var target = Utils.GetPlayerById(MarkedPlayer);
            if (IsUndertaker) target.TP(pc);
            else pc.TP(target);

            if (!(target == null || !target.IsAlive() || Pelican.IsEaten(target.PlayerId) || target.inVent || !GameStates.IsInTask) && pc.RpcCheckAndMurder(target))
            {
                MarkedPlayer = byte.MaxValue;
                SendRPC(pc.PlayerId);
                pc.ResetKillCooldown();
                pc.SyncSettings();
                pc.SetKillCooldown(DefaultKillCooldown);
            }
        }
    }

    public override void SetButtonTexts(HudManager __instance, byte playerId)
    {
        bool shifted = playerId.IsPlayerShifted();
        __instance.KillButton.OverrideText(!shifted ? GetString("AssassinMarkButtonText") : GetString("KillButtonText"));
        if (MarkedPlayer != byte.MaxValue && !shifted)
            if (!UsePets.GetBool()) __instance.AbilityButton.OverrideText(GetString("AssassinShapeshiftText"));
            else __instance.PetButton.OverrideText(GetString("AssassinShapeshiftText"));
    }
}