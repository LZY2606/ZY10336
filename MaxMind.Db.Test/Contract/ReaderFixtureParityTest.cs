#region

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using MaxMind.Db.ReflectionFallback.TestModels;
using MaxMind.Db.Test.Helper;
using Xunit;

#endregion

namespace MaxMind.Db.Test.Contract
{
    /// <summary>
    ///     Reads the real Reader fixtures with both activators and deep-compares
    ///     the resulting object graphs. The generated-assembly models resolve to
    ///     source-generated registrations; their mirrors in
    ///     MaxMind.Db.ReflectionFallback.TestModels resolve to the reflection
    ///     activator. Each fixture runs over the memory-mapped, in-memory, and
    ///     stream buffer backends.
    /// </summary>
    public class ReaderFixtureParityTest
    {
        private static readonly MethodInfo DecodeMethod =
            typeof(Decoder).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(method => method.Name == "Decode" && method.IsGenericMethodDefinition);

        private readonly string _testDataRoot =
            Path.Combine(TestUtils.TestDirectory, "TestData", "MaxMind-DB", "test-data");

        [Theory]
        [InlineData(FileAccessMode.MemoryMapped)]
        [InlineData(FileAccessMode.Memory)]
        public void ConstructorFixtureMatchesAcrossActivatorsAndBackends(FileAccessMode mode)
        {
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"), mode);
            var address = IPAddress.Parse("1.1.1.1");

            var generated = reader.Find<TypeHolder>(address, injectables);
            var reflection = reader.Find<ReflectionTypeHolder>(address, injectables);

            Assert.NotNull(generated);
            Assert.NotNull(reflection);
            DeepAssert.Equal(generated, reflection);
        }

        [Fact]
        public void ConstructorFixtureMatchesAcrossActivatorsOverStream()
        {
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            using var stream = File.OpenRead(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            using var reader = new Reader(stream);
            var address = IPAddress.Parse("1.1.1.1");

            var generated = reader.Find<TypeHolder>(address, injectables);
            var reflection = reader.Find<ReflectionTypeHolder>(address, injectables);

            Assert.NotNull(generated);
            Assert.NotNull(reflection);
            DeepAssert.Equal(generated, reflection);
        }

        [Theory]
        [InlineData(FileAccessMode.MemoryMapped)]
        [InlineData(FileAccessMode.Memory)]
        public void PropertyFixtureMatchesAcrossActivatorsAndBackends(FileAccessMode mode)
        {
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"), mode);
            var address = IPAddress.Parse("1.1.1.1");

            var generated = reader.Find<PropTypeHolder>(address, injectables);
            var reflection = reader.Find<ReflectionPropTypeHolder>(address, injectables);

            Assert.NotNull(generated);
            Assert.NotNull(reflection);
            DeepAssert.Equal(generated, reflection);
        }

        [Fact]
        public void PropertyFixtureMatchesAcrossActivatorsOverStream()
        {
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            using var stream = File.OpenRead(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            using var reader = new Reader(stream);
            var address = IPAddress.Parse("1.1.1.1");

            var generated = reader.Find<PropTypeHolder>(address, injectables);
            var reflection = reader.Find<ReflectionPropTypeHolder>(address, injectables);

            Assert.NotNull(generated);
            Assert.NotNull(reflection);
            DeepAssert.Equal(generated, reflection);
        }

        [Fact]
        public void EnumerationMatchesAcrossActivators()
        {
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected_value");
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));

            var generated = reader.FindAll<PropNoNetworkTypeHolder>(injectables).ToList();
            var reflection = reader.FindAll<ReflectionPropNoNetworkTypeHolder>(injectables).ToList();

            Assert.Equal(26, generated.Count);
            Assert.Equal(generated.Count, reflection.Count);
            foreach (var (expected, actual) in generated.Zip(reflection))
            {
                Assert.Equal(expected.Start, actual.Start);
                Assert.Equal(expected.PrefixLength, actual.PrefixLength);
                DeepAssert.Equal(expected.Data, actual.Data);
            }
        }

        [Theory]
        [InlineData(0, "long_value1", null)]
        [InlineData(22, "long_value2", null)]
        [InlineData(37, null, "long_value1")]
        [InlineData(50, null, "long_value2")]
        [InlineData(55, "long_value1", null)]
        [InlineData(57, null, "long_value2")]
        public void PointerFixtureMatchesAcrossActivatorsAndBackends(
            long offset,
            string? expectedLongKey,
            string? expectedLongKey2
            )
        {
            var path = Path.Combine(_testDataRoot, "maps-with-pointers.raw");
            var generated = DecodeAcrossBufferBackends(typeof(ContractPointerModel), path, offset);
            var reflection = DecodeAcrossBufferBackends(
                typeof(ReflectionContractPointerModel), path, offset);

            ContractAssert.BothSucceeded(generated, reflection);
            var value = Assert.IsType<ContractPointerModel>(generated.Value);
            Assert.Equal(expectedLongKey, value.LongKey);
            Assert.Equal(expectedLongKey2, value.LongKey2);
        }

        private static ContractResult DecodeAcrossBufferBackends(
            Type modelType,
            string path,
            long offset
            )
        {
            // The file-backed memory map and an anonymous map over the file
            // bytes must produce the same outcome for the same model.
            var memoryMapped = DecodeWithBuffer(modelType, new MemoryMapBuffer(path), offset);
            var inMemory = DecodeWithBuffer(
                modelType,
                new MemoryMapBuffer(new MemoryStream(File.ReadAllBytes(path), writable: false)),
                offset);
            if (memoryMapped.Error != null || inMemory.Error != null)
            {
                ContractAssert.BothFailedWith(memoryMapped, inMemory);
                return memoryMapped;
            }
            DeepAssert.Equal(memoryMapped.Value, inMemory.Value);
            return memoryMapped;
        }

        private static ContractResult DecodeWithBuffer(
            Type modelType,
            MemoryMapBuffer buffer,
            long offset
            )
        {
            using (buffer)
            {
                var decoder = new Decoder(buffer, 0);
                try
                {
                    var value = DecodeMethod.MakeGenericMethod(modelType).Invoke(
                        decoder,
                        [offset, 0L, null, null]);
                    return new ContractResult(value, null);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    return new ContractResult(null, ex.InnerException);
                }
            }
        }
    }
}
