using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate
{
    public class Lyncher : RoleBase
    {
        public static bool On;
        public static List<Lyncher> Instances = [];

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
        private byte LyncherId;
        private int TasksCompleted;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            int id = 648550;
            Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Lyncher);
            TaskNum = new IntegerOptionItem(++id, "Lyncher.TaskNum", new(1, 10, 1), 3, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Lyncher]);
            GuessMode = new StringOptionItem(++id, "Lyncher.GuessMode", GuessModes, 1, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Lyncher]);
            Vision = new FloatOptionItem(++id, "Vision", new(0f, 1.5f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Lyncher]);
            Options.OverrideTasksData.Create(++id, TabGroup.CrewmateRoles, CustomRoles.Lyncher);
        }

        public override void Init()
        {
            On = false;
            Instances = [];
            AllRoleNames = [];
            KnownCharacters = [];
            LateTask.New(() => AllRoleNames = Main.PlayerStates.ToDictionary(x => x.Key, x => Translator.GetString($"{x.Value.MainRole}").ToUpper().Shuffle()), 3f, log: false);
        }

        public override void Add(byte playerId)
        {
            On = true;
            Instances.Add(this);
            LyncherId = playerId;
            TasksCompleted = 0;
            Utils.SendRPC(CustomRPC.SyncRoleData, LyncherId, 1, TasksCompleted);
            LateTask.New(() => KnownCharacters = AllRoleNames.ToDictionary(x => x.Key, _ => new List<char>()), 4f, log: false);
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
            else Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

            Utils.SendRPC(CustomRPC.SyncRoleData, LyncherId, 1, TasksCompleted);
        }

        void RevealLetter()
        {
            var nextLetters = AllRoleNames.ToDictionary(x => x.Key, x => x.Value.Except(KnownCharacters[x.Key]).RandomElement());
            KnownCharacters.Do(x =>
            {
                x.Value.Add(nextLetters[x.Key]);
                Utils.SendRPC(CustomRPC.SyncRoleData, LyncherId, 2, x.Key, nextLetters[x.Key]);
            });
        }

        public void OnRoleChange(byte id)
        {
            Utils.SendRPC(CustomRPC.SyncRoleData, LyncherId, 3, id);
            var newRole = Main.PlayerStates[id].MainRole;
            AllRoleNames[id] = Translator.GetString($"{newRole}").ToUpper().Shuffle();
            var count = KnownCharacters[id].Count;
            KnownCharacters[id] = AllRoleNames[id].Take(count).ToList();
            KnownCharacters[id].ForEach(x => Utils.SendRPC(CustomRPC.SyncRoleData, LyncherId, 2, id, x));
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
            if (seer.PlayerId != LyncherId) return string.Empty;

            return (seer.PlayerId == target.PlayerId) switch
            {
                false when KnownCharacters.TryGetValue(target.PlayerId, out var chars) && chars.Count > 0 => string.Join(' ', chars),
                true when (!seer.IsModClient() || hud) => string.Format(Translator.GetString("Lyncher.Suffix"), TaskNum.GetInt() - TasksCompleted),
                _ => string.Empty
            };
        }
    }
}