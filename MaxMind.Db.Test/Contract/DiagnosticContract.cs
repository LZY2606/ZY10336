#region

using System;
using System.Collections.Generic;

#endregion

namespace MaxMind.Db.Test.Contract
{
    /// <summary>
    ///     One row of the shared diagnostic contract between the source generator
    ///     and the reflection fallback. The generator reports
    ///     <see cref="DiagnosticId"/> at compile time on <see cref="ModelSource"/>;
    ///     the reflection fallback surfaces the matching runtime behavior for
    ///     <see cref="RuntimeTypeName"/> when the same model shape is decoded.
    ///     The two sides deliberately do not agree on timing: the contract is the
    ///     error code, the model path, and the runtime outcome.
    /// </summary>
    internal sealed class DiagnosticContractCase
    {
        internal DiagnosticContractCase(
            string name,
            string diagnosticId,
            string modelSource,
            string expectedLocationText,
            string? runtimeTypeName,
            Type? runtimeExceptionType,
            string? runtimeMessageFragment,
            byte[] runtimeGraph,
            bool runtimeMessageNamesModel = false
            )
        {
            Name = name;
            DiagnosticId = diagnosticId;
            ModelSource = modelSource;
            ExpectedLocationText = expectedLocationText;
            RuntimeTypeName = runtimeTypeName;
            RuntimeExceptionType = runtimeExceptionType;
            RuntimeMessageFragment = runtimeMessageFragment;
            RuntimeGraph = runtimeGraph;
            RuntimeMessageNamesModel = runtimeMessageNamesModel;
        }

        /// <summary>Gets the contract case name.</summary>
        internal string Name { get; }

        /// <summary>Gets the MMDBSG diagnostic id the generator must report.</summary>
        internal string DiagnosticId { get; }

        /// <summary>Gets the model source compiled for the generator-side check.</summary>
        internal string ModelSource { get; }

        /// <summary>
        ///     Gets the text the reported diagnostic location must contain, pinning
        ///     the diagnostic to the offending model or member.
        /// </summary>
        internal string ExpectedLocationText { get; }

        /// <summary>
        ///     Gets the name of the mirror type in
        ///     MaxMind.Db.ReflectionFallback.TestModels, or <see langword="null"/>
        ///     when the shape cannot be decoded through the runtime API at all.
        /// </summary>
        internal string? RuntimeTypeName { get; }

        /// <summary>
        ///     Gets the exception type the reflection fallback throws, or
        ///     <see langword="null"/> when the runtime path accepts the model.
        /// </summary>
        internal Type? RuntimeExceptionType { get; }

        /// <summary>Gets the fragment the runtime exception message must contain.</summary>
        internal string? RuntimeMessageFragment { get; }

        /// <summary>
        ///     Gets whether the runtime exception message names the model type,
        ///     aligning the runtime failure with the diagnostic's model path.
        /// </summary>
        internal bool RuntimeMessageNamesModel { get; }

        /// <summary>Gets the value graph decoded by the runtime-side check.</summary>
        internal byte[] RuntimeGraph { get; }
    }

    /// <summary>
    ///     The shared diagnostic contract cases consumed by both
    ///     MaxMind.Db.Test (runtime side) and MaxMind.Db.SourceGenerator.Test
    ///     (compile-time side).
    /// </summary>
    internal static class DiagnosticContract
    {
        private const string ModelsNamespace = "MaxMind.Db.ReflectionFallback.TestModels.";

        private static readonly byte[] ValueGraph = ContractValueGraph.Map(
            new KeyValuePair<string, byte[]>("value", ContractValueGraph.Utf8("from-database")));

