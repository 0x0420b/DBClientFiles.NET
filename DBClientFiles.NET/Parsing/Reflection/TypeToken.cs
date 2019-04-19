﻿using DBClientFiles.NET.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DBClientFiles.NET.Parsing.Reflection
{
    /// <summary>
    /// A representation of a type.
    /// </summary>
    internal class TypeToken
    {
        /// <summary>
        /// The underlying CLR <see cref="Type"/> representation of this type.
        /// </summary>
        public Type Type { get; }

        private Dictionary<Type, TypeToken> _declaredTypes;
        private List<MemberToken> _members;

        public IEnumerable<TypeToken> DeclaredTypes => _declaredTypes.Values;
        public IList<MemberToken> Members => _members;

        public IEnumerable<MemberToken> Fields
            => Members.Where(m => m.MemberType == TypeTokenType.Field);

        public IEnumerable<MemberToken> Properties
            => Members.Where(m => m.MemberType == TypeTokenType.Property);

        public TypeToken(Type type) : this(type, new Dictionary<Type, TypeToken>())
        {
        }

        private TypeToken(Type type, Dictionary<Type, TypeToken> knownTypeTokenCache)
        {
            _declaredTypes = new Dictionary<Type, TypeToken>();
            
            _members = new List<MemberToken>();

            Type = type;

            // We pretend a string or a primitive type have no properties or fields.
            // In truth they do but for our purposes they should be treated as "pure" types.
            if (type.IsPrimitive || type == typeof(string))
                return;

            if (!type.IsArray)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < fields.Length; ++i)
                {
                    ref readonly var fieldInfo = ref fields[i];
                    var childTypeToken = GetChildToken(fieldInfo.FieldType);

                    var fieldType = new FieldToken(this, fieldInfo);
                    _members.Add(fieldType);
                }

                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < properties.Length; i++)
                {
                    ref readonly var propInfo = ref properties[i];
                    var childTypeToken = GetChildToken(propInfo.PropertyType);

                    _members.Add(new PropertyToken(this, propInfo));
                }
            }
        }

        public MemberToken GetMemberByIndex(int index, TypeTokenType memberType)
        {
            foreach (var child in Members)
            {
                if (child.MemberType != memberType)
                    continue;

                if (index == 0)
                    return child;

                --index;
            }

            return null;
        }

        public MemberToken FindChild(MemberInfo reflectionInfo)
        {
            foreach (var child in Members)
                if (child.MemberInfo == reflectionInfo)
                    return child;

            return null;
        }

        public T GetAttribute<T>() where T : Attribute
        {
            return Type.GetCustomAttribute<T>();
        }

        public TypeToken GetChildToken(Type type)
        {
            if (_declaredTypes.TryGetValue(type, out var typeInfo))
                return typeInfo;

            _declaredTypes[type] = new TypeToken(type);
            return _declaredTypes[type];
        }

        public TypeToken GetElementTypeToken()
        {
            if (!Type.IsArray)
                throw new InvalidOperationException("not an array");

            return GetChildToken(Type.GetElementType());
        }

        public bool HasChild(IMemberToken child)
        {
            return _members.Any(m => m == child);
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }
}
