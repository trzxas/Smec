using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Smec.Models
{
    [DataContract]
    public class NamedBase : ObservableObject
    {
        [JsonPropertyOrder(-1)]
        [XmlAttribute] // attribute ignored with DataContract 
        [DataMember]
        [BsonIgnoreIfNull]
        public virtual string Name
        {
            get;
            set => SetProperty(ref field, value);
        } = String.Empty;



        [XmlIgnore]
        [BsonIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public virtual string Description
        {
            get;
            set => SetProperty(ref field, value);
        } = String.Empty;

        [XmlIgnore]
        [BsonIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public virtual string CategoryName
        {
            get;
            set => SetProperty(ref field, value);
        } = String.Empty;
    }
}
