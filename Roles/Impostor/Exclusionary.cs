using System.Collections.Generic;
using EHR.Modules;
using Hazel;

namespace EHR.Impostor;

public class Exclusionary : RoleBase
{
    public static bool On;

    private static OptionItem ExclusionDelay;
    private static OptionItem ExclusionDuration;
    private static OptionItem CooldownAfterExclusion;

    public override bool IsEnable => On;

    private List<(byte ID, long TS)> ExcludedPlayers;

    public override void SetupCustomOption()
    {
        StartSetup(656800)
            .AutoSetupOption(ref ExclusionDelay, 5, new IntegerValueRule(0, 30, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ExclusionDuration, 10, new IntegerValueRule(0, 90, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref CooldownAfterExclusion, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        ExcludedPlayers = [];
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (ExcludedPlayers.Exists(x => x.ID == target.PlayerId)) return false;
        
        return killer.CheckDoubleTrigger(target, () =>
        {
            killer.SetKillCooldown(CooldownAfterExclusion.GetFloat());

            LateTask.New(() =>
            {
                if (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting || ExileController.Instance || GameStates.IsEnded || GameStates.IsLobby || AntiBlackout.SkipTasks || target == null || !target.IsAlive() || ExcludedPlayers.Exists(x => x.ID == target.PlayerId)) return;
                
                ExcludedPlayers.Add((target.PlayerId, Utils.TimeStamp + ExclusionDuration.GetInt()));
                
                if (target.AmOwner)
                {
                    foreach (PlayerControl player in Main.AllAlivePlayerControls)
                    {
                        if (player.AmOwner) continue;
                        player.SetPet("");
                        player.invisibilityAlpha = 0f;
                        player.cosmetics.SetPhantomRoleAlpha(player.invisibilityAlpha);
                        player.shouldAppearInvisible = true;
                        player.Visible = false;
                    }

                    return;
                }

                if (target.IsModdedClient())
                {
                    var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.Exclusionary, SendOption.Reliable, target.OwnerId);
                    writer.Write(true);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    return;
                }

                var sender = CustomRpcSender.Create("Exclusionary", SendOption.Reliable);
                sender.StartMessage(target.GetClientId());

                foreach (PlayerControl player in Main.AllAlivePlayerControls)
                {
                    if (target == player) continue;

                    if (sender.stream.Length > 500)
                    {
                        Utils.NumSnapToCallsThisRound++;
                        sender.SendMessage();
                        sender = CustomRpcSender.Create("Exclusionary", SendOption.Reliable);
                        sender.StartMessage(target.GetClientId());
                    }

                    sender.StartRpc(player.NetId, RpcCalls.SetPetStr)
                        .Write("")
                        .Write(player.GetNextRpcSequenceId(RpcCalls.SetPetStr))
                        .EndRpc();
                    sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                        .WriteVector2(new Vector2(50f, 50f))
                        .Write(player.NetTransform.lastSequenceId)
                        .EndRpc();
                    sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                        .WriteVector2(new Vector2(50f, 50f))
                        .Write((ushort)(player.NetTransform.lastSequenceId + 16383))
                        .EndRpc();
                }

                sender.SendMessage();
                Utils.NumSnapToCallsThisRound++;
            }, ExclusionDelay.GetInt());
        });
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (!On || lowLoad || !pc.IsAlive() || !ExcludedPlayers.FindFirst(x => x.ID == pc.PlayerId, out var tuple) || tuple.TS > Utils.TimeStamp) return;
        ExcludedPlayers.RemoveAll(x => x.ID == pc.PlayerId);
        RevertExclusion(pc);
    }

    public override void OnReportDeadBody()
    {
        ExcludedPlayers.ForEach(x =>
        {
            var pc = x.ID.GetPlayer();
            if (pc == null || !pc.IsAlive()) return;
            RevertExclusion(pc);
        });
        ExcludedPlayers.Clear();
    }

    private static void RevertExclusion(PlayerControl pc)
    {
        if (pc.AmOwner)
        {
            foreach (PlayerControl player in Main.AllAlivePlayerControls)
            {
                if (player.AmOwner) continue;
                if (Options.UsePets.GetBool()) PetsHelper.SetPet(player, PetsHelper.GetPetId());
                player.shouldAppearInvisible = false;
                player.Visible = true;
                player.invisibilityAlpha = 1f;
                player.cosmetics.SetPhantomRoleAlpha(player.invisibilityAlpha);
                player.shouldAppearInvisible = false;
                player.Visible = !player.inVent;
            }

            return;
        }

        if (pc.IsModdedClient())
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.Exclusionary, SendOption.Reliable, pc.OwnerId);
            writer.Write(false);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            return;
        }
        
        var sender = CustomRpcSender.Create("Exclusionary Revert", SendOption.Reliable);
        sender.StartMessage(pc.GetClientId());

        foreach (PlayerControl player in Main.AllAlivePlayerControls)
        {
            if (pc == player) continue;

            if (sender.stream.Length > 500)
            {
                Utils.NumSnapToCallsThisRound++;
                sender.SendMessage();
                sender = CustomRpcSender.Create("Exclusionary Revert", SendOption.Reliable);
                sender.StartMessage(pc.GetClientId());
            }

            if (Options.UsePets.GetBool())
            {
                sender.StartRpc(player.NetId, RpcCalls.SetPetStr)
                    .Write(PetsHelper.GetPetId())
                    .Write(player.GetNextRpcSequenceId(RpcCalls.SetPetStr))
                    .EndRpc();
            }
            
            sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(new Vector2(50f, 50f))
                .Write((ushort)(player.NetTransform.lastSequenceId + 32767))
                .EndRpc();
            sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(new Vector2(50f, 50f))
                .Write((ushort)(player.NetTransform.lastSequenceId + 32767 + 16383))
                .EndRpc();
            sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(player.transform.position)
                .Write(player.NetTransform.lastSequenceId)
                .EndRpc();
        }
        
        sender.SendMessage();
        Utils.NumSnapToCallsThisRound++;
    }
}