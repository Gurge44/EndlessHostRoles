using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

internal static class Assassin
{
    private static readonly int Id = 700;
    public static List<byte> playerIdList = [];

    private static OptionItem MarkCooldown;
    public static OptionItem AssassinateCooldown;
    private static OptionItem CanKillAfterAssassinate;

    public static Dictionary<byte, byte> MarkedPlayer = [];
    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Assassin);
        MarkCooldown = FloatOptionItem.Create(Id + 10, "AssassinMarkCooldown", new(0f, 180f, 0.5f), 1f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Assassin])
            .SetValueFormat(OptionFormat.Seconds);
        AssassinateCooldown = FloatOptionItem.Create(Id + 11, "AssassinAssassinateCooldown", new(0f, 180f, 0.5f), 18.5f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Assassin])
            .SetValueFormat(OptionFormat.Seconds);
        CanKillAfterAssassinate = BooleanOptionItem.Create(Id + 12, "AssassinCanKillAfterAssassinate", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Assassin]);
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
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMarkedPlayer, SendOption.Reliable, -1);
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
    private static bool Shapeshifting(this byte id) => Utils.GetPlayerById(id).shapeshifting;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = id.Shapeshifting() ? DefaultKillCooldown : MarkCooldown.GetFloat();
    public static void ApplyGameOptions() => AURoleOptions.ShapeshifterCooldown = AssassinateCooldown.GetFloat();
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
            killer.ResetKillCooldown();
            killer.SyncSettings();
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
            if (UsePets.GetBool()) Main.AssassinCD.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
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
                    pc.TP(target.Pos());
                    pc.ResetKillCooldown();
                    pc.SyncSettings();
                    pc.SetKillCooldown(DefaultKillCooldown);
                    pc.RpcCheckAndMurder(target);
                }
            }, UsePets.GetBool() ? 0.1f : 0.2f, "Assassin Assassinate");
            return;
        }
    }
    public static void SetKillButtonText(byte playerId)
    {
        if (!playerId.Shapeshifting())
            HudManager.Instance.KillButton.OverrideText(GetString("AssassinMarkButtonText"));
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