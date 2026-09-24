#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MaxMind.Db.ReflectionFallback.TestModels;
using MaxMind.Db.Test.Contract;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    /// <summary>
    ///     The runtime side of the shared diagnostic contract: the mirror model
    ///     of each case in MaxMind.Db.Test/Contract/DiagnosticContract.cs is
    ///     decoded through the reflection fallback, and the outcome (exception
    ///     type, message, or successful value) must match the case. The
    ///     compile-time side lives in
    ///     MaxMind.Db.SourceGenerator.Test.DiagnosticAlignmentTest.
    /// </summary>
    public class DiagnosticAlignmentTest
    {
        private static readonly Assembly ReflectionModelsAssembly =
            typeof(ReflectionConstructorModel).Assembly;

        public static IEnumerable<object[]> Cases =>
            DiagnosticContract.Cases
                .Where(contractCase => contractCase.RuntimeTypeName != null)
                .Select(contractCase => new object[] { contractCase.Name });

        [Theory]
        [MemberData(nameof(Cases))]
        public void ReflectionFallbackMatchesContract(string caseName)
        {
            var contractCase = DiagnosticContract.Cases.Single(
                candidate => candidate.Name == caseName);
            var modelType = ResolveModelType(contractCase.RuntimeTypeName!);
            Assert.False(
                SourceGeneratorSupport.TryGetTypeRegistration(modelType, out _),
                $"{modelType.Name} must decode through the reflection fallback");

            var outcome = Record.Exception(() =>
            {
                Decode(modelType, contractCase.RuntimeGraph);
            });

            if (contractCase.RuntimeExceptionType != null)
            {
                Assert.NotNull(outcome);
                var exception = outcome;
                Assert.Equal(contractCase.RuntimeExceptionType, exception.GetType());
                Assert.Contains(
                    contractCase.RuntimeMessageFragment!, exception.Message);
                if (contractCase.RuntimeMessageNamesModel)
                {
                    // The runtime message names the model, aligning the failure
                    // with the model path the generator diagnostic points at.
                    Assert.Contains(modelType.Name, exception.Message);
                }
                return;
            }

            Assert.Null(outcome);
            AssertRuntimeValue(caseName, Decode(modelType, contractCase.RuntimeGraph));
        }

        private static Type ResolveModelType(string runtimeTypeName)
        {
            var type = ReflectionModelsAssembly.GetType(runtimeTypeName);
            Assert.True(type != null, $"Missing runtime mirror type {runtimeTypeName}");
            return type.IsGenericTypeDefinition
                ? type.MakeGenericType(typeof(string))
                : type;
        }

        private static object Decode(Type modelType, byte[] graph)
        {
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "runtime-value");
            using var buffer = new MemoryMapBuffer(new MemoryStream(graph, writable: false));
            var decoder = new Decoder(buffer, 0);
            var decode = typeof(Decoder)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(method => method.Name == "Decode" && method.IsGenericMethodDefinition);
            try
            {
                return decode.MakeGenericMethod(modelType)
                    .Invoke(decoder, [0L, null, injectables, null])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static void AssertRuntimeValue(string caseName, object model)
        {
            switch (caseName)
            {
                case "RequiredMemberWithoutSetsRequiredMembers":
                    Assert.Equal("from-database", ((ReflectionRequiredMemberModel)model).Value);
                    break;
                case "HiddenAnnotatedProperty":
                    // The reflection fallback binds the annotated base property.
                    Assert.Equal("from-database", ((ReflectionHiddenPropertyBase)model).Value);
                    break;
                case "ConflictingMemberAttributes":
                    // The injectable overwrites the value read from the database.
                    Assert.Equal("runtime-value", ((ReflectionConflictingMemberModel)model).Value);
                    break;
                case "DerivedMapKeyAttribute":
                    Assert.Equal("from-database", ((ReflectionDerivedMapKeyModel)model).Value);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"No runtime value assertion registered for contract case {caseName}");
            }
        }
    }
}
