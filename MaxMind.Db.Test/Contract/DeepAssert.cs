#region

using System.Collections;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection;
using Xunit.Sdk;

#endregion

namespace MaxMind.Db.Test.Contract
{
    /// <summary>
    ///     Field-by-field comparison of two object graphs produced by different
    ///     activators. The expected and actual graphs are built from different
    ///     model types (one source-generated, one reflection-activated), so
    ///     members are paired by property name and the mismatch path is reported
    ///     as a dotted traversal from the root.
    /// </summary>
    internal static class DeepAssert
    {
        public static void Equal(object? expected, object? actual)
        {
            Equal(expected, actual, "$");
        }

        private static void Equal(object? expected, object? actual, string path)
        {
            if (expected == null || actual == null)
            {
                if (expected != null || actual != null)
                {
                    throw new XunitException(
                        $"Mismatch at {path}: expected {expected ?? "<null>"}, "
                        + $"actual {actual ?? "<null>"}");
                }
                return;
            }

            var expectedType = expected.GetType();
            if (expectedType.IsEnum || actual.GetType().IsEnum)
            {
                if (expected.ToString() != actual.ToString())
                {
                    throw new XunitException(
                        $"Mismatch at {path}: expected {expected}, actual {actual}");
                }
                return;
            }

            if (expectedType.IsPrimitive || expected is string or BigInteger)
            {
                if (!expected.Equals(actual))
                {
                    throw new XunitException(
                        $"Mismatch at {path}: expected {expected}, actual {actual}");
                }
                return;
            }

            if (expected is Network expectedNetwork)
            {
                if (actual is not Network actualNetwork ||
                    expectedNetwork.ToString() != actualNetwork.ToString())
                {
                    throw new XunitException(
                        $"Mismatch at {path}: expected {expectedNetwork}, actual {actual}");
                }
                return;
            }

            // IPAddress appears in models through [Inject("ip_address")]. Compare
            // by value instead of reflecting over its properties: some getters
            // (e.g. ScopeId on an IPv4 address) throw.
            if (expected is IPAddress expectedAddress)
            {
                if (!expectedAddress.Equals(actual))
                {
                    throw new XunitException(
                        $"Mismatch at {path}: expected {expectedAddress}, actual {actual}");
                }
                return;
            }

            if (expected is byte[] expectedBytes)
            {
                if (actual is not byte[] actualBytes ||
                    !expectedBytes.SequenceEqual(actualBytes))
                {
                    throw new XunitException(
                        $"Mismatch at {path}: byte arrays differ");
                }
                return;
            }

            if (expected is IEnumerable expectedEnumerable)
            {
                if (actual is not IEnumerable actualEnumerable)
                {
                    throw new XunitException(
                        $"Mismatch at {path}: expected enumerable, actual {actual.GetType()}");
                }
                var expectedItems = expectedEnumerable.Cast<object?>().ToList();
                var actualItems = actualEnumerable.Cast<object?>().ToList();
                if (expectedItems.Count != actualItems.Count)
                {
                    throw new XunitException(
                        $"Mismatch at {path}: expected {expectedItems.Count} items, "
                        + $"actual {actualItems.Count}");
                }
                for (var i = 0; i < expectedItems.Count; i++)
                {
                    Equal(expectedItems[i], actualItems[i], $"{path}[{i}]");
                }
                return;
            }

            var actualType = actual.GetType();
            foreach (var property in expectedType.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length != 0)
                {
                    continue;
                }
                var actualProperty = actualType.GetProperty(
                    property.Name,
                    BindingFlags.Instance | BindingFlags.Public);
                if (actualProperty == null)
                {
                    throw new XunitException(
                        $"Mismatch at {path}: {actualType} has no property {property.Name}");
                }
                Equal(
                    property.GetValue(expected),
                    actualProperty.GetValue(actual),
                    $"{path}.{property.Name}");
            }
        }
    }
}
