#region

using System.Collections.Generic;
using System.Numerics;

#endregion

namespace MaxMind.Db.Test.Contract
{
    // Models in this assembly are registered by the MaxMind.Db source generator,
    // so decoding them exercises the source-generated activator. The mirrored
    // models in MaxMind.Db.ReflectionFallback.TestModels have identical shapes,
    // map keys, and property names, and exercise the reflection activator.

    internal enum ContractKind
    {
        Unknown = 0,
        City = 1,
        Country = 2,
    }

    [method: Constructor]
    internal sealed record ContractRecord(
        [MapKey("city_name")] string? City,
        [MapKey("population")] long? Population,
        [MapKey("tags")] List<string>? Tags);

    internal sealed class ContractConstructorModel
    {
        [Constructor]
        public ContractConstructorModel(
            [MapKey("city_name")] string? city,
            [MapKey("population")] long? population,
            [MapKey("score")] double score,
            [MapKey("active")] bool active,
            [MapKey("ratio")] float ratio,
            [MapKey("big")] BigInteger big,
            [MapKey("blob")] byte[]? blob,
            [MapKey("count")] int count,
            [MapKey("total")] ulong total,
            [Inject("origin")] string? origin,
            [Network] Network? network
            )
        {
            Active = active;
            Big = big;
            Blob = blob;
            City = city;
            Count = count;
            Network = network;
            Origin = origin;
            Population = population;
            Ratio = ratio;
            Score = score;
            Total = total;
        }

        public bool Active { get; }
        public BigInteger Big { get; }
        public byte[]? Blob { get; }
        public string? City { get; }
        public int Count { get; }
        public Network? Network { get; }
        public string? Origin { get; }
        public long? Population { get; }
        public float Ratio { get; }
        public double Score { get; }
        public ulong Total { get; }
    }

    internal sealed class ContractInitModel
    {
        [MapKey("city_name")]
        public string? City { get; init; }

        [MapKey("missing")]
        public string Missing { get; init; } = "default-value";

        [Network]
        public Network? Network { get; init; }

        [Inject("origin")]
        public string? Origin { get; init; }

        [MapKey("population")]
        public long? Population { get; init; }
    }

    internal sealed class ContractCollectionModel
    {
        [Constructor]
        public ContractCollectionModel(
            [MapKey("longs")] List<long> longs,
            [MapKey("lookup")] Dictionary<string, long> lookup,
            [MapKey("names")] IReadOnlyList<string> names,
            [MapKey("nested")] List<Dictionary<string, long>> nested
            )
        {
            Longs = longs;
            Lookup = lookup;
            Names = names;
            Nested = nested;
        }

        public List<long> Longs { get; }
        public Dictionary<string, long> Lookup { get; }
        public IReadOnlyList<string> Names { get; }
        public List<Dictionary<string, long>> Nested { get; }
    }

    internal struct ContractStructInner
    {
        [Constructor]
        public ContractStructInner([MapKey("hits")] long hits, [MapKey("label")] string? label)
        {
            Hits = hits;
            Label = label;
        }

        public long Hits { get; }
        public string? Label { get; }
    }

    internal sealed class ContractStructHolder
    {
        [Constructor]
        public ContractStructHolder([MapKey("inner")] ContractStructInner inner)
        {
            Inner = inner;
        }

        public ContractStructInner Inner { get; }
    }

    internal sealed class ContractNullableStructHolder
    {
        [Constructor]
        public ContractNullableStructHolder([MapKey("inner")] ContractStructInner? inner)
        {
            Inner = inner;
        }

        public ContractStructInner? Inner { get; }
    }

    internal sealed class ContractEnumModel
    {
        [Constructor]
        public ContractEnumModel([MapKey("kind")] ContractKind kind)
        {
            Kind = kind;
        }

        public ContractKind Kind { get; }
    }

    internal sealed class ContractInnerModel
    {
        [Constructor]
        public ContractInnerModel(
            [MapKey("label")] string? label,
            [Inject("origin")] string? origin,
            [Network] Network? network
            )
        {
            Label = label;
            Origin = origin;
            Network = network;
        }

        public string? Label { get; }
        public Network? Network { get; }
        public string? Origin { get; }
    }

    internal sealed class ContractOuterModel
    {
        [Constructor]
        public ContractOuterModel(
            [MapKey("inner")] ContractInnerModel? inner,
            [MapKey("absent", true)] ContractInnerModel? absent
            )
        {
            Absent = absent;
            Inner = inner;
        }

        public ContractInnerModel? Absent { get; }
        public ContractInnerModel? Inner { get; }
    }

    internal sealed class ContractAmbiguousModel
    {
        [Constructor]
        public ContractAmbiguousModel(string value)
        {
            Value = value;
        }

        [Constructor]
        public ContractAmbiguousModel(long value)
        {
            Value = value;
        }

        public object Value { get; }
    }

    internal sealed class ContractPointerModel
    {
        [Constructor]
        public ContractPointerModel(
            [MapKey("long_key")] string? longKey,
            [MapKey("long_key2")] string? longKey2
            )
        {
            LongKey = longKey;
            LongKey2 = longKey2;
        }

        public string? LongKey { get; }
        public string? LongKey2 { get; }
    }
}
