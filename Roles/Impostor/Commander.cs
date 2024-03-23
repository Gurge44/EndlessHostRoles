using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Roles.Impostor
{
    internal class Commander : RoleBase
    {
        public static List<Commander> PlayerList = [];
        public static bool On;
        public override bool IsEnable => On;

        private const int Id = 643560;
        public static OptionItem CannotSpawnAsSoloImp;
        private static OptionItem ShapeshiftCooldown;

        enum Mode
        {
            Whistle,
            Mark,
            KillAnyone,
            DontKillMark,
            DontSabotage,
            UseAbility
        }

        public bool IsWhistling;
        public byte MarkedPlayer;
        public HashSet<byte> DontKillMarks = [];
        private Mode CurrentMode;
        private byte CommanderId;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Commander);
            CannotSpawnAsSoloImp = BooleanOptionItem.Create(Id + 2, "CannotSpawnAsSoloImp", true, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Commander]);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 3, "ShapeshiftCooldown", new(0f, 60f, 1f), 1f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Commander]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            IsWhistling = false;
            MarkedPlayer = byte.MaxValue;
            DontKillMarks = [];
            CurrentMode = Mode.Whistle;
            CommanderId = playerId;
            PlayerList.Add(this);
        }

        public override void Init()
        {
            On = false;
            PlayerList = [];
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetValue();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            if (!Options.UsePets.GetBool()) CycleMode(physics.myPlayer);
        }

        public override void OnPet(PlayerControl pc)
        {
            CycleMode(pc);
        }

        void CycleMode(PlayerControl pc)
        {
            CurrentMode = (Mode)(((int)CurrentMode + 1) % Enum.GetValues(typeof(Mode)).Length);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting) return true;

            switch (CurrentMode)
            {
                case Mode.Whistle:
                    Whistle(shapeshifter, target.Is(Team.Impostor) ? target : null);
                    break;
                case Mode.Mark:
                    MarkPlayer(target);
                    break;
                case Mode.KillAnyone:
                    if (target.Is(Team.Impostor)) target.Notify(Translator.GetString("CommanderKillAnyoneNotify"), 7f);
                    else
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            if (!pc.Is(Team.Impostor) || pc.PlayerId == shapeshifter.PlayerId) continue;
                            pc.Notify(Translator.GetString("CommanderKillAnyoneNotify"), 7f);
                        }
                    }

                    break;
                case Mode.DontKillMark:
                    if (target.Is(Team.Impostor)) target.Notify(Translator.GetString("CommanderDontKillAnyoneNotify"), 7f);
                    else MarkPlayerAsDontKill(target);
                    break;
                case Mode.DontSabotage:
                    foreach (var pc in Main.AllAlivePlayerControls)
                    {
                        if (!pc.Is(Team.Impostor) || pc.PlayerId == shapeshifter.PlayerId) continue;
                        pc.Notify(Translator.GetString("CommanderDontSabotageNotify"), 7f);
                    }

                    break;
                case Mode.UseAbility:
                    if (target.Is(Team.Impostor)) target.Notify(Translator.GetString("CommanderUseAbilityNotify"), 7f);
                    else
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            if (!pc.Is(Team.Impostor) || pc.PlayerId == shapeshifter.PlayerId) continue;
                            pc.Notify(Translator.GetString("CommanderUseAbilityNotify"), 7f);
                        }
                    }

                    break;
            }

            return false;
        }

        void Whistle(PlayerControl commander, PlayerControl target = null)
        {
            if (IsWhistling) return;

            IsWhistling = true;

            if (target != null)
            {
                AddArrowAndNotify(target);
                return;
            }

            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (!pc.Is(Team.Impostor) || pc.Is(CustomRoles.Commander)) continue;
                AddArrowAndNotify(pc);
            }

            return;

            void AddArrowAndNotify(PlayerControl pc)
            {
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

            Utils.NotifyRoles(SpecifyTarget: target);
        }

        void MarkPlayerAsDontKill(PlayerControl target)
        {
            if (target == null) return;

            DontKillMarks.Add(target.PlayerId);
            Utils.NotifyRoles(SpecifyTarget: target);
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || !GameStates.IsInTask || !On || !IsWhistling) return;

            if (TargetArrow.GetArrows(pc, CommanderId) == "・")
            {
                TargetArrow.Remove(pc.PlayerId, CommanderId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }

            if (Main.AllPlayerControls.Where(x => x.Is(Team.Impostor)).All(x => TargetArrow.GetArrows(x, CommanderId) == string.Empty)) IsWhistling = false;
        }

        public override void OnReportDeadBody()
        {
            IsWhistling = false;
            MarkedPlayer = byte.MaxValue;
            DontKillMarks = [];
        }

        public static string GetSuffixText(PlayerControl seer, PlayerControl target, bool hud = false)
        {
            if (seer == null || !seer.Is(Team.Impostor)) return string.Empty;

            if (seer.PlayerId == target.PlayerId && Main.PlayerStates[seer.PlayerId].Role is Commander { IsEnable: true } cm)
            {
                if (seer.IsModClient() && !hud) return string.Empty;
                string whistlingText = cm.IsWhistling ? $"\n<size=70%>{Translator.GetString("CommanderWhistling")}</size>" : string.Empty;
                return $"{string.Format(Translator.GetString("WMMode"), Translator.GetString($"Commander{cm.CurrentMode}Mode"))}{whistlingText}";
            }

            bool isTargetTarget = PlayerList.Any(x => x.MarkedPlayer == target.PlayerId);
            bool isTargetDontKill = PlayerList.Any(x => x.DontKillMarks.Contains(target.PlayerId));
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
            else if (isTargetDontKill)
            {
                return Utils.ColorString(ColorUtility.TryParseHtmlString("#0daeff", out Color color) ? color : Utils.GetRoleColor(CustomRoles.TaskManager), Translator.GetString("CommanderDontKill"));
            }

            return string.Empty;
        }
    }
}