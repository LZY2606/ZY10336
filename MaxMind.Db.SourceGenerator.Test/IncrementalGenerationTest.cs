#region

using System.Linq;
using Xunit;

#endregion

namespace MaxMind.Db.SourceGenerator.Test
{
    /// <summary>
    ///     Proves the generator's output is a pure function of the current
    ///     compilation: declaration order and syntax-tree order never change the
    ///     generated source or the constructor selection, and successive
    ///     compilations through one driver never observe stale registrations.
    /// </summary>
    public class IncrementalGenerationTest
    {
        private const string AlphaModel = """
            using MaxMind.Db;

            namespace Alpha;

            internal sealed class Holder
            {
                [Constructor]
                internal Holder([MapKey("value")] string value)
                {
                }
            }
            """;

        private const string BetaModel = """
            using MaxMind.Db;

            namespace Beta;

            internal sealed class Holder
            {
                [Constructor]
                internal Holder([MapKey("value")] string value)
                {
                }
            }
            """;

        [Fact]
        public void SameNameModelsInDifferentNamespacesDoNotGoStale()
        {
            var driver = GeneratorHarness.CreateDriver(aotDiagnostics: true);

            var alphaOnly = GeneratorHarness.Run(
                driver,
                GeneratorHarness.CreateCompilation(AlphaModel));
            Assert.Contains("global::Alpha.Holder", alphaOnly.Source);
            Assert.DoesNotContain("global::Beta.Holder", alphaOnly.Source);
            Assert.Empty(alphaOnly.Errors);

            var both = GeneratorHarness.Run(
                alphaOnly.Driver,
                GeneratorHarness.CreateCompilation(AlphaModel, BetaModel));
            Assert.Contains("global::Alpha.Holder", both.Source);
            Assert.Contains("global::Beta.Holder", both.Source);
            Assert.Empty(both.Errors);

            var betaOnly = GeneratorHarness.Run(
                both.Driver,
                GeneratorHarness.CreateCompilation(BetaModel));
            Assert.DoesNotContain("global::Alpha.Holder", betaOnly.Source);
            Assert.Contains("global::Beta.Holder", betaOnly.Source);
            Assert.Empty(betaOnly.Errors);
        }

