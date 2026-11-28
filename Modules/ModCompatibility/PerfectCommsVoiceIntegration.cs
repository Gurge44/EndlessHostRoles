#if IL2CPP
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Unity.IL2CPP;
using EHR.Roles;
using PerfectComms.Api;

namespace EHR;

internal static class PerfectCommsVoiceIntegration
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Register()
    {
        PerfectCommsApi.RegisterVoiceRule(Main.PluginGuid, ctx =>
        {
            if (ChatCommands.CheckMute(ctx.Player.PlayerId)) return VoiceRuleResult.Mute("Muted");
            
            if (!Main.PlayerStates.TryGetValue(ctx.Player.PlayerId, out PlayerState playerState) || playerState.IsDead) return VoiceRuleResult.Pass;
            
            return ctx.Phase switch
            {
                VoicePhaseKind.Lobby => VoiceRuleResult.Pass,
                VoicePhaseKind.Meeting or VoicePhaseKind.Exile when Silencer.ForSilencer.Contains(ctx.Player.PlayerId) => VoiceRuleResult.Mute("Silenced"),
                VoicePhaseKind.Tasks when Main.Invisible.Contains(ctx.Player.PlayerId) => VoiceRuleResult.Mute("Invisible"),
                VoicePhaseKind.Tasks when Camouflage.IsCamouflage => VoiceRuleResult.Mute("Camouflage"),
                VoicePhaseKind.Tasks when Stasis.IsTimeFrozen => VoiceRuleResult.Mute("Time Frozen"),
                VoicePhaseKind.Tasks when Devourer.PlayerIdList.Count > 0 && Main.PlayerStates.Values.Any(x => x.Role is Devourer devourer && devourer.PlayerSkinsCosumed.Contains(ctx.Player.PlayerId)) => VoiceRuleResult.Mute("Devoured"),
                VoicePhaseKind.Tasks when playerState.Role is Venerer { ChangedSkin: true } => VoiceRuleResult.Mute("Venerer Camouflaged"),
                VoicePhaseKind.Tasks when Shadow.On && Main.PlayerStates.Values.Any(x => x.Role is Shadow shadow && shadow.IsCamouflaged(ctx.Player.PlayerId)) => VoiceRuleResult.Mute("Shadow Camouflaged"),
                _ => VoiceRuleResult.Pass
            };
        });
        
        PerfectCommsApi.RegisterManagedRadioChannel(Main.PluginGuid, ctx =>
        {
            if (ctx.Phase == VoicePhaseKind.Lobby || Main.LoversPlayers.Count != 2 || Main.IsLoversDead) return null;

            PlayerControl first = Main.LoversPlayers[0];
            PlayerControl second = Main.LoversPlayers[1];

            if (!first || !second) return null;

            bool thisIsFirst = first.PlayerId == ctx.Player.PlayerId;
            if (!thisIsFirst && second.PlayerId != ctx.Player.PlayerId) return null;

            return new VoiceManagedRadioChannelResult(
                Key: $"lovers:{(thisIsFirst ? second.PlayerId : first.PlayerId)}",
                Label: "Lovers",
                Badge: "L");
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Unregister() => PerfectCommsApi.Unregister(Main.PluginGuid);
}
#endif