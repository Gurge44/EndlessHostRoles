namespace EHR.AddOns
{
    internal interface IAddon
    {
        public AddonTypes Type { get; }
        public void SetupCustomOption();
    }
}