using Smec.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Smec.Services.DataServices
{
    public class JsonPolymorphicNodeResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

            // Если это базовый класс Node, динамически регистрируем всех его наследников
            if (jsonTypeInfo.Type == typeof(Node) /*|| type.IsSubclassOf(typeof(Node))*/)
            {
                // Создаем специальный объект настроек полиморфизма
                var polymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type", // Имя служебного поля в JSON
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
                };

                // Находим все неабстрактные классы, унаследованные от Node
                var derivedTypes = Assembly.GetExecutingAssembly()
                                         .GetTypes()
                                         .Where(t => t.IsSubclassOf(typeof(Node)) && !t.IsAbstract);

                foreach (var derivedType in derivedTypes)
                {
                    // В качестве $type будет использоваться имя класса (например, "Logic_OR")
                    string discriminator = derivedType.Name;

                    // Добавляем наследника в коллекцию внутри настроек полиморфизма
                    polymorphismOptions.DerivedTypes.Add(new JsonDerivedType(derivedType, discriminator));
                }

                //Присваиваем созданные настройки объекту jsonTypeInfo
                jsonTypeInfo.PolymorphismOptions = polymorphismOptions;
            }

            return jsonTypeInfo;
        }
    }
}
