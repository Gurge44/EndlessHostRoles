using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

internal class Impartial : RoleBase
{
    public static bool On;
    private static List<Impartial> Instances;

    private static readonly Dictionary<string, string> ReplacementDictionary = new() { { "Minimum", "<color=#00ffff>Minimum</color>" }, { "Maximum", "<color=#00a5ff>Maximum</color>" } };

    private static OptionItem ImpMinOpt;
    private static OptionItem ImpMaxOpt;
    private static OptionItem NeutralMinOpt;
    private static OptionItem NeutralMaxOpt;
    private static OptionItem CrewMinOpt;
    private static OptionItem CrewMaxOpt;
    private static OptionItem CovenMinOpt;
    private static OptionItem CovenMaxOpt;
    private static OptionItem CanVent;
    private static OptionItem CanVentAfterWinning;
    private static OptionItem HasImpVision;
    private static OptionItem HasImpVisionAfterWinning;
    private static OptionItem CanWinWhenKillingMore;
    private static OptionItem ChangeRoleWhenWinningIsImpossible;
    private static OptionItem RoleToChangeTo;

    private static readonly CustomRoles[] ChangeRoles =
    [
        CustomRoles.Amnesiac,
        CustomRoles.Pursuer,
        CustomRoles.Maverick,
        CustomRoles.Follower,
        CustomRoles.Opportunist,
        CustomRoles.Crewmate,
        CustomRoles.Jester
    ];

    private Dictionary<Team, (int Killed, int Limit)> Kills = [];

    private byte ImpartialId;

    public override bool IsEnable => On;

    public bool IsWon => CanWinWhenKillingMore.GetBool() ? Kills.Values.All(x => x.Killed >= x.Limit) : Kills.Values.All(x => x.Killed == x.Limit);

    public override void SetupCustomOption()
    {
        const int id = 651100;
        Options.SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.Impartial);
        ImpMinOpt = CreateSetting(id + 2, true, "Imp");
        ImpMaxOpt = CreateSetting(id + 3, false, "Imp");
        NeutralMinOpt = CreateSetting(id + 4, true, "Neutral");
        NeutralMaxOpt = CreateSetting(id + 5, false, "Neutral");
        CrewMinOpt = CreateSetting(id + 6, true, "Crew");
        CrewMaxOpt = CreateSetting(id + 7, false, "Crew");
        CovenMinOpt = CreateSetting(id + 13, true, "Coven");
        CovenMaxOpt = CreateSetting(id + 14, false, "Coven");

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
        
        ChangeRoleWhenWinningIsImpossible = new BooleanOptionItem(id + 15, "VultureChangeRoleWhenCantWin", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Impartial]);

        RoleToChangeTo = new StringOptionItem(id + 16, "VultureChangeRole", ChangeRoles.Select(x => x.ToColoredString()).ToArray(), 0, TabGroup.NeutralRoles, noTranslation: true)
            .SetParent(ChangeRoleWhenWinningIsImpossible);
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
        ImpartialId = playerId;
        Instances ??= [];
        Instances.Add(this);
        var r = IRandom.Instance;
        Kills = new()
        {
            [Team.Impostor] = (0, r.Next(ImpMinOpt.GetInt(), ImpMaxOpt.GetInt() + 1)),
            [Team.Neutral] = (0, r.Next(NeutralMinOpt.GetInt(), NeutralMaxOpt.GetInt() + 1)),
            [Team.Crewmate] = (0, r.Next(CrewMinOpt.GetInt(), CrewMaxOpt.GetInt() + 1)),
            [Team.Coven] = (0, r.Next(CovenMinOpt.GetInt(), CovenMaxOpt.GetInt() + 1)),
            [Team.None] = (0, 0)
        };
    }

    public override void Init()
    {
        On = false;
        Instances = null;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
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
        Team team = (Team)reader.ReadByte();
        var tuple = Kills[team];
        tuple.Killed++;
        Kills[team] = tuple;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        Team team = target.GetTeam();
        if (team == Team.None) return;
        var tuple = Kills[team];
        tuple.Killed++;
        Kills[team] = tuple;
        Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, (byte)team);
    }

    public static void OnAnyoneDead()
    {
        if (!ChangeRoleWhenWinningIsImpossible.GetBool()) return;

        var aapc = Main.CachedAlivePlayerControls();
        Dictionary<Team, int> numAlive = new()
        {
            [Team.Impostor] = 0,
            [Team.Neutral] = 0,
            [Team.Crewmate] = 0,
            [Team.Coven] = 0,
            [Team.None] = 0
        };

        for (int index = 0; index < aapc.Count; index++)
            numAlive[aapc[index].GetTeam()]++;

        foreach (Impartial instance in Instances)
        {
            var pc = instance.ImpartialId.GetPlayer();
            if (!pc || !pc.IsAlive()) continue;
            
            for (int index = 1; index < Main.TeamValues.Length; index++)
            {
                Team team = Main.TeamValues[index];
                var kills = instance.Kills[team];

                if (kills.Killed + numAlive[team] < kills.Limit)
                {
                    CustomRoles role = ChangeRoles[RoleToChangeTo.GetValue()];
                    pc.RpcSetCustomRole(role);
                    pc.RpcChangeRoleBasis(role);
                    return;
                }
            }
        }
    }

    public override void GetProgressText(byte playerId, bool comms, StringBuilder resultText)
    {
        if (IsWon)
        {
            resultText.Append(" \u2713");
            return;
        }

        for (int index = 1; index < Main.TeamValues.Length; index++)
        {
            Team team = Main.TeamValues[index];
            var kills = Kills[team];
            
            resultText.Append(' ');
            resultText.Append('<');
            resultText.Append(team.GetTextColor());
            resultText.Append('>');
            resultText.Append(kills.Killed);
            resultText.Append('/');
            resultText.Append(kills.Limit);
            resultText.Append("</color>");
        }
    }
}