﻿using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace DBClientFiles.NET.IO
{
    internal static class _FileReader<TKey>
    {
        public static MethodInfo ReadUInt64 { get; } = typeof(FileReader<TKey>).GetMethod("ReadUInt64", Type.EmptyTypes);
        public static MethodInfo ReadUInt32 { get; } = typeof(FileReader<TKey>).GetMethod("ReadUInt32", Type.EmptyTypes);
        public static MethodInfo ReadUInt16 { get; } = typeof(FileReader<TKey>).GetMethod("ReadUInt16", Type.EmptyTypes);
        public static MethodInfo ReadSByte  { get; } = typeof(FileReader<TKey>).GetMethod("ReadSByte", Type.EmptyTypes);

        public static MethodInfo ReadInt64  { get; } = typeof(FileReader<TKey>).GetMethod("ReadInt64", Type.EmptyTypes);
        public static MethodInfo ReadInt32  { get; } = typeof(FileReader<TKey>).GetMethod("ReadInt32", Type.EmptyTypes);
        public static MethodInfo ReadInt16  { get; } = typeof(FileReader<TKey>).GetMethod("ReadInt16", Type.EmptyTypes);
        public static MethodInfo ReadByte   { get; } = typeof(FileReader<TKey>).GetMethod("ReadByte", Type.EmptyTypes);
        
        public static MethodInfo ReadSingle { get; } = typeof(FileReader<TKey>).GetMethod("ReadSingle", Type.EmptyTypes);
        public static MethodInfo ReadBit    { get; } = typeof(FileReader<TKey>).GetMethod("ReadBit", Type.EmptyTypes);
        public static MethodInfo ReadString { get; } = typeof(FileReader<TKey>).GetMethod("ReadString", Type.EmptyTypes);

        public static MethodInfo ReadBits   { get; } = typeof(FileReader<TKey>).GetMethod("ReadBits", new[] { typeof(int) });

        public static MethodInfo ReadPalletMember      { get; } = typeof(FileReader<TKey>).GetMethod("ReadPalletMember", new[] { typeof(int), typeof(TKey) });
        public static MethodInfo ReadPalletArrayMember { get; } = typeof(FileReader<TKey>).GetMethod("ReadPalletArrayMember", new[] { typeof(int), typeof(TKey) });
        public static MethodInfo ReadCommonMember      { get; } = typeof(FileReader<TKey>).GetMethod("ReadCommonMember", new[] { typeof(int), typeof(TKey) });
        public static MethodInfo ReadForeignKeyMember  { get; } = typeof(FileReader<TKey>).GetMethod("ReadForeignKeyMember", new[] { typeof(int), typeof(TKey) });

        public static MethodInfo ResetBitReader { get; } = typeof(FileReader<TKey>).GetMethod("ResetBitReader", Type.EmptyTypes);
    }

    /// <summary>
    /// The basic class in charge of processing <code>.dbc</code> and <code>.db2</code> files.
    /// </summary>
    internal abstract class FileReader<U> : BinaryReader
    {
        private int _bitIndex;

        public FileReader(Stream strm, bool keepOpen = false) : base(strm, Encoding.UTF8, keepOpen)
        {
        }

        public abstract T ReadPalletMember<T>(int memberIndex, U key);
        public abstract T ReadCommonMember<T>(int memberIndex, U key);
        public abstract T ReadForeignKeyMember<T>(int memberIndex, U key);
        public abstract T[] ReadPalletArrayMember<T>(int memberIndex, U key);

        public override byte[] ReadBytes(int byteCount)
        {
            ResetBitReader();
            return base.ReadBytes(byteCount);
        }

        public void ResetBitReader()
        {
            if (_bitIndex == 0)
                return;

            // Consume the byte and move on
            if (_bitIndex < 8)
                BaseStream.Position += 1;

            _bitIndex = 0;
        }

        public override ulong ReadUInt64()
        {
            ResetBitReader();
            return base.ReadUInt64();
        }

        public override long ReadInt64()
        {
            ResetBitReader();
            return base.ReadInt64();
        }

        public override uint ReadUInt32()
        {
            ResetBitReader();
            return base.ReadUInt32();
        }
        public override int ReadInt32()
        {
            ResetBitReader();
            return base.ReadInt32();
        }

        public virtual uint ReadUInt24()
        {
            ResetBitReader();
            throw new NotImplementedException();
        }
        public virtual int ReadInt24()
        {
            ResetBitReader();

            throw new NotImplementedException();
        }

        public override ushort ReadUInt16()
        {
            ResetBitReader();
            return base.ReadUInt16();
        }
        public override short ReadInt16()
        {
            ResetBitReader();
            return base.ReadInt16();
        }

        public override byte ReadByte()
        {
            ResetBitReader();
            return base.ReadByte();
        }
        public override sbyte ReadSByte()
        {
            ResetBitReader();
            return base.ReadSByte();
        }

        public override float ReadSingle()
        {
            ResetBitReader();
            return base.ReadSingle();
        }

        public bool ReadBit()
        {
            var currByte = (byte)PeekChar();
            var bitMask = 1 << (7 - _bitIndex++);

            if (_bitIndex == 8)
            {
                BaseStream.Position += 1;
                _bitIndex = 0;
            }

            return (currByte & bitMask) != 0;
        }

        public long ReadBits(int bitCount)
        {
            long value = 0;
            for (var i = bitCount - 1; i >= 0; --i)
                if (ReadBit())
                    value |= 1L << i;

            return value;
        }
    }
}