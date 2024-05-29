using System.Collections.Generic;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    public class DoubleShot : IAddon
    {
        public static Dictionary<byte, int> Tries = [];

        private static OptionItem MaxTries;

        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(13900, CustomRoles.DoubleShot, canSetNum: true, teamSpawnOptions: true);
            MaxTries = IntegerOptionItem.Create(13906, "DoubleShot.MaxTries", new(1, 30, 1), 1, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DoubleShot])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            Tries = [];
        }

        public static bool CheckGuess(PlayerControl guesser, bool isUI)
        {
            if (!guesser.Is(CustomRoles.DoubleShot)) return false;

            if (!Tries.TryGetValue(guesser.PlayerId, out var tries))
            {
                Tries[guesser.PlayerId] = 1;
                LogAndNotify();
                return true;
            }

            if (tries < MaxTries.GetValue())
            {
                Tries[guesser.PlayerId] = ++tries;
                LogAndNotify();
                return true;
            }

            Tries.Remove(guesser.PlayerId);
            return false;

            void LogAndNotify()
            {
                Logger.Msg($"{guesser.PlayerId} : {Tries[guesser.PlayerId]}", "GuesserDoubleShotTries");

                if (!isUI) Utils.SendMessage(Translator.GetString("GuessDoubleShot"), guesser.PlayerId);
                else guesser.ShowPopUp(Translator.GetString("GuessDoubleShot"));
            }
        }
    }
}