using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class WeaponMaster
{
    private static readonly int Id = 641200;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem Radius;
    private static OptionItem HighKCD;

    private static byte Mode;
    private static bool shieldUsed;

    /*
     * 0 = Kill (Sword) ~ Normal Kill
     * 1 = TOHEN Werewolf Kill / Higher KCD (Axe)
     * 2 = Reach + Swift / Can't Vent (Lance)
     * 3 = 1-Time Shield / Can't Kill (Shield)
     */

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.WeaponMaster, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster]);
        Radius = FloatOptionItem.Create(Id + 12, "WMRadius", new(0f, 10f, 0.25f), 2f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster])
            .SetValueFormat(OptionFormat.Multiplier);
        HighKCD = FloatOptionItem.Create(Id + 14, "GamblerHighKCD", new(0f, 180f, 2.5f), 35f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = [];
        Mode = 0;
        shieldUsed = false;
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SendRPC(byte mode, bool shieldUsed)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetWeaponMasterMode, SendOption.Reliable, -1);
        writer.Write(mode);
        writer.Write(shieldUsed);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        Mode = reader.ReadByte();
        shieldUsed = reader.ReadBoolean();
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt)
    {
        if (Mode == 2) opt.SetInt(Int32OptionNames.KillDistance, 2);
        else opt.SetInt(Int32OptionNames.KillDistance, 0);

        opt.SetVision(HasImpostorVision.GetBool());
    }

    public static bool CanKill(PlayerControl pc) => Mode != 3;
    public static void SwitchMode()
    {
        var id = playerIdList[0];
        var WM = Utils.GetPlayerById(id);

        if (WM == null || !WM.IsAlive()) return;

        if (Mode == 3) Mode = 0;
        else Mode++;

        switch (Mode)
        {
            case 1:
                Main.AllPlayerKillCooldown[id] = HighKCD.GetFloat();
                break;
            case 2:
                Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
                break;
            case 3 when shieldUsed:
                WM.Notify(Translator.GetString("WMShieldAlreadyUsed"));
                break;
        }

        SendRPC(Mode, shieldUsed);
        WM.MarkDirtySettings();
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;
        if (target == null) return false;

        switch (Mode)
        {
            case 0:
                return true;
            case 1:
                _ = new LateTask(() =>
                {
                    foreach (PlayerControl player in Main.AllAlivePlayerControls)
                    {
                        if (Pelican.IsEaten(player.PlayerId)) continue;
                        if (player == killer) continue;
                        if (player.Is(CustomRoles.Pestilence) || Main.VeteranInProtect.ContainsKey(target.PlayerId)) continue;
                        if (Vector2.Distance(killer.transform.position, player.transform.position) <= Radius.GetFloat())
                        {
                            player.SetRealKiller(killer);
                            player.Kill(player);
                        }
                    }
                    killer.SetKillCooldown(time: HighKCD.GetFloat());
                }, 0.1f, "Weapon Master Axe Kill");
                return true;
            case 2:
                if (killer.RpcCheckAndMurder(target, true))
                {
                    target.Kill(target);
                    killer.SetKillCooldown();
                }
                return false;
            case 3:
                return false;
            default:
                Logger.Error("Invalid Mode", "WeaponMaster");
                return true;
        }
    }
    public static bool OnAttack(PlayerControl killer, PlayerControl target)
    {
        if (Mode == 3 && !shieldUsed)
        {
            shieldUsed = true;
            SendRPC(Mode, shieldUsed);
            return true;
        }
        else return false;
    }
    public static void OnEnterVent(PlayerControl pc, int ventId)
    {
        if (Mode == 2)
        {
            _ = new LateTask(() =>
            {
                pc?.MyPhysics?.RpcBootFromVent(ventId);
            }, 0.5f);
        }
    }
    public static string GetHudAndProgressText()
    {
        return $"<color=#00ffa5>Mode:</color> <color=#ffffff><b>{ModeToText(Mode)}</b></color>";
    }
    public static string ModeToText(byte mode)
    {
        return mode switch
        {
            0 => "Sword",
            1 => "Axe",
            2 => "Lance",
            3 => "Shield",
            _ => string.Empty,
        };
    }
}
