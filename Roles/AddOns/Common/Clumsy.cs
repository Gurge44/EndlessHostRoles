namespace EHR.AddOns.Common
{
    internal class Clumsy : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(15170, CustomRoles.Clumsy, canSetNum: true, teamSpawnOptions: true);
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (IRandom.Instance.Next(300) == 0 && !pc.inVent && !pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() && !pc.MyPhysics.Animations.IsPlayingEnterVentAnimation() && !pc.inMovingPlat && pc.MyPhysics.Animations.IsPlayingRunAnimation())
            {
                float duration = IRandom.Instance.Next(1, 4);
                float speed = Main.AllPlayerSpeed[pc.PlayerId];
                Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                pc.MarkDirtySettings();

                LateTask.New(() =>
                {
                    Main.AllPlayerSpeed[pc.PlayerId] = speed;
                    pc.MarkDirtySettings();
                }, duration, "Clumsy Trip");
            }
        }
    }
}