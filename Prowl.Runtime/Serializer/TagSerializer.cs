﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Prowl.Runtime
{
    public static class TagSerializer
    {
        public class SerializationContext
        {
            public Dictionary<object, int> objectToId = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            public Dictionary<int, object> idToObject = new Dictionary<int, object>();
            public int nextId = 1;
            public List<Guid> dependencies = new List<Guid>();

            public SerializationContext()
            {
                objectToId.Clear();
                objectToId.Add(new NullKey(), 0);
                idToObject.Clear();
                idToObject.Add(0, new NullKey());
                nextId = 1;
                dependencies.Clear();
            }

        }

        private class NullKey { }

        private static bool IsPrimitive(Type t) => t.IsPrimitive || t.IsAssignableTo(typeof(string)) || t.IsAssignableTo(typeof(Guid)) || t.IsAssignableTo(typeof(DateTime)) || t.IsEnum || t.IsAssignableTo(typeof(byte[]));


        #region Serialize

        public static SerializedProperty Serialize(object? value) => Serialize(value, new());
        public static SerializedProperty Serialize(object? value, SerializationContext ctx)
        {
            if (value == null)
                return new SerializedProperty(PropertyType.Null, null);

            if (value is SerializedProperty t)
            {
                var clone = t.Clone();
                return clone;
            }

            var type = value.GetType();
            if (IsPrimitive(type))
                return PrimitiveToTag(value);

            if (type.IsArray && value is Array array)
                return ArrayToListTag(array, ctx);

            var tag = DictionaryToTag(value, ctx);
            if (tag != null) return tag;

            if(value is IList iList)
                return IListToTag(iList, ctx);

            return SerializeObject(value, ctx);
        }

        private static SerializedProperty PrimitiveToTag(object p)
        {
            if (p is byte b) return new(PropertyType.Byte, b);
            else if (p is sbyte sb) return new(PropertyType.sByte, sb);
            else if (p is short s) return new(PropertyType.Short, s);
            else if (p is int i) return new(PropertyType.Int, i);
            else if (p is long l) return new(PropertyType.Long, l);
            else if (p is uint ui) return new(PropertyType.UInt, ui);
            else if (p is ulong ul) return new(PropertyType.ULong, ul);
            else if (p is ushort us) return new(PropertyType.UShort, us);
            else if (p is float f) return new(PropertyType.Float, f);
            else if (p is double d) return new(PropertyType.Double, d);
            else if (p is decimal dec) return new(PropertyType.Decimal, dec);
            else if (p is string str) return new(PropertyType.String, str);
            else if (p is byte[] bArr) return new(PropertyType.ByteArray, bArr);
            else if (p is bool bo) return new(PropertyType.Bool, bo);
            else if (p is DateTime date) return new(PropertyType.Long, date.ToBinary());
            else if (p is Guid g) return new(PropertyType.String, g.ToString());
            else if (p.GetType().IsEnum) return new(PropertyType.Int, (int)p); // Serialize enums as integers
            else throw new NotSupportedException("The type '" + p.GetType() + "' is not a supported primitive.");
        }

        private static SerializedProperty ArrayToListTag(Array array, SerializationContext ctx)
        {
            List<SerializedProperty> tags = [];
            for (int i = 0; i < array.Length; i++)
                tags.Add(Serialize(array.GetValue(i), ctx));
            return new SerializedProperty(tags);
        }

        private static SerializedProperty? DictionaryToTag(object obj, SerializationContext ctx)
        {
            var t = obj.GetType();
            if (obj is IDictionary dict &&
                 t.IsGenericType &&
                 t.GetGenericArguments()[0] == typeof(string))
            {
                SerializedProperty tag = new(PropertyType.Compound, null);
                foreach (DictionaryEntry kvp in dict)
                    tag.Add((string)kvp.Key, Serialize(kvp.Value, ctx));
                return tag;
            }
            return null;
        }

        private static SerializedProperty IListToTag(IList iList, SerializationContext ctx)
        {
            List<SerializedProperty> tags = [];
            foreach (var item in iList)
                tags.Add(Serialize(item, ctx));
            return new SerializedProperty(tags);
        }

        private static SerializedProperty SerializeObject(object? value, SerializationContext ctx)
        {
            if (value == null) return new(PropertyType.Null, null); // ID defaults to 0 which is null or an Empty Compound

            var type = value.GetType();

            var compound = new SerializedProperty();

            if (ctx.objectToId.TryGetValue(value, out int id))
            {
                compound.SerializedID = id;
                // Dont need to write compound data, its already been serialized at some point earlier
                return compound;
            }

            id = ctx.nextId++;
            ctx.objectToId[value] = id;
            ctx.idToObject[id] = value;

            if (value is ISerializeCallbacks callback)
                callback.PreSerialize();

            if (value is ISerializable serializable)
            {
                // Manual Serialization
                compound = serializable.Serialize(ctx);
            }
            else
            {
                // Automatic Serializer
                var properties = GetAllFields(type).Where(field => (field.IsPublic || field.GetCustomAttribute<SerializeFieldAttribute>() != null) && field.GetCustomAttribute<SerializeIgnoreAttribute>() == null);
                // If public and has System.NonSerializedAttribute then ignore
                properties = properties.Where(field => !field.IsPublic || field.GetCustomAttribute<NonSerializedAttribute>() == null);

                foreach (var field in properties)
                {
                    string name = field.Name;

                    var propValue = field.GetValue(value);
                    if (propValue == null)
                    {
                        if (Attribute.GetCustomAttribute(field, typeof(IgnoreOnNullAttribute)) != null) continue;
                        compound.Add(name, new(PropertyType.Null, null));
                    }
                    else
                    {
                        SerializedProperty tag = Serialize(propValue, ctx);
                        compound.Add(name, tag);
                    }
                }
            }

            compound.SerializedID = id;
            compound.SerializedType = type.AssemblyQualifiedName;

            if (value is ISerializeCallbacks callback2)
                callback2.PostSerialize();

            return compound;
        }

        #endregion

        #region Deserialize

        public static T? Deserialize<T>(SerializedProperty value) => (T?)Deserialize(value, typeof(T));

        public static object? Deserialize(SerializedProperty value, Type type) => Deserialize(value, type, new SerializationContext());

        public static T? Deserialize<T>(SerializedProperty value, SerializationContext ctx) => (T?)Deserialize(value, typeof(T), ctx);
        public static object? Deserialize(SerializedProperty value, Type targetType, SerializationContext ctx)
        {
            if (value.TagType == PropertyType.Null) return null;

            if (value.GetType().IsAssignableTo(targetType)) return value;

            if (IsPrimitive(targetType))
            {
                // Special Cases
                if (targetType.IsEnum)
                    if (value.TagType == PropertyType.Int)
                        return Enum.ToObject(targetType, value.IntValue);

                if (targetType == typeof(DateTime))
                    if (value.TagType == PropertyType.Long)
                        return DateTime.FromBinary(value.LongValue);

                if (targetType == typeof(Guid))
                    if (value.TagType == PropertyType.String)
                        return Guid.Parse(value.StringValue);
                
                return Convert.ChangeType(value.Value, targetType);
            }

            if (value.TagType == PropertyType.List)
            {
                if (targetType.IsArray)
                {
                    // Deserialize List into Array
                    Type type = targetType.GetElementType();
                    var array = Array.CreateInstance(type, value.Count);
                    for (int idx = 0; idx < array.Length; idx++)
                        array.SetValue(Deserialize(value[idx], type, ctx), idx);
                    return array;
                }
                else if (targetType.IsAssignableTo(typeof(IList)))
                {
                    // IEnumerable covers many types, we need to find the type of element in the IEnumrable
                    // For now just assume its the first generic argument
                    Type type = targetType.GetGenericArguments()[0];
                    var list2 = (IList)Activator.CreateInstance(targetType);
                    foreach (var tag in value.List)
                        list2.Add(Deserialize(tag, type, ctx));
                    return list2;

                }

                throw new InvalidCastException("ListTag cannot deserialize into type of: '" + targetType + "'");
            }
            else if (value.TagType == PropertyType.Compound)
            {
                if (targetType.IsAssignableTo(typeof(IDictionary)) &&
                                          targetType.IsGenericType &&
                                          targetType.GetGenericArguments()[0] == typeof(string))
                {
                    var dict = (IDictionary)Activator.CreateInstance(targetType);
                    var valueType = targetType.GetGenericArguments()[1];
                    foreach (var tag in value.Tags)
                        dict.Add(tag.Key, Deserialize(tag.Value, valueType, ctx));
                    return dict;
                }

                return DeserializeObject(value, ctx);
            }

            throw new NotSupportedException("The node type '" + value.GetType() + "' is not supported.");
        }

        private static object? DeserializeObject(SerializedProperty compound, SerializationContext ctx)
        {
            if (ctx.idToObject.TryGetValue(compound.SerializedID, out object? existingObj))
                return existingObj;

            if (string.IsNullOrWhiteSpace(compound.SerializedType))
                return null;

            Type oType = Type.GetType(compound.SerializedType);
            if (oType == null)
            {
                Debug.LogError("[TagSerializer] Couldn't find type: " + compound.SerializedType);
                return null;
            }

            object resultObject = CreateInstance(oType);

            ctx.idToObject[compound.SerializedID] = resultObject;
            resultObject = DeserializeInto(compound, resultObject, ctx);

            return resultObject;
        }

        public static object DeserializeInto(SerializedProperty tag, object into) => DeserializeInto(tag, into, new SerializationContext());
        private static object DeserializeInto(SerializedProperty tag, object into, SerializationContext ctx)
        {
            if (into is ISerializeCallbacks callback1)
                callback1.PreDeserialize();

            if (into is ISerializable serializable)
            {
                serializable.Deserialize(tag, ctx);
                into = serializable;
            }
            else
            {
                FieldInfo[] fields = GetAllFields(into.GetType()).ToArray();

                var properties = fields.Where(field => (field.IsPublic || field.GetCustomAttribute<SerializeFieldAttribute>() != null) && field.GetCustomAttribute<SerializeIgnoreAttribute>() == null);
                // If public and has System.NonSerializedAttribute then ignore
                properties = properties.Where(field => !field.IsPublic || field.GetCustomAttribute<NonSerializedAttribute>() == null);
                foreach (var field in properties)
                {
                    string name = field.Name;

                    if (!tag.TryGet(name, out var node))
                    {
                        // Before we completely give up, a field can have FormerlySerializedAs Attributes
                        // This allows backwards compatibility
                        var formerNames = Attribute.GetCustomAttributes(field, typeof(FormerlySerializedAsAttribute));
                        foreach (FormerlySerializedAsAttribute formerName in formerNames)
                        {
                            if (tag.TryGet(formerName.oldName, out node))
                            {
                                name = formerName.oldName;
                                break;
                            }
                        }
                        if (node == null) // Continue onto the next field
                            continue;
                    }

                    object data = Deserialize(node, field.FieldType, ctx);

                    // Some manual casting for edge cases
                    if (data is byte @byte)
                    {
                        if (field.FieldType == typeof(bool))
                            data = @byte != 0;
                        if (field.FieldType == typeof(sbyte))
                            data = (sbyte)@byte;
                    }

                    field.SetValue(into, data);
                }
            }

            if (into is ISerializeCallbacks callback2)
                callback2.PostDeserialize();
            return into;
        }

        static object CreateInstance(Type type)
        {
            object data = Activator.CreateInstance(type);
            return data;
        }

        static IEnumerable<FieldInfo> GetAllFields(Type t)
        {
            if (t == null)
                return Enumerable.Empty<FieldInfo>();

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance | BindingFlags.DeclaredOnly;

            return t.GetFields(flags).Concat(GetAllFields(t.BaseType));
        }

        #endregion
    }
}
