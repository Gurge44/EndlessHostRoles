namespace EHR.Impostor
{
    internal class Trickster : RoleBase
    {
        public override bool IsEnable => false;
        public override void SetupCustomOption() => Options.SetupRoleOptions(4300, TabGroup.ImpostorRoles, CustomRoles.Trickster);

        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }
    }
}