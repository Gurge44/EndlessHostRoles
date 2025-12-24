using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;

namespace EHR.Neutral;

public class SoulCollector : RoleBase
{
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;

    public override bool IsEnable => On;

    public List<byte> ToExile = [];

    public override void SetupCustomOption()
    {
        StartSetup(657400)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        ToExile = [];
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

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!killer.RpcCheckAndMurder(target, check: true)) return false;
        
        ToExile.Add(target.PlayerId);
        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
        
        if (Main.PlayerStates.TryGetValue(target.PlayerId, out PlayerState state))
        {
            state.deathReason = PlayerState.DeathReason.Kill;
            state.SetDead();
            target.SetRealKiller(killer);
        }

        if (killer.AmOwner)
        {
            Logger.Info("Host MurderPlayer & SetRole", "SoulCollector");
            killer.MurderPlayer(target, MurderResultFlags.Succeeded);
            target.SetRole(RoleTypes.CrewmateGhost);
        }
        else
        {
            var sender = CustomRpcSender.Create("SoulCollector Kill: Killer", SendOption.Reliable);
            sender.StartMessage(killer.OwnerId);
            sender.StartRpc(killer.NetId, RpcCalls.MurderPlayer)
                .WriteNetObject(target)
                .Write((int)MurderResultFlags.Succeeded)
                .EndRpc();
            sender.StartRpc(target.NetId, RpcCalls.SetRole)
                .Write((ushort)RoleTypes.CrewmateGhost)
                .Write(true)
                .EndRpc();
            sender.SendMessage();
        }

        if (target.AmOwner)
        {
            Logger.Info("Host Exiled", "SoulCollector");
            target.Exiled();
        }
        else
        {
            
            var sender = CustomRpcSender.Create("SoulCollector Kill: Target", SendOption.Reliable);
            sender.AutoStartRpc(target.NetId, RpcCalls.Exiled, target.OwnerId)
                .EndRpc();
            sender.SendMessage();
        }

        target.MarkDirtySettings();
        return false;
    }

    public override void OnReportDeadBody()
    {
        if (ToExile.Count > 0)
        {
            var sender = CustomRpcSender.Create("SoulCollector Post Exile", SendOption.Reliable);
            sender.StartMessage();
            List<PlayerControl> toExile = ToExile.ToValidPlayers();
            ToExile = [];
            toExile.ForEach(x =>
            {
                Main.AllPlayerSpeed[x.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                x.Exiled();
                sender.StartRpc(x.NetId, RpcCalls.Exiled)
                    .EndRpc();
            });
            sender.SendMessage();
        }
    }
}