using System;
using System.Collections.Generic;
using EHR.Modules;
using Hazel;

namespace EHR.AddOns.Common
{
    public class Allergic : IAddon
    {
        private static OptionItem Time;
        private static OptionItem Range;

        private static Dictionary<byte, byte> AllergicPlayers = [];
        private static Dictionary<byte, long> AllergyMaxTS = [];
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(645941, CustomRoles.Allergic, canSetNum: true, teamSpawnOptions: true);

            Time = new IntegerOptionItem(645948, "Allergic.Time", new(0, 60, 1), 15, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Allergic])
                .SetValueFormat(OptionFormat.Seconds);

            Range = new FloatOptionItem(645949, "Allergic.Range", new(0.1f, 10f, 0.1f), 1.5f, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Allergic])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public static void Init()
        {
            AllergicPlayers = [];
            AllergyMaxTS = [];

            LateTask.New(() =>
            {
                PlayerControl[] aapc = Main.AllAlivePlayerControls;

                foreach (PlayerControl pc in aapc)
                {
                    if (pc.Is(CustomRoles.Allergic))
                    {
                        PlayerControl target = aapc.RandomElement();
                        AllergicPlayers[pc.PlayerId] = target.PlayerId;
                    }
                }
            }, 10f, "Allergic.Init");
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (Main.HasJustStarted || ExileController.Instance) return;

            if (!AllergicPlayers.TryGetValue(pc.PlayerId, out byte targetId)) return;

            PlayerControl target = targetId.GetPlayer();

            if (target == null || !target.IsAlive() || Vector2.Distance(pc.Pos(), target.Pos()) > Range.GetFloat())
            {
                if (AllergyMaxTS.Remove(pc.PlayerId))
                {
                    Utils.NotifyRoles(SpecifyTarget: pc, SpecifySeer: pc);
                    Utils.SendRPC(CustomRPC.SyncAllergic, 1, pc.PlayerId);
                }

                return;
            }

            if (!AllergyMaxTS.TryGetValue(pc.PlayerId, out long endTS))
            {
                endTS = Utils.TimeStamp + Time.GetInt();
                AllergyMaxTS[pc.PlayerId] = endTS;
                Utils.SendRPC(CustomRPC.SyncAllergic, 2, pc.PlayerId, endTS);
                return;
            }

            if (Utils.TimeStamp >= endTS)
            {
                AllergicPlayers.Remove(pc.PlayerId);
                AllergyMaxTS.Remove(pc.PlayerId);
                pc.Suicide(PlayerState.DeathReason.Allergy);
            }
            else
                Utils.NotifyRoles(SpecifyTarget: pc, SpecifySeer: pc);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    AllergyMaxTS.Remove(reader.ReadByte());
                    break;
                case 2:
                    AllergyMaxTS[reader.ReadByte()] = long.Parse(reader.ReadString());
                    break;
            }
        }

        public static string GetSelfSuffix(PlayerControl seer)
        {
            if (!seer.IsAlive() || !AllergyMaxTS.TryGetValue(seer.PlayerId, out long endTS)) return string.Empty;

            return string.Format(Translator.GetString("Allergic.Suffix"), Math.Ceiling(endTS / (float)Utils.TimeStamp * 100));
        }
    }
}