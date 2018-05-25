﻿using DBClientFiles.NET.Attributes;
using System;
using System.Collections.Generic;
using DBClientFiles.NET.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using DBClientFiles.NET.Internals;
using static System.Reflection.CustomAttributeExtensions;

namespace DBClientFiles.NET.Utils
{
    internal sealed class ExtendedMemberInfo
    {
        public MemberInfo MemberInfo { get; }
        public MemberCompressionType CompressionType { get; private set; } = MemberCompressionType.None;

        // TODO: Figure out a better name for this member.
        public Type Type { get; }

        /// <summary>
        /// Returns true if the associated member has a getter.
        /// </summary>
        public bool HasGetter { get; }

        /// <summary>
        /// Returns true if the associated member has a setter.
        /// </summary>
        public bool HasSetter { get; }

        /// <summary>
        /// Returns true if the associated member is read-only.
        /// </summary>
        public bool IsInitOnly { get; }

        /// <summary>
        /// The size of the array, assuming it is one; 0 otherwise.
        /// </summary>
        public int ArraySize { get; private set; }

        /// <summary>
        /// Index of the member in the declaring type.
        /// </summary>
        public int MemberIndex { get; }
        
        /// <summary>
        /// The bit size of this field - this is used only if <see cref="CompressionType"/>
        /// is set to <see cref="MemberCompressionType.Bitpacked"/>, <see cref="MemberCompressionType.BitpackedPalletData"/>
        /// or <see cref="MemberCompressionType.BitpackedPalletArrayData"/>.
        /// </summary>
        public int BitSize { get; set; }

        public ExtendedMemberInfo(PropertyInfo member, int memberIndex)
        {
            MemberInfo = member;
            MemberIndex = memberIndex;
            Type = member.PropertyType;

            IsInitOnly = false;
            HasGetter = member.GetGetMethod() != null;
            HasSetter = member.GetSetMethod() != null;

            InitializeMemberHelpers();
        }

        public ExtendedMemberInfo(FieldInfo member, int memberIndex)
        {
            MemberInfo = member;
            MemberIndex = memberIndex;
            Type = member.FieldType;

            IsInitOnly = member.IsInitOnly;

            InitializeMemberHelpers();
        }

        public static ExtendedMemberInfo Initialize(MemberInfo memberInfo, int memberIndex)
        {
            if (memberInfo is PropertyInfo propInfo)
                return new ExtendedMemberInfo(propInfo, memberIndex);
            else if (memberInfo is FieldInfo fieldInfo)
                return new ExtendedMemberInfo(fieldInfo, memberIndex);
            return null;
        }

        private void InitializeMemberHelpers()
        {
            if (Type.IsArray)
            {
                var marshalAttr = MemberInfo.GetCustomAttribute<MarshalAsAttribute>();
                if (marshalAttr != null)
                    ArraySize = marshalAttr.SizeConst;
                else
                {
                    var arraySizeAttribute = MemberInfo.GetCustomAttribute<CardinalityAttribute>();
                    if (arraySizeAttribute != null)
                        ArraySize = arraySizeAttribute.SizeConst;
                    else
                    {
                        var storageAttribute = MemberInfo.GetCustomAttribute<StoragePresenceAttribute>();
                        if (storageAttribute != null)
                            ArraySize = storageAttribute.SizeConst;
                    }
                }
            }
        }

        public ExtendedMemberExpression MakeMemberAccess(Expression source)
        {
            return new ExtendedMemberExpression(source, this);
        }
        
        public MemberTypes MemberType => MemberInfo.MemberType;
        public string Name => MemberInfo.Name;

        public object[] GetCustomAttributes(bool inherit = false) => MemberInfo.GetCustomAttributes(inherit);
        public object[] GetCustomAttributes(Type attributeType, bool inherit = false) => MemberInfo.GetCustomAttributes(attributeType, inherit);
        public T GetCustomAttribute<T>(bool inherit = false) where T : Attribute
            => MemberInfo.GetCustomAttribute<T>(inherit);
        public bool IsDefined(Type attributeType, bool inherit = false) => MemberInfo.IsDefined(attributeType, inherit);
    }
}
