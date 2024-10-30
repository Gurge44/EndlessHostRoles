using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral
{
    internal class Impartial : RoleBase
    {
        public static bool On;

        private static readonly Dictionary<string, string> ReplacementDictionary = new() { { "Minimum", "<color=#00ffff>Minimum</color>" }, { "Maximum", "<color=#00a5ff>Maximum</color>" } };

        private static OptionItem ImpMinOpt;
        private static OptionItem ImpMaxOpt;
        private static OptionItem NeutralMinOpt;
        private static OptionItem NeutralMaxOpt;
        private static OptionItem CrewMinOpt;
        private static OptionItem CrewMaxOpt;
        private static OptionItem CanVent;
        private static OptionItem CanVentAfterWinning;
        private static OptionItem HasImpVision;
        private static OptionItem HasImpVisionAfterWinning;
        private static OptionItem CanWinWhenKillingMore;
        private (int Killed, int Limit) CrewKillCount = (0, 0);

        private (int Killed, int Limit) ImpKillCount = (0, 0);
        private (int Killed, int Limit) NeutralKillCount = (0, 0);
        public override bool IsEnable => On;

        public bool IsWon
        {
            get
            {
                if (CanWinWhenKillingMore.GetBool())
                {
                    return ImpKillCount.Killed >= ImpKillCount.Limit && NeutralKillCount.Killed >= NeutralKillCount.Limit && CrewKillCount.Killed >= CrewKillCount.Limit;
                }

                return ImpKillCount.Killed == ImpKillCount.Limit && NeutralKillCount.Killed == NeutralKillCount.Limit && CrewKillCount.Killed == CrewKillCount.Limit;
            }
        }

        public override void SetupCustomOption()
        {
            const int id = 10490;
            Options.SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.Impartial);
            ImpMinOpt = CreateSetting(id + 2, true, "Imp");
            ImpMaxOpt = CreateSetting(id + 3, false, "Imp");
            NeutralMinOpt = CreateSetting(id + 4, true, "Neutral");
            NeutralMaxOpt = CreateSetting(id + 5, false, "Neutral");
            CrewMinOpt = CreateSetting(id + 6, true, "Crew");
            CrewMaxOpt = CreateSetting(id + 7, false, "Crew");
            CanVent = new BooleanOptionItem(id + 8, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);
            CanVentAfterWinning = new BooleanOptionItem(id + 9, "EvenAfterWinning", false, TabGroup.NeutralRoles)
                .SetParent(CanVent);
            HasImpVision = new BooleanOptionItem(id + 10, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);
            HasImpVisionAfterWinning = new BooleanOptionItem(id + 11, "EvenAfterWinning", false, TabGroup.NeutralRoles)
                .SetParent(HasImpVision);
            CanWinWhenKillingMore = new BooleanOptionItem(id + 12, "ImpartialCanWinWhenKillingMore", false, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);
        }

        private static OptionItem CreateSetting(int id, bool min, string roleType)
        {
            OptionItem opt = new IntegerOptionItem(id, $"Impartial{roleType}{(min ? "min" : "max")}", new(0, 14, 1), 1, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);
            opt.ReplacementDictionary = ReplacementDictionary;
            return opt;
        }

        public override void Add(byte playerId)
        {
            On = true;
            IRandom r = IRandom.Instance;
            ImpKillCount = (0, r.Next(ImpMinOpt.GetInt(), ImpMaxOpt.GetInt() + 1));
            NeutralKillCount = (0, r.Next(NeutralMinOpt.GetInt(), NeutralMaxOpt.GetInt() + 1));
            CrewKillCount = (0, r.Next(CrewMinOpt.GetInt(), CrewMaxOpt.GetInt() + 1));

            LateTask.New(() => { Utils.SendRPC(CustomRPC.SyncRoleData, playerId, 1, ImpKillCount.Killed, ImpKillCount.Limit, NeutralKillCount.Killed, NeutralKillCount.Limit, CrewKillCount.Killed, CrewKillCount.Limit); }, 5f, log: false);
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return !IsWon;
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool() && (!IsWon || CanVentAfterWinning.GetBool());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(HasImpVision.GetBool() && (!IsWon || HasImpVisionAfterWinning.GetBool()));
        }

        public void ReceiveRPC(MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    ImpKillCount = (reader.ReadInt32(), reader.ReadInt32());
                    NeutralKillCount = (reader.ReadInt32(), reader.ReadInt32());
                    CrewKillCount = (reader.ReadInt32(), reader.ReadInt32());
                    break;
                case 2:
                    ImpKillCount.Killed++;
                    break;
                case 3:
                    NeutralKillCount.Killed++;
                    break;
                case 4:
                    CrewKillCount.Killed++;
                    break;
            }
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            switch (target.GetTeam())
            {
                case Team.Impostor:
                    ImpKillCount.Killed++;
                    Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 2);
                    break;
                case Team.Neutral:
                    NeutralKillCount.Killed++;
                    Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 3);
                    break;
                case Team.Crewmate:
                    CrewKillCount.Killed++;
                    Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 4);
                    break;
            }
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            if (IsWon)
            {
                return " \u2713";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($" <{Main.ImpostorColor}>{ImpKillCount.Killed}/{ImpKillCount.Limit}</color>");
            sb.Append($" <{Main.NeutralColor}>{NeutralKillCount.Killed}/{NeutralKillCount.Limit}</color>");
            sb.Append($" <{Main.CrewmateColor}>{CrewKillCount.Killed}/{CrewKillCount.Limit}</color>");
            return sb.ToString();
        }
    }
}