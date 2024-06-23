namespace EHR.Impostor
{
    public class Augmenter : RoleBase
    {
        public static bool On;

        public byte Target;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(649700, TabGroup.ImpostorRoles, CustomRoles.Augmenter);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            Target = byte.MaxValue;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting) return true;
            Target = target.PlayerId;
            return false;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            var newTarget = Utils.GetPlayerById(Target);
            if (newTarget == null || !killer.RpcCheckAndMurder(newTarget, check: true)) return true;

            var pos = newTarget.Pos();
            newTarget.TP(target);
            target.TP(pos);

            LateTask.New(() => killer.Kill(newTarget), 0.2f, "AugmenterKill");

            return false;
        }
    }
}