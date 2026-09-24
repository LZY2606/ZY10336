namespace MaxMind.Db.ReflectionFallback.TestModels
{
    // Runtime mirrors of the diagnostic contract cases in
    // MaxMind.Db.Test/Contract/DiagnosticContract.cs. This assembly has no
    // source generator, so every type here goes through the reflection
    // fallback, whose behavior the contract pins against the MMDBSG
    // diagnostic reported for the same model shape at compile time.

    public sealed class ReflectionMultipleConstructorsModel
    {
        [Constructor]
        public ReflectionMultipleConstructorsModel(string value)
        {
            Value = value;
        }

        [Constructor]
        public ReflectionMultipleConstructorsModel(long value)
        {
            Value = value;
        }

        public object Value { get; }
    }

    public sealed class ReflectionMissingParameterlessModel
    {
        public ReflectionMissingParameterlessModel(string value)
        {
            Value = value;
        }

        [MapKey("value")]
        public string Value { get; set; }
    }

    public sealed class ReflectionReadOnlyPropertyModel
    {
        [MapKey("value")]
        public string Value { get; } = "";
    }

    public sealed class ReflectionDuplicateMapKeyModel
    {
        [Constructor]
        public ReflectionDuplicateMapKeyModel(
            [MapKey("value")] string first,
            [MapKey("value")] string second
            )
        {
            First = first;
            Second = second;
        }

        public string First { get; }
        public string Second { get; }
    }

    public sealed class ReflectionRequiredMemberModel
    {
        [MapKey("value")]
        public required string Value { get; init; }
    }

    public sealed class ReflectionOpenGenericModel<T>
    {
        [Constructor]
        public ReflectionOpenGenericModel([MapKey("value")] T value)
        {
            Value = value;
        }

        public T Value { get; }
    }

    public class ReflectionHiddenPropertyBase
    {
        [MapKey("value")]
        public string? Value { get; set; }
    }

    public sealed class ReflectionHiddenPropertyModel : ReflectionHiddenPropertyBase
    {
        public new int Value { get; set; }
    }

    public sealed class ReflectionConflictingMemberModel
    {
        [Constructor]
        public ReflectionConflictingMemberModel(
            [MapKey("value")] [Inject("injected")] string? value = null
            )
        {
            Value = value;
        }

        public string? Value { get; }
    }

    public sealed class ReflectionPrefixedKeyAttribute : MapKeyAttribute
    {
        public ReflectionPrefixedKeyAttribute(string name)
            : base("prefix_" + name)
        {
        }
    }

    public sealed class ReflectionDerivedMapKeyModel
    {
        [Constructor]
        public ReflectionDerivedMapKeyModel([ReflectionPrefixedKey("value")] string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}
