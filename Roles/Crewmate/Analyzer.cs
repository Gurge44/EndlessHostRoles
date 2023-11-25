using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    internal class Analyzer
    {
        private static readonly int Id = 643100;
        private static byte playerId = byte.MaxValue;
        private static int UseLimit = 0;

        private static OptionItem UseLimitOpt;
        private static OptionItem CD;
        private static OptionItem Duration;
        private static OptionItem SeeKillCount;
        private static OptionItem SeeVentCount;
        private static OptionItem SeeRoleBasis;

        public static Dictionary<byte, int> VentCount = [];
        private static (byte, long) CurrentTarget = (byte.MaxValue, GetTimeStamp());

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Analyzer, 1);
            UseLimitOpt = IntegerOptionItem.Create(Id + 10, "AbilityUseLimit", new(0, 5, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer])
                .SetValueFormat(OptionFormat.Times);
            CD = FloatOptionItem.Create(Id + 11, "AnalyzeCD", new(0f, 60f, 2.5f), 15f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer])
                .SetValueFormat(OptionFormat.Seconds);
            Duration = IntegerOptionItem.Create(Id + 12, "AnalyzeDur", new(1, 30, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer])
                .SetValueFormat(OptionFormat.Seconds);
            SeeKillCount = BooleanOptionItem.Create(Id + 13, "AnalyzerSeeKillCount", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer]);
            SeeVentCount = BooleanOptionItem.Create(Id + 14, "AnalyzerSeeVentCount", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer]);
            SeeRoleBasis = BooleanOptionItem.Create(Id + 15, "AnalyzerSeeRoleBasis", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer]);
        }

        public static bool CanUseKillButton => CurrentTarget.Item1 == byte.MaxValue;

        public static bool IsEnable => playerId != byte.MaxValue;

        private static string GetRoleBasis(CustomRoles role)
        {
            return role.GetDYRole() == AmongUs.GameOptions.RoleTypes.Impostor
                ? ColorString(GetRoleColor(CustomRoles.Impostor), GetString("Impostor"))
                : role.GetVNRole() switch
                {
                    CustomRoles.Impostor => ColorString(GetRoleColor(CustomRoles.Impostor), GetString("Impostor")),
                    CustomRoles.Shapeshifter => ColorString(GetRoleColor(CustomRoles.Speedrunner), GetString("Shapeshifter")),
                    CustomRoles.Crewmate => ColorString(GetRoleColor(CustomRoles.Crewmate), GetString("Crewmate")),
                    CustomRoles.Engineer => ColorString(GetRoleColor(CustomRoles.Autocrat), GetString("Engineer")),
                    CustomRoles.Scientist => ColorString(GetRoleColor(CustomRoles.Doctor), GetString("Scientist")),
                    _ => string.Empty
                };
        }

        private static int GetKillCount(byte id) => Main.PlayerStates.Count(x => x.Value.GetRealKiller() == id);

        private static int GetVentCount(byte id) => VentCount.TryGetValue(id, out var count) ? count : 0;

        public static void Init()
        {
            playerId = byte.MaxValue;
            UseLimit = 0;
            VentCount = [];
            CurrentTarget = (byte.MaxValue, GetTimeStamp());
        }

        public static void Add(byte id)
        {
            playerId = id;
            UseLimit = UseLimitOpt.GetInt();

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(id))
                Main.ResetCamPlayerList.Add(id);
        }

        private static void SendRPCSyncTarget()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAnalyzerTarget, SendOption.Reliable, -1);
            writer.Write(CurrentTarget.Item1);
            writer.Write(CurrentTarget.Item2);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPCSyncTarget(MessageReader reader)
        {
            var item1 = reader.ReadByte();
            var item2 = long.Parse(reader.ReadString());
            CurrentTarget = (item1, item2);
        }

        public static void SendRPC()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAnalyzer, SendOption.Reliable, -1);
            writer.Write(UseLimit);
            writer.Write(VentCount.Count);
            foreach (var item in VentCount)
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            UseLimit = reader.ReadInt32();

            int length = reader.ReadInt32();
            for (int i = 0; i < length; i++)
            {
                var key = reader.ReadByte();
                var value = reader.ReadInt32();
                VentCount.Add(key, value);
            }
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null) return;
            if (UseLimit <= 0) return;
            if (CurrentTarget.Item1 != byte.MaxValue) return;

            UseLimit--;
            CurrentTarget = (target.PlayerId, GetTimeStamp());
            killer.SetKillCooldown(time: Duration.GetFloat());
            NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if ()
        }
    }
}
