#region

using System;
using System.Net;
using System.Numerics;
using MaxMind.Db.ReflectionFallback.TestModels;
using Xunit;

#endregion

namespace MaxMind.Db.Test.Contract
{
    /// <summary>
    ///     One contract suite, two activators: every case decodes the same
    ///     in-memory value graph through the source-generated activator (models
    ///     in this assembly) and the reflection activator (mirrored models in
    ///     MaxMind.Db.ReflectionFallback.TestModels). Successful decodes compare
    ///     field-by-field; failures compare the exception type and the
    ///     expected/actual type information in the message.
    /// </summary>
    public class ActivationContractTest
    {
        private static readonly Network ContractNetwork = new(IPAddress.Parse("10.0.0.0"), 8);

        private static InjectableValues ContractInjectables
        {
            get
            {
                var injectables = new InjectableValues();
                injectables.AddValue("origin", "contract-origin");
                return injectables;
            }
        }

        [Fact]
        public void GeneratedModelsAreRegisteredAndReflectionModelsAreNot()
        {
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractRecord), out _));
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractConstructorModel), out _));
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractInitModel), out _));
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractCollectionModel), out _));
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractStructHolder), out _));
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractNullableStructHolder), out _));
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractEnumModel), out _));
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractInnerModel), out _));
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractOuterModel), out _));

            // The generator rejects these shapes at compile time (MMDBSG007 and
            // MMDBSG012), so even in this assembly they fall back to reflection
            // at runtime. The failure contract below is what keeps the two
            // activators aligned for them.
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractAmbiguousModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ContractStructInner), out _));

            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractRecord), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractConstructorModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractInitModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractCollectionModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractStructHolder), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractNullableStructHolder), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractEnumModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractInnerModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractOuterModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(typeof(ReflectionContractAmbiguousModel), out _));
        }

        [Fact]
        public void PositionalRecordMatchesAcrossActivators()
        {
            // Unknown keys (scalar, nested container, and array) must be skipped
            // identically by both activators.
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry("city_name", "London"),
                MmdbValueGraph.Entry("population", 8_800_000L),
                MmdbValueGraph.Entry("tags", MmdbValueGraph.Array("a", "b")),
                MmdbValueGraph.Entry("unknown_scalar", 42L),
                MmdbValueGraph.Entry(
                    "unknown_container",
                    MmdbValueGraph.Map(
                        MmdbValueGraph.Entry("nested", MmdbValueGraph.Array(1L, 2L)))),
                MmdbValueGraph.Entry("unknown_array", MmdbValueGraph.Array("x", 3L)));

            var generated = ContractDecoder.Decode(typeof(ContractRecord), graph);
            var reflection = ContractDecoder.Decode(typeof(ReflectionContractRecord), graph);

            ContractAssert.BothSucceeded(generated, reflection);
            var value = Assert.IsType<ContractRecord>(generated.Value);
            Assert.Equal("London", value.City);
            Assert.Equal(8_800_000L, value.Population);
            Assert.Equal(["a", "b"], value.Tags);
        }

        [Fact]
        public void ConstructorModelMatchesAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry("city_name", "Paris"),
                MmdbValueGraph.Entry("population", 2_100_000L),
                MmdbValueGraph.Entry("score", 42.5),
                MmdbValueGraph.Entry("active", true),
                MmdbValueGraph.Entry("ratio", 1.5F),
                MmdbValueGraph.Entry("big", BigInteger.Parse("1329227995784915872903807060280344576")),
                MmdbValueGraph.Entry("blob", new byte[] { 0, 1, 42 }),
                MmdbValueGraph.Entry("count", -7),
                MmdbValueGraph.Entry("total", 1152921504606846976UL));

            var generated = ContractDecoder.Decode(
                typeof(ContractConstructorModel), graph, ContractInjectables, ContractNetwork);
            var reflection = ContractDecoder.Decode(
                typeof(ReflectionContractConstructorModel), graph, ContractInjectables, ContractNetwork);

            ContractAssert.BothSucceeded(generated, reflection);
            var value = Assert.IsType<ContractConstructorModel>(generated.Value);
            Assert.Equal("Paris", value.City);
            Assert.Equal(2_100_000L, value.Population);
            Assert.Equal(42.5, value.Score);
            Assert.True(value.Active);
            Assert.Equal(1.5F, value.Ratio);
            Assert.Equal(BigInteger.Parse("1329227995784915872903807060280344576"), value.Big);
            Assert.Equal([0, 1, 42], value.Blob);
            Assert.Equal(-7, value.Count);
            Assert.Equal(1152921504606846976UL, value.Total);
            Assert.Equal("contract-origin", value.Origin);
            Assert.Equal("10.0.0.0/8", value.Network?.ToString());
        }

        [Fact]
        public void ConstructorModelWithAbsentNullablesMatchesAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry("score", 1.25),
                MmdbValueGraph.Entry("active", false),
                MmdbValueGraph.Entry("ratio", 0.5F),
                MmdbValueGraph.Entry("big", BigInteger.One),
                MmdbValueGraph.Entry("count", 0),
                MmdbValueGraph.Entry("total", 0UL));

            var generated = ContractDecoder.Decode(
                typeof(ContractConstructorModel), graph, ContractInjectables, ContractNetwork);
            var reflection = ContractDecoder.Decode(
                typeof(ReflectionContractConstructorModel), graph, ContractInjectables, ContractNetwork);

            ContractAssert.BothSucceeded(generated, reflection);
            var value = Assert.IsType<ContractConstructorModel>(generated.Value);
            Assert.Null(value.City);
            Assert.Null(value.Population);
            Assert.Null(value.Blob);
        }

        [Fact]
        public void InitOnlyPropertyModelMatchesAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry("city_name", "Berlin"),
                MmdbValueGraph.Entry("population", 3_700_000L),
                MmdbValueGraph.Entry("unrelated", "ignored"));

            var generated = ContractDecoder.Decode(
                typeof(ContractInitModel), graph, ContractInjectables, ContractNetwork);
            var reflection = ContractDecoder.Decode(
                typeof(ReflectionContractInitModel), graph, ContractInjectables, ContractNetwork);

            ContractAssert.BothSucceeded(generated, reflection);
            var value = Assert.IsType<ContractInitModel>(generated.Value);
            Assert.Equal("Berlin", value.City);
            Assert.Equal(3_700_000L, value.Population);
            Assert.Equal("default-value", value.Missing);
            Assert.Equal("contract-origin", value.Origin);
            Assert.Equal("10.0.0.0/8", value.Network?.ToString());
        }

        [Fact]
        public void CollectionModelMatchesAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry("longs", MmdbValueGraph.Array(1L, 2L, 3L)),
                MmdbValueGraph.Entry(
                    "lookup",
                    MmdbValueGraph.Map(
                        MmdbValueGraph.Entry("one", 1L),
                        MmdbValueGraph.Entry("two", 2L))),
                MmdbValueGraph.Entry("names", MmdbValueGraph.Array("alpha", "beta")),
                MmdbValueGraph.Entry(
                    "nested",
                    MmdbValueGraph.Array(
                        MmdbValueGraph.Map(MmdbValueGraph.Entry("a", 7L)),
                        MmdbValueGraph.Map(MmdbValueGraph.Entry("b", 8L)))));

            var generated = ContractDecoder.Decode(typeof(ContractCollectionModel), graph);
            var reflection = ContractDecoder.Decode(typeof(ReflectionContractCollectionModel), graph);

            ContractAssert.BothSucceeded(generated, reflection);
            var value = Assert.IsType<ContractCollectionModel>(generated.Value);
            Assert.Equal([1L, 2L, 3L], value.Longs);
            Assert.Equal(2, value.Lookup.Count);
            Assert.Equal(1L, value.Lookup["one"]);
            Assert.Equal(["alpha", "beta"], value.Names);
            Assert.Equal(2, value.Nested.Count);
            Assert.Equal(8L, value.Nested[1]["b"]);
        }

        [Fact]
        public void StructMemberFailsIdenticallyAcrossActivators()
        {
            // Struct models are unsupported by design: the generator reports
            // MMDBSG012 at compile time, and both activators fail at runtime
            // when the expression compiler cannot return a value type as
            // object. The failure shape is the contract.
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry(
                    "inner",
                    MmdbValueGraph.Map(
                        MmdbValueGraph.Entry("hits", 5L),
                        MmdbValueGraph.Entry("label", "five"))));

            var generated = ContractDecoder.Decode(typeof(ContractStructHolder), graph);
            var reflection = ContractDecoder.Decode(typeof(ReflectionContractStructHolder), graph);

            ContractAssert.BothFailedWith(
                generated,
                reflection,
                "cannot be used for return type 'System.Object'",
                "ContractStructInner");
            Assert.IsType<ArgumentException>(generated.Error);
        }

        [Fact]
        public void NullableStructMemberFailsIdenticallyAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry(
                    "inner",
                    MmdbValueGraph.Map(MmdbValueGraph.Entry("hits", 5L))));

            var generated = ContractDecoder.Decode(typeof(ContractNullableStructHolder), graph);
            var reflection = ContractDecoder.Decode(typeof(ReflectionContractNullableStructHolder), graph);

            ContractAssert.BothFailedWith(
                generated,
                reflection,
                "Unexpected number of Dictionary generic arguments: 1");
            Assert.IsType<DeserializationException>(generated.Error);
        }

        [Fact]
        public void EnumMemberFailsIdenticallyAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(MmdbValueGraph.Entry("kind", 1L));

            var generated = ContractDecoder.Decode(typeof(ContractEnumModel), graph);
            var reflection = ContractDecoder.Decode(typeof(ReflectionContractEnumModel), graph);

            ContractAssert.BothFailedWith(
                generated,
                reflection,
                "Could not convert",
                "System.Int64",
                "ContractKind");
            Assert.IsType<DeserializationException>(generated.Error);
            Assert.Contains(typeof(ContractKind).FullName!, generated.Error!.Message);
            Assert.Contains(typeof(ReflectionContractKind).FullName!, reflection.Error!.Message);
        }

        [Fact]
        public void NestedAndAlwaysCreateModelsMatchAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry(
                    "inner",
                    MmdbValueGraph.Map(MmdbValueGraph.Entry("label", "present"))));

            var generated = ContractDecoder.Decode(
                typeof(ContractOuterModel), graph, ContractInjectables, ContractNetwork);
            var reflection = ContractDecoder.Decode(
                typeof(ReflectionContractOuterModel), graph, ContractInjectables, ContractNetwork);

            ContractAssert.BothSucceeded(generated, reflection);
            var value = Assert.IsType<ContractOuterModel>(generated.Value);
            Assert.Equal("present", value.Inner?.Label);
            Assert.NotNull(value.Absent);
            Assert.Equal("contract-origin", value.Absent.Origin);
            Assert.Equal("10.0.0.0/8", value.Absent.Network?.ToString());
        }

        [Fact]
        public void AmbiguousConstructorFailsIdenticallyAcrossActivators()
        {
            // The source generator reports MMDBSG007 for this shape at compile
            // time and produces no registration, so both legs reach the runtime
            // reflection error. The compile-time diagnostic and the runtime
            // exception name the same model; only the timing differs.
            var graph = MmdbValueGraph.Map(MmdbValueGraph.Entry("value", 1L));

            var generated = ContractDecoder.Decode(typeof(ContractAmbiguousModel), graph);
            var reflection = ContractDecoder.Decode(typeof(ReflectionContractAmbiguousModel), graph);

            ContractAssert.BothFailedWith(
                generated,
                reflection,
                "More than one constructor found",
                "ContractAmbiguousModel");
            Assert.IsType<DeserializationException>(generated.Error);
            Assert.Contains(typeof(ContractAmbiguousModel).FullName!, generated.Error!.Message);
            Assert.Contains(
                typeof(ReflectionContractAmbiguousModel).FullName!, reflection.Error!.Message);
        }

        [Fact]
        public void ScalarTypeMismatchFailsIdenticallyAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry("city_name", "Paris"),
                MmdbValueGraph.Entry("score", 42.5),
                MmdbValueGraph.Entry("active", true),
                MmdbValueGraph.Entry("ratio", 1.5F),
                MmdbValueGraph.Entry("big", BigInteger.One),
                MmdbValueGraph.Entry("count", "not-a-number"),
                MmdbValueGraph.Entry("total", 0UL));

            var generated = ContractDecoder.Decode(
                typeof(ContractConstructorModel), graph, ContractInjectables, ContractNetwork);
            var reflection = ContractDecoder.Decode(
                typeof(ReflectionContractConstructorModel), graph, ContractInjectables, ContractNetwork);

            ContractAssert.BothFailedWith(
                generated,
                reflection,
                "Could not convert 'System.String' to 'System.Int32'");
            Assert.IsType<DeserializationException>(generated.Error);
        }

        [Fact]
        public void MissingInjectableFailsIdenticallyAcrossActivators()
        {
            var graph = MmdbValueGraph.Map(
                MmdbValueGraph.Entry("city_name", "Paris"),
                MmdbValueGraph.Entry("score", 42.5),
                MmdbValueGraph.Entry("active", true),
                MmdbValueGraph.Entry("ratio", 1.5F),
                MmdbValueGraph.Entry("big", BigInteger.One),
                MmdbValueGraph.Entry("count", 0),
                MmdbValueGraph.Entry("total", 0UL));

            var generated = ContractDecoder.Decode(typeof(ContractConstructorModel), graph);
            var reflection = ContractDecoder.Decode(typeof(ReflectionContractConstructorModel), graph);

            ContractAssert.BothFailedWith(
                generated,
                reflection,
                "No injectable value found for origin");
            Assert.IsType<DeserializationException>(generated.Error);
        }
    }
}
