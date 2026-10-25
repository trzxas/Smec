using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Emulation.Models
{
    public enum SidePosition
    {
        Left=0,
        Top,
        Right,
        Bottom,
        Center,
    }



    public class NodePort:NamedBase, IOwnable<Node>
    {
        //TODO Можно сделать универсальный [IgnoreAll] для [BsonIgnore][XmlIgnore][JsonIgnore]
        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        public Node? Owner { get; set; }

        [XmlAttribute]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SidePosition Side { get; set; }

    }
}
