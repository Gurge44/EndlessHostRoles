using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

internal class Nemesis : RoleBase
{
    public static bool On;

    public static Dictionary<byte, int> NemesisRevenged = [];
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(3100, TabGroup.ImpostorRoles, CustomRoles.Nemesis);

        NemesisCanKillNum = new IntegerOptionItem(3200, "NemesisCanKillNum", new(0, 15, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Nemesis])
            .SetValueFormat(OptionFormat.Players);

        LegacyNemesis = new BooleanOptionItem(3210, "UseLegacyVersion", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Nemesis]);

        NemesisShapeshiftCD = new FloatOptionItem(3211, "ShapeshiftCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(LegacyNemesis)
            .SetValueFormat(OptionFormat.Seconds);

        NemesisShapeshiftDur = new FloatOptionItem(3212, "ShapeshiftDuration", new(1f, 180f, 1f), 15f, TabGroup.ImpostorRoles)
            .SetParent(LegacyNemesis)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = NemesisShapeshiftCD.GetFloat();
        AURoleOptions.ShapeshifterDuration = NemesisShapeshiftDur.GetFloat();
    }

    public static bool NemesisMsgCheck(PlayerControl pc, string msg, bool isUI = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        if (!GameStates.IsInGame || pc == null) return false;

        if (!pc.Is(CustomRoles.Nemesis)) return false;

        msg = msg.Trim().ToLower().Replace(" ", string.Empty);
        if (msg.Length < 3 || !msg.StartsWith("/rv")) return false;

        if (NemesisCanKillNum.GetInt() < 1)
        {
            if (!isUI)
                Utils.SendMessage(GetString("NemesisKillDisable"), pc.PlayerId, importance: MessageImportance.Low);
            else
                pc.ShowPopUp(GetString("NemesisKillDisable"));

            return true;
        }

        if (pc.IsAlive())
        {
            Utils.SendMessage(GetString("NemesisAliveKill"), pc.PlayerId, importance: MessageImportance.Low);
            return true;
        }

        if (msg == "/rv")
        {
            string text = GetString("PlayerIdList");
            text = Main.EnumerateAlivePlayerControls().Aggregate(text, (current, npc) => current + "\n" + npc.PlayerId + " → (" + npc.GetDisplayRoleName() + ") " + npc.GetRealName());

            Utils.SendMessage(text, pc.PlayerId, importance: MessageImportance.High);
            return true;
        }

        if (!NemesisRevenged.TryAdd(pc.PlayerId, 0))
        {
            if (NemesisRevenged[pc.PlayerId] >= NemesisCanKillNum.GetInt())
            {
                if (!isUI)
                    Utils.SendMessage(GetString("NemesisKillMax"), pc.PlayerId);
                else
                    pc.ShowPopUp(GetString("NemesisKillMax"));

                return true;
            }
        }

        PlayerControl target;

        try
        {
            int targetId = int.Parse(msg.Replace("/rv", string.Empty));
            target = Utils.GetPlayerById(targetId);
        }
        catch
        {
            if (!isUI)
                Utils.SendMessage(GetString("NemesisKillDead"), pc.PlayerId, importance: MessageImportance.Low);
            else
                pc.ShowPopUp(GetString("NemesisKillDead"));

            return true;
        }

        if (target == null || !target.IsAlive())
        {
            if (!isUI)
                Utils.SendMessage(GetString("NemesisKillDead"), pc.PlayerId, importance: MessageImportance.Low);
            else
                pc.ShowPopUp(GetString("NemesisKillDead"));

            return true;
        }

        if (target.Is(CustomRoles.Pestilence))
        {
            if (!isUI)
                Utils.SendMessage(GetString("PestilenceImmune"), pc.PlayerId);
            else
                pc.ShowPopUp(GetString("PestilenceImmune"));

            return true;
        }

        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} revenged {target.GetNameWithRole().RemoveHtmlTags()}", "Nemesis");

        string name = target.GetRealName();

        NemesisRevenged[pc.PlayerId]++;

        CustomSoundsManager.RPCPlayCustomSoundAll("AWP");

        LateTask.New(() =>
        {
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
            target.SetRealKiller(pc);

            if (GameStates.IsMeeting)
            {
                target.RpcGuesserMurderPlayer();
                Utils.AfterPlayerDeathTasks(target, true);
            }
            else
            {
                target.Kill(target);
                Main.PlayerStates[target.PlayerId].SetDead();
            }

            LateTask.New(() => Utils.SendMessage(string.Format(GetString("NemesisKillSucceed"), name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Nemesis), GetString("NemesisRevengeTitle")), importance: MessageImportance.High), 0.6f, "Nemesis Kill");
        }, 0.2f, "Nemesis Kill");

        return true;
    }

    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.NemesisRevenge, SendOption.Reliable, AmongUsClient.Instance.HostId);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        int playerId = reader.ReadByte();
        NemesisMsgCheck(pc, $"/rv {playerId}", true);
    }

    private static void NemesisOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Nemesis UI");
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        if (AmongUsClient.Instance.AmHost)
            NemesisMsgCheck(PlayerControl.LocalPlayer, $"/rv {playerId}", true);
        else
            SendRPC(playerId);
    }

    public static void CreateJudgeButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.95f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite =  Utils.LoadSprite("EHR.Resources.Images.Skills.MeetingKillButton.png", 140f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => NemesisOnClick(pva.TargetPlayerId)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Nemesis) && !PlayerControl.LocalPlayer.IsAlive())
                CreateJudgeButton(__instance);
        }
    }
}
