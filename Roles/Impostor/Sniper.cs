using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Impostor;

public class Sniper : RoleBase
{
    private const int Id = 1900;
    private static List<byte> PlayerIdList = [];

    private static OptionItem SniperBulletCount;
    private static OptionItem SniperPrecisionShooting;
    private static OptionItem SniperAimAssist;
    private static OptionItem SniperAimAssistOnshot;
    private static OptionItem ShapeshiftDuration;
    private static OptionItem CanKillWithBullets;

    private static bool MeetingReset;
    private static int MaxBulletCount;
    private static bool PrecisionShooting;
    private static bool AimAssist;
    private static bool AimAssistOneshot;

    public static bool On;
    private float AimTime;
    private int bulletCount;
    public bool IsAim;
    private Vector3 LastPosition;
    private List<byte> shotNotify = [];
    private Vector3 snipeBasePosition;

    private byte snipeTarget;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Sniper);

        SniperBulletCount = new IntegerOptionItem(Id + 10, "SniperBulletCount", new(1, 10, 1), 2, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sniper])
            .SetValueFormat(OptionFormat.Pieces);

        SniperPrecisionShooting = new BooleanOptionItem(Id + 11, "SniperPrecisionShooting", false, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sniper]);

        SniperAimAssist = new BooleanOptionItem(Id + 12, "SniperAimAssist", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sniper]);

        SniperAimAssistOnshot = new BooleanOptionItem(Id + 13, "SniperAimAssistOneshot", false, TabGroup.ImpostorRoles)
            .SetParent(SniperAimAssist);

        CanKillWithBullets = new BooleanOptionItem(Id + 14, "SniperCanKill", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sniper]);

        ShapeshiftDuration = new FloatOptionItem(Id + 15, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sniper])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        Logger.Disable("Sniper");

        PlayerIdList = [];
        On = false;

        snipeBasePosition = new();
        LastPosition = new();
        snipeTarget = 0x7F;
        bulletCount = SniperBulletCount.GetInt();
        shotNotify = [];
        IsAim = false;
        AimTime = 0f;
        MeetingReset = false;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        On = true;

        MaxBulletCount = SniperBulletCount.GetInt();
        PrecisionShooting = SniperPrecisionShooting.GetBool();
        AimAssist = SniperAimAssist.GetBool();
        AimAssistOneshot = SniperAimAssistOnshot.GetBool();

        snipeBasePosition = new();
        LastPosition = new();
        snipeTarget = 0x7F;
        bulletCount = MaxBulletCount;
        shotNotify = [];
        IsAim = false;
        AimTime = 0f;
        MeetingReset = false;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private static bool IsSniper(byte playerId)
    {
        return PlayerIdList.Contains(playerId);
    }

    private void SendRPC(byte sniperId)
    {
        if (!On || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SniperSync, HazelExtensions.SendOption);
        writer.Write(sniperId);
        writer.Write(shotNotify.Count);
        foreach (byte sn in shotNotify) writer.Write(sn);

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(MessageReader msg)
    {
        shotNotify.Clear();
        int count = msg.ReadInt32();

        while (count > 0)
        {
            shotNotify.Add(msg.ReadByte());
            count--;
        }
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        if (!pc.IsAlive()) return false;

        var canUse = false;
        if (pc.IsShifted()) return false;

        if (bulletCount <= 0) canUse = true;

        if (CanKillWithBullets.GetBool()) canUse = true;

        return canUse;
    }

    private Dictionary<PlayerControl, float> GetSnipeTargets(PlayerControl sniper)
    {
        var targets = new Dictionary<PlayerControl, float>();
        Vector3 snipeBasePos = snipeBasePosition;
        Vector3 snipePos = sniper.transform.position;
        Vector3 dir = (snipePos - snipeBasePos).normalized;

        snipePos -= dir;

        foreach (PlayerControl target in Main.AllAlivePlayerControls)
        {
            if (target.PlayerId == sniper.PlayerId) continue;

            Vector3 target_pos = target.transform.position - snipePos;
            if (target_pos.magnitude < 1) continue;

            Vector3 target_dir = target_pos.normalized;
            float target_dot = Vector3.Dot(dir, target_dir);
            if (target_dot < 0.995) continue;

            if (PrecisionShooting)
            {
                float err = Vector3.Cross(dir, target_pos).magnitude;
                if (err < 0.5) targets.Add(target, err);
            }
            else
            {
                float err = target_pos.magnitude;
                targets.Add(target, err);
            }
        }

        return targets;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        return Options.UseUnshiftTrigger.GetBool() ? Snipe(shapeshifter, !IsAim) : Snipe(shapeshifter, shapeshifting);
    }

    public override void OnPet(PlayerControl pc)
    {
        Snipe(pc, !IsAim, true);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        Snipe(pc, !IsAim, true);
        return false;
    }

    private bool Snipe(PlayerControl sniper, bool shapeshifting, bool isPet = false)
    {
        if (!IsSniper(sniper.PlayerId) || !sniper.IsAlive()) return true;

        byte sniperId = sniper.PlayerId;

        if (bulletCount <= 0)
        {
            float CD = ShapeshiftDuration.GetFloat() + 1f;
            if (Main.KillTimers[sniper.PlayerId] < CD && !isPet) sniper.SetKillCooldown(CD);

            return false;
        }

        if (shapeshifting)
        {
            MeetingReset = false;

            snipeBasePosition = sniper.transform.position;

            LastPosition = sniper.transform.position;
            IsAim = true;
            AimTime = 0f;

            return false;
        }

        IsAim = false;
        AimTime = 0f;

        if (MeetingReset)
        {
            MeetingReset = false;
            return false;
        }

        bulletCount--;

        if (!AmongUsClient.Instance.AmHost || Pelican.IsEaten(sniperId) || Medic.ProtectList.Contains(sniperId)) return false;

        sniper.RPCPlayCustomSound("AWP");

        Dictionary<PlayerControl, float> targets = GetSnipeTargets(sniper);

        if (targets.Count > 0)
        {
            PlayerControl snipedTarget = targets.MinBy(c => c.Value).Key;
            snipeTarget = snipedTarget.PlayerId;
            snipedTarget.CheckMurder(snipedTarget);
            sniper.SetKillCooldown();
            snipeTarget = 0x7F;

            targets.Remove(snipedTarget);
            List<byte> snList = shotNotify;
            snList.Clear();

            foreach (PlayerControl otherPc in targets.Keys)
            {
                snList.Add(otherPc.PlayerId);
                Utils.NotifyRoles(SpecifySeer: otherPc);
            }

            SendRPC(sniperId);

            LateTask.New(() =>
            {
                snList.Clear();
                foreach (PlayerControl otherPc in targets.Keys) Utils.NotifyRoles(SpecifySeer: otherPc);

                SendRPC(sniperId);
            }, 0.5f, "Sniper shot Notify");
        }

        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = bulletCount > 0 ? Options.DefaultKillCooldown : 255f;
        else
        {
            if (Options.UsePets.GetBool()) return;

            try
            {
                if (bulletCount > 0)
                    AURoleOptions.ShapeshifterDuration = ShapeshiftDuration.GetFloat();
                else
                {
                    AURoleOptions.ShapeshifterDuration = 1f;
                    AURoleOptions.ShapeshifterCooldown = 255f;
                }
            }
            catch { }
        }
    }

    public override void OnFixedUpdate(PlayerControl sniper)
    {
        if (!IsSniper(sniper.PlayerId) || !sniper.IsAlive()) return;

        if (!AimAssist) return;

        if (!IsAim) return;

        if (!GameStates.IsInTask)
        {
            IsAim = false;
            AimTime = 0f;
            return;
        }

        Vector3 pos = sniper.transform.position;

        if (pos != LastPosition)
        {
            AimTime = 0f;
            LastPosition = pos;
        }
        else
        {
            AimTime += Time.fixedDeltaTime;
            Utils.NotifyRoles(SpecifySeer: sniper, SpecifyTarget: sniper);
        }
    }

    public override void OnReportDeadBody()
    {
        MeetingReset = true;
    }

    public static bool TryGetSniper(byte targetId, ref PlayerControl sniper)
    {
        foreach (KeyValuePair<byte, PlayerState> state in Main.PlayerStates)
        {
            if (state.Value.Role is Sniper { IsEnable: true } sp)
            {
                if (sp.snipeTarget == targetId)
                {
                    sniper = Utils.GetPlayerById(state.Key);
                    return true;
                }
            }
        }

        return false;
    }

    public static string GetShotNotify(byte seerId)
    {
        if (AimAssist && IsSniper(seerId))
        {
            if (Main.PlayerStates[seerId].Role is not Sniper sp) return string.Empty;

            if (0.5f < sp.AimTime && (!AimAssistOneshot || sp.AimTime < 1.0f))
                if (sp.GetSnipeTargets(Utils.GetPlayerById(seerId)).Count > 0)
                    return $"<size=200%>{Utils.ColorString(Palette.ImpostorRed, "◎")}</size>";
        }
        else
        {
            foreach (byte sniperId in PlayerIdList)
            {
                if (Main.PlayerStates[sniperId].Role is not Sniper sp) continue;

                List<byte> snList = sp.shotNotify;
                if (snList.Count > 0 && snList.Contains(seerId)) return $"<size=200%>{Utils.ColorString(Palette.ImpostorRed, "!")}</size>";
            }
        }

        return string.Empty;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (Options.UsePets.GetBool())
            hud.PetButton.OverrideText(GetString(bulletCount <= 0 ? "DefaultShapeshiftText" : "SniperSnipeButtonText"));
        else
        {
            if (IsSniper(id)) hud.AbilityButton.SetUsesRemaining(bulletCount);

            hud.AbilityButton.OverrideText(GetString(bulletCount <= 0 ? "DefaultShapeshiftText" : "SniperSnipeButtonText"));
        }
    }
}