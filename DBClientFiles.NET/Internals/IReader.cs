﻿using DBClientFiles.NET.Collections;
using System;
using System.Collections.Generic;
using DBClientFiles.NET.Internals.Serializers;
using DBClientFiles.NET.Utils;

namespace DBClientFiles.NET.Internals
{
    internal interface IReader<T> : IDisposable
        where T : class, new()
    {
        bool ReadHeader();
        void ReadSegments();
        void MapRecords();
        IEnumerable<T> ReadRecords();
        
        CodeGenerator<T> Generator { get; }

        ExtendedMemberInfoCollection MemberStore
        {
            get;
            set;
        }

        uint TableHash { get; }
        uint LayoutHash { get; }

        U ExtractKey<U>(T instance) where U : struct;
    }
}
