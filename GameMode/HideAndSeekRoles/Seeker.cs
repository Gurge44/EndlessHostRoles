using System;
using AmongUs.GameOptions;

namespace EHR.GameMode.HideAndSeekRoles
{
    internal class Seeker : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem Vision;
        public static OptionItem Speed;
        public static OptionItem KillCooldown;
        public static OptionItem CanVent;
        public override bool IsEnable => On;
        public Team Team => Team.Impostor;
        public int Chance => 100;
        public int Count => Math.Min(Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors), 1);

        public static void SetupCustomOption()
        {
            TextOptionItem.Create(69_211_205, "Seeker", TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetHeader(true)
                .SetColor(new(255, 25, 25, byte.MaxValue));

            Vision = FloatOptionItem.Create(69_211_201, "SeekerVision", new(0.05f, 5f, 0.05f), 0.5f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(255, 25, 25, byte.MaxValue));
            Speed = FloatOptionItem.Create(69_211_202, "SeekerSpeed", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(255, 25, 25, byte.MaxValue));
            KillCooldown = FloatOptionItem.Create(69_211_203, "KillCooldown", new(0f, 90f, 1f), 10f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(255, 25, 25, byte.MaxValue));
            CanVent = BooleanOptionItem.Create(69_211_204, "CanVent", false, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(new(255, 25, 25, byte.MaxValue));
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            Main.AllPlayerSpeed[playerId] = Speed.GetFloat();
            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.PlayerSpeedMod, Speed.GetFloat());
        }
    }
}