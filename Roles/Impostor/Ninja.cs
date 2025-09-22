using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

internal class Ninja : RoleBase
{
    private const int Id = 700;
    public static List<byte> PlayerIdList = [];

    private static OptionItem MarkCooldownOpt;
    public static OptionItem AssassinateCooldownOpt;
    private static OptionItem CanKillAfterAssassinateOpt;
    private static OptionItem InvisibilityTimeAfterAssassinateOpt;

    private float AssassinateCooldown;
    private bool CanKillAfterAssassinate;
    private bool IsUndertaker;
    private float MarkCooldown;
    public byte MarkedPlayer;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Ninja);

        MarkCooldownOpt = new FloatOptionItem(Id + 10, "NinjaMarkCooldown", new(0f, 180f, 0.5f), 1f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ninja])
            .SetValueFormat(OptionFormat.Seconds);

        AssassinateCooldownOpt = new FloatOptionItem(Id + 11, "NinjaAssassinateCooldown", new(0f, 180f, 0.5f), 18.5f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ninja])
            .SetValueFormat(OptionFormat.Seconds);

        CanKillAfterAssassinateOpt = new BooleanOptionItem(Id + 12, "NinjaCanKillAfterAssassinate", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ninja]);

        InvisibilityTimeAfterAssassinateOpt = new FloatOptionItem(Id + 13, "NinjaInvisibilityTimeAfterAssassinate", new(0f, 30f, 0.5f), 5f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ninja])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        MarkedPlayer = byte.MaxValue;
        IsUndertaker = false;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
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

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private void SendRPC(byte playerId)
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

        if (Main.PlayerStates[playerId].Role is not Ninja ninja) return;

        ninja.MarkedPlayer = targetId;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = id.IsPlayerShifted() ? AdjustedDefaultKillCooldown : MarkCooldown;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = AssassinateCooldown;
        else
        {
            if (UsePets.GetBool()) return;

            AURoleOptions.ShapeshifterCooldown = AssassinateCooldown;
        }
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return false;
        return CanKillAfterAssassinate || (!pc.IsShifted() && (pc.Data.Role as PhantomRole) is null or { IsInvisible: false });
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.IsShifted())
        {
            bool canUseKillButton = CanUseKillButton(killer);

            if (canUseKillButton)
            {
                killer.ResetKillCooldown();
                killer.SyncSettings();

                if (Main.Invisible.Contains(killer.PlayerId) && !target.Is(CustomRoles.Bait))
                {
                    if (!killer.RpcCheckAndMurder(target, true)) return false;

                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    target.Suicide(PlayerState.DeathReason.Swooped, killer);
                    killer.SetKillCooldown();
                    return false;
                }
            }

            return canUseKillButton;
        }

        MarkedPlayer = target.PlayerId;
        SendRPC(killer.PlayerId);
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        if (killer.IsModdedClient()) killer.RpcResetAbilityCooldown();
        if (UsePets.GetBool()) killer.AddAbilityCD(includeDuration: false);

        killer.SyncSettings();
        killer.RPCPlayCustomSound("Clothe");
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        Take(pc);
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl t, bool shapeshifting)
    {
        if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return false;

        if (!shapeshifting) return true;

        Take(pc);

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return false;

        if (!IsUndertaker)
        {
            float time = InvisibilityTimeAfterAssassinateOpt.GetFloat();

            if (time >= 1f)
            {
                pc.RpcMakeInvisible();

                LateTask.New(() =>
                {
                    if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
                    pc.RpcMakeVisible();
                }, time, log: false);
            }
        }

        Take(pc);
        return false;
    }

    private void Take(PlayerControl pc)
    {
        if (MarkedPlayer != byte.MaxValue)
        {
            PlayerControl target = Utils.GetPlayerById(MarkedPlayer);
            bool tpSuccess = IsUndertaker ? target.TP(pc) : pc.TP(target);

            if (!(target == null || !target.IsAlive() || Pelican.IsEaten(target.PlayerId) || target.inVent || !GameStates.IsInTask) && tpSuccess && pc.RpcCheckAndMurder(target))
            {
                MarkedPlayer = byte.MaxValue;
                SendRPC(pc.PlayerId);
                pc.ResetKillCooldown();
                pc.SyncSettings();
                pc.SetKillCooldown(AdjustedDefaultKillCooldown);
            }
            else if (!tpSuccess) pc.Notify(GetString("TargetCannotBeTeleported"));
        }
    }

    public override void SetButtonTexts(HudManager __instance, byte playerId)
    {
        bool shifted = playerId.IsPlayerShifted();
        __instance.KillButton.OverrideText(!shifted ? GetString("AssassinMarkButtonText") : GetString("KillButtonText"));

        if (MarkedPlayer != byte.MaxValue && !shifted)
        {
            if (!UsePets.GetBool())
                __instance.AbilityButton.OverrideText(GetString("AssassinShapeshiftText"));
            else
                __instance.PetButton.OverrideText(GetString("AssassinShapeshiftText"));
        }
    }
}