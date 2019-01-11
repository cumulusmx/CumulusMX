using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions
{
    public interface ISettingsProvider
    {
        T GetSetting<T>(string path);
        T SetSetting<T>(string path);
    }
}
