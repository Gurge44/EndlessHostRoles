using System.Collections.Generic;
using AmongUs.GameOptions;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor
{
    public class Dazzler : RoleBase
    {
        private const int Id = 3500;
        public static List<byte> playerIdList = [];

        public List<byte> PlayersDazzled = [];

        private static OptionItem KillCooldown;
        private static OptionItem ShapeshiftCooldown;
        private static OptionItem CauseVision;
        public static OptionItem DazzleLimitOpt;
        private static OptionItem ResetDazzledVisionOnDeath;
        public static OptionItem DazzlerAbilityUseGainWithEachKill;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Dazzler);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 11, "DazzleCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
                .SetValueFormat(OptionFormat.Seconds);
            CauseVision = FloatOptionItem.Create(Id + 13, "DazzlerCauseVision", new(0f, 5f, 0.05f), 0.4f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
                .SetValueFormat(OptionFormat.Multiplier);
            DazzleLimitOpt = IntegerOptionItem.Create(Id + 14, "DazzlerDazzleLimit", new(0, 15, 1), 1, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
                .SetValueFormat(OptionFormat.Times);
            ResetDazzledVisionOnDeath = BooleanOptionItem.Create(Id + 15, "DazzlerResetDazzledVisionOnDeath", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler]);
            DazzlerAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 16, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.5f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
            PlayersDazzled = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            PlayersDazzled = [];
            playerId.SetAbilityUseLimit(DazzleLimitOpt.GetInt());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();

        public override bool OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
        {
            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId) || !shapeshifting) return false;

            if (!PlayersDazzled.Contains(target.PlayerId) && PlayersDazzled.Count < pc.GetAbilityUseLimit())
            {
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Dazzler), GetString("DazzlerDazzled")));
                PlayersDazzled.Add(target.PlayerId);
                target.MarkDirtySettings();
            }

            return false;
        }

        public static void SetDazzled(PlayerControl player, IGameOptions opt)
        {
            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is Dazzler { IsEnable: true } dz)
                {
                    if (dz.PlayersDazzled.Contains(player.PlayerId) && (!ResetDazzledVisionOnDeath.GetBool() || Utils.GetPlayerById(state.Key).IsAlive()))
                    {
                        opt.SetVision(false);
                        opt.SetFloat(FloatOptionNames.CrewLightMod, CauseVision.GetFloat());
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, CauseVision.GetFloat());
                    }
                }
            }
        }
    }
}