using System;
using System.Collections.Generic;
using System.Text;

namespace Emulation.Services
{
    public interface IAppSettings
    {
        bool IsAsyncMode { get; }
        //string ProjectExtension { get; }
    }
}
