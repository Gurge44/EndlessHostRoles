using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor
{
    public class Duellist : RoleBase
    {
        private const int Id = 642850;
        private static List<byte> playerIdList = [];
        private static Dictionary<byte, byte> DuelPair = [];
        private static OptionItem SSCD;

        public override bool IsEnable => playerIdList.Count > 0 || Randomizer.Exists;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Duellist);
            SSCD = new FloatOptionItem(Id + 5, "ShapeshiftCooldown", new(0f, 60f, 2.5f), 15f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Duellist])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            playerIdList = [];
            DuelPair = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ShapeshifterCooldown = SSCD.GetFloat();
        }

        public override bool OnShapeshift(PlayerControl duellist, PlayerControl target, bool shapeshifting)
        {
            if (!IsEnable) return false;
            if (duellist == null || target == null) return false;

            var pos = Pelican.GetBlackRoomPS();

            if (target.TP(pos))
            {
                if (Main.KillTimers[duellist.PlayerId] < 1f)
                {
                    duellist.SetKillCooldown(1f); // Give the other player a chance to kill
                }

                duellist.TP(pos);
                DuelPair[duellist.PlayerId] = target.PlayerId;
            }
            else
            {
                duellist.Notify(GetString("TargetCannotBeTeleported"));
            }

            return false;
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || DuelPair.Count == 0) return;

            foreach (var pair in DuelPair)
            {
                var duellist = GetPlayerById(pair.Key);
                var target = GetPlayerById(pair.Value);
                var DAlive = duellist.IsAlive();
                var TAlive = target.IsAlive();

                switch (DAlive)
                {
                    case false when !TAlive:
                        DuelPair.Remove(pair.Key);
                        break;
                    case true when !TAlive:
                        DuelPair.Remove(pair.Key);
                        LateTask.New(() => { duellist.TPtoRndVent(); }, 0.5f, log: false);
                        break;
                    case false:
                        DuelPair.Remove(pair.Key);
                        LateTask.New(() => { target.TPtoRndVent(); }, 0.5f, log: false);
                        break;
                }
            }
        }
    }
}