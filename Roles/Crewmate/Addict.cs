using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Roles.Crewmate
{
    using static Options;

    public class Addict : RoleBase
    {
        private const int Id = 5200;
        private static List<byte> playerIdList = [];

        public static OptionItem VentCooldown;
        public static OptionItem TimeLimit;

        public static OptionItem ImmortalTimeAfterVent;

        public static OptionItem SpeedWhileImmortal;
        public static OptionItem FreezeTimeAfterImmortal;

        private static float DefaultSpeed;
        private float ImmortalTimer = 420f;

        private float SuicideTimer = -10f;

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Addict);
            VentCooldown = new FloatOptionItem(Id + 11, "VentCooldown", new(5f, 70f, 1f), 40f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Seconds);
            TimeLimit = new FloatOptionItem(Id + 12, "SerialKillerLimit", new(5f, 75f, 1f), 45f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Seconds);
            ImmortalTimeAfterVent = new FloatOptionItem(Id + 13, "AddictInvulnerbilityTimeAfterVent", new(0f, 30f, 1f), 10f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Seconds);
            SpeedWhileImmortal = new FloatOptionItem(Id + 14, "AddictSpeedWhileInvulnerble", new(0.25f, 5f, 0.25f), 1.75f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Multiplier);
            FreezeTimeAfterImmortal = new FloatOptionItem(Id + 15, "AddictFreezeTimeAfterInvulnerbility", new(0f, 10f, 1f), 3f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            playerIdList = [];
            SuicideTimer = -10f;
            ImmortalTimer = 420f;
            DefaultSpeed = new();
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            SuicideTimer = -10f;
            ImmortalTimer = 420f;
            DefaultSpeed = Main.AllPlayerSpeed[playerId];
        }

        bool IsImmortal(PlayerControl player) => player.Is(CustomRoles.Addict) && ImmortalTimer <= ImmortalTimeAfterVent.GetFloat();

        public override void OnReportDeadBody()
        {
            foreach (byte player in playerIdList.ToArray())
            {
                SuicideTimer = -10f;
                ImmortalTimer = 420f;
                Main.AllPlayerSpeed[player] = DefaultSpeed;
            }
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !IsImmortal(target);
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask || !IsEnable || Math.Abs(SuicideTimer - (-10f)) < 0.5f || !player.IsAlive()) return;

            if (SuicideTimer >= TimeLimit.GetFloat())
            {
                player.Suicide();
                SuicideTimer = -10f;
            }
            else
            {
                SuicideTimer += Time.fixedDeltaTime;

                if (IsImmortal(player))
                {
                    ImmortalTimer += Time.fixedDeltaTime;
                }
                else
                {
                    if (Math.Abs(ImmortalTimer - 420f) > 0.5f && FreezeTimeAfterImmortal.GetFloat() > 0)
                    {
                        AddictGetDown(player);
                        ImmortalTimer = 420f;
                    }
                }
            }
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (!pc.Is(CustomRoles.Addict)) return;

            SuicideTimer = 0f;
            ImmortalTimer = 0f;

            Main.AllPlayerSpeed[pc.PlayerId] = SpeedWhileImmortal.GetFloat();
            pc.MarkDirtySettings();
        }

        private static void AddictGetDown(PlayerControl addict)
        {
            Main.AllPlayerSpeed[addict.PlayerId] = Main.MinSpeed;
            ReportDeadBodyPatch.CanReport[addict.PlayerId] = false;
            addict.MarkDirtySettings();
            LateTask.New(() =>
            {
                Main.AllPlayerSpeed[addict.PlayerId] = DefaultSpeed;
                ReportDeadBodyPatch.CanReport[addict.PlayerId] = true;
                addict.MarkDirtySettings();
            }, FreezeTimeAfterImmortal.GetFloat(), "AddictGetDown");
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton?.OverrideText(Translator.GetString("AddictVentButtonText"));
        }
    }
}