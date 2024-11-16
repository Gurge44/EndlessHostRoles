using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral
{
    public class NoteKiller : RoleBase
    {
        public static bool On;

        private static OptionItem NumLettersRevealed;
        private static OptionItem AbilityCooldown;
        private static OptionItem ClueShowDuration;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;

        private static readonly string[] Names =
        [
            "John", "Jane", "Alice", "Bob", "Charlie", "David", "Eve", "Frank", "Grace", "Hank",
            "Ivy", "Jack", "Kate", "Liam", "Mia", "Nina", "Oliver", "Penny", "Quinn", "Ryan"
        ];

        public static Dictionary<byte, string> RealNames = [];
        private static Dictionary<byte, string> ShownClues = [];
        private static long ShowClueEndTimeStamp;

        private static byte NoteKillerID;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(645950, single: true)
                .AutoSetupOption(ref NumLettersRevealed, 2, new IntegerValueRule(1, Names.Max(x => x.Length), 1))
                .AutoSetupOption(ref AbilityCooldown, 15f, new FloatValueRule(0f, 90f, 0.5f), OptionFormat.Seconds)
                .AutoSetupOption(ref ClueShowDuration, 5, new IntegerValueRule(0, 30, 1), OptionFormat.Seconds)
                .AutoSetupOption(ref CanVent, true)
                .AutoSetupOption(ref HasImpostorVision, true);
        }

        public override void Init()
        {
            On = false;

            RealNames = [];
            ShownClues = [];
            ShowClueEndTimeStamp = 0;

            LateTask.New(() =>
            {
                var names = Names.ToList();

                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (pc.Is(CustomRoles.NoteKiller)) continue;
                    var name = names.RandomElement();
                    RealNames[pc.PlayerId] = name;
                    names.Remove(name);
                    Logger.Info($"{pc.GetRealName()}'s real name is {name}", "NoteKiller.Init");
                }
            }, 10f, log: false);
        }

        public override void Add(byte playerId)
        {
            On = true;
            NoteKillerID = playerId;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(HasImpostorVision.GetBool());
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }

        public override void OnPet(PlayerControl pc)
        {
            Dictionary<byte, List<int>> revealedPositions = [];

            for (int i = 0; i < NumLettersRevealed.GetInt(); i++)
            {
                foreach (KeyValuePair<byte, string> kvp in RealNames)
                {
                    var range = Enumerable.Range(0, Names.Max(x => x.Length) - 1);
                    var hasExceptions = revealedPositions.TryGetValue(kvp.Key, out var exceptions);
                    if (hasExceptions) range = range.Except(exceptions);

                    var position = range.RandomElement();

                    if (!hasExceptions) revealedPositions[kvp.Key] = [position];
                    else exceptions.Add(position);
                }
            }

            IEnumerable<(byte ID, List<int> Positions, string Name, PlayerControl Player)> datas = revealedPositions.Join(
                RealNames, x => x.Key, x => x.Key, (x, y) => (
                    ID: x.Key,
                    Positions: x.Value,
                    Name: y.Value,
                    Player: y.Key.GetPlayer()));

            foreach ((byte ID, List<int> Positions, string Name, PlayerControl Player) data in datas)
            {
                if (data.Player == null || !data.Player.IsAlive())
                {
                    RealNames.Remove(data.ID);
                    continue;
                }

                var clue = new char[data.Name.Length];
                Loop.Times(data.Name.Length, i => clue[i] = data.Positions.Contains(i) ? data.Name[i] : '_');

                string shownClue = new(clue);
                ShownClues[data.ID] = shownClue;
                Utils.SendRPC(CustomRPC.SyncRoleData, NoteKillerID, 1, data.ID, shownClue);
            }

            ShowClueEndTimeStamp = Utils.TimeStamp + ClueShowDuration.GetInt();
            Utils.SendRPC(CustomRPC.SyncRoleData, NoteKillerID, 2, ShowClueEndTimeStamp);
            Utils.NotifyRoles(SpecifySeer: pc);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || ExileController.Instance || pc == null || !pc.IsAlive() || ShowClueEndTimeStamp == 0 || ShownClues.Count == 0) return;

            if (Utils.TimeStamp >= ShowClueEndTimeStamp)
            {
                ShowClueEndTimeStamp = 0;
                ShownClues.Clear();
                Utils.SendRPC(CustomRPC.SyncRoleData, NoteKillerID, 3);
                Utils.NotifyRoles(SpecifySeer: pc);
            }
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (Options.UseUnshiftTrigger.GetBool() && Options.UseUnshiftTriggerForNKs.GetBool() && !shapeshifting)
                OnPet(shapeshifter);

            return false;
        }

        public void ReceiveRPC(MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    ShownClues[reader.ReadByte()] = reader.ReadString();
                    break;
                case 2:
                    ShowClueEndTimeStamp = long.Parse(reader.ReadString());
                    break;
                case 3:
                    ShowClueEndTimeStamp = 0;
                    ShownClues.Clear();
                    break;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (seer.PlayerId == target.PlayerId && CustomRoles.NoteKiller.RoleExist() && seer.PlayerId != NoteKillerID && !meeting && RealNames.TryGetValue(seer.PlayerId, out var ownName))
                return string.Format(Translator.GetString("NoteKiller.OthersSelfSuffix"), CustomRoles.NoteKiller.ToColoredString(), ownName);

            if (seer.PlayerId != NoteKillerID || meeting || ShowClueEndTimeStamp == 0 || ShownClues.Count == 0) return string.Empty;
            if (ShownClues.TryGetValue(target.PlayerId, out var clue)) return clue;
            return seer.PlayerId == target.PlayerId ? $"\u25a9 ({ShowClueEndTimeStamp - Utils.TimeStamp}s)" : string.Empty;
        }
    }
}