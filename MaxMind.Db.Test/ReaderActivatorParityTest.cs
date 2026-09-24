#region

using System;
using System.IO;
using System.Linq;
using System.Net;
using MaxMind.Db.ReflectionFallback.TestModels;
using MaxMind.Db.Test.Helper;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    /// <summary>
    ///     Reads the same database through every Reader backend with both
    ///     activators — the source-generated models in this assembly and their
    ///     reflection-fallback mirrors — and deep-compares the results.
    /// </summary>
    public class ReaderActivatorParityTest
    {
        private readonly string _testDataRoot =
            Path.Combine(TestUtils.TestDirectory, "TestData", "MaxMind-DB", "test-data");

        public enum ReaderBackend
        {
            MemoryMapped,
            Memory,
            Stream,
        }

        [Theory]
        [InlineData(ReaderBackend.MemoryMapped)]
        [InlineData(ReaderBackend.Memory)]
        [InlineData(ReaderBackend.Stream)]
        public void ConstructorModelsMatchAcrossActivators(ReaderBackend backend)
        {
            using var reader = OpenReader("MaxMind-DB-test-decoder.mmdb", backend);
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");

            foreach (var address in new[] { "1.1.1.1", "1.1.1.32" })
            {
                var ip = IPAddress.Parse(address);
                var generated = reader.Find<TypeHolder>(ip, injectables);
                var reflection = reader.Find<ReflectionTypeHolder>(ip, injectables);

                Assert.NotNull(generated);
                Assert.NotNull(reflection);
                Assert.Equal("unicode! ☯ - ♫", generated.Utf8String);
                DeepModelComparer.AssertEquivalent(generated, reflection);
            }
        }

        [Theory]
        [InlineData(ReaderBackend.MemoryMapped)]
        [InlineData(ReaderBackend.Memory)]
        [InlineData(ReaderBackend.Stream)]
        public void PropertyModelsMatchAcrossActivators(ReaderBackend backend)
        {
            using var reader = OpenReader("MaxMind-DB-test-decoder.mmdb", backend);
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            var ip = IPAddress.Parse("1.1.1.1");

            var generated = reader.Find<PropTypeHolder>(ip, injectables);
            var reflection = reader.Find<ReflectionPropTypeHolder>(ip, injectables);

            Assert.NotNull(generated);
            Assert.NotNull(reflection);
            Assert.Equal("unicode! ☯ - ♫", generated.Utf8String);
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Theory]
        [InlineData(ReaderBackend.MemoryMapped)]
        [InlineData(ReaderBackend.Memory)]
        [InlineData(ReaderBackend.Stream)]
        public void EnumerationMatchesAcrossActivators(ReaderBackend backend)
        {
            using var generatedReader = OpenReader("MaxMind-DB-test-decoder.mmdb", backend);
            using var reflectionReader = OpenReader("MaxMind-DB-test-decoder.mmdb", backend);

            var generatedNodes = generatedReader.FindAll<Helper.ContractNestedModel>().ToArray();
            var reflectionNodes = reflectionReader
                .FindAll<ReflectionFallback.TestModels.ContractNestedModel>()
                .ToArray();

            Assert.NotEmpty(generatedNodes);
            Assert.Equal(generatedNodes.Length, reflectionNodes.Length);
            for (var i = 0; i < generatedNodes.Length; i++)
            {
                Assert.Equal(generatedNodes[i].Start, reflectionNodes[i].Start);
                Assert.Equal(generatedNodes[i].PrefixLength, reflectionNodes[i].PrefixLength);
                DeepModelComparer.AssertEquivalent(
                    generatedNodes[i].Data, reflectionNodes[i].Data);
            }
        }

        private Reader OpenReader(string file, ReaderBackend backend)
        {
            var path = Path.Combine(_testDataRoot, file);
            return backend switch
            {
                ReaderBackend.MemoryMapped => new Reader(path, FileAccessMode.MemoryMapped),
                ReaderBackend.Memory => new Reader(path, FileAccessMode.Memory),
                ReaderBackend.Stream => new Reader(
                    new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)),
                _ => throw new ArgumentOutOfRangeException(nameof(backend)),
            };
        }
    }
}
