﻿using AmongUs.GameOptions;

namespace EHR.GameMode.HideAndSeekRoles
{
    internal class Hider : RoleBase, IHideAndSeekRole
    {
        public static bool On;
        public override bool IsEnable => On;

        public static OptionItem Vision;
        public static OptionItem Speed;
        public static OptionItem TimeDecreaseOnTaskComplete;

        public static void SetupCustomOption()
        {
            TextOptionItem.Create(69_211_105, "Hider", TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetHeader(true)
                .SetColor(new(52, 94, 235, byte.MaxValue));

            Vision = FloatOptionItem.Create(69_211_101, "HiderVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles, false)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(52, 94, 235, byte.MaxValue));
            Speed = FloatOptionItem.Create(69_211_102, "HiderSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles, false)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(52, 94, 235, byte.MaxValue));
            TimeDecreaseOnTaskComplete = IntegerOptionItem.Create(69_211_103, "TimeDecreaseOnTaskComplete", new(0, 60, 1), 5, TabGroup.CrewmateRoles, false)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(52, 94, 235, byte.MaxValue));
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            Main.AllPlayerSpeed[playerId] = Speed.GetFloat();
            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.PlayerSpeedMod, Speed.GetFloat());
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            CustomHideAndSeekManager.TimeLeft -= TimeDecreaseOnTaskComplete.GetInt();
            pc.Notify(Translator.GetString("TimeDecreased"));
            if (60 - (CustomHideAndSeekManager.TimeLeft % 60) <= TimeDecreaseOnTaskComplete.GetInt()) Utils.NotifyRoles();
        }
    }
}
