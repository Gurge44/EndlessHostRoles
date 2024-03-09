using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor
{
    public class Devourer : RoleBase
    {
        private static readonly GameData.PlayerOutfit ConsumedOutfit = new GameData.PlayerOutfit().Set("", 15, "", "", "visor_Crack", "", "");
        private static Dictionary<byte, GameData.PlayerOutfit> OriginalPlayerSkins = [];

        private const int Id = 3550;
        public static List<byte> playerIdList = [];

        private static OptionItem DefaultKillCooldown;
        private static OptionItem ReduceKillCooldown;
        private static OptionItem MinKillCooldown;

        private static OptionItem ShapeshiftCooldown;

        //   private static OptionItem ShapeshiftDuration;
        public static OptionItem HideNameOfConsumedPlayer;

        public List<byte> PlayerSkinsCosumed = [];
        private float NowCooldown;

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Devourer);
            DefaultKillCooldown = FloatOptionItem.Create(Id + 10, "SansDefaultKillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Devourer])
                .SetValueFormat(OptionFormat.Seconds);
            ReduceKillCooldown = FloatOptionItem.Create(Id + 11, "SansReduceKillCooldown", new(0f, 30f, 0.5f), 1.5f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Devourer])
                .SetValueFormat(OptionFormat.Seconds);
            MinKillCooldown = FloatOptionItem.Create(Id + 12, "SansMinKillCooldown", new(0f, 30f, 0.5f), 21f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Devourer])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 14, "DevourCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Devourer])
                .SetValueFormat(OptionFormat.Seconds);
            HideNameOfConsumedPlayer = BooleanOptionItem.Create(Id + 16, "DevourerHideNameConsumed", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Devourer]);
        }

        public override void Init()
        {
            playerIdList = [];
            PlayerSkinsCosumed = [];
            OriginalPlayerSkins = [];
            NowCooldown = DefaultKillCooldown.GetFloat();
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            PlayerSkinsCosumed = [];
            NowCooldown = DefaultKillCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = NowCooldown;

        public override bool OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
        {
            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId) || !shapeshifting) return false;

            if (!PlayerSkinsCosumed.Contains(target.PlayerId))
            {
                if (!Camouflage.IsCamouflage)
                {
                    SetSkin(target, ConsumedOutfit);
                }

                PlayerSkinsCosumed.Add(target.PlayerId);
                pc.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Devourer), GetString("DevourerEatenSkin")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Devourer), GetString("EatenByDevourer")));
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: pc);

                OriginalPlayerSkins.Add(target.PlayerId, Camouflage.PlayerSkins[target.PlayerId]);
                Camouflage.PlayerSkins[target.PlayerId] = ConsumedOutfit;

                float cdReduction = ReduceKillCooldown.GetFloat() * PlayerSkinsCosumed.Count;
                float cd = DefaultKillCooldown.GetFloat() - cdReduction;

                NowCooldown = cd < MinKillCooldown.GetFloat() ? MinKillCooldown.GetFloat() : cd;
            }

            return false;
        }

        public static void OnDevourerDied(byte Devourer)
        {
            if (Main.PlayerStates[Devourer].Role is not Devourer { IsEnable: true } dv) return;

            foreach (byte player in dv.PlayerSkinsCosumed.ToArray())
            {
                Camouflage.PlayerSkins[player] = OriginalPlayerSkins[player];
                if (!Camouflage.IsCamouflage)
                {
                    PlayerControl pc = Main.AllAlivePlayerControls.FirstOrDefault(a => a.PlayerId == player);
                    if (pc == null) continue;
                    SetSkin(pc, OriginalPlayerSkins[player]);
                }
            }

            dv.PlayerSkinsCosumed.Clear();
        }

        private static void SetSkin(PlayerControl target, GameData.PlayerOutfit outfit)
        {
            var sender = CustomRpcSender.Create(name: $"Camouflage.RpcSetSkin({target.Data.PlayerName})");

            target.SetColor(outfit.ColorId);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetColor)
                .Write(outfit.ColorId)
                .EndRpc();

            target.SetHat(outfit.HatId, outfit.ColorId);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetHatStr)
                .Write(outfit.HatId)
                .EndRpc();

            target.SetSkin(outfit.SkinId, outfit.ColorId);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetSkinStr)
                .Write(outfit.SkinId)
                .EndRpc();

            target.SetVisor(outfit.VisorId, outfit.ColorId);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetVisorStr)
                .Write(outfit.VisorId)
                .EndRpc();

            target.SetPet(outfit.PetId);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetPetStr)
                .Write(outfit.PetId)
                .EndRpc();

            sender.SendMessage();
        }
    }
}