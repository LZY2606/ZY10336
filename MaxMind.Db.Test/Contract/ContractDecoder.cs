#region

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

#endregion

namespace MaxMind.Db.Test.Contract
{
    /// <summary>
    ///     The outcome of decoding one contract case through one activator. Keeping
    ///     the exception as data lets the contract suite compare the failure shape
    ///     of the generated and reflection activators instead of only asserting that
    ///     each one throws.
    /// </summary>
    internal sealed class ContractResult
    {
        internal ContractResult(object? value, Exception? error)
        {
            Value = value;
            Error = error;
        }

        internal Exception? Error { get; }
        internal object? Value { get; }
    }

    /// <summary>
    ///     Decodes an in-memory value graph into a model type through the same
    ///     <see cref="Decoder" /> the <see cref="Reader" /> uses.
    /// </summary>
    internal static class ContractDecoder
    {
        private static readonly MethodInfo DecodeMethod =
            typeof(Decoder).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(method => method.Name == "Decode" && method.IsGenericMethodDefinition);

        public static ContractResult Decode(
            Type modelType,
            object graph,
            InjectableValues? injectables = null,
            Network? network = null
            )
        {
            var bytes = MmdbValueGraph.Encode(graph);
            try
            {
                using var buffer = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
                var decoder = new Decoder(buffer, 0);
                var value = DecodeMethod.MakeGenericMethod(modelType).Invoke(
                    decoder,
                    [0L, 0L, injectables, network]);
                return new ContractResult(value, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return new ContractResult(null, ex.InnerException);
            }
        }
    }

    /// <summary>
    ///     Parity assertions for the pair of results one contract case produces:
    ///     one from the source-generated activator and one from the reflection
    ///     activator.
    /// </summary>
    internal static class ContractAssert
    {
        public static void BothSucceeded(ContractResult generated, ContractResult reflection)
        {
            Assert.Null(generated.Error);
            Assert.Null(reflection.Error);
            DeepAssert.Equal(generated.Value, reflection.Value);
        }

        public static void BothFailedWith(
            ContractResult generated,
            ContractResult reflection,
            params string[] fragments
            )
        {
            Assert.Null(generated.Value);
            Assert.Null(reflection.Value);
            var generatedError = generated.Error;
            var reflectionError = reflection.Error;
            Assert.NotNull(generatedError);
            Assert.NotNull(reflectionError);
            Assert.Equal(generatedError.GetType(), reflectionError.GetType());
            foreach (var fragment in fragments)
            {
                Assert.Contains(fragment, generatedError.Message);
                Assert.Contains(fragment, reflectionError.Message);
            }
        }
    }
}
