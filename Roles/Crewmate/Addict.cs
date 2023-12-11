namespace TOHE.Roles.Crewmate
{
    using System.Collections.Generic;
    using UnityEngine;
    using static TOHE.Options;

    public static class Addict
    {
        private static readonly int Id = 5200;
        private static List<byte> playerIdList = [];

        public static OptionItem VentCooldown;
        public static OptionItem TimeLimit;
        public static OptionItem ImmortalTimeAfterVent;
        //     public static OptionItem SpeedWhileImmortal;
        public static OptionItem FreezeTimeAfterImmortal;

        private static Dictionary<byte, float> SuicideTimer = [];
        private static Dictionary<byte, float> ImmortalTimer = [];

        private static float DefaultSpeed;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Addict);
            VentCooldown = FloatOptionItem.Create(Id + 11, "VentCooldown", new(5f, 70f, 1f), 40f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Seconds);
            TimeLimit = FloatOptionItem.Create(Id + 12, "SerialKillerLimit", new(5f, 75f, 1f), 45f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Seconds);
            ImmortalTimeAfterVent = FloatOptionItem.Create(Id + 13, "AddictInvulnerbilityTimeAfterVent", new(0f, 30f, 1f), 10f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Seconds);
            //     SpeedWhileImmortal = FloatOptionItem.Create(Id + 14, "AddictSpeedWhileInvulnerble", new(0.25f, 5f, 0.25f), 1.75f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
            //       .SetValueFormat(OptionFormat.Multiplier);
            FreezeTimeAfterImmortal = FloatOptionItem.Create(Id + 15, "AddictFreezeTimeAfterInvulnerbility", new(0f, 10f, 1f), 3f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void Init()
        {
            playerIdList = [];
            SuicideTimer = [];
            ImmortalTimer = [];
            DefaultSpeed = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            SuicideTimer.TryAdd(playerId, -10f);
            ImmortalTimer.TryAdd(playerId, 420f);
            DefaultSpeed = Main.AllPlayerSpeed[playerId];
        }
        public static bool IsEnable => playerIdList.Count > 0;

        public static bool IsImmortal(PlayerControl player) => player.Is(CustomRoles.Addict) && ImmortalTimer[player.PlayerId] <= ImmortalTimeAfterVent.GetFloat();

        public static void OnReportDeadBody()
        {
            foreach (byte player in playerIdList.ToArray())
            {
                SuicideTimer[player] = -10f;
                ImmortalTimer[player] = 420f;
                Main.AllPlayerSpeed[player] = DefaultSpeed;
            }
        }

        public static void FixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask || !IsEnable || !SuicideTimer.ContainsKey(player.PlayerId) || !player.IsAlive()) return;

            if (SuicideTimer[player.PlayerId] >= TimeLimit.GetFloat())
            {
                player.Suicide();
                SuicideTimer.Remove(player.PlayerId);
            }
            else
            {
                SuicideTimer[player.PlayerId] += Time.fixedDeltaTime;

                if (IsImmortal(player))
                {
                    ImmortalTimer[player.PlayerId] += Time.fixedDeltaTime;
                }
                else
                {
                    if (ImmortalTimer[player.PlayerId] != 420f && FreezeTimeAfterImmortal.GetFloat() > 0)
                    {
                        AddictGetDown(player);
                        ImmortalTimer[player.PlayerId] = 420f;
                    }
                }
            }
        }

        public static void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (!pc.Is(CustomRoles.Addict)) return;

            SuicideTimer[pc.PlayerId] = 0f;
            ImmortalTimer[pc.PlayerId] = 0f;

            //   Main.AllPlayerSpeed[pc.PlayerId] = SpeedWhileImmortal.GetFloat();
            pc.MarkDirtySettings();
        }

        private static void AddictGetDown(PlayerControl addict)
        {
            Main.AllPlayerSpeed[addict.PlayerId] = Main.MinSpeed;
            ReportDeadBodyPatch.CanReport[addict.PlayerId] = false;
            addict.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                Main.AllPlayerSpeed[addict.PlayerId] = DefaultSpeed;
                ReportDeadBodyPatch.CanReport[addict.PlayerId] = true;
                addict.MarkDirtySettings();
            }, FreezeTimeAfterImmortal.GetFloat(), "AddictGetDown");
        }
    }
}