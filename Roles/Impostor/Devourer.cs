using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor
{
    public class Devourer : RoleBase
    {
        private const int Id = 3550;
        private static readonly NetworkedPlayerInfo.PlayerOutfit ConsumedOutfit = new NetworkedPlayerInfo.PlayerOutfit().Set("", 15, "", "", "visor_Crack", "", "");
        private static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> OriginalPlayerSkins = [];
        public static List<byte> PlayerIdList = [];

        private static OptionItem DefaultKillCooldown;
        private static OptionItem ReduceKillCooldown;
        private static OptionItem MinKillCooldown;
        private static OptionItem ShapeshiftCooldown;
        public static OptionItem HideNameOfConsumedPlayer;
        private float NowCooldown;

        public List<byte> PlayerSkinsCosumed = [];

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Devourer);
            DefaultKillCooldown = new FloatOptionItem(Id + 10, "SansDefaultKillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Devourer])
                .SetValueFormat(OptionFormat.Seconds);
            ReduceKillCooldown = new FloatOptionItem(Id + 11, "SansReduceKillCooldown", new(0f, 30f, 0.5f), 1.5f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Devourer])
                .SetValueFormat(OptionFormat.Seconds);
            MinKillCooldown = new FloatOptionItem(Id + 12, "SansMinKillCooldown", new(0f, 30f, 0.5f), 21f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Devourer])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = new FloatOptionItem(Id + 14, "DevourCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Devourer])
                .SetValueFormat(OptionFormat.Seconds);
            HideNameOfConsumedPlayer = new BooleanOptionItem(Id + 16, "DevourerHideNameConsumed", true, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Devourer]);
        }

        public override void Init()
        {
            PlayerIdList = [];
            PlayerSkinsCosumed = [];
            OriginalPlayerSkins = [];
            NowCooldown = DefaultKillCooldown.GetFloat();
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
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

        private static void SetSkin(PlayerControl target, NetworkedPlayerInfo.PlayerOutfit outfit)
        {
            var sender = CustomRpcSender.Create(name: $"Camouflage.RpcSetSkin({target.Data.PlayerName})");

            target.SetColor(outfit.ColorId);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetColor)
                .Write(target.Data.NetId)
                .Write((byte)outfit.ColorId)
                .EndRpc();

            target.SetHat(outfit.HatId, outfit.ColorId);
            target.Data.DefaultOutfit.HatSequenceId += 10;
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetHatStr)
                .Write(outfit.HatId)
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetHatStr))
                .EndRpc();

            target.SetSkin(outfit.SkinId, outfit.ColorId);
            target.Data.DefaultOutfit.SkinSequenceId += 10;
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetSkinStr)
                .Write(outfit.SkinId)
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
                .EndRpc();

            target.SetVisor(outfit.VisorId, outfit.ColorId);
            target.Data.DefaultOutfit.VisorSequenceId += 10;
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetVisorStr)
                .Write(outfit.VisorId)
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
                .EndRpc();

            target.SetPet(outfit.PetId);
            target.Data.DefaultOutfit.PetSequenceId += 10;
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetPetStr)
                .Write(outfit.PetId)
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetPetStr))
                .EndRpc();

            sender.SendMessage();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton?.OverrideText(GetString("DevourerButtonText"));
        }
    }
}