        [Fact]
        public void PartialModelTurningGenericInvalidatesItsRegistration()
        {
            const string partOne = """
                using MaxMind.Db;

                namespace Models;

                internal sealed partial class PartialModel
                {
                    [MapKey("one")]
                    internal string? One { get; set; }
                }
                """;
            const string partTwo = """
                using MaxMind.Db;

                namespace Models;

                internal sealed partial class PartialModel
                {
                    [MapKey("two")]
                    internal string? Two { get; set; }
                }
                """;
            const string genericPartOne = """
                using MaxMind.Db;

                namespace Models;

                internal sealed partial class PartialModel<T>
                {
                    [MapKey("one")]
                    internal string? One { get; set; }
                }
                """;
            const string genericPartTwo = """
                using MaxMind.Db;

                namespace Models;

                internal sealed partial class PartialModel<T>
                {
                    [MapKey("two")]
                    internal string? Two { get; set; }
                }
                """;

            var driver = GeneratorHarness.CreateDriver(aotDiagnostics: true);

            var closed = GeneratorHarness.Run(
                driver,
                GeneratorHarness.CreateCompilation(partOne, partTwo));
            Assert.Contains("RegisterType<global::Models.PartialModel>(", closed.Source);
            Assert.DoesNotContain(
                closed.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG004");
            Assert.Empty(closed.Errors);

            var open = GeneratorHarness.Run(
                closed.Driver,
                GeneratorHarness.CreateCompilation(genericPartOne, genericPartTwo));
            Assert.DoesNotContain("PartialModel", open.Source);
            Assert.Contains(
                open.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG004");
            Assert.Empty(open.Errors);

            var closedAgain = GeneratorHarness.Run(
                open.Driver,
                GeneratorHarness.CreateCompilation(partOne, partTwo));
            Assert.Equal(closed.Source, closedAgain.Source);
            Assert.DoesNotContain(
                closedAgain.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG004");
            Assert.Empty(closedAgain.Errors);
        }

        [Fact]
        public void DeletedAnnotatedPropertyDisappearsFromGeneratedSource()
        {
            const string threeProperties = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class PropertyModel
                {
                    [MapKey("one")]
                    internal string? One { get; set; }

                    [MapKey("two")]
                    internal string? Two { get; set; }

                    [MapKey("three")]
                    internal string? Three { get; set; }
                }
                """;
            const string twoProperties = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class PropertyModel
                {
                    [MapKey("one")]
                    internal string? One { get; set; }

                    [MapKey("three")]
                    internal string? Three { get; set; }
                }
                """;

            var driver = GeneratorHarness.CreateDriver(aotDiagnostics: true);

            var before = GeneratorHarness.Run(
                driver,
                GeneratorHarness.CreateCompilation(threeProperties));
            Assert.Contains("\"one\"", before.Source);
            Assert.Contains("\"two\"", before.Source);
            Assert.Contains("\"three\"", before.Source);
            Assert.Empty(before.Errors);

            var after = GeneratorHarness.Run(
                before.Driver,
                GeneratorHarness.CreateCompilation(twoProperties));
            Assert.Contains("\"one\"", after.Source);
            Assert.DoesNotContain("\"two\"", after.Source);
            Assert.DoesNotContain("Two =", after.Source);
            Assert.Contains("\"three\"", after.Source);
            Assert.Empty(after.Errors);
        }

        [Fact]
        public void PropertyDeclarationOrderDoesNotChangeGeneratedSource()
        {
            const string ordered = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class PropertyModel
                {
                    [MapKey("one")]
                    internal string? One { get; set; }

                    [MapKey("two")]
                    internal string? Two { get; set; }

                    [MapKey("three")]
                    internal string? Three { get; set; }
                }
                """;
            const string reversed = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class PropertyModel
                {
                    [MapKey("three")]
                    internal string? Three { get; set; }

                    [MapKey("two")]
                    internal string? Two { get; set; }

                    [MapKey("one")]
                    internal string? One { get; set; }
                }
                """;

            var orderedRun = GeneratorHarness.Run(
                GeneratorHarness.CreateDriver(aotDiagnostics: true),
                GeneratorHarness.CreateCompilation(ordered));
            var reversedRun = GeneratorHarness.Run(
                GeneratorHarness.CreateDriver(aotDiagnostics: true),
                GeneratorHarness.CreateCompilation(reversed));

            Assert.Equal(orderedRun.Source, reversedRun.Source);
            Assert.Empty(orderedRun.Errors);
            Assert.Empty(reversedRun.Errors);
        }

        [Fact]
        public void SyntaxTreeOrderDoesNotChangeGeneratedSource()
        {
            const string modelA = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class ModelA
                {
                    [Constructor]
                    internal ModelA([MapKey("a")] string value)
                    {
                    }
                }
                """;
            const string modelB = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class ModelB
                {
                    [Constructor]
                    internal ModelB([MapKey("b")] string value)
                    {
                    }
                }
                """;

            var forward = GeneratorHarness.Run(
                GeneratorHarness.CreateDriver(aotDiagnostics: true),
                GeneratorHarness.CreateCompilation(modelA, modelB));
            var backward = GeneratorHarness.Run(
                GeneratorHarness.CreateDriver(aotDiagnostics: true),
                GeneratorHarness.CreateCompilation(modelB, modelA));

            Assert.Equal(forward.Source, backward.Source);
            Assert.Empty(forward.Errors);
            Assert.Empty(backward.Errors);
        }

        [Fact]
        public void ConstructorSelectionIgnoresDeclarationOrder()
        {
            const string markedFirst = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class MultiCtor
                {
                    [Constructor]
                    internal MultiCtor([MapKey("name")] string name)
                    {
                    }

                    internal MultiCtor(long value)
                    {
                    }
                }
                """;
            const string markedSecond = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class MultiCtor
                {
                    internal MultiCtor(long value)
                    {
                    }

                    [Constructor]
                    internal MultiCtor([MapKey("name")] string name)
                    {
                    }
                }
                """;

            var firstRun = GeneratorHarness.Run(
                GeneratorHarness.CreateDriver(aotDiagnostics: true),
                GeneratorHarness.CreateCompilation(markedFirst));
            var secondRun = GeneratorHarness.Run(
                GeneratorHarness.CreateDriver(aotDiagnostics: true),
                GeneratorHarness.CreateCompilation(markedSecond));

            Assert.Equal(firstRun.Source, secondRun.Source);
            Assert.Contains(
                "new global::Models.MultiCtor(",
                firstRun.Source);
            Assert.Contains(
                "(global::System.String)values[0]!",
                firstRun.Source);
            Assert.Empty(firstRun.Errors);
            Assert.Empty(secondRun.Errors);
        }

        [Fact]
        public void RepeatedRunsOverOneCompilationAreIdentical()
        {
            var driver = GeneratorHarness.CreateDriver(aotDiagnostics: true);
            var compilation = GeneratorHarness.CreateCompilation(AlphaModel, BetaModel);

            var first = GeneratorHarness.Run(driver, compilation);
            var second = GeneratorHarness.Run(first.Driver, compilation);

            Assert.Equal(first.Source, second.Source);
            Assert.NotEmpty(first.Source);
        }
    }
}
