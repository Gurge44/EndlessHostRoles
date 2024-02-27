using AmongUs.GameOptions;
using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral
{
    public class Spiritcaller : RoleBase
    {
        private const int Id = 13400;
        private static List<byte> playerIdList = [];

        private static Dictionary<byte, long> PlayersHaunted = [];

        private static OptionItem KillCooldown;
        public static OptionItem CanVent;
        public static OptionItem ImpostorVision;
        private static OptionItem SpiritMax;
        public static OptionItem SpiritAbilityCooldown;
        private static OptionItem SpiritFreezeTime;
        private static OptionItem SpiritProtectTime;
        private static OptionItem SpiritCauseVision;
        private static OptionItem SpiritCauseVisionTime;

        private static long ProtectTimeStamp;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.Spiritcaller, 1, zeroOne: false);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 60f, 2.5f), 22.5f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller]);
            ImpostorVision = BooleanOptionItem.Create(Id + 12, "ImpostorVision", true, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller]);
            SpiritMax = IntegerOptionItem.Create(Id + 13, "SpiritcallerSpiritMax", new(1, 15, 1), 2, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
                .SetValueFormat(OptionFormat.Times);
            SpiritAbilityCooldown = FloatOptionItem.Create(Id + 14, "SpiritcallerSpiritAbilityCooldown", new(5f, 90f, 1f), 30f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
                .SetValueFormat(OptionFormat.Seconds);
            SpiritFreezeTime = FloatOptionItem.Create(Id + 15, "SpiritcallerFreezeTime", new(0f, 30f, 1f), 3f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
                .SetValueFormat(OptionFormat.Seconds);
            SpiritProtectTime = FloatOptionItem.Create(Id + 16, "SpiritcallerProtectTime", new(0f, 30f, 1f), 5f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
                .SetValueFormat(OptionFormat.Seconds);
            SpiritCauseVision = FloatOptionItem.Create(Id + 17, "SpiritcallerCauseVision", new(0f, 5f, 0.05f), 0.4f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
                .SetValueFormat(OptionFormat.Multiplier);
            SpiritCauseVisionTime = FloatOptionItem.Create(Id + 18, "SpiritcallerCauseVisionTime", new(0f, 45f, 1f), 10f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritcaller])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            playerIdList = [];
            ProtectTimeStamp = new();
            PlayersHaunted = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(SpiritMax.GetInt());
            ProtectTimeStamp = 0;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override bool IsEnable => playerIdList.Count > 0;
        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public static bool InProtect(PlayerControl player) => player.Is(CustomRoles.Spiritcaller) && ProtectTimeStamp > Utils.TimeStamp;

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer.GetAbilityUseLimit() < 1) return true;

            if (!target.GetCustomRole().IsAbleToBeSidekicked() && !target.GetCustomRole().IsImpostor())
            {
                killer.RpcRemoveAbilityUse();

                target.RpcSetCustomRole(CustomRoles.EvilSpirit);

                var writer = CustomRpcSender.Create("SpiritCallerSendMessage");
                writer.StartMessage(target.GetClientId());
                writer.StartRpc(target.NetId, (byte)RpcCalls.SetName)
                    .Write(GetString("SpiritcallerNoticeTitle"))
                    .EndRpc();
                writer.StartRpc(target.NetId, (byte)RpcCalls.SendChat)
                    .Write(GetString("SpiritcallerNoticeMessage"))
                    .EndRpc();
                writer.StartRpc(target.NetId, (byte)RpcCalls.SetName)
                    .Write(target.Data.PlayerName)
                    .EndRpc();
                writer.EndMessage();
                writer.SendMessage();
            }

            return true;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask) return;

            if (pc.Is(CustomRoles.Spiritcaller))
            {
                if (ProtectTimeStamp < Utils.TimeStamp && ProtectTimeStamp != 0)
                {
                    ProtectTimeStamp = 0;
                }
            }
            else if (PlayersHaunted.ContainsKey(pc.PlayerId) && PlayersHaunted[pc.PlayerId] < Utils.TimeStamp)
            {
                PlayersHaunted.Remove(pc.PlayerId);
                pc.MarkDirtySettings();
            }
        }

        public static void HauntPlayer(PlayerControl target)
        {
            if (SpiritCauseVisionTime.GetFloat() > 0 || SpiritFreezeTime.GetFloat() > 0)
            {
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Spiritcaller), GetString("HauntedByEvilSpirit")));
            }

            if (SpiritCauseVisionTime.GetFloat() > 0 && !PlayersHaunted.ContainsKey(target.PlayerId))
            {
                long time = Utils.TimeStamp + (long)SpiritCauseVisionTime.GetFloat();
                PlayersHaunted.Add(target.PlayerId, time);
            }

            if (SpiritFreezeTime.GetFloat() > 0)
            {
                var tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
                target.MarkDirtySettings();
                _ = new LateTask(() =>
                {
                    Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed;
                    ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
                    target.MarkDirtySettings();
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
                }, SpiritFreezeTime.GetFloat());
            }
        }

        public static void ReduceVision(IGameOptions opt, PlayerControl target)
        {
            if (PlayersHaunted.ContainsKey(target.PlayerId))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, SpiritCauseVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, SpiritCauseVision.GetFloat());
            }
        }

        public static void ProtectSpiritcaller()
        {
            ProtectTimeStamp = Utils.TimeStamp + (long)SpiritProtectTime.GetFloat();
        }
    }
}