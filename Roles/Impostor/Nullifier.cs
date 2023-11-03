using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    internal class Nullifier
    {
        private static readonly int Id = 643000;
        public static List<byte> playerIdList = new();

        public static OptionItem NullCD;
        private static OptionItem KCD;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Nullifier);
            NullCD = FloatOptionItem.Create(Id + 10, "NullCD", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Nullifier])
                .SetValueFormat(OptionFormat.Seconds);
            KCD = FloatOptionItem.Create(Id + 11, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Nullifier])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            playerIdList = new();
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static bool IsEnable => playerIdList.Any();

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            return killer.CheckDoubleTrigger(target, () =>
            {
                switch (target.GetCustomRole())
                {
                    case CustomRoles.Admirer:
                        Admirer.AdmireLimit--;
                        Admirer.SendRPC();
                        break;
                    case CustomRoles.Aid:
                        Aid.UseLimit[target.PlayerId]--;
                        break;
                    case CustomRoles.Bloodhound:
                        Bloodhound.UseLimit[target.PlayerId]--;
                        Bloodhound.SendRPCPlus(target.PlayerId, true);
                        break;
                    case CustomRoles.CameraMan:
                        CameraMan.UseLimit[target.PlayerId]--;
                        CameraMan.SendRPC(target.PlayerId, true);
                        break;
                    case CustomRoles.Chameleon:
                        Chameleon.UseLimit[target.PlayerId]--;
                        Chameleon.SendRPCPlus(target.PlayerId, true);
                        break;
                    case CustomRoles.Cleanser:
                        Cleanser.CleanserUses[target.PlayerId]--;
                        Cleanser.SendRPC(target.PlayerId);
                        break;
                    case CustomRoles.Crusader:
                        Crusader.CrusaderLimit[target.PlayerId]--;
                        break;
                    case CustomRoles.Deputy:
                        Deputy.HandcuffLimit--;
                        Deputy.SendRPC();
                        break;
                    case CustomRoles.Divinator:
                        Divinator.CheckLimit[target.PlayerId]--;
                        Divinator.SendRPC(target.PlayerId, true);
                        break;
                    case CustomRoles.Doormaster:
                        Doormaster.UseLimit[target.PlayerId]--;
                        Doormaster.SendRPC(target.PlayerId, true);
                        break;
                    case CustomRoles.Mediumshiper:
                        Mediumshiper.ContactLimit[target.PlayerId]--;
                        Mediumshiper.SendRPC(target.PlayerId, true);
                        break;
                    case CustomRoles.Monarch:
                        Monarch.KnightLimit--;
                        Monarch.SendRPC();
                        break;

                }
            });
        }
    }
}
