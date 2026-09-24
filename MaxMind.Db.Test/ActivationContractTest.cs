#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using MaxMind.Db.ReflectionFallback.TestModels;
using MaxMind.Db.Test.Contract;
using MaxMind.Db.Test.Helper;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    /// <summary>
    ///     The shared activation contract: one set of in-memory value graphs is
    ///     decoded through the source-generated activator (models in this
    ///     assembly) and through the reflection activator (mirror models in
    ///     MaxMind.Db.ReflectionFallback.TestModels). Successful decodes compare
    ///     member by member; failures compare the exception type and message,
    ///     which carry the expected and actual types.
    /// </summary>
    public class ActivationContractTest
    {
        private static readonly Network ContractNetwork = new(IPAddress.Parse("1.1.1.0"), 24);

        private static readonly InjectableValues ContractInjectables = CreateInjectables();

        private static InjectableValues CreateInjectables()
        {
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            return injectables;
        }

        [Fact]
        public void PositionalRecordContract()
        {
            var graph = ContractValueGraph.Map(
                Entry("utf8_string", ContractValueGraph.Utf8("unicode! ☯ - ♫")),
                Entry("uint16", ContractValueGraph.Uint16(100)),
                Entry("unknown", ContractValueGraph.Array(
                    ContractValueGraph.Uint16(1),
                    ContractValueGraph.Map(Entry("x", ContractValueGraph.Boolean(true))))));

            var (generated, reflection) = DecodeBoth<
                Helper.ContractPositionalRecord,
                ReflectionFallback.TestModels.ContractPositionalRecord>(graph);

            Assert.Equal("unicode! ☯ - ♫", generated.Name);
            Assert.Equal(100, generated.Population);
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Fact]
        public void InitOnlyRecordContract()
        {
            var graph = ContractValueGraph.Map(
                Entry("utf8_string", ContractValueGraph.Utf8("init only")),
                Entry("uint16", ContractValueGraph.Uint16(7)));

            var (generated, reflection) = DecodeBoth<
                Helper.ContractInitRecord,
                ReflectionFallback.TestModels.ContractInitRecord>(graph);

            Assert.Equal("init only", generated.Name);
            Assert.Equal(7, generated.Count);
            Assert.Null(generated.Missing);
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Fact]
        public void ConstructorMappedClassContract()
        {
            var graph = ContractValueGraph.Map(
                Entry("utf8_string", ContractValueGraph.Utf8("mapped")),
                Entry("double", ContractValueGraph.Double(42.25)),
                Entry("boolean", ContractValueGraph.Boolean(true)));

            var (generated, reflection) = DecodeBoth<
                Helper.ContractCtorClass,
                ReflectionFallback.TestModels.ContractCtorClass>(graph);

            Assert.Equal("mapped", generated.Name);
            Assert.Null(generated.Missing);
            Assert.Equal(42.25, generated.Score);
            Assert.True(generated.Flag);
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Fact]
        public void NullableMembersContract()
        {
            var populated = ContractValueGraph.Map(
                Entry("uint16", ContractValueGraph.Uint16(7)),
                Entry("boolean", ContractValueGraph.Boolean(true)));

            var (generated, reflection) = DecodeBoth<
                Helper.ContractNullableModel,
                ReflectionFallback.TestModels.ContractNullableModel>(populated);

            Assert.Equal(7, generated.Present);
            Assert.Null(generated.Absent);
            Assert.True(generated.Flag);
            DeepModelComparer.AssertEquivalent(generated, reflection);

            var empty = ContractValueGraph.Map();
            var (emptyGenerated, emptyReflection) = DecodeBoth<
                Helper.ContractNullableModel,
                ReflectionFallback.TestModels.ContractNullableModel>(empty);

            Assert.Null(emptyGenerated.Present);
            Assert.Null(emptyGenerated.Absent);
            Assert.Null(emptyGenerated.Flag);
            DeepModelComparer.AssertEquivalent(emptyGenerated, emptyReflection);
        }

        [Fact]
        public void EnumMemberFailsIdenticallyOnBothPaths()
        {
            var graph = ContractValueGraph.Map(
                Entry("uint16", ContractValueGraph.Uint16(1)));

            var (generated, reflection) = FailBoth<
                Helper.ContractEnumModel,
                ReflectionFallback.TestModels.ContractEnumModel>(graph);

            Assert.Equal(
                "Could not convert 'System.Int32' to 'MaxMind.Db.Test.Helper.ContractStatus'.",
                generated.Message);
            Assert.Equal(
                "Could not convert 'System.Int32' to 'MaxMind.Db.ReflectionFallback.TestModels.ContractStatus'.",
                reflection.Message);
        }

        [Fact]
        public void GenericCollectionsContract()
        {
            var graph = ContractValueGraph.Map(
                Entry("array", ContractValueGraph.Array(
                    ContractValueGraph.Uint32(1),
                    ContractValueGraph.Uint32(2),
                    ContractValueGraph.Uint32(3))),
                Entry("map", ContractValueGraph.Map(
                    Entry("name", ContractValueGraph.Utf8("value")),
                    Entry("count", ContractValueGraph.Uint16(2)))));

            var (generated, reflection) = DecodeBoth<
                Helper.ContractCollectionModel,
                ReflectionFallback.TestModels.ContractCollectionModel>(graph);

            Assert.Equal([1L, 2L, 3L], generated.Numbers);
            Assert.IsType<Helper.ContractList<long>>(generated.Numbers);
            Assert.IsType<ReflectionFallback.TestModels.ContractList<long>>(reflection.Numbers);
            Assert.Equal("value", generated.Attributes["name"]);
            Assert.Equal(2, generated.Attributes["count"]);
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Fact]
        public void NestedModelAndAlwaysCreateContract()
        {
            var graph = ContractValueGraph.Map(
                Entry("map", ContractValueGraph.Map(
                    Entry("utf8_stringX", ContractValueGraph.Utf8("hello")),
                    Entry("unknown", ContractValueGraph.Uint16(1)))));

            var (generated, reflection) = DecodeBoth<
                Helper.ContractNestedModel,
                ReflectionFallback.TestModels.ContractNestedModel>(graph);

            Assert.Equal("hello", generated.Inner.Value);
            Assert.Null(generated.Inner.Missing);
            Assert.NotNull(generated.AlwaysCreated);
            Assert.Null(generated.AlwaysCreated.Value);
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Fact]
        public void StructMemberFailsIdenticallyOnBothPaths()
        {
            // Struct models are never registered (MMDBSG012 pins the compile-time
            // side), so both holders decode their struct member through the
            // reflection fallback, whose compiled constructor lambda cannot box a
            // value type. Both paths therefore surface the same ArgumentException.
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(Helper.ContractStructValue), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(ReflectionFallback.TestModels.ContractStructValue), out _));

            var graph = ContractValueGraph.Map(
                Entry("map", ContractValueGraph.Map(
                    Entry("uint16", ContractValueGraph.Uint16(42)))));

            var (generated, reflection) = FailBoth<
                Helper.ContractStructHolder,
                ReflectionFallback.TestModels.ContractStructHolder>(graph);

            Assert.Equal(typeof(ArgumentException), generated.GetType());
            Assert.Contains("ContractStructValue", generated.Message);
        }

        [Fact]
        public void InjectedAndNetworkMembersContract()
        {
            var graph = ContractValueGraph.Map(
                Entry("utf8_string", ContractValueGraph.Utf8("with runtime members")));

            var (generated, reflection) = DecodeBoth<
                Helper.ContractInjectModel,
                ReflectionFallback.TestModels.ContractInjectModel>(
                graph, ContractInjectables, ContractNetwork);

            Assert.Equal("with runtime members", generated.Name);
            Assert.Equal("injected string", generated.Injected);
            Assert.Equal("1.1.1.0/24", generated.Network.ToString());
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Fact]
        public void UnknownFieldsAreSkippedIdentically()
        {
            var graph = ContractValueGraph.Map(
                Entry("unknown_scalar", ContractValueGraph.Uint16(1)),
                Entry("utf8_string", ContractValueGraph.Utf8("kept")),
                Entry("unknown_map", ContractValueGraph.Map(
                    Entry("nested", ContractValueGraph.Array(
                        ContractValueGraph.Utf8("deep"),
                        ContractValueGraph.Boolean(false))))),
                Entry("unknown_array", ContractValueGraph.Array(
                    ContractValueGraph.Map(
                        Entry("x", ContractValueGraph.Uint64(9))))));

            var (generated, reflection) = DecodeBoth<
                Helper.ContractSparseModel,
                ReflectionFallback.TestModels.ContractSparseModel>(graph);

            Assert.Equal("kept", generated.Name);
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Fact]
        public void PointerToSharedNestedContainerContract()
        {
            // The nested map lives at offset 0 and both root entries point to it.
            var inner = ContractValueGraph.Map(
                Entry("utf8_stringX", ContractValueGraph.Utf8("shared")));
            var root = ContractValueGraph.Map(
                Entry("map", ContractValueGraph.Pointer(0)),
                Entry("absent_map", ContractValueGraph.Pointer(0)));
            var graph = inner.Concat(root).ToArray();

            // The root map follows the shared nested map in the buffer.
            var (generated, reflection) = DecodeBoth<
                Helper.ContractNestedModel,
                ReflectionFallback.TestModels.ContractNestedModel>(
                graph, offset: inner.Length);

            Assert.Equal("shared", generated.Inner.Value);
            Assert.Equal("shared", generated.AlwaysCreated.Value);
            DeepModelComparer.AssertEquivalent(generated, reflection);
        }

        [Fact]
        public void ScalarTypeMismatchFailsIdenticallyOnBothPaths()
        {
            var graph = ContractValueGraph.Map(
                Entry("uint16", ContractValueGraph.Utf8("not a number")));

            var (generated, reflection) = FailBoth<
                Helper.ContractMismatchModel,
                ReflectionFallback.TestModels.ContractMismatchModel>(graph);

            Assert.Equal(
                "Could not convert 'System.String' to 'System.Int64'.",
                generated.Message);
            Assert.Equal(generated.Message, reflection.Message);
        }

        private static KeyValuePair<string, byte[]> Entry(string key, byte[] value)
            => new(key, value);

        private static (TGenerated Generated, TReflection Reflection) DecodeBoth<TGenerated, TReflection>(
            byte[] graph,
            InjectableValues? injectables = null,
            Network? network = null,
            long offset = 0
            )
            where TGenerated : class
            where TReflection : class
        {
            AssertActivatorParity<TGenerated, TReflection>();
            using var buffer = new MemoryMapBuffer(new MemoryStream(graph, writable: false));
            var decoder = new Decoder(buffer, 0);
            var generated = decoder.Decode<TGenerated>(offset, out _, injectables, network);
            var reflection = decoder.Decode<TReflection>(offset, out _, injectables, network);
            return (generated, reflection);
        }

        private static (Exception Generated, Exception Reflection) FailBoth<TGenerated, TReflection>(
            byte[] graph
            )
            where TGenerated : class
            where TReflection : class
        {
            AssertActivatorParity<TGenerated, TReflection>();
            using var buffer = new MemoryMapBuffer(new MemoryStream(graph, writable: false));
            var decoder = new Decoder(buffer, 0);
            var generated = Record.Exception(() => decoder.Decode<TGenerated>(0, out _));
            var reflection = Record.Exception(() => decoder.Decode<TReflection>(0, out _));
            Assert.NotNull(generated);
            Assert.NotNull(reflection);
            Assert.Equal(generated.GetType(), reflection.GetType());
            // The expected/actual types in the message name the model types,
            // which live in different namespaces on each side.
            Assert.Equal(
                generated.Message.Replace(
                    typeof(TGenerated).Namespace!, "{Models}", StringComparison.Ordinal),
                reflection.Message.Replace(
                    typeof(TReflection).Namespace!, "{Models}", StringComparison.Ordinal));
            return (generated, reflection);
        }

        /// <summary>
        ///     Pins which activator path each side uses and that both activators
        ///     made the same constructor choice: the same database keys mapped to
        ///     the same positions with the same member type shapes, and the same
        ///     injectable, network, and always-created members.
        /// </summary>
        private static void AssertActivatorParity<TGenerated, TReflection>()
        {
            Assert.True(
                SourceGeneratorSupport.TryGetTypeRegistration(typeof(TGenerated), out _),
                $"{typeof(TGenerated).Name} should use the source-generated activator");
            Assert.False(
                SourceGeneratorSupport.TryGetTypeRegistration(typeof(TReflection), out _),
                $"{typeof(TReflection).Name} should use the reflection activator");

            var creator = new TypeActivatorCreator();
            var generated = creator.GetActivator(typeof(TGenerated));
            var reflection = creator.GetActivator(typeof(TReflection));

            Assert.Equal(
                generated.DefaultParameters.Length,
                reflection.DefaultParameters.Length);

            // The reflection activator keys injectable and network members by
            // parameter/property name in DeserializationParameters (their values
            // are overwritten later), while generated metadata keeps them out of
            // the key dictionary. The contract is over the mapped keys only.
            var runtimePositions = reflection.InjectableParameters
                .Select(injectable => injectable.Value.Position)
                .Concat(reflection.NetworkParameters.Select(param => param.Position))
                .ToHashSet();
            var reflectionMapped = reflection.DeserializationParameters
                .Where(pair => !runtimePositions.Contains(pair.Value.Position))
                .ToArray();
            Assert.Equal(
                generated.DeserializationParameters.Count + runtimePositions.Count,
                reflection.DeserializationParameters.Count);
            Assert.Equal(generated.DeserializationParameters.Count, reflectionMapped.Length);
            foreach (var pair in reflectionMapped)
            {
                Assert.True(generated.DeserializationParameters.ContainsKey(pair.Key));
            }
            foreach (var pair in generated.DeserializationParameters)
            {
                Assert.True(
                    reflection.DeserializationParameters.TryGetValue(pair.Key, out var reflectionMember),
                    $"Reflection activator is missing the database key at position {pair.Value.Position}");
                Assert.Equal(pair.Value.Position, reflectionMember.Position);
                Assert.Equal(
                    TypeShape(pair.Value.MemberType),
                    TypeShape(reflectionMember.MemberType));
            }

            Assert.Equal(
                generated.InjectableParameters.Select(injectable => injectable.Key),
                reflection.InjectableParameters.Select(injectable => injectable.Key));
            Assert.Equal(
                generated.InjectableParameters.Select(injectable => injectable.Value.Position),
                reflection.InjectableParameters.Select(injectable => injectable.Value.Position));
            Assert.Equal(
                generated.NetworkParameters.Select(param => param.Position),
                reflection.NetworkParameters.Select(param => param.Position));
            Assert.Equal(
                generated.AlwaysCreatedParameters.Select(param => param.Position),
                reflection.AlwaysCreatedParameters.Select(param => param.Position));
        }

        private static string TypeShape(Type type)
        {
            if (type.IsArray)
            {
                return TypeShape(type.GetElementType()!) + "[]";
            }
            if (type.IsGenericType)
            {
                return type.Name + "<" + string.Join(
                    ",",
                    type.GetGenericArguments().Select(TypeShape)) + ">";
            }
            return type.Name;
        }
    }
}
