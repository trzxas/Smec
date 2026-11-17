using System;
using System.Collections.Generic;
using System.Text;

namespace Smec
{
    public interface IOwnable<P>
    {
        P? Owner { get; set; }
        bool IsRoot => Owner == null;
    }
}
