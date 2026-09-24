#region

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

#endregion

namespace MaxMind.Db.Test.Contract
{
    /// <summary>
    ///     Encodes an in-memory value graph into MaxMind DB data-section bytes so
    ///     contract cases can drive <see cref="Decoder" /> without a database file.
    ///     A map is a <see cref="List{T}" /> of <see cref="KeyValuePair{TKey,TValue}" />
    ///     (declaration order is preserved), an array is a <see cref="List{T}" /> of
    ///     <see cref="object" />, and scalars map to database types as follows:
    ///     <see cref="string" /> to utf8_string, <see cref="byte" /> array to bytes,
    ///     <see cref="bool" /> to boolean, <see cref="int" /> to int32,
    ///     <see cref="long" /> to uint32, <see cref="ulong" /> to uint64,
    ///     <see cref="BigInteger" /> to uint128, <see cref="double" /> to double,
    ///     and <see cref="float" /> to float.
    /// </summary>
    internal static class MmdbValueGraph
    {
        private const int TypeArray = 11;
        private const int TypeBoolean = 14;
        private const int TypeBytes = 4;
        private const int TypeDouble = 3;
        private const int TypeFloat = 15;
        private const int TypeInt32 = 8;
        private const int TypeMap = 7;
        private const int TypeUInt128 = 10;
        private const int TypeUInt32 = 6;
        private const int TypeUInt64 = 9;
        private const int TypeUtf8String = 2;

        public static List<object?> Array(params object?[] values)
        {
            return new List<object?>(values);
        }

        public static byte[] Encode(object graph)
        {
            var bytes = new List<byte>();
            Write(bytes, graph);
            return bytes.ToArray();
        }

        public static KeyValuePair<string, object?> Entry(string key, object? value)
        {
            return new KeyValuePair<string, object?>(key, value);
        }

        public static List<KeyValuePair<string, object?>> Map(
            params KeyValuePair<string, object?>[] entries)
        {
            return new List<KeyValuePair<string, object?>>(entries);
        }

        private static void Write(List<byte> bytes, object? value)
        {
            switch (value)
            {
                case null:
                    throw new ArgumentException("The MaxMind DB format has no null value.");
                case string text:
                    WriteString(bytes, text);
                    break;
                case byte[] blob:
                    WriteTypeAndSize(bytes, TypeBytes, blob.Length);
                    bytes.AddRange(blob);
                    break;
                case bool flag:
                    WriteTypeAndSize(bytes, TypeBoolean, flag ? 1 : 0);
                    break;
                case int number:
                    WriteInt32(bytes, number);
                    break;
                case long number:
                    WriteUInt32(bytes, number);
                    break;
                case ulong number:
                    WriteUInt64(bytes, number);
                    break;
                case BigInteger number:
                    WriteUInt128(bytes, number);
                    break;
                case double number:
                    WriteTypeAndSize(bytes, TypeDouble, 8);
                    WriteBigEndian(bytes, (ulong)BitConverter.DoubleToInt64Bits(number), 8);
                    break;
                case float number:
                    WriteTypeAndSize(bytes, TypeFloat, 4);
                    WriteBigEndian(bytes, (uint)BitConverter.SingleToInt32Bits(number), 4);
                    break;
                case List<KeyValuePair<string, object?>> map:
                    WriteTypeAndSize(bytes, TypeMap, map.Count);
                    foreach (var entry in map)
                    {
                        WriteString(bytes, entry.Key);
                        Write(bytes, entry.Value);
                    }
                    break;
                case List<object?> array:
                    WriteTypeAndSize(bytes, TypeArray, array.Count);
                    foreach (var element in array)
                    {
                        Write(bytes, element);
                    }
                    break;
                default:
                    throw new ArgumentException(
                        $"Unsupported value graph node: {value.GetType()}");
            }
        }

        private static void WriteString(List<byte> bytes, string value)
        {
            var payload = Encoding.UTF8.GetBytes(value);
            WriteTypeAndSize(bytes, TypeUtf8String, payload.Length);
            bytes.AddRange(payload);
        }

        private static void WriteInt32(List<byte> bytes, int value)
        {
            // A negative int32 is stored as four big-endian two's-complement
            // bytes; the decoder reads the payload without sign extension.
            var size = value < 0 ? 4 : MinimalSize((uint)value);
            WriteTypeAndSize(bytes, TypeInt32, size);
            WriteBigEndian(bytes, (uint)value, size);
        }

        private static void WriteUInt32(List<byte> bytes, long value)
        {
            if (value < 0 || value > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            var size = MinimalSize((ulong)value);
            WriteTypeAndSize(bytes, TypeUInt32, size);
            WriteBigEndian(bytes, (ulong)value, size);
        }

        private static void WriteUInt64(List<byte> bytes, ulong value)
        {
            var size = MinimalSize(value);
            WriteTypeAndSize(bytes, TypeUInt64, size);
            WriteBigEndian(bytes, value, size);
        }

        private static void WriteUInt128(List<byte> bytes, BigInteger value)
        {
            if (value.Sign < 0 || value.GetBitLength() > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            var payload = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            WriteTypeAndSize(bytes, TypeUInt128, payload.Length);
            bytes.AddRange(payload);
        }

        private static int MinimalSize(ulong value)
        {
            var size = 0;
            do
            {
                size++;
                value >>= 8;
            }
            while (value != 0);
            return size;
        }

        private static void WriteBigEndian(List<byte> bytes, ulong value, int size)
        {
            for (var i = size - 1; i >= 0; i--)
            {
                bytes.Add((byte)(value >> (8 * i)));
            }
        }

        private static void WriteTypeAndSize(List<byte> bytes, int typeNumber, int size)
        {
            // Type numbers above 7 are stored in an extended-type byte that
            // follows the control byte and precedes any size bytes.
            var typeBits = typeNumber > 7 ? 0 : typeNumber;
            if (size < 29)
            {
                bytes.Add((byte)((typeBits << 5) | size));
            }
            else if (size <= 284)
            {
                bytes.Add((byte)((typeBits << 5) | 29));
            }
            else if (size <= 65820)
            {
                bytes.Add((byte)((typeBits << 5) | 30));
            }
            else
            {
                bytes.Add((byte)((typeBits << 5) | 31));
            }

            if (typeNumber > 7)
            {
                bytes.Add((byte)(typeNumber - 7));
            }

            if (size < 29)
            {
                return;
            }
            if (size <= 284)
            {
                bytes.Add((byte)(size - 29));
            }
            else if (size <= 65820)
            {
                WriteBigEndian(bytes, (ulong)(size - 285), 2);
            }
            else
            {
                WriteBigEndian(bytes, (ulong)(size - 65821), 3);
            }
        }
    }
}
