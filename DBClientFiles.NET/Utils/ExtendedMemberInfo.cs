﻿using DBClientFiles.NET.Attributes;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using DBClientFiles.NET.Internals;
using static System.Reflection.CustomAttributeExtensions;

namespace DBClientFiles.NET.Utils
{
    /// <summary>
    /// A convenient class that is used to store metadata information about fields in the record.
    /// </summary>
    internal struct ExtendedMemberInfo
    {
        public MemberInfo MemberInfo { get; }
        public MemberCompressionType CompressionType { get; set; }

        /// <summary>
        /// The type of the target property, as declared in the structure.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Returns true if the associated member has a getter.
        /// </summary>
        /// <remarks>Always <code>false</code> if <see cref="MemberType"/> is not <see cref="MemberTypes.Property"/>.</remarks>
        public bool HasGetter { get; }

        /// <summary>
        /// Returns true if the associated member has a setter.
        /// </summary>
        /// <remarks>Always <code>false</code> if <see cref="MemberType"/> is not <see cref="MemberTypes.Property"/>.</remarks>
        public bool HasSetter { get; }

        /// <summary>
        /// Returns true if the associated member is read-only.
        /// </summary>
        public bool IsInitOnly { get; }

        /// <summary>
        /// The size of the array, assuming it is one; 0 otherwise.
        /// </summary>
        public int Cardinality { get; set; }

        /// <summary>
        /// Index of the member in the declaring type.
        /// </summary>
        public int MemberIndex { get; }

        /// <summary>
        /// Indicates wether or not the member is flagged as signed in file metadata.
        /// </summary>
        public bool IsSigned { get; set; }

        /// <summary>
        /// The default value as specified in file metadata, if any. This could also be a float.
        /// </summary>
        public byte[] DefaultValue { get; set; }

        /// <summary>
        /// Offset, in bits, of this member in the record.
        /// </summary>
        public int OffsetInRecord { get; set; }
        
        /// <summary>
        /// The bit size of this field - this is used only if <see cref="CompressionType"/>
        /// is set to <see cref="MemberCompressionType.Immediate"/>, <see cref="MemberCompressionType.BitpackedPalletData"/>
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

            BitSize = 0;
            OffsetInRecord = 0;
            DefaultValue = null;
            IsSigned = false;
            CompressionType = MemberCompressionType.None;
            Cardinality = 0;
        }

        public ExtendedMemberInfo(FieldInfo member, int memberIndex)
        {
            MemberInfo = member;
            MemberIndex = memberIndex;
            Type = member.FieldType;

            IsInitOnly = member.IsInitOnly;
            HasGetter = false;
            HasSetter = false;

            BitSize = 0;
            OffsetInRecord = 0;
            DefaultValue = null;
            IsSigned = false;
            CompressionType = MemberCompressionType.None;
            Cardinality = 0;
        }

        public static ExtendedMemberInfo Initialize(MemberInfo memberInfo, int memberIndex)
        {
            ExtendedMemberInfo extendedMemberInfo;
            if (memberInfo is PropertyInfo propInfo)
                extendedMemberInfo = new ExtendedMemberInfo(propInfo, memberIndex);
            else if (memberInfo is FieldInfo fieldInfo)
                extendedMemberInfo = new ExtendedMemberInfo(fieldInfo, memberIndex);

            extendedMemberInfo.Initialize();
            return extendedMemberInfo;
        }

        public void Initialize()
        {
            if (Type.IsArray)
            {
                var marshalAttr = MemberInfo.GetCustomAttribute<MarshalAsAttribute>();
                if (marshalAttr != null)
                    Cardinality = marshalAttr.SizeConst;
                else
                {
                    var arraySizeAttribute = MemberInfo.GetCustomAttribute<CardinalityAttribute>();
                    if (arraySizeAttribute != null)
                        Cardinality = arraySizeAttribute.SizeConst;
                    else
                    {
                        var storageAttribute = MemberInfo.GetCustomAttribute<StoragePresenceAttribute>();
                        if (storageAttribute != null)
                            Cardinality = storageAttribute.SizeConst;
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

        public bool IsDefined(Type attributeType, bool inherit = false) => MemberInfo.IsDefined(attributeType, inherit);
    }
}
