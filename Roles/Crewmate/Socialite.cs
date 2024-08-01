using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Crewmate
{
    public class Socialite : RoleBase
    {
        public static bool On;
        private static List<Socialite> Instances = [];

        private static OptionItem Cooldown;
        public static OptionItem UsePet;
        public static OptionItem CancelVote;
        public HashSet<byte> GuestList = [];
        public byte MarkedPlayerId;
        private byte SocialiteId;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            int id = 647300;
            Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Socialite);
            Cooldown = new FloatOptionItem(++id, "AbilityCooldown", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Socialite])
                .SetValueFormat(OptionFormat.Seconds);
            UsePet = Options.CreatePetUseSetting(++id, CustomRoles.Socialite);
            CancelVote = Options.CreateVoteCancellingUseSetting(++id, CustomRoles.Socialite, TabGroup.CrewmateRoles);
        }

        public override void Init()
        {
            On = false;
            Instances = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
            Instances.Add(this);
            GuestList = [];
            SocialiteId = playerId;
            MarkedPlayerId = byte.MaxValue;
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Cooldown.GetFloat();
        public override bool CanUseKillButton(PlayerControl pc) => pc.IsAlive() && GuestList.Count < Main.PlayerStates.Count;
        public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target) || MarkedPlayerId != byte.MaxValue) return false;

            MarkedPlayerId = target.PlayerId;
            Utils.SendRPC(CustomRPC.SyncRoleData, SocialiteId, 1, MarkedPlayerId);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            return false;
        }

        public override void OnReportDeadBody()
        {
            MarkedPlayerId = byte.MaxValue;
            Utils.SendRPC(CustomRPC.SyncRoleData, SocialiteId, 1, MarkedPlayerId);
        }

        public static bool OnAnyoneCheckMurder(PlayerControl killer, PlayerControl target)
        {
            foreach (var socialite in Instances)
            {
                if (socialite.MarkedPlayerId == target.PlayerId && socialite.GuestList.Add(killer.PlayerId))
                {
                    Utils.SendRPC(CustomRPC.SyncRoleData, socialite.SocialiteId, 2, killer.PlayerId);
                    Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(socialite.SocialiteId), SpecifyTarget: killer);
                    return false;
                }
            }

            return true;
        }

        public override bool OnVote(PlayerControl pc, PlayerControl target)
        {
            if (pc == null || target == null || pc.PlayerId == target.PlayerId || Main.DontCancelVoteList.Contains(pc.PlayerId)) return false;

            if (GuestList.Add(target.PlayerId))
            {
                Utils.SendRPC(CustomRPC.SyncRoleData, SocialiteId, 2, target.PlayerId);
                Main.DontCancelVoteList.Add(pc.PlayerId);
                return true;
            }

            return false;
        }

        public void ReceiveRPC(Hazel.MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    MarkedPlayerId = reader.ReadByte();
                    break;
                case 2:
                    GuestList.Add(reader.ReadByte());
                    break;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != SocialiteId || seer.PlayerId != target.PlayerId || (seer.IsModClient() && !isHUD)) return string.Empty;
            return string.Format(Translator.GetString("Socialite.Suffix"), MarkedPlayerId.ColoredPlayerName());
        }
    }
}