﻿using System.IO;

namespace DBClientFiles.NET.Parsing.File
{
    public interface IHeader
    {
        Signatures Signature { get; }

        uint TableHash { get; }
        uint LayoutHash { get; }

        int RecordSize { get; }
        int RecordCount { get; }
        int FieldCount { get; }

        int StringTableLength { get; }

        // For offsetMap
        int MinIndex { get; }
        int MaxIndex { get; }

        int CopyTableLength { get; }
        int IndexColumn { get; }

        bool HasIndexTable { get; }
        bool HasForeignIds { get; }
        bool HasOffsetMap { get; }
    }

    internal interface IFileHeader : IHeader
    {
        int Size { get; }

        void Read(BinaryReader reader);
    }
}