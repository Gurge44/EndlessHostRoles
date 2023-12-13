namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Utils;

    public static class CameraMan
    {
        private static readonly int Id = 641600;
        private static List<byte> playerIdList = [];
        public static Dictionary<byte, float> UseLimit = [];

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem CameraManAbilityUseGainWithEachTaskCompleted;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.CameraMan);
            VentCooldown = FloatOptionItem.Create(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Times);
            CameraManAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = [];
            UseLimit = [];
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SendRPC(byte playerId)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCameraManLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(UseLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            if (AmongUsClient.Instance.AmHost) return;

            byte playerId = reader.ReadByte();
            float uses = reader.ReadSingle();
            UseLimit[playerId] = uses;
        }
        public static void OnEnterVent(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.CameraMan)) return;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                UseLimit[pc.PlayerId] -= 1;
                SendRPC(pc.PlayerId);

                Vector2 pos = Main.NormalOptions.MapId switch
                {
                    0 => new(-13.5f, -5.5f),
                    1 => new(15.3f, 3.8f),
                    2 => new(3.0f, -12.0f),
                    4 => new(5.8f, -10.8f),
                    5 => new(9.5f, 1.2f),
                    _ => throw new NotImplementedException(),
                };

                _ = new LateTask(() => { TP(pc.NetTransform, pos); }, UsePets.GetBool() ? 0.1f : 2f, "CameraMan Teleport");
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            var sb = new StringBuilder();

            var taskState = Main.PlayerStates?[playerId].GetTaskState();
            Color TextColor;
            var TaskCompleteColor = Color.green;
            var NonCompleteColor = Color.yellow;
            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
            TextColor = comms ? Color.gray : NormalColor;
            string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";

            Color TextColor1;
            if (UseLimit[playerId] < 1) TextColor1 = Color.red;
            else TextColor1 = Color.white;

            sb.Append(ColorString(TextColor, $"<color=#777777>-</color> {Completed}/{taskState.AllTasksCount}"));
            sb.Append(ColorString(TextColor1, $" <color=#777777>-</color> {Math.Round(UseLimit[playerId], 1)}"));

            return sb.ToString();
        }
        public static string GetHudText(PlayerControl pc)
        {
            return !UsePets.GetBool() || !Main.PetCD.TryGetValue(pc.PlayerId, out var CD)
                ? string.Empty
                : string.Format(Translator.GetString("CDPT"), VentCooldown.GetInt() - (GetTimeStamp() - CD.START_TIMESTAMP) + 1);
        }
    }
}