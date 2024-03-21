namespace EHR
{
    internal interface IVanillaSettingHolder
    {
        public TabGroup Tab { get; }
        public void SetupCustomOption();
    }
}
