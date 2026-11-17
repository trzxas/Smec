using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Smec.Models;
using static System.Windows.Forms.LinkLabel;
using Link = Smec.Models.Link;

namespace Smec.Services.DataServices
{
    public class BsonCustomSerializer : SerializerBase<SysModel>
    {
        // Кэшируем стандартный сериализатор на уровне экземпляра
        private readonly IBsonSerializer<SysModel> _defaultSerializer;

        // Флаг использования стандартного сериалайзера
        private readonly bool UseStandartSerializer = false;

        // Возвратит "_t" (дефолт для MongoDB) или то, что настроено глобально
        // StandardDiscriminatorConvention.Hierarchical.ElementName;
        readonly string defaultDiscriminator = "$type";
        readonly string defaultId = "$id";
        readonly string defaultRef = "$ref";

        public BsonCustomSerializer()
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(Link)))
            {
                BsonClassMap.RegisterClassMap<Link>(cm =>
                {
                    cm.AutoMap();
                    // Исключаем свойства с циклическими ссылками
                    cm.UnmapProperty(c => c.PortA); 
                    cm.UnmapProperty(c => c.PortB); 
                });
            }

            // Дефолтный сериалайзер (для тестов)
            // Если класс SysModel не зарегистрирован, LookupClassMap сам автоматически 
            // создаст для него дефолтную карту "на лету" без явного RegisterClassMap
            var classMap = BsonClassMap.LookupClassMap(typeof(SysModel));
            _defaultSerializer = new BsonClassMapSerializer<SysModel>(classMap);

        }

        #region Сериализация
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, SysModel value)
        {
            if (value == null) { context.Writer.WriteNull(); return; }

            // Если нужен стандартный сериалайзер
            if (UseStandartSerializer)
            {
                _defaultSerializer.Serialize(context, args, value);
                return;
            }

            var writer = context.Writer;

            // Сквозной словарь для регистрации портов в рамках одного сеанса сериализации
            var seenPorts = new Dictionary<NodePort, int>(ReferenceEqualityComparer.Instance);

            writer.WriteStartDocument();

            // Стандартный сериализатор для строки Descripton
            writer.WriteName(nameof(SysModel.Descripton));
            BsonSerializer.Serialize(context.Writer, value.Descripton);

            // 1. Сериализация иерархии Nodes -> Ports
            writer.WriteName(nameof(SysModel.Nodes));
            writer.WriteStartArray();
            foreach (var node in value.Nodes)
            {
                if (node == null) continue;
                SerializeSingleNode(writer, node, seenPorts);
            }
            writer.WriteEndArray();

            // 2. Сериализация Links
            writer.WriteName(nameof(SysModel.Links));
            writer.WriteStartArray();
            foreach (var link in value.Links)
            {
                if (link == null) continue;
                SerializeSingleLink(writer, link, seenPorts);
            }
            writer.WriteEndArray();

            writer.WriteEndDocument();
        }

        // ==========================================
        // Сериализация одной Ноды
        // ==========================================
        private void SerializeSingleNode(IBsonWriter writer, Node node, Dictionary<NodePort, int> seenPorts)
        {
            // Определяем точный тип наследника Node в рантайме (например, Logic_OR)
            Type actualNodeType = node.GetType();

            // Автоматически сериализуем ноду со всеми ее уникальными свойствами
            BsonDocument nodeDocument = node.ToBsonDocument(
                actualNodeType,
                args: new BsonSerializationArgs { NominalType = actualNodeType, SerializeAsNominalType = true }
            );

            // ФИКС: Если драйвер не добавил дискриминатор (чего ожидалось по умолчанию!),
            // добавляем его принудительно
            if (!nodeDocument.Contains(defaultDiscriminator))
            {
                // ОПЦИЯ 1, в нашем случае этого достаточно
                // Записываем просто строку типа, без массивов и циклов
                // Вставляем поле дискриминатора в самый верх BsonDocument
                nodeDocument.InsertAt(0, new BsonElement(defaultDiscriminator, actualNodeType.Name));

                //// ОПЦИЯ 2  (оставляем на всякий случай если нужно будет работать с БД MongoDb, там это пригодится) 
                //// Собираем всю цепочку наследования в массив до класса Node включительно
                //// (или нет если  использовать 2 условие ниже)
                //var currentType = actualNodeType;
                //var typesArray = new BsonArray();
                //while (currentType != null && currentType != typeof(object))
                //{
                //    typesArray.Insert(0, currentType.Name);
                //    if (currentType == typeof(Node)) break;
                //    currentType = currentType.BaseType;
                //}
                //nodeDocument.InsertAt(0, new BsonElement(defaultDiscriminator, typesArray));
            }

            // Перехватываем порты: сериализуем их вручную через временный документ-контейнер
            var tempContainer = new BsonDocument();
            using (var portsWriter = new BsonDocumentWriter(tempContainer))
            {
                portsWriter.WriteStartDocument();
                portsWriter.WriteName(nameof(Node.Ports));
                portsWriter.WriteStartArray();

                foreach (var port in node.Ports)
                {
                    SerializePort(portsWriter, port, seenPorts);
                }

                portsWriter.WriteEndArray();
                portsWriter.WriteEndDocument();
            }

            // Заменяем дефолтный массив портов в документе ноды на наш кастомный
            nodeDocument[nameof(Node.Ports)] = tempContainer[nameof(Node.Ports)].AsBsonArray;

            // Записываем полностью готовый полиморфный документ ноды в основной поток
            BsonSerializer.Serialize(writer, nodeDocument);
        }

        // ==========================================
        // Сериализация одного Порта
        // ==========================================
        private void SerializePort(IBsonWriter writer, NodePort port, Dictionary<NodePort, int> seenPorts)
        {
            if (port == null) { writer.WriteNull(); return; }

            // Проверяем, сериализовался ли этот порт ранее в дереве Nodes
            if (seenPorts.TryGetValue(port, out int existingId))
            {
                // Если да (Этот порт используется в Link) — пишем вместо всего объекта только его цифровой ID-ссылку
                writer.WriteStartDocument();
                writer.WriteName(defaultRef);
                writer.WriteInt32(existingId);
                writer.WriteEndDocument();
                return;
            }

            // Если порт новый (встретили впервые), генерируем ему ID и записываем полностью
            int newId = seenPorts.Count + 1;
            seenPorts.Add(port, newId);

            writer.WriteStartDocument();
            writer.WriteName(defaultId); writer.WriteInt32(newId);

            // если Name равно null, поле "Name" не записываем
            if (port.Name != null)
            {
                writer.WriteName(nameof(NodePort.Name));
                writer.WriteString(port.Name);
            }

            // Здесь мы вручную добавляем свойства Порта для сохранения 
            writer.WriteName(nameof(NodePort.Side)); writer.WriteInt32((int)port.Side); // приводим enum SidePosition к числу, для сохранения
            writer.WriteName(nameof(NodePort.Mode)); writer.WriteInt32((int)port.Mode);
            writer.WriteName(nameof(NodePort.Decorator)); writer.WriteInt32((int)port.Decorator);
            writer.WriteName(nameof(NodePort.PinNumber)); writer.WriteString(port.PinNumber);
            writer.WriteEndDocument();
        }

        // ==========================================
        // Сериализация одного Линка
        // ==========================================
        private void SerializeSingleLink(IBsonWriter writer, Link link, Dictionary<NodePort, int> seenPorts)
        {
            writer.WriteStartDocument();

            // Безопасно получаем документ со всеми свойствами Link. Порты проигнорируются благодаря UnmapProperty в конструкторе
            BsonDocument linkDocument = link.ToBsonDocument();

            // Автоматически записываем все свойства из полученного документа, которые не null
            foreach (var element in linkDocument.Elements)
            {
                if (element.Value != null && !element.Value.IsBsonNull)
                {
                    writer.WriteName(element.Name);
                    BsonSerializer.Serialize(writer, element.Value);
                }
            }

            // Вручную записываем порт "A" по вашей логике ссылок
            writer.WriteName(nameof(Link.PortA));
            SerializePort(writer, link.PortA, seenPorts);

            // Вручную записываем порт "B" по вашей логике ссылок
            writer.WriteName(nameof(Link.PortB));
            SerializePort(writer, link.PortB, seenPorts);

            writer.WriteEndDocument();
        }
        #endregion

        #region Десериализация
        public override SysModel Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var reader = context.Reader;

            if (reader.GetCurrentBsonType() == BsonType.Null)
            {
                reader.ReadNull();
                return default;
            }

            // Использование стандартного сериалайзера
            if (UseStandartSerializer)
                return _defaultSerializer.Deserialize(context, args);

            var model = new SysModel();
            // Словарь для восстановления ссылок на порты по их $id
            var idToPortMap = new Dictionary<int, NodePort>();

            // Начинаем чтение
            reader.ReadStartDocument();
            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var fieldName = reader.ReadName();
                switch (fieldName)
                {
                    case nameof(SysModel.Descripton):
                        model.Descripton = BsonSerializer.Deserialize<string>(reader);
                        break;

                    case nameof(SysModel.Nodes):
                        reader.ReadStartArray();
                        while (reader.ReadBsonType() != BsonType.EndOfDocument)
                        {
                            model.Nodes.Add(DeserializeSingleNode(reader, idToPortMap));
                        }
                        reader.ReadEndArray();
                        break;

                    case nameof(SysModel.Links):
                        reader.ReadStartArray();
                        while (reader.ReadBsonType() != BsonType.EndOfDocument)
                        {
                            model.Links.Add(DeserializeSingleLink(reader, idToPortMap));
                        }
                        reader.ReadEndArray();
                        break;
                    default:
                        reader.SkipValue(); // Игнорируем неизвестные поля для стабильности
                        break;
                }
            }

            reader.ReadEndDocument();

            return model;
        }

        // ============================================
        // ПОДПРОГРАММА 1: Автоматический импорт Ноды
        // ============================================
        private Node DeserializeSingleNode(IBsonReader reader, Dictionary<int, NodePort> idToPortMap)
        {
            // 1. Читаем всю ноду целиком во временный BsonDocument
            var nodeDocument = BsonSerializer.Deserialize<BsonDocument>(reader);

            // 2. Дефолтный тип — базовый Node
            Type targetType = typeof(Node);

            // 3. Извлекаем реальный тип наследника из поля дискриминатора 
            if (nodeDocument.Contains(defaultDiscriminator))
            {
                var tValue = nodeDocument[defaultDiscriminator];
                string typeName = tValue.IsBsonArray ? tValue.AsBsonArray.Last().AsString : tValue.AsString;

                if (!string.IsNullOrEmpty(typeName))
                {
                    var foundType = System.Reflection.Assembly.GetExecutingAssembly()
                        .GetTypes()
                        .FirstOrDefault(t => t.Name == typeName);

                    if (foundType != null) targetType = foundType;
                }

                // Удаляем дискриминатор, чтобы не мешать авто-десериализатору
                nodeDocument.Remove(defaultDiscriminator);
            }


            // 4. Вырезаем массив портов, чтобы он не мешал авто-десериализатору
            BsonValue? savedPorts = null;
            if (nodeDocument.Contains(nameof(Node.Ports)))
            {
                savedPorts = nodeDocument[nameof(Node.Ports)];
                nodeDocument.Remove(nameof(Node.Ports));
            }

            // 5. Автоматически восстанавливаем все свойства ноды (Name, Location, Caption и любые новые)
            Node node;
            using (var docReader = new BsonDocumentReader(nodeDocument))
            {
                node = (Node)BsonSerializer.Deserialize(docReader, targetType);
            }

            // 6. Руками восстанавливаем порты 
            if (savedPorts != null && savedPorts.IsBsonArray)
            {
                foreach (var portValue in savedPorts.AsBsonArray)
                {
                    if (portValue.IsBsonDocument)
                    {
                        using (var portReader = new BsonDocumentReader(portValue.AsBsonDocument))
                        {
                            node.Ports.Add(DeserializeSinglePort(portReader, idToPortMap));
                        }
                    }
                }
            }

            return node;

        }

        private NodePort DeserializeSinglePort(IBsonReader reader, Dictionary<int, NodePort> idToPortMap)
        {
            reader.ReadStartDocument();

            // Проверяем, что это: ссылка на существующий порт или создание нового
            var firstFieldName = reader.ReadName();

            if (firstFieldName == defaultRef)
            {
                // Это ссылка! Читаем ID, находим порт в словаре и возвращаем его
                int refId = reader.ReadInt32();
                reader.ReadEndDocument();

                if (idToPortMap.TryGetValue(refId, out var existingPort))
                {
                    return existingPort;
                }
                throw new BsonSerializationException($"Порт с {defaultRef}={refId} не найден в текущем контексте документа.");
            }

            // Это не ссылка, значит это создание нового порта (поле $id)
            if (firstFieldName != defaultId)
            {
                throw new BsonSerializationException($"Ожидалось поле {defaultId} или {defaultRef} при десериализации Port.");
            }

            int portIdNum = reader.ReadInt32();
            var port = new NodePort();

            // Регистрируем пустой экземпляр в словаре ДО чтения его свойств 
            // (на случай, если внутри порта будут ссылки на самого себя)
            idToPortMap[portIdNum] = port;

            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var fieldName = reader.ReadName();
                switch (fieldName)
                {
                    case nameof(NodePort.Name):
                        port.Name = BsonSerializer.Deserialize<string>(reader);
                        break;
                    case nameof(NodePort.Side):
                        port.Side = (SidePosition)BsonSerializer.Deserialize<int>(reader); // возвращаемся к enum
                        break;
                    case nameof(NodePort.Mode):
                        port.Mode = (PortMode)BsonSerializer.Deserialize<int>(reader); // возвращаемся к enum
                        break;
                    case nameof(NodePort.Decorator):
                        port.Decorator = (PortDecorator)BsonSerializer.Deserialize<int>(reader); // возвращаемся к enum
                        break;
                    case nameof(NodePort.PinNumber):
                        port.PinNumber = BsonSerializer.Deserialize<string>(reader); // Номер пина
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndDocument();
            return port;
        }


        // ============================================
        // ПОДПРОГРАММА 2: Автоматический импорт Линка
        // ============================================
        private Link DeserializeSingleLink(IBsonReader reader, Dictionary<int, NodePort> idToPortMap)
        {
           
            // 1. Читаем весь линк во временный BsonDocument
            var linkDocument = BsonSerializer.Deserialize<BsonDocument>(reader);

            // 2. Вырезаем PortA и PortB, так как в afqkt они лежат в кастомном формате ($ref)
            BsonValue? savedPortA = null;
            BsonValue? savedPortB = null;

            if (linkDocument.Contains(nameof(Link.PortA)))
            {
                savedPortA = linkDocument[nameof(Link.PortA)];
                linkDocument.Remove(nameof(Link.PortA));
            }
            if (linkDocument.Contains(nameof(Link.PortB)))
            {
                savedPortB = linkDocument[nameof(Link.PortB)];
                linkDocument.Remove(nameof(Link.PortB));
            }

            // 3. Автоматически восстанавливаем все остальные свойства (Name, Activity и любые будущие свойства)
            Link link;
            using (var docReader = new MongoDB.Bson.IO.BsonDocumentReader(linkDocument))
            {
                link = BsonSerializer.Deserialize<Link>(docReader);
            }

            // 4. Руками привязываем порты обратно, восстанавливая циклические ссылки из idToPortMap
            if (savedPortA != null && savedPortA.IsBsonDocument)
            {
                using (var portReader = new BsonDocumentReader(savedPortA.AsBsonDocument))
                {
                    link.PortA = DeserializeSinglePort(portReader, idToPortMap);
                }
            }
            if (savedPortB != null && savedPortB.IsBsonDocument)
            {
                using (var portReader = new BsonDocumentReader(savedPortB.AsBsonDocument))
                {
                    link.PortB = DeserializeSinglePort(portReader, idToPortMap);
                }
            }

            return link;
        }
        #endregion
    }

}

