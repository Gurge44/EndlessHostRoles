using System.Collections.Generic;
using EHR.Modules;
using UnityEngine;

namespace EHR.Impostor
{
    public class SerialKiller : RoleBase
    {
        private const int Id = 1700;
        public static List<byte> PlayerIdList = [];

        private static OptionItem KillCooldown;
        private static OptionItem TimeLimit;
        private static OptionItem WaitFor1Kill;

        private float SuicideTimer;
        private int Timer;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.SerialKiller);

            KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller])
                .SetValueFormat(OptionFormat.Seconds);

            TimeLimit = new FloatOptionItem(Id + 11, "SerialKillerLimit", new(5f, 180f, 5f), 40f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller])
                .SetValueFormat(OptionFormat.Seconds);

            WaitFor1Kill = new BooleanOptionItem(Id + 12, "WaitFor1Kill", true, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller]);
        }

        public override void Init()
        {
            PlayerIdList = [];
            SuicideTimer = TimeLimit.GetFloat();
            Timer = TimeLimit.GetInt();
        }

        public override void Add(byte serial)
        {
            PlayerIdList.Add(serial);
            Timer = TimeLimit.GetInt();
            SuicideTimer = TimeLimit.GetFloat();
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        private static bool HasKilled(PlayerControl pc)
        {
            return pc != null && pc.Is(CustomRoles.SerialKiller) && pc.IsAlive() && (Main.PlayerStates[pc.PlayerId].GetKillCount(true) > 0 || !WaitFor1Kill.GetBool());
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!killer.Is(CustomRoles.SerialKiller)) return true;

            SuicideTimer = float.NaN;
            Timer = TimeLimit.GetInt();
            killer.MarkDirtySettings();
            return true;
        }

        public override void OnReportDeadBody()
        {
            SuicideTimer = float.NaN;
            Timer = TimeLimit.GetInt();
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask) return;

            if (!HasKilled(player))
            {
                SuicideTimer = float.NaN;
                Timer = TimeLimit.GetInt();
                return;
            }

            if (float.IsNaN(SuicideTimer)) return;

            if (SuicideTimer >= TimeLimit.GetFloat())
            {
                player.Suicide();
                SuicideTimer = float.NaN;
                Timer = TimeLimit.GetInt();

                if (player.IsLocalPlayer())
                    Achievements.Type.OutOfTime.Complete();
            }
            else
            {
                SuicideTimer += Time.fixedDeltaTime;
                int tempTimer = Timer;
                Timer = TimeLimit.GetInt() - (int)SuicideTimer;
                if (Timer != tempTimer && Timer <= 20 && !player.IsModClient()) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
            }
        }

        public override void AfterMeetingTasks()
        {
            SuicideTimer = 0f;
            Timer = TimeLimit.GetInt();
        }
    }
}