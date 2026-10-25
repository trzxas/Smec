using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Emulation.Models
{
    public class NamedBase :Base
    {
        [JsonPropertyOrder(-1)]
        [XmlAttribute]
        public string Name
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        } = string.Empty;
    }
}
