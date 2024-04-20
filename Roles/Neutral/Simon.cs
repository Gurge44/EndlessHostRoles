using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral
{
    public class Simon : RoleBase
    {
        public enum Instruction
        {
            None,
            Kill,
            Task
        }

        private const int Id = 12800;
        public static bool On;
        public static List<Simon> Instances = [];

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        private bool DoMode;
        private bool Executed;

        private Dictionary<byte, (bool DoAction, Instruction Instruction)> MarkedPlayers;

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Simon);
            KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Simon])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Simon]);
            HasImpostorVision = BooleanOptionItem.Create(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Simon]);
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
            MarkedPlayers = [];
            DoMode = true;
            Executed = false;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseSabotage(PlayerControl pc) => true;

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            return killer.CheckDoubleTrigger(target, () =>
            {
                MarkedPlayers[target.PlayerId] = (DoMode, Main.PlayerStates[target.PlayerId].Role.CanUseKillButton(target) ? Instruction.Kill : target.GetTaskState().hasTasks ? Instruction.Task : Instruction.None);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            });
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            if (Executed || MarkedPlayers.Count == 0) return;

            MarkedPlayers.Where(x => x.Value.Instruction == Instruction.None).ToList().ForEach(x => MarkedPlayers.Remove(x.Key));

            Executed = true;
            foreach (var kvp in MarkedPlayers)
            {
                var pc = Utils.GetPlayerById(kvp.Key);
                if (pc == null || pc.IsAlive()) continue;

                pc.Notify(Translator.GetString(GetNotify(kvp.Value.Instruction, kvp.Value.DoAction, false)), 300f);
            }
        }

        static string GetNotify(Instruction instruction, bool doAction, bool forSimon)
        {
            if (!forSimon)
            {
                if (instruction == Instruction.Kill) return doAction ? "SimonKill" : "SimonDontKill";
                return doAction ? "SimonTask" : "SimonDontTask";
            }

            return doAction ? "SimonDoShort" : "SimonDontShort";
        }

        public override void OnPet(PlayerControl pc)
        {
            DoMode = !DoMode;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            OnPet(pc);
            return false;
        }

        public override void OnReportDeadBody()
        {
            if (Executed)
            {
                Executed = false;
                foreach (var kvp in MarkedPlayers)
                {
                    if (!kvp.Value.DoAction) continue;
                    var pc = Utils.GetPlayerById(kvp.Key);
                    if (pc == null || !pc.IsAlive()) continue;
                    pc.Suicide();
                }
            }

            MarkedPlayers.Clear();
        }

        public static string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false)
        {
            if (Main.PlayerStates[seer.PlayerId].Role is not Simon simon) return string.Empty;
            bool self = seer.PlayerId == target.PlayerId;
            if (seer.IsModClient() && !hud && self) return string.Empty;

            if (self) return Translator.GetString(simon.DoMode ? "SimonDoMode" : "SimonDontDoMode");
            else if (simon.MarkedPlayers.TryGetValue(target.PlayerId, out var value)) return Translator.GetString(GetNotify(value.Instruction, value.DoAction, true));

            return string.Empty;
        }

        public static void RemoveTarget(PlayerControl pc, Instruction instruction)
        {
            foreach (var simon in Instances)
            {
                if (simon.MarkedPlayers.TryGetValue(pc.PlayerId, out var value))
                {
                    if (value.Instruction != instruction) continue;
                    if (value.DoAction) pc.Notify(Utils.ColorString(Color.green, "\u2713"));
                    else pc.Suicide();
                    simon.MarkedPlayers.Remove(pc.PlayerId);
                }
            }
        }
    }
}