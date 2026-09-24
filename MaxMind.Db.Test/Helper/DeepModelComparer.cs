#region

using System;
using System.Collections;
using System.Linq;
using System.Numerics;
using Xunit;

#endregion

namespace MaxMind.Db.Test.Helper
{
    /// <summary>
    ///     Structurally compares two object graphs produced by the two activation
    ///     paths. The generated and reflection models are different CLR types, so
    ///     comparison walks public properties by name and reports the property
    ///     path of any mismatch.
    /// </summary>
    internal static class DeepModelComparer
    {
        internal static void AssertEquivalent(object? generated, object? reflection)
            => AssertEquivalent(generated, reflection, "$");

        private static void AssertEquivalent(object? generated, object? reflection, string path)
        {
            if (generated == null || reflection == null)
            {
                Assert.True(
                    generated == null && reflection == null,
                    $"Mismatch at {path}: generated is {Describe(generated)}, reflection is {Describe(reflection)}");
                return;
            }

            switch (generated)
            {
                case string:
                case bool:
                case char:
                case BigInteger:
                    Assert.Equal(generated, reflection);
                    return;
                case Enum:
                    // The enum types are declared twice, once per assembly.
                    Assert.Equal(generated.ToString(), reflection.ToString());
                    return;
                case Network network:
                    Assert.Equal(network.ToString(), Assert.IsType<Network>(reflection).ToString());
                    return;
                case byte[] bytes:
                    Assert.Equal(bytes, Assert.IsType<byte[]>(reflection));
                    return;
            }

            var generatedType = generated.GetType();
            if (generatedType.IsPrimitive || generated is decimal)
            {
                Assert.Equal(generated, reflection);
                return;
            }

            if (generated is IDictionary generatedDictionary)
            {
                var reflectionDictionary = Assert.IsAssignableFrom<IDictionary>(reflection);
                Assert.Equal(generatedDictionary.Count, reflectionDictionary.Count);
                foreach (DictionaryEntry entry in generatedDictionary)
                {
                    var key = Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture);
                    Assert.True(
                        reflectionDictionary.Contains(entry.Key),
                        $"Missing dictionary key at {path}: {key}");
                    AssertEquivalent(entry.Value, reflectionDictionary[entry.Key], $"{path}[{key}]");
                }
                return;
            }

            if (generated is IEnumerable generatedEnumerable)
            {
                var generatedItems = generatedEnumerable.Cast<object?>().ToArray();
                var reflectionItems = ((IEnumerable)reflection).Cast<object?>().ToArray();
                Assert.Equal(generatedItems.Length, reflectionItems.Length);
                for (var i = 0; i < generatedItems.Length; i++)
                {
                    AssertEquivalent(generatedItems[i], reflectionItems[i], $"{path}[{i}]");
                }
                return;
            }

            foreach (var property in generatedType
                         .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                         .Where(property => property.GetIndexParameters().Length == 0))
            {
                var reflectionProperty = reflection.GetType().GetProperty(property.Name);
                Assert.True(
                    reflectionProperty != null,
                    $"Reflection model {reflection.GetType().Name} is missing property {property.Name} at {path}");
                AssertEquivalent(
                    property.GetValue(generated),
                    reflectionProperty.GetValue(reflection),
                    $"{path}.{property.Name}");
            }
        }

        private static string Describe(object? value) => value?.ToString() ?? "null";
    }
}
