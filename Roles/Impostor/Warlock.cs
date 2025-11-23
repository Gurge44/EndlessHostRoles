using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using UnityEngine;
using static EHR.Options;

namespace EHR.Impostor;

internal class Warlock : RoleBase
{
    public static bool On;

    private static OptionItem WarlockCanKillAllies;
    private static OptionItem WarlockCanKillSelf;
    private static OptionItem KillCooldown;
    private static OptionItem CurseCooldown;
    public static OptionItem ShapeshiftCooldown;
    private static OptionItem FreezeAfterCurseKill;
    private static OptionItem FreezeDurationAfterCurseKill;

    public static Dictionary<byte, float> WarlockTimer = [];
    public static Dictionary<byte, PlayerControl> CursedPlayers = [];
    public static Dictionary<byte, bool> IsCurseAndKill = [];
    public static bool IsCursed;
    private float CurseCD;

    private float KCD;
    private long LastNotify;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(4600, TabGroup.ImpostorRoles, CustomRoles.Warlock);

        WarlockCanKillAllies = new BooleanOptionItem(4610, "CanKillAllies", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock]);

        WarlockCanKillSelf = new BooleanOptionItem(4611, "CanKillSelf", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock]);

        KillCooldown = new FloatOptionItem(4613, "KillCooldown", new(0f, 180f, 1f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock])
            .SetValueFormat(OptionFormat.Seconds);

        CurseCooldown = new FloatOptionItem(4614, "CurseCooldown", new(0f, 180f, 1f), 15f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock])
            .SetValueFormat(OptionFormat.Seconds);

        ShapeshiftCooldown = new FloatOptionItem(4612, "ShapeshiftCooldown", new(0f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock])
            .SetValueFormat(OptionFormat.Seconds);

        FreezeAfterCurseKill = new BooleanOptionItem(4615, "FreezeAfterCurseKill", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock]);

        FreezeDurationAfterCurseKill = new FloatOptionItem(4616, "FreezeDuration", new(0f, 60f, 1f), 4f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Warlock])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add(byte playerId)
    {
        On = true;
        CursedPlayers[playerId] = null;
        IsCurseAndKill[playerId] = false;
        KCD = KillCooldown.GetFloat();
        CurseCD = CurseCooldown.GetFloat();
        LastNotify = 0;
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        try
        {
            if (UsePhantomBasis.GetBool())
                AURoleOptions.PhantomCooldown = IsCursed ? 1f : ShapeshiftCooldown.GetFloat();
            else
            {
                if (UsePets.GetBool()) return;

                AURoleOptions.ShapeshifterCooldown = IsCursed ? 1f : ShapeshiftCooldown.GetFloat();
                AURoleOptions.ShapeshifterDuration = 1f;
            }
        }
        catch { }
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = 1f;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        bool curse = IsCurseAndKill.TryGetValue(id, out bool wcs) && wcs;
        bool shapeshifting = id.IsPlayerShifted();

        if (!shapeshifting && !curse)
            hud.KillButton?.OverrideText(Translator.GetString("WarlockCurseButtonText"));
        else
            hud.KillButton?.OverrideText(Translator.GetString("KillButtonText"));

        if (!shapeshifting && curse) hud.AbilityButton?.OverrideText(Translator.GetString("WarlockShapeshiftButtonText"));
    }

    private void ResetCooldowns(bool killCooldown = false, bool curseCooldown = false, bool shapeshiftCooldown = false, PlayerControl warlock = null)
    {
        if (killCooldown) KCD = KillCooldown.GetFloat();

        if (curseCooldown) CurseCD = CurseCooldown.GetFloat();

        if (warlock == null) return;

        if (shapeshiftCooldown)
        {
            if (!UsePets.GetBool())
                warlock.RpcResetAbilityCooldown();
            else
                warlock.AddAbilityCD();
        }

        if (KCD > 0f && CurseCD > 0f) LateTask.New(() => { warlock.SetKillCooldown(Math.Min(KCD, CurseCD) - 1f); }, 0.1f, log: false);

        Utils.NotifyRoles(SpecifySeer: warlock, SpecifyTarget: warlock);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (target.Is(CustomRoles.LazyGuy) || target.Is(CustomRoles.Lazy) || Thanos.IsImmune(target)) return false;
        
        if (killer.IsShifted()) return false;

        if (killer.CheckDoubleTrigger(target, () =>
        {
            if (CurseCD > 0f) return;

            IsCurseAndKill.TryAdd(killer.PlayerId, false);

            if (!killer.IsShifted() && !IsCurseAndKill[killer.PlayerId])
            {
                if (target.Is(CustomRoles.LazyGuy) || target.Is(CustomRoles.Lazy)) return;

                IsCursed = true;
                killer.SetKillCooldown();
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                killer.RPCPlayCustomSound("Line");
                CursedPlayers[killer.PlayerId] = target;
                WarlockTimer[killer.PlayerId] = 0f;
                IsCurseAndKill[killer.PlayerId] = true;

                ResetCooldowns(true, true, warlock: killer);

                return;
            }

            if (IsCurseAndKill[killer.PlayerId]) killer.RpcGuardAndKill(target);
        }))
        {
            if (KCD > 0f) return false;

            ResetCooldowns(true, true, true, killer);

            return true;
        }

        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        Curse(pc);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        Curse(shapeshifter);

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        Curse(pc);
        return false;
    }

    private void Curse(PlayerControl pc)
    {
        IsCurseAndKill.TryAdd(pc.PlayerId, false);

        if (CursedPlayers.TryGetValue(pc.PlayerId, out PlayerControl cp))
        {
            if (cp == null) return;

            if (cp.IsAlive())
            {
                Vector2 cppos = cp.Pos();
                Dictionary<PlayerControl, float> cpdistance = [];

                foreach (PlayerControl p in Main.AllAlivePlayerControls)
                {
                    if (p.PlayerId == cp.PlayerId) continue;

                    if (!WarlockCanKillSelf.GetBool() && p.PlayerId == pc.PlayerId) continue;

                    if (!WarlockCanKillAllies.GetBool() && p.GetCustomRole().IsImpostor()) continue;

                    if (p.Is(CustomRoles.Pestilence)) continue;

                    if (Pelican.IsEaten(p.PlayerId) || Medic.ProtectList.Contains(p.PlayerId)) continue;

                    float dis = Vector2.Distance(cppos, p.Pos());
                    cpdistance[p] = dis;
                    Logger.Info($"{p.Data?.PlayerName}'s distance: {dis}", "Warlock");
                }

                if (cpdistance.Count > 0)
                {
                    KeyValuePair<PlayerControl, float> min = cpdistance.OrderBy(c => c.Value).FirstOrDefault();
                    PlayerControl targetw = min.Key;

                    if (cp.RpcCheckAndMurder(targetw, true))
                    {
                        ResetCooldowns(true, true, true, pc);

                        targetw.SetRealKiller(pc);
                        Logger.Info($"{targetw.GetNameWithRole().RemoveHtmlTags()} was killed", "Warlock");
                        cp.Kill(targetw);
                        pc.Notify(Translator.GetString("WarlockControlKill"));
                        RPC.PlaySoundRPC(pc.PlayerId, Sounds.KillSound);

                        if (FreezeAfterCurseKill.GetBool())
                        {
                            float speed = Main.AllPlayerSpeed[pc.PlayerId];
                            Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                            pc.MarkDirtySettings();

                            LateTask.New(() =>
                            {
                                Main.AllPlayerSpeed[pc.PlayerId] = speed;
                                pc.MarkDirtySettings();
                            }, FreezeDurationAfterCurseKill.GetFloat(), "Warlock Freeze");
                        }
                    }

                    if (!UsePets.GetBool()) LateTask.New(() => { pc.RpcShapeshift(pc, false); }, 1.5f, "Warlock RpcRevertShapeshift");
                }
                else
                    pc.Notify(Translator.GetString("WarlockNoTarget"));

                IsCurseAndKill[pc.PlayerId] = false;
            }
        }

        CursedPlayers[pc.PlayerId] = null;
    }

    public override void OnGlobalFixedUpdate(PlayerControl player, bool lowLoad)
    {
        byte playerId = player.PlayerId;

        if (GameStates.IsInTask && WarlockTimer.ContainsKey(playerId))
        {
            if (player.IsAlive())
            {
                if (WarlockTimer[playerId] >= 1f)
                {
                    player.RpcResetAbilityCooldown();
                    IsCursed = false;
                    player.MarkDirtySettings();
                    WarlockTimer.Remove(playerId);
                }
                else
                    WarlockTimer[playerId] += Time.fixedDeltaTime;
            }
            else
                WarlockTimer.Remove(playerId);
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || !pc.IsAlive() || pc.inVent) return;

        float beforeKCD = KCD;
        float beforeCCD = CurseCD;

        if (KCD > 0f) KCD -= Time.fixedDeltaTime;

        if (CurseCD > 0f) CurseCD -= Time.fixedDeltaTime;

        long now = Utils.TimeStamp;

        if ((Math.Abs(KCD - beforeKCD) > 0.01f || Math.Abs(beforeCCD - CurseCD) > 0.01f) && LastNotify != now)
        {
            LastNotify = now;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    public override void AfterMeetingTasks()
    {
        ResetCooldowns(true, true);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.IsModdedClient() && !hud || Main.PlayerStates[seer.PlayerId].Role is not Warlock { IsEnable: true } wl || seer.PlayerId != target.PlayerId) return string.Empty;

        var sb = new StringBuilder();
        if (wl.KCD > 0f) sb.Append($"<#ffa500>{Translator.GetString("KillCooldown")}:</color> <#ffffff>{(int)Math.Round(wl.KCD)}s</color>");

        if (wl.CurseCD > 0f) sb.Append($"{(sb.Length > 0 ? "\n" : string.Empty)}<#00ffa5>{Translator.GetString("CurseCooldown")}:</color> <#ffffff>{(int)Math.Round(wl.CurseCD)}s</color>");

        return hud ? sb.ToString() : $"<size=1.7>{sb}</size>";
    }
}