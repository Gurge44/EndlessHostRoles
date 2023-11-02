using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class Oracle
{
    private static readonly int Id = 7600;
    private static List<byte> playerIdList = new();

    public static OptionItem CheckLimitOpt;
    //  private static OptionItem OracleCheckMode;
    public static OptionItem HideVote;
    public static OptionItem FailChance;
    public static OptionItem OracleAbilityUseGainWithEachTaskCompleted;

    public static List<byte> didVote = new();
    public static Dictionary<byte, float> CheckLimit = new();

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Oracle);
        CheckLimitOpt = IntegerOptionItem.Create(Id + 10, "OracleSkillLimit", new(0, 10, 1), 0, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Times);
        //    OracleCheckMode = BooleanOptionItem.Create(Id + 11, "AccurateCheckMode", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Oracle]);
        HideVote = BooleanOptionItem.Create(Id + 12, "OracleHideVote", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Oracle]);
        //  OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Oracle);
        FailChance = IntegerOptionItem.Create(Id + 13, "FailChance", new(0, 100, 5), 0, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Percent);
        OracleAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 14, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Times);
    }
    public static void Init()
    {
        playerIdList = new();
        CheckLimit = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        CheckLimit.TryAdd(playerId, CheckLimitOpt.GetInt());
    }
    public static bool IsEnable => playerIdList.Any();
    public static void SendRPC(byte playerId, bool isMinus)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetOracleLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(isMinus);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        byte playerId = reader.ReadByte();
        bool isMinus = reader.ReadBoolean();

        if (isMinus) CheckLimit[playerId]--;
        else CheckLimit[playerId]++;
    }
    public static void OnVote(PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null) return;
        if (didVote.Contains(player.PlayerId)) return;
        didVote.Add(player.PlayerId);

        if (CheckLimit[player.PlayerId] < 1)
        {
            Utils.SendMessage(GetString("OracleCheckReachLimit"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));
            return;
        }

        CheckLimit[player.PlayerId] -= 1;
        SendRPC(player.PlayerId, true);

        if (player.PlayerId == target.PlayerId)
        {
            Utils.SendMessage(GetString("OracleCheckSelfMsg") + "\n\n" + string.Format(GetString("OracleCheckLimit"), CheckLimit[player.PlayerId]), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));
            return;
        }

        string text = target.GetCustomRole() switch
        {
            CustomRoles.Impostor or
            CustomRoles.Shapeshifter or
            CustomRoles.ShapeshifterTOHE or
            CustomRoles.ImpostorTOHE or
            CustomRoles.EvilDiviner or
            CustomRoles.Wildling or
            CustomRoles.BountyHunter or
            CustomRoles.Vampire or
            CustomRoles.Witch or
            CustomRoles.Vindicator or
            CustomRoles.ShapeMaster or
            CustomRoles.Zombie or
            CustomRoles.Warlock or
            CustomRoles.Assassin or
            CustomRoles.Undertaker or
            CustomRoles.Hacker or
            CustomRoles.Miner or
            CustomRoles.Escapee or
            CustomRoles.SerialKiller or
            //     CustomRoles.Mare or
            CustomRoles.Inhibitor or
            CustomRoles.Sapper or
            CustomRoles.Mastermind or
            CustomRoles.Gambler or
            CustomRoles.Hitman or
            CustomRoles.RiftMaker or
            CustomRoles.Councillor or
            CustomRoles.Saboteur or
            CustomRoles.Puppeteer or
            CustomRoles.TimeThief or
            //      CustomRoles.Trickster or // Trickster appears as crew to Oracle
            CustomRoles.Mafia or
            CustomRoles.Minimalism or
            CustomRoles.FireWorks or
            CustomRoles.Sniper or
            CustomRoles.EvilTracker or
            CustomRoles.EvilGuesser or
            CustomRoles.AntiAdminer or
            CustomRoles.Sans or
            CustomRoles.Bomber or
            CustomRoles.Nuker or
            CustomRoles.Scavenger or
            CustomRoles.BoobyTrap or
            CustomRoles.Capitalism or
            CustomRoles.Gangster or
            CustomRoles.Cleaner or
            CustomRoles.BallLightning or
            CustomRoles.Greedier or
            CustomRoles.CursedWolf or
            CustomRoles.ImperiusCurse or
            CustomRoles.QuickShooter or
            CustomRoles.Eraser or
            CustomRoles.OverKiller or
            CustomRoles.Hangman or
            CustomRoles.Bard or
            CustomRoles.Swooper or
            CustomRoles.Disperser or
            CustomRoles.Dazzler or
            CustomRoles.Deathpact or
            CustomRoles.Devourer or
            CustomRoles.Camouflager or
            CustomRoles.Twister or
            CustomRoles.Lurker
                => "Imp",

            CustomRoles.Jester or
            CustomRoles.Opportunist or
            CustomRoles.Mario or
            CustomRoles.HexMaster or
            CustomRoles.Crewpostor or
            //CustomRoles.NWitch or
            CustomRoles.Wraith or
            CustomRoles.Parasite or
            CustomRoles.Refugee or
            CustomRoles.Terrorist or
            CustomRoles.Executioner or
            CustomRoles.Juggernaut or
            CustomRoles.Jinx or
            CustomRoles.Lawyer or
            CustomRoles.Arsonist or
            CustomRoles.Jackal or
            CustomRoles.Medusa or
            CustomRoles.Maverick or
            CustomRoles.Sidekick or
            CustomRoles.God or
            CustomRoles.PlagueBearer or
            CustomRoles.Pestilence or
            //CustomRoles.Masochist or
            CustomRoles.Innocent or
            CustomRoles.Pursuer or
            CustomRoles.Poisoner or
            CustomRoles.NSerialKiller or
            CustomRoles.PlagueDoctor or
            CustomRoles.Postman or
            CustomRoles.Magician or
            CustomRoles.WeaponMaster or
            CustomRoles.Reckless or
            CustomRoles.Pyromaniac or
            CustomRoles.Eclipse or
            CustomRoles.Imitator or
            CustomRoles.Werewolf or
            CustomRoles.Ritualist or
            CustomRoles.Pelican or
            CustomRoles.Revolutionist or
            CustomRoles.FFF or
            CustomRoles.Konan or
            CustomRoles.Gamer or
            CustomRoles.DarkHide or
            CustomRoles.Infectious or
            CustomRoles.Workaholic or
            CustomRoles.Collector or
            CustomRoles.Provocateur or
            CustomRoles.Sunnyboy or
            CustomRoles.Phantom or
            CustomRoles.BloodKnight or
            CustomRoles.Totocalcio or
            CustomRoles.Romantic or
            CustomRoles.VengefulRomantic or
            CustomRoles.RuthlessRomantic or
            CustomRoles.Virus or
            CustomRoles.Medusa or
            CustomRoles.Succubus
                //CustomRoles.Pirate
                => "Neut",

            _ => "Crew",
        };
        if (FailChance.GetInt() > 0)
        {
            int random_number_1 = IRandom.Instance.Next(1, 101);
            if (random_number_1 <= FailChance.GetInt())
            {
                int random_number_2 = IRandom.Instance.Next(1, 3);
                if (text == "Crew")
                {
                    if (random_number_2 == 1) text = "Neut";
                    if (random_number_2 == 2) text = "Imp";
                }
                if (text == "Neut")
                {
                    if (random_number_2 == 1) text = "Crew";
                    if (random_number_2 == 2) text = "Imp";
                }
                if (text == "Imp")
                {
                    if (random_number_2 == 1) text = "Neut";
                    if (random_number_2 == 2) text = "Crew";
                }
            }
        }

        string msg = string.Format(GetString("OracleCheck." + text), target.GetRealName());

        Utils.SendMessage(GetString("OracleCheck") + "\n" + msg + "\n\n" + string.Format(GetString("OracleCheckLimit"), CheckLimit[player.PlayerId]), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));

    }
}