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
        public static int UseLimit;

        private static OptionItem UseLimitOpt;
        private static OptionItem CD;
        private static OptionItem Duration;
        private static OptionItem SeeKillCount;
        private static OptionItem SeeVentCount;
        private static OptionItem SeeRoleBasis;
        public static OptionItem UsePet;

        private static readonly Dictionary<string, string> replacementDict = new() { { "Analyze", ColorString(GetRoleColor(CustomRoles.Analyzer), "Analyze") } };

        public static Dictionary<byte, int> VentCount = [];
        public static (byte ID, long TIME) CurrentTarget = (byte.MaxValue, GetTimeStamp());

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Analyzer, 1);
            UseLimitOpt = IntegerOptionItem.Create(Id + 10, "AbilityUseLimit", new(0, 30, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer])
                .SetValueFormat(OptionFormat.Times);
            CD = FloatOptionItem.Create(Id + 11, "AnalyzeCD", new(0f, 60f, 2.5f), 15f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer])
                .SetValueFormat(OptionFormat.Seconds);
            CD.ReplacementDictionary = replacementDict;
            Duration = IntegerOptionItem.Create(Id + 12, "AnalyzeDur", new(1, 30, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer])
                .SetValueFormat(OptionFormat.Seconds);
            Duration.ReplacementDictionary = replacementDict;
            SeeKillCount = BooleanOptionItem.Create(Id + 13, "AnalyzerSeeKillCount", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer]);
            SeeVentCount = BooleanOptionItem.Create(Id + 14, "AnalyzerSeeVentCount", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer]);
            SeeRoleBasis = BooleanOptionItem.Create(Id + 15, "AnalyzerSeeRoleBasis", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Analyzer]);
            UsePet = CreatePetUseSetting(Id + 16, CustomRoles.Analyzer);
        }

        public static bool CanUseKillButton => CurrentTarget.ID == byte.MaxValue && UseLimit > 0;

        public static bool IsEnable => playerId != byte.MaxValue;

        private static string GetRoleBasis(CustomRoles role) =>
            SeeRoleBasis.GetBool()
            ? role.GetDYRole() == AmongUs.GameOptions.RoleTypes.Impostor
                ? ColorString(GetRoleColor(CustomRoles.Impostor), GetString("Impostor"))
                : role.GetVNRole() switch
                {
                    CustomRoles.Impostor => ColorString(GetRoleColor(CustomRoles.Impostor), GetString("Impostor")),
                    CustomRoles.Shapeshifter => ColorString(GetRoleColor(CustomRoles.Speedrunner), GetString("Shapeshifter")),
                    CustomRoles.Crewmate => ColorString(GetRoleColor(CustomRoles.Crewmate), GetString("Crewmate")),
                    CustomRoles.Engineer => ColorString(GetRoleColor(CustomRoles.Autocrat), GetString("Engineer")),
                    CustomRoles.Scientist => ColorString(GetRoleColor(CustomRoles.Doctor), GetString("Scientist")),
                    _ => string.Empty
                }
            : string.Empty;

        private static int GetKillCount(byte id) => SeeKillCount.GetBool() ? Main.PlayerStates.Count(x => x.Value.GetRealKiller() == id) : 0;

        private static int GetVentCount(byte id) => SeeVentCount.GetBool() ? VentCount.TryGetValue(id, out var count) ? count : 0 : 0;

        private static string GetAnalyzeResult(PlayerControl pc) => string.Format(GetString("AnalyzerResult"), pc.GetRealName().RemoveHtmlTags(), GetKillCount(pc.PlayerId), GetVentCount(pc.PlayerId), GetRoleBasis(pc.GetCustomRole()));

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

            if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(id))
                Main.ResetCamPlayerList.Add(id);
        }

        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = UseLimit > 0 ? CD.GetFloat() : 300f;

        public static void SendRPC()
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAnalyzer, SendOption.Reliable, -1);
            writer.Write(UseLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;

            UseLimit = reader.ReadInt32();
        }

        public static void OnAnyoneEnterVent(PlayerControl pc)
        {
            if (!IsEnable || !AmongUsClient.Instance.AmHost) return;
            if (VentCount.ContainsKey(pc.PlayerId)) VentCount[pc.PlayerId]++;
            else VentCount[pc.PlayerId] = 1;
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return;
            if (killer == null || target == null) return;
            if (UseLimit <= 0) return;
            if (CurrentTarget.ID != byte.MaxValue) return;

            CurrentTarget = (target.PlayerId, GetTimeStamp());
            killer.SetKillCooldown(time: Duration.GetFloat());
            NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable) return;
            if (pc == null) return;
            if (CurrentTarget.ID == byte.MaxValue) return;

            PlayerControl target = GetPlayerById(CurrentTarget.ID);
            if (target == null) return;

            if (UnityEngine.Vector2.Distance(target.Pos(), pc.Pos()) > (pc.Is(CustomRoles.Reach) ? 2.5f : 1.5f))
            {
                CurrentTarget.ID = byte.MaxValue;
                NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
                return;
            }

            if (CurrentTarget.TIME + Duration.GetInt() < GetTimeStamp())
            {
                CurrentTarget.ID = byte.MaxValue;
                UseLimit--;
                pc.Notify(GetAnalyzeResult(target), 10f);
                pc.SetKillCooldown();
            }
        }

        public static string GetProgressText() => $" <color=#777777>-</color> <color=#{(UseLimit > 0 ? "ffffff" : "ff0000")}>{UseLimit}</color>";

        public static void OnReportDeadBody()
        {
            if (!IsEnable) return;
            CurrentTarget.ID = byte.MaxValue;
        }
    }
}
