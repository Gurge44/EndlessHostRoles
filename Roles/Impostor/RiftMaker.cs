using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class RiftMaker
    {
        private static readonly int Id = 640900;
        public static List<byte> playerIdList = new();

        public static List<Vector2> Marks = new();

        public static OptionItem KillCooldown;
        public static OptionItem ShapeshiftCooldown;

        public static long LastTP = GetTimeStamp();

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.RiftMaker, 1);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.RiftMaker])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 11, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.RiftMaker])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            playerIdList = new();
            Marks = new();
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            LastTP = GetTimeStamp();
        }

        public static bool IsEnable => playerIdList.Any();

        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask) return;
            if (Pelican.IsEaten(player.PlayerId) || player.Data.IsDead) return;
            if (!player.Is(CustomRoles.RiftMaker)) return;
            if (Marks.Count != 2) return;
            if (Vector2.Distance(Marks[0], Marks[1]) <= 4f)
            {
                player.Notify(GetString("IncorrectMarks"));
                Marks.Clear();
                return;
            }
            if (LastTP + 5 > GetTimeStamp()) return;

            Vector2 position = player.transform.position;

            bool isTP = false;
            Vector2 from = Marks[0];

            for (int i = 0; i < Marks.Count; i++)
            {
                Vector2 mark = Marks[i];
                var dis = Vector2.Distance(mark, position);
                if (dis > 2f) continue;

                isTP = true;
                from = mark;
            }

            if (isTP)
            {
                LastTP = GetTimeStamp();
                if (from == Marks[0])
                {
                    TP(player.NetTransform, Marks[1]);
                }
                else if (from == Marks[1])
                {
                    TP(player.NetTransform, Marks[0]);
                }
                else
                {
                    Logger.Error($"Teleport failed - from: {from}", "RiftMakerTP");
                }
            }
        }

        public static void OnReportDeadBody()
        {
            Marks.Clear();
        }

        public static void OnEnterVent(PlayerControl player, int ventId)
        {
            Marks.Clear();
            player.Notify(GetString("MarksCleared"));

            _ = new LateTask(() =>
            {
                player.MyPhysics?.RpcBootFromVent(ventId);
            }, 0.5f, "RiftMaker-ResetMarks.RpcBootFromVent");
        }

        public static void OnShapeshift(PlayerControl player, bool shapeshifting, bool isPet = false)
        {
            if (player == null) return;
            if (!shapeshifting) return;
            if (Marks.Count >= 2) return;

            Marks.Add((Vector2)player.transform.position);
            if (Marks.Count == 2) LastTP = GetTimeStamp();
            player.Notify(GetString("MarkDone"));

            //if (!isPet) _ = new LateTask(() => { player.CmdCheckRevertShapeshift(false); }, 1.5f, "Rift Maker RpcRevertShapeshift");
        }

        public static string GetProgressText()
        {
            return $" <color=#777777>-</color> {(Marks.Count == 2 ? "<color=#00ff00>" : "<color=#777777>")}{Marks.Count}/2</color>";
        }
    }
}
