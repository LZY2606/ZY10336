#region

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

#endregion

namespace MaxMind.Db.Test.Contract
{
    /// <summary>
    ///     Builds MaxMind DB data-section value graphs in memory so that contract
    ///     tests can decode the exact same bytes through the reflection activator
    ///     and the source-generated activator. This is a test-support encoder only;
    ///     it does not bypass any production decode path.
    /// </summary>
    internal static class ContractValueGraph
    {
        // ObjectType numbers from the MaxMind DB specification. Types 8 and above
        // are stored in the byte following an "extended" control byte.
        private const int TypePointer = 1;
        private const int TypeUtf8String = 2;
        private const int TypeDouble = 3;
        private const int TypeBytes = 4;
        private const int TypeUint16 = 5;
        private const int TypeUint32 = 6;
        private const int TypeMap = 7;
        private const int TypeInt32 = 8;
        private const int TypeUint64 = 9;
        private const int TypeUint128 = 10;
        private const int TypeArray = 11;
        private const int TypeBoolean = 14;
        private const int TypeFloat = 15;

        internal static byte[] Map(params KeyValuePair<string, byte[]>[] entries)
        {
            var bytes = new List<byte>();
            WriteHeader(bytes, TypeMap, entries.Length);
            foreach (var entry in entries)
            {
                bytes.AddRange(Utf8(entry.Key));
                bytes.AddRange(entry.Value);
            }
            return bytes.ToArray();
        }

        internal static byte[] Array(params byte[][] items)
        {
            var bytes = new List<byte>();
            WriteHeader(bytes, TypeArray, items.Length);
            foreach (var item in items)
            {
                bytes.AddRange(item);
            }
            return bytes.ToArray();
        }

        internal static byte[] Utf8(string value)
        {
            var payload = Encoding.UTF8.GetBytes(value);
            var bytes = new List<byte>(payload.Length + 4);
            WriteHeader(bytes, TypeUtf8String, payload.Length);
            bytes.AddRange(payload);
            return bytes.ToArray();
        }

        internal static byte[] Bytes(byte[] payload)
        {
            var bytes = new List<byte>(payload.Length + 4);
            WriteHeader(bytes, TypeBytes, payload.Length);
            bytes.AddRange(payload);
            return bytes.ToArray();
        }

        internal static byte[] Uint16(int value) => Unsigned(TypeUint16, value);

        internal static byte[] Uint32(long value) => Unsigned(TypeUint32, value);

        internal static byte[] Uint64(ulong value)
        {
            var payload = MinimalBytes(value);
            var bytes = new List<byte>(payload.Length + 2);
            WriteHeader(bytes, TypeUint64, payload.Length);
            bytes.AddRange(payload);
            return bytes.ToArray();
        }

        internal static byte[] Uint128(BigInteger value)
        {
            var payload = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            var bytes = new List<byte>(payload.Length + 2);
            WriteHeader(bytes, TypeUint128, payload.Length);
            bytes.AddRange(payload);
            return bytes.ToArray();
        }

        internal static byte[] Int32(int value)
        {
            var bytes = new List<byte>(6);
            WriteHeader(bytes, TypeInt32, 4);
            bytes.AddRange(BigEndian((ulong)(uint)value, 4));
            return bytes.ToArray();
        }

        internal static byte[] Double(double value)
        {
            var bytes = new List<byte>(9);
            WriteHeader(bytes, TypeDouble, 8);
            bytes.AddRange(BigEndian((ulong)BitConverter.DoubleToInt64Bits(value), 8));
            return bytes.ToArray();
        }

        internal static byte[] Float(float value)
        {
            var bytes = new List<byte>(5);
            WriteHeader(bytes, TypeFloat, 4);
            bytes.AddRange(BigEndian(BitConverter.SingleToUInt32Bits(value), 4));
            return bytes.ToArray();
        }

        internal static byte[] Boolean(bool value)
        {
            var bytes = new List<byte>(2);
            WriteHeader(bytes, TypeBoolean, value ? 1 : 0);
            return bytes.ToArray();
        }

        /// <summary>
        ///     A one-byte-pointer-payload pointer (pointer size 1, base 0) to
        ///     <paramref name="target"/>. Targets must be below 2048.
        /// </summary>
        internal static byte[] Pointer(int target) =>
        [
            (byte)((TypePointer << 5) | ((target >> 8) & 0x7)),
            (byte)(target & 0xFF),
        ];

        private static byte[] Unsigned(int type, long value) => Unsigned(type, (ulong)value);

        private static byte[] Unsigned(int type, ulong value)
        {
            var payload = MinimalBytes(value);
            var bytes = new List<byte>(payload.Length + 4);
            WriteHeader(bytes, type, payload.Length);
            bytes.AddRange(payload);
            return bytes.ToArray();
        }

        private static byte[] MinimalBytes(ulong value)
        {
            if (value == 0)
            {
                return [];
            }
            var count = 0;
            for (var remaining = value; remaining != 0; remaining >>= 8)
            {
                count++;
            }
            return BigEndian(value, count);
        }

        private static byte[] BigEndian(ulong value, int count)
        {
            var bytes = new byte[count];
            for (var i = count - 1; i >= 0; i--)
            {
                bytes[i] = (byte)(value & 0xFF);
                value >>= 8;
            }
            return bytes;
        }

        private static void WriteHeader(List<byte> bytes, int type, int size)
        {
            if (type >= 8)
            {
                WriteControl(bytes, 0, size);
                bytes.Add((byte)(type - 7));
                return;
            }
            WriteControl(bytes, type, size);
        }

        private static void WriteControl(List<byte> bytes, int type, int size)
        {
            switch (size)
            {
                case < 29:
                    bytes.Add((byte)((type << 5) | size));
                    return;
                case < 285:
                    bytes.Add((byte)((type << 5) | 29));
                    bytes.Add((byte)(size - 29));
                    return;
                case < 65805:
                    bytes.Add((byte)((type << 5) | 30));
                    bytes.Add((byte)((size - 285) >> 8));
                    bytes.Add((byte)((size - 285) & 0xFF));
                    return;
                default:
                    bytes.Add((byte)((type << 5) | 31));
                    bytes.Add((byte)((size - 65805) >> 16));
                    bytes.Add((byte)(((size - 65805) >> 8) & 0xFF));
                    bytes.Add((byte)((size - 65805) & 0xFF));
                    return;
            }
        }
    }
}
