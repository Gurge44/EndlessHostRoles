using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Librarian
    {
        private static readonly int Id = 643150;
        private static List<byte> playerIdList = [];
        private static Dictionary<byte, (bool SILENCING, long LAST_CHANGE)> isInSilencingMode = [];
        private static List<byte> sssh = [];

        private static OptionItem Radius;
        private static OptionItem ShowSSAnimation;
        private static OptionItem NameDuration;
        private static OptionItem SSCD;
        private static OptionItem SSDur;
        private static OptionItem CanKillWhileShifted;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Librarian);
            Radius = FloatOptionItem.Create(Id + 5, "LibrarianRadius", new(0.5f, 5f, 0.5f), 2f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Librarian])
                .SetValueFormat(OptionFormat.Multiplier);
            ShowSSAnimation = BooleanOptionItem.Create(Id + 6, "LibrarianShowSSAnimation", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Librarian]);
            SSCD = FloatOptionItem.Create(Id + 7, "ShapeshiftCooldown", new(2.5f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(ShowSSAnimation)
                .SetValueFormat(OptionFormat.Seconds);
            SSDur = FloatOptionItem.Create(Id + 8, "ShapeshiftDuration", new(2.5f, 60f, 2.5f), 10f, TabGroup.ImpostorRoles, false)
                .SetParent(ShowSSAnimation)
                .SetValueFormat(OptionFormat.Seconds);
            CanKillWhileShifted = BooleanOptionItem.Create(Id + 9, "CanKillWhileShifted", false, TabGroup.ImpostorRoles, false)
                .SetParent(ShowSSAnimation);
            NameDuration = IntegerOptionItem.Create(Id + 10, "LibrarianNameNotifyDuration", new(1, 30, 1), 10, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Librarian])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            playerIdList = [];
            isInSilencingMode = [];
            sssh = [];
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            isInSilencingMode.Add(playerId, (false, GetTimeStamp()));
        }

        public static bool IsEnable => playerIdList.Count > 0;

        public static bool CanUseKillButton(PlayerControl pc) => !pc.shapeshifting || CanKillWhileShifted.GetBool();

        public static void ApplyGameOptions()
        {
            AURoleOptions.ShapeshifterDuration = SSDur.GetFloat();
            AURoleOptions.ShapeshifterCooldown = SSCD.GetFloat();
        }

        private static void SendRPC(byte playerId, bool isInSilenceMode)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetLibrarianMode, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(isInSilenceMode);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            byte playerId = reader.ReadByte();
            bool isInSilenceMode = reader.ReadBoolean();

            isInSilencingMode[playerId] = (isInSilenceMode, GetTimeStamp());
        }

        private static void SendRPCSyncList()
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncLibrarianList, SendOption.Reliable, -1);
            writer.Write(sssh.Count);
            foreach (var item in sssh.ToArray())
            {
                writer.Write(item);
            }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPCSyncList(MessageReader reader)
        {
            if (!IsEnable) return;
            sssh.Clear();

            int length = reader.ReadInt32();
            if (length == 0) return;

            for (int i = 0; i < length; i++)
            {
                sssh.Add(reader.ReadByte());
            }
        }

        public static bool OnAnyoneReport(PlayerControl reporter)
        {
            if (!IsEnable) return true;
            if (reporter == null) return true;

            PlayerControl librarian = null;
            float silenceRadius = Radius.GetFloat();

            foreach (var id in playerIdList.ToArray())
            {
                if (!isInSilencingMode.TryGetValue(id, out var x) || !x.SILENCING) continue;
                var pc = GetPlayerById(id);
                if (UnityEngine.Vector2.Distance(pc.Pos(), reporter.Pos()) <= silenceRadius)
                {
                    librarian = pc;
                }
            }

            if (librarian == null) return true;

            reporter.SetRealKiller(librarian);
            if (librarian.RpcCheckAndMurder(reporter))
            {
                sssh.Add(librarian.PlayerId);
                SendRPCSyncList();
                _ = new LateTask(() =>
                {
                    sssh.Remove(librarian.PlayerId);
                    SendRPCSyncList();
                }, NameDuration.GetInt(), "Librarian sssh text");
            }

            return false;
        }

        public static bool OnShapeshift(PlayerControl pc, bool shapeshifting)
        {
            if (!IsEnable) return false;
            if (pc == null) return false;

            byte id = pc.PlayerId;

            isInSilencingMode[id] = (!isInSilencingMode[id].SILENCING, GetTimeStamp());
            SendRPC(id, isInSilencingMode[id].SILENCING);

            return !shapeshifting || ShowSSAnimation.GetBool();
        }

        public static void OnFixedUpdate()
        {
            if (!IsEnable) return;
            if (ShowSSAnimation.GetBool()) return;

            foreach (var kvp in isInSilencingMode.Where(x => x.Value.SILENCING && x.Value.LAST_CHANGE + SSDur.GetInt() < GetTimeStamp()).ToArray())
            {
                var id = kvp.Key;
                isInSilencingMode[id] = (!isInSilencingMode[id].SILENCING, GetTimeStamp());
                SendRPC(id, isInSilencingMode[id].SILENCING);
            }
        }

        public static void OnReportDeadBody()
        {
            if (!IsEnable) return;
            foreach (var id in isInSilencingMode.Keys.ToArray())
            {
                isInSilencingMode[id] = (false, GetTimeStamp());
            }
            sssh.Clear();
        }

        public static string GetNameTextForSuffix(byte playerId) => IsEnable && isInSilencingMode.TryGetValue(playerId, out var x) && x.SILENCING && sssh.Contains(playerId) && GameStates.IsInTask
                ? GetString("LibrarianNameText")
                : string.Empty;

        public static string GetSelfSuffixAndHUDText(byte playerId)
        {
            if (!IsEnable || !GameStates.IsInTask) return string.Empty;
            if (!isInSilencingMode.TryGetValue(playerId, out var x)) return string.Empty;

            return string.Format(GetString("LibrarianModeText"), x.SILENCING ? GetString("LibrarianSilenceMode") : GetString("LibrarianNormalMode"));
        }
    }
}
