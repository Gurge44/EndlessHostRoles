using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE
{
    internal interface IVanillaSettingHolder
    {
        public TabGroup Tab { get; }
        public void SetupCustomOption();
    }
}
