using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;

namespace EHR.Roles.Neutral
{
    internal class Impartial : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

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

        private (int Killed, int Limit) ImpKillCount = (0, 0);
        private (int Killed, int Limit) NeutralKillCount = (0, 0);
        private (int Killed, int Limit) CrewKillCount = (0, 0);

        public bool IsWon
        {
            get
            {
                if (CanWinWhenKillingMore.GetBool()) return ImpKillCount.Killed >= ImpKillCount.Limit && NeutralKillCount.Killed >= NeutralKillCount.Limit && CrewKillCount.Killed >= CrewKillCount.Limit;
                return ImpKillCount.Killed == ImpKillCount.Limit && NeutralKillCount.Killed == NeutralKillCount.Limit && CrewKillCount.Killed == CrewKillCount.Limit;
            }
        }

        public static void SetupCustomOption()
        {
            const int id = 10490;
            Options.SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.Impartial);
            ImpMinOpt = CreateSetting(id + 2, true, "Imp");
            ImpMaxOpt = CreateSetting(id + 3, false, "Imp");
            NeutralMinOpt = CreateSetting(id + 4, true, "Neutral");
            NeutralMaxOpt = CreateSetting(id + 5, false, "Neutral");
            CrewMinOpt = CreateSetting(id + 6, true, "Crew");
            CrewMaxOpt = CreateSetting(id + 7, false, "Crew");
            CanVent = BooleanOptionItem.Create(id + 8, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);
            CanVentAfterWinning = BooleanOptionItem.Create(id + 9, "EvenAfterWinning", false, TabGroup.NeutralRoles)
                .SetParent(CanVent);
            HasImpVision = BooleanOptionItem.Create(id + 10, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);
            HasImpVisionAfterWinning = BooleanOptionItem.Create(id + 11, "EvenAfterWinning", false, TabGroup.NeutralRoles)
                .SetParent(HasImpVision);
            CanWinWhenKillingMore = BooleanOptionItem.Create(id + 12, "ImpartialCanWinWhenKillingMore", false, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);
        }

        static OptionItem CreateSetting(int id, bool min, string roleType)
        {
            var opt = IntegerOptionItem.Create(id, $"Impartial{roleType}{(min ? "min" : "max")}", new(0, 14, 1), 1, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);
            opt.ReplacementDictionary = ReplacementDictionary;
            return opt;
        }

        public override void Add(byte playerId)
        {
            On = true;
            var r = IRandom.Instance;
            ImpKillCount = (0, r.Next(ImpMinOpt.GetInt(), ImpMaxOpt.GetInt() + 1));
            NeutralKillCount = (0, r.Next(NeutralMinOpt.GetInt(), NeutralMaxOpt.GetInt() + 1));
            CrewKillCount = (0, r.Next(CrewMinOpt.GetInt(), CrewMaxOpt.GetInt() + 1));
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

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            switch (target.GetTeam())
            {
                case Team.Impostor:
                    ImpKillCount.Killed++;
                    break;
                case Team.Neutral:
                    NeutralKillCount.Killed++;
                    break;
                case Team.Crewmate:
                    CrewKillCount.Killed++;
                    break;
            }
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            if (IsWon) return " \u2713";
            var sb = new StringBuilder();
            sb.Append($" <{Main.ImpostorColor}>{ImpKillCount.Killed}/{ImpKillCount.Limit}</color>");
            sb.Append($" <{Main.NeutralColor}>{NeutralKillCount.Killed}/{NeutralKillCount.Limit}</color>");
            sb.Append($" <{Main.CrewmateColor}>{CrewKillCount.Killed}/{CrewKillCount.Limit}</color>");
            return sb.ToString();
        }
    }
}
