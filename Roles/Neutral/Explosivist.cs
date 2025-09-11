using AmongUs.GameOptions;
using Hazel;

namespace EHR.Neutral;

public class Explosivist : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    private static OptionItem ImpostorVision;
    public static OptionItem ExplosionDelay;
    private static OptionItem ExplosionRadius;

    public override bool IsEnable => On;

    private TNT Explosive;
    private Vector2 RealPosition;
    private long ExplodeTS;
    private byte ExplosivistId;

    public override void SetupCustomOption()
    {
        StartSetup(654300)
            .AutoSetupOption(ref AbilityCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref ImpostorVision, true)
            .AutoSetupOption(ref ExplosionDelay, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ExplosionRadius, 4f, new FloatValueRule(0.25f, 10f, 0.25f), OptionFormat.Multiplier);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Explosive = null;
        RealPosition = Vector2.zero;
        ExplodeTS = 0;
        ExplosivistId = playerId;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());

        if (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool())
        {
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.PhantomDuration = 1f;
        }
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        UseAbility(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        UseAbility(pc);
    }

    void UseAbility(PlayerControl player)
    {
        if (Explosive != null) return;
        
        Vector2 pos = player.Pos();
        Explosive = new TNT(pos);
        RealPosition = pos;
        
        // From https://github.com/Rabek009/MoreGamemodes/blob/master/Roles/Impostor/Concealing/Droner.cs
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc == player || pc.AmOwner) continue;
            CustomRpcSender sender = CustomRpcSender.Create("Explosivist", SendOption.Reliable);
            sender.StartMessage(pc.GetClientId());
            sender.StartRpc(player.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                .WriteVector2(player.transform.position)
                .Write(player.NetTransform.lastSequenceId)
                .EndRpc();
            sender.StartRpc(player.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                .WriteVector2(player.transform.position)
                .Write((ushort)(player.NetTransform.lastSequenceId + 16383))
                .EndRpc();
            sender.EndMessage();
            sender.SendMessage();
            Utils.NumSnapToCallsThisRound += 2;
        }

        player.Visible = false;

        ExplodeTS = Utils.TimeStamp + ExplosionDelay.GetInt();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;

        Explosive?.TP(pc.transform.position);

        if (Explosive != null && ExplodeTS <= Utils.TimeStamp)
        {
            Utils.GetPlayersInRadius(ExplosionRadius.GetFloat(), Explosive.Position).Without(pc).Do(x => x.Suicide(PlayerState.DeathReason.Bombed, pc));
            
            RevertFreeze(pc);
            if (!Options.UsePets.GetBool()) pc.RpcResetAbilityCooldown();
            
            Explosive.Despawn();
            Explosive = null;
            RealPosition = Vector2.zero;
            ExplodeTS = 0;
        }
    }

    public override void OnReportDeadBody()
    {
        if (Explosive != null)
        {
            PlayerControl pc = ExplosivistId.GetPlayer();
            RevertFreeze(pc);
        }
    }

    private void RevertFreeze(PlayerControl pc)
    {
        pc.NetTransform.SnapTo(RealPosition, (ushort)(pc.NetTransform.lastSequenceId + 128));
        CustomRpcSender sender = CustomRpcSender.Create("Explosivist Revert", SendOption.Reliable);
        sender.StartMessage();
        sender.StartRpc(pc.NetTransform.NetId, (byte)RpcCalls.SnapTo)
            .WriteVector2(pc.transform.position)
            .Write((ushort)(pc.NetTransform.lastSequenceId + 32767))
            .EndRpc();
        sender.StartRpc(pc.NetTransform.NetId, (byte)RpcCalls.SnapTo)
            .WriteVector2(pc.transform.position)
            .Write((ushort)(pc.NetTransform.lastSequenceId + 32767 + 16383))
            .EndRpc();
        sender.StartRpc(pc.NetTransform.NetId, (byte)RpcCalls.SnapTo)
            .WriteVector2(pc.transform.position)
            .Write(pc.NetTransform.lastSequenceId)
            .EndRpc();
        sender.EndMessage();
        sender.SendMessage();
        pc.Visible = true;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != ExplosivistId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || ExplodeTS == 0) return string.Empty;
        return string.Format(Translator.GetString("ExplosivistSuffix"), ExplodeTS - Utils.TimeStamp);
    }
}