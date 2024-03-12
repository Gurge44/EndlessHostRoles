using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Impostor
{
    internal class Commander : RoleBase
    {
        public static List<Commander> PlayerList = [];
        public static bool On;
        public override bool IsEnable => On;

        private const int Id = 643560;
        public static OptionItem CannotSpawnAsSoloImp;

        public bool IsWhistling;
        public byte MarkedPlayer;
        public bool IsModeWhistle;
        private byte CommanderId;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Commander);
            CannotSpawnAsSoloImp = BooleanOptionItem.Create(Id + 2, "CannotSpawnAsSoloImp", true, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Commander]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            IsWhistling = false;
            MarkedPlayer = byte.MaxValue;
            CommanderId = playerId;
            PlayerList.Add(this);
        }

        public override void Init()
        {
            On = false;
            PlayerList = [];
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            IsModeWhistle = !IsModeWhistle;
        }

        public override void OnPet(PlayerControl pc)
        {
            if (IsModeWhistle) Whistle(pc);
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting) return true;

            if (IsModeWhistle) Whistle(shapeshifter);
            else MarkPlayer(target);

            return false;
        }

        void Whistle(PlayerControl commander)
        {
            if (IsWhistling) return;

            IsWhistling = true;
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (!pc.Is(Team.Impostor)) continue;
                TargetArrow.Add(pc.PlayerId, commander.PlayerId);
                pc.Notify(Translator.GetString("CommanderNotify"));
            }
        }

        void MarkPlayer(PlayerControl target)
        {
            if (target == null)
            {
                MarkedPlayer = byte.MaxValue;
                return;
            }

            MarkedPlayer = target.PlayerId;
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || !GameStates.IsInTask || !On || !IsWhistling) return;

            if (TargetArrow.GetArrows(pc, CommanderId) == "・")
            {
                TargetArrow.Remove(pc.PlayerId, CommanderId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }
        }

        public override void OnReportDeadBody()
        {
            IsWhistling = false;
            MarkedPlayer = byte.MaxValue;
        }

        public static string GetSuffixText(PlayerControl seer, PlayerControl target, bool hud = false)
        {
            if (seer == null || !seer.Is(Team.Impostor)) return string.Empty;

            if (seer.PlayerId == target.PlayerId && Main.PlayerStates[seer.PlayerId].Role is Commander { IsEnable: true } cm)
            {
                if (seer.IsModClient() && !hud) return string.Empty;
                return string.Format(Translator.GetString("WMMode"), cm.IsModeWhistle ? Translator.GetString("CommanderWhistleMode") : Translator.GetString("CommanderMarkMode"));
            }

            bool isTargetTarget = PlayerList.Any(x => x.MarkedPlayer == target.PlayerId);
            string arrowToCommander = PlayerList.Aggregate(string.Empty, (result, commander) => result + TargetArrow.GetArrows(seer, commander.CommanderId));

            if (seer.PlayerId == target.PlayerId)
            {
                if (arrowToCommander.Length > 0)
                {
                    return $"{Translator.GetString("Commander")} {arrowToCommander}";
                }
            }
            else if (isTargetTarget)
            {
                return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sprayer), Translator.GetString("CommanderTarget"));
            }

            return string.Empty;
        }
    }
}
