#region

using System.Collections.Generic;

#endregion

namespace MaxMind.Db.Test.Helper
{
    // Generated-side contract models. This assembly references the MaxMind.Db
    // source generator, so every type here is activated through a generated
    // registration. The twins in MaxMind.Db.ReflectionFallback.TestModels use
    // the reflection fallback; ActivationContractTest decodes the same value
    // graphs through both and compares the results member by member.

    public enum ContractStatus
    {
        Unknown = 0,
        Active = 1,
        Suspended = 2,
    }

    [method: Constructor]
    public sealed record ContractPositionalRecord(
        [MapKey("utf8_string")] string Name,
        [MapKey("uint16")] int Population);

    public sealed record ContractInitRecord
    {
        [MapKey("uint16")]
        public int Count { get; init; }

        [MapKey("missing")]
        public string? Missing { get; init; }

        [MapKey("utf8_string")]
        public string Name { get; init; } = "";
    }

    public sealed class ContractCtorClass
    {
        [Constructor]
        public ContractCtorClass(
            [MapKey("utf8_string")] string name,
            [MapKey("missing")] string? missing,
            [MapKey("double")] double score,
            [MapKey("boolean")] bool flag
            )
        {
            Flag = flag;
            Missing = missing;
            Name = name;
            Score = score;
        }

        public bool Flag { get; }
        public string? Missing { get; }
        public string Name { get; }
        public double Score { get; }
    }

    public sealed class ContractNullableModel
    {
        [Constructor]
        public ContractNullableModel(
            [MapKey("uint16")] int? present,
            [MapKey("absent")] int? absent,
            [MapKey("boolean")] bool? flag
            )
        {
            Absent = absent;
            Flag = flag;
            Present = present;
        }

        public int? Absent { get; }
        public bool? Flag { get; }
        public int? Present { get; }
    }

    public sealed class ContractEnumModel
    {
        [Constructor]
        public ContractEnumModel([MapKey("uint16")] ContractStatus status)
        {
            Status = status;
        }

        public ContractStatus Status { get; }
    }

    public sealed class ContractList<T> : List<T>
    {
    }

    public sealed class ContractCollectionModel
    {
        [Constructor]
        public ContractCollectionModel(
            [MapKey("array")] ContractList<long> numbers,
            [MapKey("map")] Dictionary<string, object> attributes
            )
        {
            Attributes = attributes;
            Numbers = numbers;
        }

        public Dictionary<string, object> Attributes { get; }
        public ContractList<long> Numbers { get; }
    }

    public sealed class ContractInner
    {
        [Constructor]
        public ContractInner(
            [MapKey("utf8_stringX")] string value,
            [MapKey("missing")] string? missing
            )
        {
            Missing = missing;
            Value = value;
        }

        public string? Missing { get; }
        public string Value { get; }
    }

    public sealed class ContractNestedModel
    {
        [Constructor]
        public ContractNestedModel(
            [MapKey("map")] ContractInner inner,
            [MapKey("absent_map", true)] ContractInner alwaysCreated
            )
        {
            AlwaysCreated = alwaysCreated;
            Inner = inner;
        }

        public ContractInner AlwaysCreated { get; }
        public ContractInner Inner { get; }
    }

    public struct ContractStructValue
    {
        [Constructor]
        public ContractStructValue([MapKey("uint16")] int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class ContractStructHolder
    {
        [Constructor]
        public ContractStructHolder([MapKey("map")] ContractStructValue inner)
        {
            Inner = inner;
        }

        public ContractStructValue Inner { get; }
    }

    public sealed class ContractInjectModel
    {
        [Constructor]
        public ContractInjectModel(
            [MapKey("utf8_string")] string name,
            [Inject("injected")] string injected,
            [Network] Network network
            )
        {
            Injected = injected;
            Name = name;
            Network = network;
        }

        public string Injected { get; }
        public string Name { get; }
        public Network Network { get; }
    }

    public sealed class ContractMismatchModel
    {
        [Constructor]
        public ContractMismatchModel([MapKey("uint16")] long number)
        {
            Number = number;
        }

        public long Number { get; }
    }

    public sealed class ContractSparseModel
    {
        [MapKey("utf8_string")]
        public string? Name { get; set; }
    }
}
