﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DBClientFiles.NET.Parsing.Enums;
using DBClientFiles.NET.Parsing.Reflection;
using DBClientFiles.NET.Parsing.Serialization.Generators;
using DBClientFiles.NET.Parsing.Shared.Records;
using DBClientFiles.NET.Parsing.Shared.Segments;
using DBClientFiles.NET.Parsing.Shared.Segments.Handlers.Implementations;
using DBClientFiles.NET.Parsing.Versions.WDB5.Binding;

namespace DBClientFiles.NET.Parsing.Versions.WDB5
{
    internal sealed class SerializerGenerator<T> : TypedSerializerGenerator<T, int>
    {
        public delegate void MethodType(IRecordReader recordReader, Parser<T> fileParser, out T instance);
        private MethodType _methodImpl;

        public MethodType Method
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_methodImpl == null)
                    _methodImpl = GenerateDeserializer<MethodType>();

                Debug.Assert(_methodImpl != null, "failed to generate deserializer method");
                return _methodImpl;
            }
        }

        private IList<MemberMetadata> _memberMetadata;

        /// <summary>
        /// Stores the index column's position in the record. This field is set <b>iff</b> the index table exists.
        /// </summary>
        private int? _indexColumn;

        public SerializerGenerator(IBinaryStorageFile storage) : base(storage.Type, storage.Options.TokenType, 0)
        {
            Parameters.Add(Expression.Parameter(typeof(IRecordReader), "recordReader"));
            Parameters.Add(Expression.Parameter(typeof(Parser<T>), "fileParser"));
            Parameters.Add(Expression.Parameter(typeof(T).MakeByRefType(), "instance"));

            _memberMetadata = storage.FindSegment(SegmentIdentifier.FieldInfo)?.Handler as FieldInfoHandler<MemberMetadata>;

            if (storage.Header.IndexTable.Exists)
                _indexColumn = storage.Header.IndexColumn;
        }

        public MemberMetadata GetMemberInfo(int callIndex)
        {
            if (_indexColumn.HasValue)
            {
                // WDB5 doesn't list the index column if it's part of the index table
                // So we have to jump through some hoops to get it to run properly
                
                if (callIndex == _indexColumn)
                    return default;
                else if (callIndex > _indexColumn)
                    --callIndex; // Account for the index column
            }

            // TODO: Is improving this needed?
            for (var i = 0; i < _memberMetadata.Count; ++i)
            {
                var memberMetadata = _memberMetadata[i];
                for (var j = 0; j < memberMetadata.Cardinality; ++j)
                {
                    if (callIndex == 0)
                        return memberMetadata;

                    --callIndex;
                }
            }

            return RELATIONSHIP_TABLE_ENTRY;
        }

        private static MemberMetadata RELATIONSHIP_TABLE_ENTRY = new MemberMetadata();
        static SerializerGenerator()
        {
            RELATIONSHIP_TABLE_ENTRY.CompressionData.Type = MemberCompressionType.RelationshipData;
        }

        public override Expression GenerateExpressionReader(TypeToken typeToken, MemberToken memberToken)
        {
            // NOTE: This only works because the generator tries to unroll any loop instead of rolling them
            var memberMetadata = GetMemberInfo(State++);
            if (memberMetadata == null)
                return null;

            switch (memberMetadata.CompressionData.Type)
            {
                // We have to use immediate readers because all the other ones assume sequential reads
                case MemberCompressionType.RelationshipData:
                    {
                        // This is used to parse values found in WMOMinimapTexture (@barncastle)
                        // Well ok fair it isn't yet but that's the plan

                        // TODO: use Parameters[1] for this (because it lets us access blocks!)
                        break;
                    }
                case MemberCompressionType.None:
                case MemberCompressionType.Immediate:
                    if (typeToken.IsPrimitive)
                    {
                        return Expression.Call(RecordReader,
                            typeToken.MakeGenericMethod(_IRecordReader.ReadImmediate),
                            Expression.Constant(memberMetadata.CompressionData.Offset),
                            Expression.Constant(memberMetadata.CompressionData.Size));
                    }
                    else if (typeToken == typeof(string))
                        return Expression.Call(RecordReader,
                            _IRecordReader.ReadStringImmediate,
                            Expression.Constant(memberMetadata.CompressionData.Offset),
                            Expression.Constant(memberMetadata.CompressionData.Size));

                    break;
            }
            
            throw new InvalidOperationException("Unsupported compression type");
        }

        private Expression RecordReader => Parameters[0];

        protected override Expression Instance => Parameters[1];
    }
}