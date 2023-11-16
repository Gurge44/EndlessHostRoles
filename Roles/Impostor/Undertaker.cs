using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

internal static class Undertaker
{
    private static readonly int Id = 750;
    public static List<byte> playerIdList = [];

    private static OptionItem MarkCooldown;
    public static OptionItem AssassinateCooldown;
    private static OptionItem CanKillAfterAssassinate;

    public static Dictionary<byte, byte> MarkedPlayer = [];
    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Undertaker);
        MarkCooldown = FloatOptionItem.Create(Id + 10, "UndertakerMarkCooldown", new(0f, 180f, 0.5f), 1f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker])
            .SetValueFormat(OptionFormat.Seconds);
        AssassinateCooldown = FloatOptionItem.Create(Id + 11, "UndertakerAssassinateCooldown", new(0f, 180f, 0.5f), 18.5f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker])
            .SetValueFormat(OptionFormat.Seconds);
        CanKillAfterAssassinate = BooleanOptionItem.Create(Id + 12, "UndertakerCanKillAfterAssassinate", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker]);
    }
    public static void Init()
    {
        playerIdList = [];
        MarkedPlayer = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Any();
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMarkedPlayerV2, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(MarkedPlayer.ContainsKey(playerId) ? MarkedPlayer[playerId] : byte.MaxValue);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        byte targetId = reader.ReadByte();

        MarkedPlayer.Remove(playerId);
        if (targetId != byte.MaxValue)
            MarkedPlayer.Add(playerId, targetId);
    }
    private static bool Shapeshifting(this PlayerControl pc) => pc.PlayerId.Shapeshifting();
    private static bool Shapeshifting(this byte id) => Main.CheckShapeshift.TryGetValue(id, out bool shapeshifting) && shapeshifting;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = id.Shapeshifting() ? DefaultKillCooldown : MarkCooldown.GetFloat();
    public static void ApplyGameOptions()
    {
        AURoleOptions.ShapeshifterCooldown = AssassinateCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }
    public static bool CanUseKillButton(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return false;
        if (!CanKillAfterAssassinate.GetBool() && pc.shapeshifting) return false;
        return true;
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.Shapeshifting())
        {
            return CanUseKillButton(killer);
        }
        else
        {
            MarkedPlayer.Remove(killer.PlayerId);
            MarkedPlayer.Add(killer.PlayerId, target.PlayerId);
            SendRPC(killer.PlayerId);
            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            if (killer.IsModClient()) killer.RpcResetAbilityCooldown();
            if (UsePets.GetBool()) Main.UndertakerCD.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
            killer.SyncSettings();
            killer.RPCPlayCustomSound("Clothe");
            return false;
        }
    }
    public static void OnShapeshift(PlayerControl pc, bool shapeshifting)
    {
        if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId) || Medic.ProtectList.Contains(pc.PlayerId)) return;
        if (!shapeshifting)
        {
            return;
        }
        if (MarkedPlayer.ContainsKey(pc.PlayerId))
        {
            var target = Utils.GetPlayerById(MarkedPlayer[pc.PlayerId]);
            MarkedPlayer.Remove(pc.PlayerId);
            SendRPC(pc.PlayerId);
            _ = new LateTask(() =>
            {
                if (!(target == null || !target.IsAlive() || Pelican.IsEaten(target.PlayerId) || target.inVent || !GameStates.IsInTask))
                {
                    target.TP(new UnityEngine.Vector2(pc.transform.position.x, pc.transform.position.y + 0.3636f));
                    pc.ResetKillCooldown();
                    pc.SyncSettings();
                    pc.SetKillCooldown();
                    pc.RpcCheckAndMurder(target);
                }
            }, UsePets.GetBool() ? 0.1f : 0.2f, "Undertaker Assassinate");
        }
    }
    public static void SetKillButtonText(byte playerId)
    {
        if (!playerId.Shapeshifting())
            HudManager.Instance.KillButton.OverrideText(GetString("UndertakerMarkButtonText"));
        else
            HudManager.Instance.KillButton.OverrideText(GetString("KillButtonText"));
    }
    public static void GetAbilityButtonText(HudManager __instance, byte playerId)
    {
        if (MarkedPlayer.ContainsKey(playerId) && !playerId.Shapeshifting())
            if (!UsePets.GetBool()) __instance.AbilityButton.OverrideText(GetString("AssassinShapeshiftText"));
            else __instance.PetButton.OverrideText(GetString("AssassinShapeshiftText"));
    }
}