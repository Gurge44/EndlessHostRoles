using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class Spiritcaller : RoleBase
{
    private const int Id = 13400;
    private static List<byte> PlayerIdList = [];

    private static HashSet<byte> PlayersHaunted = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem ImpostorVision;
    private static OptionItem SpiritMax;
    public static OptionItem SpiritAbilityCooldown;
    private static OptionItem SpiritFreezeTime;
    private static OptionItem SpiritProtectTime;
    private static OptionItem SpiritCauseVision;
    private static OptionItem SpiritCauseVisionTime;

    public static bool Protected;
    private static int AddProtect;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Spiritcaller);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 60f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller]);

        ImpostorVision = new BooleanOptionItem(Id + 12, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller]);

        SpiritMax = new IntegerOptionItem(Id + 13, "SpiritcallerSpiritMax", new(1, 15, 1), 2, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
            .SetValueFormat(OptionFormat.Times);

        SpiritAbilityCooldown = new FloatOptionItem(Id + 14, "SpiritcallerSpiritAbilityCooldown", new(5f, 90f, 1f), 30f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
            .SetValueFormat(OptionFormat.Seconds);

        SpiritFreezeTime = new FloatOptionItem(Id + 15, "SpiritcallerFreezeTime", new(0f, 30f, 1f), 3f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
            .SetValueFormat(OptionFormat.Seconds);

        SpiritProtectTime = new FloatOptionItem(Id + 16, "SpiritcallerProtectTime", new(0f, 30f, 1f), 5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
            .SetValueFormat(OptionFormat.Seconds);

        SpiritCauseVision = new FloatOptionItem(Id + 17, "SpiritcallerCauseVision", new(0f, 5f, 0.05f), 0.4f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
            .SetValueFormat(OptionFormat.Multiplier);

        SpiritCauseVisionTime = new FloatOptionItem(Id + 18, "SpiritcallerCauseVisionTime", new(0f, 45f, 1f), 10f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        Protected = false;
        AddProtect = 0;
        PlayersHaunted = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(SpiritMax.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return true;

        if (!target.GetCustomRole().IsAbleToBeSidekicked() && !target.GetCustomRole().IsImpostor())
        {
            killer.RpcRemoveAbilityUse();

            target.RpcSetCustomRole(CustomRoles.EvilSpirit);

            var writer = CustomRpcSender.Create("SpiritCallerSendMessage", SendOption.Reliable);
            writer.StartMessage(target.OwnerId);

            writer.StartRpc(PlayerControl.LocalPlayer.NetId, RpcCalls.SetName)
                .Write(PlayerControl.LocalPlayer.Data.NetId)
                .Write(GetString("SpiritcallerNoticeTitle"))
                .EndRpc();

            writer.StartRpc(PlayerControl.LocalPlayer.NetId, RpcCalls.SendChat)
                .Write(GetString("SpiritcallerNoticeMessage"))
                .EndRpc();

            writer.StartRpc(PlayerControl.LocalPlayer.NetId, RpcCalls.SetName)
                .Write(PlayerControl.LocalPlayer.Data.NetId)
                .Write(PlayerControl.LocalPlayer.Data.PlayerName)
                .EndRpc();

            writer.EndMessage();
            writer.SendMessage();
        }

        return true;
    }

    public static void HauntPlayer(PlayerControl target)
    {
        if (SpiritCauseVisionTime.GetFloat() > 0 || SpiritFreezeTime.GetFloat() > 0) target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Spiritcaller), GetString("HauntedByEvilSpirit")));

        if (SpiritCauseVisionTime.GetFloat() > 0 && PlayersHaunted.Add(target.PlayerId))
        {
            LateTask.New(() =>
            {
                PlayersHaunted.Remove(target.PlayerId);
                if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
                target.MarkDirtySettings();
            }, SpiritCauseVisionTime.GetFloat());
        }

        if (SpiritFreezeTime.GetFloat() > 0)
        {
            float tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
            Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
            ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
            target.MarkDirtySettings();

            LateTask.New(() =>
            {
                Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed;
                ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
                target.MarkDirtySettings();
                RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
            }, SpiritFreezeTime.GetFloat(), "SpiritcallerFreezeTime");

            if (target.AmOwner)
                Achievements.Type.TooCold.CompleteAfterGameEnd();
        }
    }

    public static void ReduceVision(IGameOptions opt, PlayerControl target)
    {
        if (PlayersHaunted.Contains(target.PlayerId))
        {
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, SpiritCauseVision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, SpiritCauseVision.GetFloat());
        }
    }

    public static void ProtectSpiritcaller()
    {
        if (!Protected) Main.Instance.StartCoroutine(ProtectCoroutine());
        else AddProtect++;
    }

    static System.Collections.IEnumerator ProtectCoroutine()
    {
        Protected = true;

        while (Protected)
        {
            yield return new WaitForSecondsRealtime(SpiritProtectTime.GetFloat());
            Protected = --AddProtect >= 0;
        }
        
        AddProtect = 0;
    }
}