using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Decryptor : RoleBase
{
    public static bool On;
    public static List<Decryptor> Instances = [];

    private static OptionItem TaskNum;
    public static OptionItem GuessMode;
    private static OptionItem Vision;

    private static readonly string[] GuessModes =
    [
        "RoleOff", // 0
        "Untouched", // 1
        "RoleOn" // 2
    ];

    private static Dictionary<byte, List<char>> AllRoleNames = [];

    private Dictionary<byte, List<char>> KnownCharacters = [];
    private byte DecryptorId;
    private int TasksCompleted;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 648550;
        Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Decryptor);

        TaskNum = new IntegerOptionItem(++id, "Decryptor.TaskNum", new(1, 10, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Decryptor]);

        GuessMode = new StringOptionItem(++id, "Decryptor.GuessMode", GuessModes, 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Decryptor]);

        Vision = new FloatOptionItem(++id, "Vision", new(0f, 1.5f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Decryptor]);

        Options.OverrideTasksData.Create(++id, TabGroup.CrewmateRoles, CustomRoles.Decryptor);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
        AllRoleNames = [];
        KnownCharacters = [];
        LateTask.New(() => AllRoleNames = Main.PlayerStates.ToDictionary(x => x.Key, x => Translator.GetString($"{x.Value.MainRole}").ToUpper().Where(c => c is not '-' and not ' ' and not '\'').Shuffle()), 10f, log: false);
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        DecryptorId = playerId;
        TasksCompleted = 0;
        Utils.SendRPC(CustomRPC.SyncRoleData, DecryptorId, 1, TasksCompleted);

        if (Main.HasJustStarted)
            LateTask.New(Action, 12f, log: false);
        else
            Action();

        return;

        void Action() => KnownCharacters = AllRoleNames.ToDictionary(x => x.Key, _ => new List<char>());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (!pc.IsAlive()) return;

        TasksCompleted++;

        if (TasksCompleted >= TaskNum.GetInt())
        {
            RevealLetter();
            TasksCompleted = 0;
            Utils.NotifyRoles(SpecifySeer: pc);
        }
        else
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

        Utils.SendRPC(CustomRPC.SyncRoleData, DecryptorId, 1, TasksCompleted);
    }

    private void RevealLetter()
    {
        Dictionary<byte, char> nextLetters = AllRoleNames.ToDictionary(x => x.Key, x =>
        {
            List<char> list = x.Value.ToList();
            KnownCharacters[x.Key].ForEach(kc => list.Remove(kc));
            return list.RandomElement();
        });

        KnownCharacters.Do(x =>
        {
            x.Value.Add(nextLetters[x.Key]);
            Utils.SendRPC(CustomRPC.SyncRoleData, DecryptorId, 2, x.Key, nextLetters[x.Key]);
        });
    }

    public void OnRoleChange(byte id)
    {
        Utils.SendRPC(CustomRPC.SyncRoleData, DecryptorId, 3, id);
        CustomRoles newRole = Main.PlayerStates[id].MainRole;
        AllRoleNames[id] = Translator.GetString($"{newRole}").ToUpper().Where(c => c is not '-' and not ' ' and not 'ё' and not 'ъ').Shuffle();
        int count = KnownCharacters[id].Count;
        KnownCharacters[id] = AllRoleNames[id].Take(count).ToList();
        KnownCharacters[id].ForEach(x => Utils.SendRPC(CustomRPC.SyncRoleData, DecryptorId, 2, id, x));
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                TasksCompleted = reader.ReadPackedInt32();
                break;
            case 2:
                KnownCharacters[reader.ReadByte()].Add(reader.ReadString()[0]);
                break;
            case 3:
                KnownCharacters[reader.ReadByte()].Clear();
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != DecryptorId) return string.Empty;

        return (seer.PlayerId == target.PlayerId) switch
        {
            false when KnownCharacters.TryGetValue(target.PlayerId, out List<char> chars) && chars.Count > 0 => string.Join(' ', chars),
            true when !seer.IsModdedClient() || hud => string.Format(Translator.GetString("Decryptor.Suffix"), TaskNum.GetInt() - TasksCompleted),
            _ => string.Empty
        };
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = GuessMode.GetValue() == 2;
        countsAs = 1;
    }
}
