using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

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

    public override bool IsEnable => playerIdList.Count > 0;

    /*
     * 0 = Kill (Sword) ~ Normal Kill
     * 1 = TOHEN Werewolf Kill / Higher KCD (Axe)
     * 2 = Reach + Swift / Can't Vent (Lance)
     * 3 = 1-Time Shield / Can't Kill (Shield)
     */

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.WeaponMaster);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster]);
        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster]);
        Radius = new FloatOptionItem(Id + 12, "WMRadius", new(0f, 10f, 0.1f), 2f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster])
            .SetValueFormat(OptionFormat.Multiplier);
        HighKCD = new FloatOptionItem(Id + 14, "GamblerHighKCD", new(0f, 180f, 2.5f), 35f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.WeaponMaster])
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

        Mode = 0;
        shieldUsed = false;
    }

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
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override bool CanUseSabotage(PlayerControl pc) => pc.IsAlive() && !(UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool());

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetInt(Int32OptionNames.KillDistance, Mode == 2 ? 2 : 0);
        opt.SetVision(HasImpostorVision.GetBool());
        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
            AURoleOptions.PhantomCooldown = 1f;
        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
            AURoleOptions.ShapeshifterCooldown = 1f;
    }

    public override bool CanUseKillButton(PlayerControl pc) => Mode != 3;

    public override void OnPet(PlayerControl pc)
    {
        SwitchMode();
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        SwitchMode();
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        SwitchMode();
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;
        SwitchMode();
        return false;
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
                LateTask.New(() =>
                {
                    foreach (PlayerControl player in Main.AllAlivePlayerControls)
                    {
                        if (Pelican.IsEaten(player.PlayerId) || player == killer || player.Is(CustomRoles.Pestilence) || Veteran.VeteranInProtect.ContainsKey(target.PlayerId)) continue;
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
            return false;
        }

        return true;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Mode == 2)
        {
            pc?.MyPhysics?.RpcBootFromVent(vent.Id);
        }
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return !playerId.IsPlayerModClient() ? GetHudAndProgressText(playerId) : string.Empty;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
    {
        return isHUD ? GetHudAndProgressText(seer.PlayerId) : string.Empty;
    }

    static string GetHudAndProgressText(byte id)
    {
        return Main.PlayerStates[id].Role is not WeaponMaster { IsEnable: true } wm ? string.Empty : string.Format(GetString("WMMode"), ModeToText(wm.Mode));
    }

    public static string ModeToText(byte mode)
    {
        return mode switch
        {
            0 => GetString("Sword"),
            1 => GetString("Axe"),
            2 => GetString("Lance"),
            3 => GetString("Shield"),
            _ => string.Empty
        };
    }
}