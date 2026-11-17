using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Services
{
    // Реалізація, яка читає дані з Properties.Settings або App.xaml.cs
    public class AppSettings : IAppSettings
    {
        public bool IsAsyncMode => Properties.Settings.Default.IsAsyncMode;

        //public string DefaultExtension => Properties.Settings.Default.ProjectFileExtension;
    }
}
