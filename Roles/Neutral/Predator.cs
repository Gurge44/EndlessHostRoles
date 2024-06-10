﻿using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using HarmonyLib;
using Hazel;

namespace EHR.Roles.Neutral
{
    internal class Predator : RoleBase
    {
        private const int Id = 643540;
        public static bool On;
        private static OptionItem NumOfRolesToKill;
        private static OptionItem MaxImpRolePicks;
        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpVision;
        public bool IsWon;

        private List<CustomRoles> RolesToKill = [];
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Predator);
            NumOfRolesToKill = new IntegerOptionItem(Id + 2, "NumOfRolesToKill", new(1, 10, 1), 3, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator]);
            MaxImpRolePicks = new IntegerOptionItem(Id + 3, "MaxImpRolePicks", new(1, 10, 1), 1, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator]);
            KillCooldown = new FloatOptionItem(Id + 4, "KillCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(Id + 5, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator]);
            HasImpVision = new BooleanOptionItem(Id + 6, "ImpostorVision", false, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            IsWon = false;

            LateTask.New(() =>
            {
                RolesToKill = [];

                var allRoles = Enum.GetValues<CustomRoles>().ToList();
                allRoles.RemoveAll(x => x == CustomRoles.Predator || x >= CustomRoles.NotAssigned || !x.RoleExist(countDead: true));

                var r = IRandom.Instance;
                var impRoles = 0;

                for (int i = 0; i < NumOfRolesToKill.GetInt(); i++)
                {
                    var index = r.Next(allRoles.Count);
                    var role = allRoles[index];
                    allRoles.RemoveAt(index);

                    if (role.Is(Team.Impostor))
                    {
                        if (impRoles >= MaxImpRolePicks.GetInt())
                        {
                            i--;
                            continue;
                        }

                        impRoles++;
                    }

                    RolesToKill.Add(role);
                }

                Logger.Info($"Predator Roles: {RolesToKill.Join()}", "Predator");

                var w = Utils.CreateRPC(CustomRPC.SyncPredator);
                w.WritePacked(1);
                w.WritePacked(RolesToKill.Count);
                foreach (var role in RolesToKill)
                {
                    w.WritePacked((int)role);
                }

                Utils.EndRPC(w);
                Utils.SendRPC(CustomRPC.SyncPredator, playerId, 2, IsWon);
            }, 3f, "Select Predator Roles");
        }

        public void ReceiveRPC(MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    RolesToKill = [];
                    var count = reader.ReadPackedInt32();
                    for (int i = 0; i < count; i++)
                    {
                        RolesToKill.Add((CustomRoles)reader.ReadPackedInt32());
                    }

                    break;
                case 2:
                    IsWon = reader.ReadBoolean();
                    break;
            }
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(HasImpVision.GetBool());
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return !IsWon;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (IsWon) return false;

            var targetRole = target.GetCustomRole();
            if (RolesToKill.Contains(targetRole))
            {
                IsWon = true;
                if (!killer.IsModClient()) killer.Notify(string.Format(Translator.GetString("PredatorCorrectKill"), Translator.GetString($"{targetRole}")));
                return true;
            }

            killer.Suicide();
            return false;
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
        {
            if (seer.PlayerId != target.PlayerId) return string.Empty;
            if (seer.IsModClient() && !hud) return string.Empty;
            if (Main.PlayerStates[seer.PlayerId].Role is not Predator { IsEnable: true } pt) return string.Empty;
            if (pt.IsWon) return !hud ? "<#00ff00>\u2713</color>" : Translator.GetString("PredatorDone");
            var text = pt.RolesToKill.Join(x => x.ToColoredString());
            return hud ? text : $"<size=1.7>{text}</size>";
        }
    }
}