using System.Collections.Generic;
using System.Numerics;

namespace MaxMind.Db.ReflectionFallback.TestModels
{
    // Mirrors of MaxMind.Db.Test.Contract's models. This assembly never runs the
    // source generator, so decoding these types exercises the reflection
    // activator over the exact shapes the generated activator handles.

    public enum ReflectionContractKind
    {
        Unknown = 0,
        City = 1,
        Country = 2,
    }

    [method: Constructor]
    public sealed record ReflectionContractRecord(
        [MapKey("city_name")] string? City,
        [MapKey("population")] long? Population,
        [MapKey("tags")] List<string>? Tags);

    public sealed class ReflectionContractConstructorModel
    {
        [Constructor]
        public ReflectionContractConstructorModel(
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

    public sealed class ReflectionContractInitModel
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

    public sealed class ReflectionContractCollectionModel
    {
        [Constructor]
        public ReflectionContractCollectionModel(
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

    public struct ReflectionContractStructInner
    {
        [Constructor]
        public ReflectionContractStructInner(
            [MapKey("hits")] long hits,
            [MapKey("label")] string? label
            )
        {
            Hits = hits;
            Label = label;
        }

        public long Hits { get; }
        public string? Label { get; }
    }

    public sealed class ReflectionContractStructHolder
    {
        [Constructor]
        public ReflectionContractStructHolder([MapKey("inner")] ReflectionContractStructInner inner)
        {
            Inner = inner;
        }

        public ReflectionContractStructInner Inner { get; }
    }

    public sealed class ReflectionContractNullableStructHolder
    {
        [Constructor]
        public ReflectionContractNullableStructHolder(
            [MapKey("inner")] ReflectionContractStructInner? inner
            )
        {
            Inner = inner;
        }

        public ReflectionContractStructInner? Inner { get; }
    }

    public sealed class ReflectionContractEnumModel
    {
        [Constructor]
        public ReflectionContractEnumModel([MapKey("kind")] ReflectionContractKind kind)
        {
            Kind = kind;
        }

        public ReflectionContractKind Kind { get; }
    }

    public sealed class ReflectionContractInnerModel
    {
        [Constructor]
        public ReflectionContractInnerModel(
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

    public sealed class ReflectionContractOuterModel
    {
        [Constructor]
        public ReflectionContractOuterModel(
            [MapKey("inner")] ReflectionContractInnerModel? inner,
            [MapKey("absent", true)] ReflectionContractInnerModel? absent
            )
        {
            Absent = absent;
            Inner = inner;
        }

        public ReflectionContractInnerModel? Absent { get; }
        public ReflectionContractInnerModel? Inner { get; }
    }

    public sealed class ReflectionContractAmbiguousModel
    {
        [Constructor]
        public ReflectionContractAmbiguousModel(string value)
        {
            Value = value;
        }

        [Constructor]
        public ReflectionContractAmbiguousModel(long value)
        {
            Value = value;
        }

        public object Value { get; }
    }

    public sealed class ReflectionContractPointerModel
    {
        [Constructor]
        public ReflectionContractPointerModel(
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
