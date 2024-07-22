using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor
{
    public class Librarian : RoleBase
    {
        private const int Id = 643150;
        private static List<byte> playerIdList = [];

        private static List<byte> sssh = [];

        private static OptionItem Radius;
        private static OptionItem ShowSSAnimation;
        private static OptionItem NameDuration;
        private static OptionItem SSCD;
        private static OptionItem SSDur;
        private static OptionItem CanKillWhileShifted;

        private (bool SILENCING, long LAST_CHANGE) IsInSilencingMode = (false, 0);

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Librarian);
            Radius = new FloatOptionItem(Id + 5, "LibrarianRadius", new(0.5f, 5f, 0.5f), 3f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Librarian])
                .SetValueFormat(OptionFormat.Multiplier);
            ShowSSAnimation = new BooleanOptionItem(Id + 6, "LibrarianShowSSAnimation", false, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Librarian]);
            SSCD = new FloatOptionItem(Id + 7, "ShapeshiftCooldown", new(2.5f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(ShowSSAnimation)
                .SetValueFormat(OptionFormat.Seconds);
            SSDur = new FloatOptionItem(Id + 8, "ShapeshiftDuration", new(2.5f, 60f, 2.5f), 10f, TabGroup.ImpostorRoles)
                .SetParent(ShowSSAnimation)
                .SetValueFormat(OptionFormat.Seconds);
            CanKillWhileShifted = new BooleanOptionItem(Id + 9, "CanKillWhileShifted", false, TabGroup.ImpostorRoles)
                .SetParent(ShowSSAnimation);
            NameDuration = new IntegerOptionItem(Id + 10, "LibrarianNameNotifyDuration", new(1, 30, 1), 10, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Librarian])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            playerIdList = [];
            IsInSilencingMode = (false, 0);
            sssh = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            IsInSilencingMode = (false, TimeStamp);
        }

        public override bool CanUseKillButton(PlayerControl pc) => !pc.IsShifted() || CanKillWhileShifted.GetBool();

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            if (UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = SSCD.GetFloat();
            else
            {
                AURoleOptions.ShapeshifterCooldown = SSCD.GetFloat();
                AURoleOptions.ShapeshifterDuration = SSDur.GetFloat();
            }
        }

        private static void SendRPC(byte playerId, bool isInSilenceMode)
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetLibrarianMode, SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(isInSilenceMode);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            bool isInSilenceMode = reader.ReadBoolean();

            if (Main.PlayerStates[playerId].Role is not Librarian lr) return;
            lr.IsInSilencingMode = (isInSilenceMode, TimeStamp);
        }

        public static bool OnAnyoneReport(PlayerControl reporter)
        {
            if (reporter == null || playerIdList.Count == 0) return true;

            PlayerControl librarian = null;
            float silenceRadius = Radius.GetFloat();

            foreach (var id in playerIdList.ToArray())
            {
                if (Main.PlayerStates[id].Role is not Librarian lr) continue;
                if (!lr.IsInSilencingMode.SILENCING) continue;

                var pc = GetPlayerById(id);
                if (Vector2.Distance(pc.Pos(), reporter.Pos()) <= silenceRadius)
                {
                    librarian = pc;
                }
            }

            if (librarian == null) return true;

            reporter.SetRealKiller(librarian);
            if (librarian.RpcCheckAndMurder(reporter))
            {
                sssh.Add(librarian.PlayerId);
                LateTask.New(() => { sssh.Remove(librarian.PlayerId); }, NameDuration.GetInt(), "Librarian sssh text");
            }

            return false;
        }

        public override bool OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
        {
            if (!IsEnable) return false;
            if (pc == null) return false;

            ChangeSilencingMode(pc);

            return (!shapeshifting && !UseUnshiftTrigger.GetBool()) || ShowSSAnimation.GetBool();
        }

        public override bool OnVanish(PlayerControl pc)
        {
            if (!IsEnable) return false;
            if (pc == null) return false;

            ChangeSilencingMode(pc);

            return false;
        }

        private void ChangeSilencingMode(PlayerControl pc)
        {
            IsInSilencingMode = (!IsInSilencingMode.SILENCING, TimeStamp);
            SendRPC(pc.PlayerId, IsInSilencingMode.SILENCING);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable) return;
            if (ShowSSAnimation.GetBool() && !UsePhantomBasis.GetBool() && !UseUnshiftTrigger.GetBool()) return;

            if (IsInSilencingMode.SILENCING && IsInSilencingMode.LAST_CHANGE + SSDur.GetInt() < TimeStamp)
            {
                var id = pc.PlayerId;
                IsInSilencingMode = (!IsInSilencingMode.SILENCING, TimeStamp);
                SendRPC(id, IsInSilencingMode.SILENCING);
            }
        }

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            IsInSilencingMode = (false, TimeStamp);
            sssh.Clear();
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            string result = string.Empty;
            if (target.Is(CustomRoles.Librarian)) result += GetNameTextForSuffix(target.PlayerId);
            if (isHUD || (seer.PlayerId == target.PlayerId && !seer.IsModClient())) result += GetSelfSuffixAndHudText(target.PlayerId);
            return result;
        }

        static string GetNameTextForSuffix(byte playerId)
        {
            if (Main.PlayerStates[playerId].Role is not Librarian lr) return string.Empty;

            return lr.IsEnable && lr.IsInSilencingMode.SILENCING && sssh.Contains(playerId) && GameStates.IsInTask
                ? GetString("LibrarianNameText")
                : string.Empty;
        }

        static string GetSelfSuffixAndHudText(byte playerId)
        {
            if (Main.PlayerStates[playerId].Role is not Librarian lr) return string.Empty;
            if (!lr.IsEnable || !GameStates.IsInTask) return string.Empty;

            return string.Format(GetString("LibrarianModeText"), lr.IsInSilencingMode.SILENCING ? GetString("LibrarianSilenceMode") : GetString("LibrarianNormalMode"));
        }
    }
}