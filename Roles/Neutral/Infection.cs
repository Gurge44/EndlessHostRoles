using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral;

public class Infection : RoleBase
{
    private const int Id = 641700;
    private static List<byte> PlayerIdList = [];

    private static OptionItem OptionInfectLimit;
    private static OptionItem OptionInfectWhenKilled;
    private static OptionItem OptionInfectTime;
    private static OptionItem OptionInfectDistance;
    private static OptionItem OptionInfectInactiveTime;
    private static OptionItem OptionInfectCanInfectSelf;
    private static OptionItem OptionInfectCanInfectVent;

    private static int InfectLimit;
    private static bool InfectWhenKilled;
    private static float InfectTime;
    private static float InfectDistance;
    private static float InfectInactiveTime;
    private static bool CanInfectSelf;
    private static bool CanInfectVent;

    private static Dictionary<byte, float> InfectInfos;
    private static bool InfectActive;
    private static bool LateCheckWin;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Infection);

        OptionInfectLimit = new IntegerOptionItem(Id + 10, "InfectionInfectLimit", new(1, 3, 1), 1, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Infection])
            .SetValueFormat(OptionFormat.Times);

        OptionInfectWhenKilled = new BooleanOptionItem(Id + 11, "InfectionInfectWhenKilled", false, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Infection]);

        OptionInfectTime = new FloatOptionItem(Id + 12, "InfectionInfectTime", new(3f, 20f, 1f), 8f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Infection])
            .SetValueFormat(OptionFormat.Seconds);

        OptionInfectDistance = new FloatOptionItem(Id + 13, "InfectionInfectDistance", new(0.5f, 2f, 0.25f), 1.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Infection]);

        OptionInfectInactiveTime = new FloatOptionItem(Id + 14, "InfectionInfectInactiveTime", new(0.5f, 10f, 0.5f), 5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Infection])
            .SetValueFormat(OptionFormat.Seconds);

        OptionInfectCanInfectSelf = new BooleanOptionItem(Id + 15, "InfectionCanInfectSelf", false, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Infection]);

        OptionInfectCanInfectVent = new BooleanOptionItem(Id + 16, "InfectionCanInfectVent", false, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Infection]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        InfectInfos = [];
        InfectActive = false;
    }

    public override void Add(byte playerId)
    {
        InfectLimit = OptionInfectLimit.GetInt();
        InfectWhenKilled = OptionInfectWhenKilled.GetBool();
        InfectTime = OptionInfectTime.GetFloat();
        InfectDistance = OptionInfectDistance.GetFloat();
        InfectInactiveTime = OptionInfectInactiveTime.GetFloat();
        CanInfectSelf = OptionInfectCanInfectSelf.GetBool();
        CanInfectVent = OptionInfectCanInfectVent.GetBool();

        playerId.SetAbilityUseLimit(InfectLimit);

        if (Main.NormalOptions.MapId == 4)
            // Fixed airship respawn selection delay
            InfectInactiveTime += 5f;
        
        LateTask.New(() => InfectActive = true, 10f + InfectInactiveTime, log: false);

        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Options.AdjustedDefaultKillCooldown;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() != 0;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override string GetProgressText(byte id, bool comms)
    {
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Infection).ShadeColor(0.25f), $"({id.GetAbilityUseLimit()})");
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(false);
    }

    private bool CanInfect(PlayerControl player)
    {
        if (!IsEnable) return false;

        // Not a plague doctor, or capable of self-infection and infected person created
        return player.PlayerId != PlayerIdList[0] || (CanInfectSelf && player.GetAbilityUseLimit() == 0);
    }

    private void SendRPC(byte targetId, float rate)
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncInfection, SendOption.Reliable);
        writer.Write(targetId);
        writer.Write(rate);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte targetId = reader.ReadByte();
        float rate = reader.ReadSingle();
        InfectInfos[targetId] = rate;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() > 0)
        {
            killer.RpcRemoveAbilityUse();
            killer.RpcGuardAndKill(target);
            DirectInfect(target);
        }

        return false;
    }

    public static void OnPDdeath(PlayerControl killer, PlayerControl target)
    {
        if (Main.PlayerStates[target.PlayerId].Role is not Infection { IsEnable: true } pd) return;

        if (InfectWhenKilled && target.GetAbilityUseLimit() > 0)
        {
            target.SetAbilityUseLimit(0);
            pd.DirectInfect(killer);
        }
    }

    public static void OnAnyMurder()
    {
        // You may win if an uninfected person dies.
        LateCheckWin = true;
    }

    public override void OnReportDeadBody()
    {
        InfectActive = false;
    }

    public override void OnCheckPlayerPosition(PlayerControl player)
    {
        if (!IsEnable) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;

        if (LateCheckWin)
        {
            // After hanging/killing, check the victory conditions just to be sure.
            LateCheckWin = false;
            CheckWin();
        }

        if (!player.IsAlive() || !InfectActive) return;

        if (InfectInfos.TryGetValue(player.PlayerId, out float rate) && rate >= 100)
        {
            // In case of an infected person
            var changed = false;
            bool inVent = player.inVent;
            List<PlayerControl> updates = [];

            foreach (PlayerControl target in Main.AllAlivePlayerControls)
            {
                // Plague doctors are excluded if they cannot infect themselves.
                if (!CanInfect(target)) continue;

                // Excluded if inside or outside the vent
                if (!CanInfectVent && target.inVent != inVent) continue;

                InfectInfos.TryGetValue(target.PlayerId, out float oldRate);
                // Exclude infected people
                if (oldRate >= 100) continue;

                // Exclude players outside the range
                float distance = Vector2.Distance(player.Pos(), target.Pos());
                if (distance > InfectDistance) continue;

                float newRate = oldRate + (Time.fixedDeltaTime / InfectTime * 100);
                newRate = Math.Clamp(newRate, 0, 100);
                InfectInfos[target.PlayerId] = newRate;

                if ((oldRate < 50 && newRate >= 50) || newRate >= 100)
                {
                    changed = true;
                    updates.Add(target);
                    Logger.Info($"InfectRate [{target.GetNameWithRole()}]: {newRate}%", "Infection");
                    SendRPC(target.PlayerId, newRate);
                }
            }

            if (changed)
            {
                //If someone is infected
                CheckWin();
                foreach (PlayerControl x in updates) Utils.NotifyRoles(SpecifyTarget: x);
            }
        }
    }

    public override void AfterMeetingTasks()
    {
        // You may win if a non-infected person is hanged.
        LateCheckWin = true;

        LateTask.New(() =>
        {
            Logger.Info("Infect Active", "Infection");
            InfectActive = true;
        }, InfectInactiveTime, "ResetInfectInactiveTime");
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null)
    {
        if (Main.PlayerStates[seer.PlayerId].Role is not Infection { IsEnable: true } pd) return string.Empty;

        seen ??= seer;
        if (!pd.CanInfect(seen) || !seer.Is(CustomRoles.Infection) && seer.IsAlive()) return string.Empty;

        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Infection), GetInfectRateCharactor(seen, pd));
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId && seer.IsAlive() || !seer.Is(CustomRoles.Infection) && seer.IsAlive() || !hud && seer.IsModdedClient() || Main.PlayerStates[seer.PlayerId].Role is not Infection { IsEnable: true } pd) return string.Empty;

        var str = new StringBuilder(40);

        foreach (PlayerControl player in Main.AllAlivePlayerControls)
        {
            if (!player.Is(CustomRoles.Infection))
                str.Append(GetInfectRateCharactor(player, pd));
        }

        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Infection), str.ToString());
    }

    public static bool IsInfected(byte playerId)
    {
        InfectInfos.TryGetValue(playerId, out float rate);
        return rate >= 100;
    }

    private static string GetInfectRateCharactor(PlayerControl player, Infection pd)
    {
        if (!pd.IsEnable || !pd.CanInfect(player) || !player.IsAlive() || !InfectInfos.TryGetValue(player.PlayerId, out float rate)) return string.Empty;

        return rate switch
        {
            < 50 => "\u2581",
            >= 50 and < 100 => "\u2584",
            >= 100 => "\u2588",
            _ => string.Empty
        };
    }

    private void DirectInfect(PlayerControl player)
    {
        if (PlayerIdList.Count == 0 || player == null) return;

        Logger.Info($"InfectRate [{player.GetNameWithRole()}]: 100%", "Infection");
        InfectInfos[player.PlayerId] = 100;
        SendRPC(player.PlayerId, 100);
        Utils.NotifyRoles(SpecifySeer: player);
        Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(PlayerIdList[0]));
        CheckWin();
    }

    private void CheckWin()
    {
        if (!IsEnable) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.IntroDestroyed) return;

        // Invalid if someone's victory is being processed
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

        if (Main.AllAlivePlayerControls.All(p => p.Is(CustomRoles.Infection) || IsInfected(p.PlayerId)))
        {
            InfectActive = false;

            PlayerControl pd = Main.AllPlayerControls.FirstOrDefault(x => x.Is(CustomRoles.Infection));

            foreach (PlayerControl player in Main.AllAlivePlayerControls)
            {
                if (player.Is(CustomRoles.Infection)) continue;
                player.Suicide(PlayerState.DeathReason.Curse, pd);
            }

            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Infection);

            foreach (PlayerControl infection in Main.AllPlayerControls)
            {
                if (infection.Is(CustomRoles.Infection))
                    CustomWinnerHolder.WinnerIds.Add(infection.PlayerId);
            }
        }
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("InfectiousKillButtonText"));
    }
}