using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class WeaponMaster : RoleBase
{
    private const int Id = 641200;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem Radius;
    private static OptionItem HighKCD;

    private byte Mode;
    private bool shieldUsed;
    private byte WMId;

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

    public override void Init()
    {
        playerIdList = [];
        Mode = 0;
        shieldUsed = false;
        WMId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        WMId = playerId;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;

    void SendRPC()
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetWeaponMasterMode, SendOption.Reliable);
        writer.Write(WMId);
        writer.Write(Mode);
        writer.Write(shieldUsed);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        var id = reader.ReadByte();
        if (Main.PlayerStates[id].Role is not WeaponMaster { IsEnable: true } wm) return;

        wm.Mode = reader.ReadByte();
        wm.shieldUsed = reader.ReadBoolean();
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (Mode == 2) opt.SetInt(Int32OptionNames.KillDistance, 2);
        else opt.SetInt(Int32OptionNames.KillDistance, 0);

        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool CanUseKillButton(PlayerControl pc) => Mode != 3;

    public override void OnPet(PlayerControl pc)
    {
        SwitchMode();
    }

    public override void OnSabotage(PlayerControl pc)
    {
        SwitchMode();
    }

    void SwitchMode()
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
                WM.Notify(GetString("WMShieldAlreadyUsed"));
                break;
        }

        SendRPC();
        WM.MarkDirtySettings();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
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
                        if (Pelican.IsEaten(player.PlayerId) || player == killer || player.Is(CustomRoles.Pestilence) || Main.VeteranInProtect.ContainsKey(target.PlayerId)) continue;
                        if (Vector2.Distance(killer.transform.position, player.transform.position) <= Radius.GetFloat())
                        {
                            player.Suicide(PlayerState.DeathReason.Kill, killer);
                        }
                    }
                    killer.SetKillCooldown(time: HighKCD.GetFloat());
                }, 0.1f, "Weapon Master Axe Kill");
                return true;
            case 2:
                if (killer.RpcCheckAndMurder(target, true))
                {
                    target.Suicide(PlayerState.DeathReason.Kill, killer);
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

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (Mode == 3 && !shieldUsed)
        {
            shieldUsed = true;
            SendRPC();
            return true;
        }

        return false;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Mode == 2)
        {
            _ = new LateTask(() => { pc?.MyPhysics?.RpcBootFromVent(vent.Id); }, 0.5f);
        }
    }

    public static string GetHudAndProgressText(byte id)
    {
        if (Main.PlayerStates[id].Role is not WeaponMaster { IsEnable: true } wm) return string.Empty;
        return string.Format(GetString("WMMode"), ModeToText(wm.Mode));
    }
    public static string ModeToText(byte mode)
    {
        return mode switch
        {
            0 => GetString("Sword"),
            1 => GetString("Axe"),
            2 => GetString("Lance"),
            3 => GetString("Shield"),
            _ => string.Empty,
        };
    }
}