        internal static IEnumerable<DiagnosticContractCase> Cases
        {
            get
            {
                yield return new DiagnosticContractCase(
                    "StructModel",
                    "MMDBSG012",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal struct StructModel
                    {
                        [Constructor]
                        internal StructModel([MapKey("value")] string value)
                        {
                        }
                    }
                    """,
                    "StructModel",
                    // Decode<T> and Reader.Find<T> both require a reference type, so
                    // a struct model has no runtime root-decode case at all. Struct
                    // members are covered by the activation contract instead.
                    null,
                    null,
                    null,
                    ValueGraph);

                yield return new DiagnosticContractCase(
                    "MultipleConstructors",
                    "MMDBSG007",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal sealed class MultipleConstructorsModel
                    {
                        [Constructor]
                        internal MultipleConstructorsModel(string value)
                        {
                        }

                        [Constructor]
                        internal MultipleConstructorsModel(long value)
                        {
                        }
                    }
                    """,
                    "MultipleConstructorsModel",
                    ModelsNamespace + "ReflectionMultipleConstructorsModel",
                    typeof(DeserializationException),
                    "More than one constructor found",
                    ValueGraph,
                    runtimeMessageNamesModel: true);

                yield return new DiagnosticContractCase(
                    "MissingParameterlessConstructor",
                    "MMDBSG006",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal sealed class MissingParameterlessModel
                    {
                        internal MissingParameterlessModel(string value)
                        {
                        }

                        [MapKey("value")]
                        internal string? Value { get; set; }
                    }
                    """,
                    "MissingParameterlessModel",
                    ModelsNamespace + "ReflectionMissingParameterlessModel",
                    typeof(DeserializationException),
                    "no parameterless constructor found",
                    ValueGraph,
                    runtimeMessageNamesModel: true);

                yield return new DiagnosticContractCase(
                    "ReadOnlyProperty",
                    "MMDBSG003",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal sealed class ReadOnlyPropertyModel
                    {
                        [MapKey("value")]
                        internal string Value { get; } = "";
                    }
                    """,
                    "Value",
                    ModelsNamespace + "ReflectionReadOnlyPropertyModel",
                    typeof(DeserializationException),
                    "must have a setter or init accessor",
                    ValueGraph,
                    runtimeMessageNamesModel: true);

                yield return new DiagnosticContractCase(
                    "DuplicateMapKey",
                    "MMDBSG005",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal sealed class DuplicateMapKeyModel
                    {
                        [Constructor]
                        internal DuplicateMapKeyModel(
                            [MapKey("value")] string first,
                            [MapKey("value")] string second)
                        {
                        }
                    }
                    """,
                    "DuplicateMapKeyModel",
                    ModelsNamespace + "ReflectionDuplicateMapKeyModel",
                    // The reflection fallback builds its key dictionary at activator
                    // creation, where the duplicate key surfaces as an
                    // ArgumentException rather than a DeserializationException.
                    typeof(ArgumentException),
                    "same key",
                    ValueGraph);

                yield return new DiagnosticContractCase(
                    "RequiredMemberWithoutSetsRequiredMembers",
                    "MMDBSG010",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal sealed class RequiredMemberModel
                    {
                        [MapKey("value")]
                        public required string Value { get; init; }
                    }
                    """,
                    "Value",
                    ModelsNamespace + "ReflectionRequiredMemberModel",
                    // Required members are a compile-time concept; the reflection
                    // fallback activates the model through the parameterless
                    // constructor and succeeds.
                    null,
                    null,
                    ValueGraph);

                yield return new DiagnosticContractCase(
                    "OpenGenericModel",
                    "MMDBSG004",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal sealed class OpenGenericModel<T>
                    {
                        [Constructor]
                        internal OpenGenericModel([MapKey("value")] T value)
                        {
                        }
                    }
                    """,
                    "OpenGenericModel",
                    ModelsNamespace + "ReflectionOpenGenericModel`1",
                    // The reflection fallback routes every generic type to the
                    // dictionary decoder, so the closed model fails there rather
                    // than at activator creation.
                    typeof(DeserializationException),
                    "Unexpected number of Dictionary generic arguments",
                    ValueGraph);

                yield return new DiagnosticContractCase(
                    "HiddenAnnotatedProperty",
                    "MMDBSG014",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal class HiddenPropertyBase
                    {
                        [MapKey("value")]
                        internal string? Value { get; set; }
                    }

                    internal sealed class HiddenPropertyModel : HiddenPropertyBase
                    {
                        internal new int Value { get; set; }
                    }
                    """,
                    "Value",
                    ModelsNamespace + "ReflectionHiddenPropertyModel",
                    // The reflection fallback binds the annotated base property.
                    null,
                    null,
                    ValueGraph);

                yield return new DiagnosticContractCase(
                    "ConflictingMemberAttributes",
                    "MMDBSG013",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal sealed class ConflictingMemberModel
                    {
                        [Constructor]
                        internal ConflictingMemberModel(
                            [MapKey("value")] [Inject("injected")] string? value = null)
                        {
                        }
                    }
                    """,
                    "value",
                    ModelsNamespace + "ReflectionConflictingMemberModel",
                    // The reflection fallback reads the key and then lets the
                    // injectable overwrite it.
                    null,
                    null,
                    ValueGraph);

                yield return new DiagnosticContractCase(
                    "DerivedMapKeyAttribute",
                    "MMDBSG011",
                    """
                    using MaxMind.Db;

                    namespace ContractDiag;

                    internal sealed class PrefixedKeyAttribute : MapKeyAttribute
                    {
                        internal PrefixedKeyAttribute(string name)
                            : base("prefix_" + name)
                        {
                        }
                    }

                    internal sealed class DerivedMapKeyModel
                    {
                        [Constructor]
                        internal DerivedMapKeyModel([PrefixedKey("value")] string value)
                        {
                        }
                    }
                    """,
                    "value",
                    ModelsNamespace + "ReflectionDerivedMapKeyModel",
                    // The reflection fallback evaluates the derived attribute at
                    // runtime and maps the prefixed key.
                    null,
                    null,
                    ContractValueGraph.Map(
                        new KeyValuePair<string, byte[]>(
                            "prefix_value", ContractValueGraph.Utf8("from-database"))));
            }
        }
    }
}
