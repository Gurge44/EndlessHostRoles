using System.Collections.Generic;
using System.Linq;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate
{
    public class Enigma : RoleBase
    {
        private const int Id = 8460;
        public static List<byte> playerIdList = [];
        private static Dictionary<byte, List<EnigmaClue>> ShownClues = [];

        public static OptionItem EnigmaClueStage1Tasks;
        public static OptionItem EnigmaClueStage2Tasks;
        public static OptionItem EnigmaClueStage3Tasks;
        public static OptionItem EnigmaClueStage2Probability;
        public static OptionItem EnigmaClueStage3Probability;
        public static OptionItem EnigmaGetCluesWithoutReporting;

        public static Dictionary<byte, string> MsgToSend = [];
        public static Dictionary<byte, string> MsgToSendTitle = [];

        private static readonly List<EnigmaClue> EnigmaClues =
        [
            new EnigmaHatClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.HatClue },
            new EnigmaHatClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.HatClue },
            new EnigmaVisorClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.VisorClue },
            new EnigmaVisorClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.VisorClue },
            new EnigmaSkinClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.SkinClue },
            new EnigmaSkinClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.SkinClue },
            new EnigmaPetClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.PetClue },
            new EnigmaPetClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.PetClue },
            new EnigmaNameClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.NameClue },
            new EnigmaNameClue { ClueStage = 2, EnigmaClueType = EnigmaClueType.NameClue },
            new EnigmaNameClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.NameClue },
            new EnigmaNameLengthClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.NameLengthClue },
            new EnigmaNameLengthClue { ClueStage = 2, EnigmaClueType = EnigmaClueType.NameLengthClue },
            new EnigmaNameLengthClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.NameLengthClue },
            new EnigmaColorClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.ColorClue },
            new EnigmaColorClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.ColorClue },
            new EnigmaLocationClue { ClueStage = 2, EnigmaClueType = EnigmaClueType.LocationClue },
            new EnigmaKillerStatusClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.KillerStatusClue },
            new EnigmaKillerRoleClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.KillerRoleClue },
            new EnigmaKillerRoleClue { ClueStage = 2, EnigmaClueType = EnigmaClueType.KillerRoleClue },
            new EnigmaKillerLevelClue { ClueStage = 1, EnigmaClueType = EnigmaClueType.KillerLevelClue },
            new EnigmaKillerLevelClue { ClueStage = 2, EnigmaClueType = EnigmaClueType.KillerLevelClue },
            new EnigmaKillerLevelClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.KillerLevelClue },
            new EnigmaFriendCodeClue { ClueStage = 3, EnigmaClueType = EnigmaClueType.FriendCodeClue }
        ];

        public override bool IsEnable => playerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Enigma);
            EnigmaClueStage1Tasks = new FloatOptionItem(Id + 11, "EnigmaClueStage1Tasks", new(0f, 10f, 1f), 1f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Enigma])
                .SetValueFormat(OptionFormat.Times);
            EnigmaClueStage2Tasks = new FloatOptionItem(Id + 12, "EnigmaClueStage2Tasks", new(0f, 10f, 1f), 3f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Enigma])
                .SetValueFormat(OptionFormat.Times);
            EnigmaClueStage3Tasks = new FloatOptionItem(Id + 13, "EnigmaClueStage3Tasks", new(0f, 10f, 1f), 7f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Enigma])
                .SetValueFormat(OptionFormat.Times);
            EnigmaClueStage2Probability = new IntegerOptionItem(Id + 14, "EnigmaClueStage2Probability", new(0, 100, 5), 75, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Enigma])
                .SetValueFormat(OptionFormat.Percent);
            EnigmaClueStage3Probability = new IntegerOptionItem(Id + 15, "EnigmaClueStage3Probability", new(0, 100, 5), 60, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Enigma])
                .SetValueFormat(OptionFormat.Percent);
            EnigmaGetCluesWithoutReporting = new BooleanOptionItem(Id + 16, "EnigmaClueGetCluesWithoutReporting", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Enigma]);

            OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Enigma);
        }

        public override void Init()
        {
            playerIdList = [];
            ShownClues = [];
            MsgToSend = [];
            MsgToSendTitle = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            ShownClues.Add(playerId, []);
        }

        public static void OnReportDeadBody(PlayerControl player, NetworkedPlayerInfo targetInfo)
        {
            if (targetInfo == null) return;

            var target = Utils.GetPlayerById(targetInfo.PlayerId);
            if (target == null) return;

            PlayerControl killer = target.GetRealKiller();
            if (killer == null) return;

            var rd = IRandom.Instance;

            foreach (byte playerId in playerIdList)
            {
                if (!EnigmaGetCluesWithoutReporting.GetBool() && playerId != player.PlayerId) continue;

                var enigmaPlayer = Utils.GetPlayerById(playerId);
                if (enigmaPlayer == null) continue;

                int tasksCompleted = enigmaPlayer.GetTaskState().CompletedTasksCount;
                int stage = 0;
                bool showStageClue = false;

                if (tasksCompleted >= EnigmaClueStage3Tasks.GetInt())
                {
                    stage = 3;
                    showStageClue = rd.Next(0, 100) < EnigmaClueStage3Probability.GetInt();
                }
                else if (tasksCompleted >= EnigmaClueStage2Tasks.GetInt())
                {
                    stage = 2;
                    showStageClue = rd.Next(0, 100) < EnigmaClueStage2Probability.GetInt();
                }
                else if (tasksCompleted >= EnigmaClueStage1Tasks.GetInt())
                    stage = 1;

                var clues = EnigmaClues.Where(a => a.ClueStage <= stage &&
                                                   !ShownClues[playerId].Any(b => b.EnigmaClueType == a.EnigmaClueType && b.ClueStage == a.ClueStage))
                    .ToList();
                if (clues.Count == 0) continue;
                if (showStageClue && clues.Any(a => a.ClueStage == stage))
                    clues = clues.Where(a => a.ClueStage == stage).ToList();

                EnigmaClue clue = clues[rd.Next(0, clues.Count - 1)];
                string title = clue.Title;
                string msg = clue.GetMessage(killer, showStageClue);

                ShownClues[playerId].Add(clue);

                MsgToSend[playerId] = msg;

                MsgToSendTitle[playerId] = title;
            }
        }

        private abstract class EnigmaClue
        {
            public int ClueStage { get; init; }
            public EnigmaClueType EnigmaClueType { get; init; }

            public abstract string Title { get; }
            public abstract string GetMessage(PlayerControl killer, bool showStageClue);
        }

        private class EnigmaHatClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaClueHatTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                var killerOutfit = Camouflage.PlayerSkins[killer.PlayerId];
                if (killerOutfit.HatId == "hat_EmptyHat")
                    return GetString("EnigmaClueHat2");

                return ClueStage switch
                {
                    1 => GetString("EnigmaClueHat1"),
                    2 => GetString("EnigmaClueHat1"),
                    3 => showStageClue ? string.Format(GetString("EnigmaClueHat3"), killerOutfit.HatId) : GetString("EnigmaClueHat1"),
                    _ => null
                };
            }
        }

        private class EnigmaVisorClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaClueVisorTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                var killerOutfit = Camouflage.PlayerSkins[killer.PlayerId];
                if (killerOutfit.VisorId == "visor_EmptyVisor")
                    return GetString("EnigmaClueVisor2");

                return ClueStage switch
                {
                    1 => GetString("EnigmaClueVisor1"),
                    2 => GetString("EnigmaClueVisor1"),
                    3 => showStageClue ? string.Format(GetString("EnigmaClueVisor3"), killerOutfit.VisorId) : GetString("EnigmaClueVisor1"),
                    _ => null
                };
            }
        }

        private class EnigmaSkinClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaClueSkinTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                var killerOutfit = Camouflage.PlayerSkins[killer.PlayerId];
                if (killerOutfit.SkinId == "skin_EmptySkin")
                    return GetString("EnigmaClueSkin2");

                return ClueStage switch
                {
                    1 => GetString("EnigmaClueSkin1"),
                    2 => GetString("EnigmaClueSkin1"),
                    3 => showStageClue ? string.Format(GetString("EnigmaClueSkin3"), killerOutfit.SkinId) : GetString("EnigmaClueSkin1"),
                    _ => null
                };
            }
        }

        private class EnigmaPetClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaCluePetTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                var killerOutfit = Camouflage.PlayerSkins[killer.PlayerId];
                if (killerOutfit.PetId == "pet_EmptyPet")
                    return GetString("EnigmaCluePet2");

                return ClueStage switch
                {
                    1 => GetString("EnigmaCluePet1"),
                    2 => GetString("EnigmaCluePet1"),
                    3 => showStageClue ? string.Format(GetString("EnigmaCluePet3"), killerOutfit.PetId) : GetString("EnigmaCluePet1"),
                    _ => null
                };
            }
        }

        private class EnigmaNameClue : EnigmaClue
        {
            private readonly IRandom rd = IRandom.Instance;

            public override string Title
            {
                get { return GetString("EnigmaClueNameTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                string killerName = killer.GetRealName();
                string letter = killerName[rd.Next(0, killerName.Length - 1)].ToString().ToLower();

                switch (ClueStage)
                {
                    case 1:
                        return GetStage1Clue(killer, letter);
                    case 2:
                        if (showStageClue) GetStage2Clue(letter);
                        return GetStage1Clue(killer, letter);
                    case 3:
                        if (showStageClue) return GetStage3Clue(killerName, letter);
                        if (rd.Next(0, 100) < EnigmaClueStage2Probability.GetInt()) return GetStage2Clue(letter);
                        return GetStage1Clue(killer, letter);
                }

                return null;
            }

            private string GetStage1Clue(PlayerControl killer, string letter)
            {
                string randomLetter = GetRandomLetter(killer, letter);
                int random = rd.Next(1, 2);
                return random == 1 ? string.Format(GetString("EnigmaClueName1"), letter, randomLetter) : string.Format(GetString("EnigmaClueName1"), randomLetter, letter);
            }

            private static string GetStage2Clue(string letter)
            {
                return string.Format(GetString("EnigmaClueName2"), letter);
            }

            private string GetStage3Clue(string killerName, string letter)
            {
                string letter2 = string.Empty;
                string tmpName = killerName.Replace(letter, string.Empty);
                if (!string.IsNullOrEmpty(tmpName))
                {
                    letter2 = tmpName[rd.Next(0, tmpName.Length - 1)].ToString().ToLower();
                }

                return string.Format(GetString("EnigmaClueName3"), letter, letter2);
            }

            private string GetRandomLetter(PlayerControl killer, string letter)
            {
                var alivePlayers = Main.AllAlivePlayerControls.Where(a => a.PlayerId != killer.PlayerId).ToArray();
                var rndPlayer = alivePlayers[rd.Next(0, alivePlayers.Length - 1)];
                string rndPlayerName = rndPlayer.GetRealName().Replace(letter, "");
                string letter2 = rndPlayerName[rd.Next(0, rndPlayerName.Length - 1)].ToString().ToLower();
                return letter2;
            }
        }

        private class EnigmaNameLengthClue : EnigmaClue
        {
            private readonly IRandom rd = IRandom.Instance;

            public override string Title
            {
                get { return GetString("EnigmaClueNameLengthTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                int length = killer.GetRealName().Length;

                return ClueStage switch
                {
                    1 => GetStage1Clue(length),
                    2 => showStageClue ? GetStage2Clue(length) : GetStage1Clue(length),
                    3 when showStageClue => GetStage3Clue(length),
                    3 => rd.Next(0, 100) < EnigmaClueStage2Probability.GetInt() ? GetStage2Clue(length) : GetStage1Clue(length),
                    _ => null
                };
            }

            private string GetStage1Clue(int length)
            {
                int start = length - rd.Next(1, 3);
                int end = length + rd.Next(1, 3);

                start = start < 0 ? 0 : start;
                end = end > 10 ? 10 : end;

                return string.Format(GetString("EnigmaClueNameLength1"), start, end);
            }

            private string GetStage2Clue(int length)
            {
                int start = length - rd.Next(0, 2);
                int end = length + rd.Next(0, 2);

                if (start == end) return GetStage3Clue(length);

                start = start < 0 ? 0 : start;
                end = end > 10 ? 10 : end;

                return string.Format(GetString("EnigmaClueNameLength1"), start, end);
            }

            private static string GetStage3Clue(int length)
            {
                return string.Format(GetString("EnigmaClueNameLength2"), length);
            }
        }

        private class EnigmaColorClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaClueColorTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                var killerOutfit = Camouflage.PlayerSkins[killer.PlayerId];

                return ClueStage switch
                {
                    1 => GetStage1Clue(killerOutfit.ColorId),
                    2 => GetStage1Clue(killerOutfit.ColorId),
                    3 => showStageClue ? string.Format(GetString("EnigmaClueColor3"), killer.Data.ColorName) : GetStage1Clue(killerOutfit.ColorId),
                    _ => GetStage1Clue(killerOutfit.ColorId)
                };
            }

            private static string GetStage1Clue(int colorId)
            {
                return colorId switch
                {
                    0 or 3 or 4 or 5 or 7 or 10 or 11 or 13 or 14 or 17 => GetString("EnigmaClueColor1"),
                    1 or 2 or 6 or 8 or 9 or 12 or 15 or 16 => GetString("EnigmaClueColor2"),
                    _ => null
                };
            }
        }

        private class EnigmaLocationClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaClueLocationTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                string room = string.Empty;
                var targetRoom = Main.PlayerStates[killer.PlayerId].LastRoom;
                if (targetRoom == null) room += GetString("FailToTrack");
                else room += GetString(targetRoom.RoomId.ToString());
                return string.Format(GetString("EnigmaClueLocation"), room);
            }
        }

        private class EnigmaKillerStatusClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaClueStatusTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                return killer.inVent ? GetString("EnigmaClueStatus1") : killer.onLadder ? GetString("EnigmaClueStatus2") : GetString(killer.Data.IsDead ? "EnigmaClueStatus3" : "EnigmaClueStatus4");
            }
        }

        private class EnigmaKillerRoleClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaClueRoleTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                CustomRoles role = killer.GetCustomRole();
                return ClueStage switch
                {
                    1 => role.IsImpostor() ? GetString("EnigmaClueRole1") : GetString(role.IsNeutral() ? "EnigmaClueRole2" : "EnigmaClueRole3"),
                    2 when showStageClue => string.Format(GetString("EnigmaClueRole4"), killer.GetDisplayRoleName()),
                    2 => role.IsImpostor() ? GetString("EnigmaClueRole1") : GetString(role.IsNeutral() ? "EnigmaClueRole2" : "EnigmaClueRole3"),
                    _ => null
                };
            }
        }

        private class EnigmaKillerLevelClue : EnigmaClue
        {
            private readonly IRandom rd = IRandom.Instance;

            public override string Title
            {
                get { return GetString("EnigmaClueLevelTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                int level = (int)killer.Data.PlayerLevel;

                return ClueStage switch
                {
                    1 => GetStage1Clue(level),
                    2 => showStageClue ? GetStage2Clue(level) : GetStage1Clue(level),
                    3 when showStageClue => GetStage3Clue(level),
                    3 => rd.Next(0, 100) < EnigmaClueStage2Probability.GetInt() ? GetStage2Clue(level) : GetStage1Clue(level),
                    _ => null
                };
            }

            private static string GetStage1Clue(int level)
            {
                return GetString(level > 50 ? "EnigmaClueLevel1" : "EnigmaClueLevel2");
            }

            private static string GetStage2Clue(int level)
            {
                int rangeStart = level - 15;
                int rangeEnd = level + 15;
                return string.Format(GetString("EnigmaClueLevel3"), rangeStart, rangeEnd >= 100 ? 100 : rangeEnd);
            }

            private static string GetStage3Clue(int level)
            {
                return string.Format(GetString("EnigmaClueLevel4"), level);
            }
        }

        private class EnigmaFriendCodeClue : EnigmaClue
        {
            public override string Title
            {
                get { return GetString("EnigmaClueFriendCodeTitle"); }
            }

            public override string GetMessage(PlayerControl killer, bool showStageClue)
            {
                string friendCode = killer.Data.FriendCode;
                return string.Format(GetString("EnigmaClueFriendCode"), friendCode);
            }
        }

        private enum EnigmaClueType
        {
            HatClue,
            VisorClue,
            SkinClue,
            PetClue,
            NameClue,
            NameLengthClue,
            ColorClue,
            LocationClue,
            KillerStatusClue,
            KillerRoleClue,
            KillerLevelClue,

            FriendCodeClue
            //SecurityClue,
            //SabotageClue,
            //RandomClue
        }
    }
}