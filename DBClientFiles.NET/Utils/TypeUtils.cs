﻿using DBClientFiles.NET.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DBClientFiles.NET.Utils
{
    internal static class TypeUtils
    {
        private static Dictionary<Type, int> _typeSizes = new Dictionary<Type, int>();

        public static bool IsRequiringMarshalling(this Type t)
        {
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var requires = field.GetCustomAttributes(typeof(MarshalAsAttribute), true).Length != 0;
                if (requires)
                    return true;

                if (t == typeof(IntPtr))
                    continue;

                if (Type.GetTypeCode(t) == TypeCode.Object)
                    requires |= field.FieldType.IsRequiringMarshalling();

                return requires;
            }
            return false;
        }

        public static int GetBinarySize(this Type t)
        {
            if (t == typeof(string))
                return 4;

            if (t == typeof(IntPtr))
                return 4;

            if (t.IsEnum)
                return t.GetEnumUnderlyingType().GetBinarySize();

            if (_typeSizes.TryGetValue(t, out var size))
                return size;

            try
            {
                // Try letting the marshaler handle getting the size.
                // It can *sometimes* do it correctly
                // If it can't, fall back to our own methods.
                /// var o = Activator.CreateInstance(t);
                return _typeSizes[t] = Marshal.SizeOf(t);
            }
            catch
            {
                int totalSize = 0;
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    var fba = field.GetCustomAttribute<FixedBufferAttribute>(false);
                    if (fba != null)
                    {
                        totalSize += fba.ElementType.GetBinarySize() * fba.Length;
                        continue;
                    }

                    totalSize += field.FieldType.GetBinarySize();
                }
                return _typeSizes[t] = totalSize;
            }
        }

        public static ExtendedMemberInfo[] GetMemberInfos(this Type type, StorageOptions options)
        {
            var memberIndex = 0;

            var store = new List<ExtendedMemberInfo>();
            foreach (var memberInfo in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (memberInfo.MemberType != options.MemberType)
                    continue;

                var extendedMemberInfo = ExtendedMemberInfo.Initialize(memberInfo, memberIndex++);
                store.Add(extendedMemberInfo);
            }

            return store.ToArray();
        }
    }
}