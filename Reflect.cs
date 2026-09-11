using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonkeNameplates
{
    // All version-sensitive game/TMP members are isolated here and in GameBridge.
    // No publicizer, Harmony patches, network writes, or game assemblies are bundled.
    internal static class Reflect
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;
        private static readonly Dictionary<Tuple<Type, string>, MemberInfo> Cache =
            new Dictionary<Tuple<Type, string>, MemberInfo>();

        public static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static MemberInfo Member(Type type, string name)
        {
            var key = Tuple.Create(type, name);
            if (Cache.TryGetValue(key, out var found)) return found;
            for (Type current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (field != null) { found = field; break; }
                var property = current.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (property != null) { found = property; break; }
            }
            Cache[key] = found;
            return found;
        }

        public static object Get(object instance, string name)
        {
            if (instance == null) return null;
            var type = instance as Type ?? instance.GetType();
            var target = instance is Type ? null : instance;
            try
            {
                var member = Member(type, name);
                if (member is FieldInfo field) return field.GetValue(target);
                if (member is PropertyInfo property) return property.GetValue(target);
            }
            catch (TargetInvocationException) { }
            return null;
        }

        public static bool Set(object instance, string name, object value)
        {
            if (instance == null) return false;
            var member = Member(instance.GetType(), name);
            Type targetType = member is PropertyInfo p ? p.PropertyType : (member as FieldInfo)?.FieldType;
            if (targetType == null) return false;
            if (targetType.IsEnum && value is string text) value = Enum.Parse(targetType, text);
            if (member is PropertyInfo property && property.CanWrite) { property.SetValue(instance, value); return true; }
            if (member is FieldInfo field) { field.SetValue(instance, value); return true; }
            return false;
        }
    }
}
