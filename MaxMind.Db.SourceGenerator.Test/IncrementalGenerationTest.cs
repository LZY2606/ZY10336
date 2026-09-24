using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace MaxMind.Db.SourceGenerator.Test
{
    /// <summary>
    ///     Pins the generator's ordering and incrementality guarantees: property
    ///     declaration order and syntax tree order must not change the generated
    ///     source or the constructor choice, and re-running the same driver over
    ///     edited compilations must never serve stale output.
    /// </summary>
    public class IncrementalGenerationTest
    {
        private const string OrderedProperties = """
            using MaxMind.Db;

            namespace Models;

            internal sealed class PropertyModel
            {
                [MapKey("alpha")]
                internal string? Alpha { get; set; }

                [MapKey("beta")]
                internal long Beta { get; set; }

                [MapKey("gamma")]
                internal bool Gamma { get; set; }
            }
            """;

        private const string ReorderedProperties = """
            using MaxMind.Db;

            namespace Models;

            internal sealed class PropertyModel
            {
                [MapKey("gamma")]
                internal bool Gamma { get; set; }

                [MapKey("alpha")]
                internal string? Alpha { get; set; }

                [MapKey("beta")]
                internal long Beta { get; set; }
            }
            """;

        [Fact]
        public void PropertyDeclarationOrderDoesNotChangeGeneratedSource()
        {
            var ordered = RunToCompletion(OrderedProperties);
            var reordered = RunToCompletion(ReorderedProperties);

            Assert.NotEmpty(ordered);
            Assert.Equal(ordered, reordered);
        }

        [Fact]
        public void ConstructorChoiceDoesNotDependOnDeclarationOrder()
        {
            const string constructorFirst = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class MixedModel
                {
                    [Constructor]
                    internal MixedModel([MapKey("alpha")] string alpha)
                    {
                    }

                    [MapKey("beta")]
                    internal string? Beta { get; set; }
                }
                """;
            const string propertiesFirst = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class MixedModel
                {
                    [MapKey("beta")]
                    internal string? Beta { get; set; }

                    [Constructor]
                    internal MixedModel([MapKey("alpha")] string alpha)
                    {
                    }
                }
                """;

            var constructorFirstSource = RunToCompletion(constructorFirst);
            var propertiesFirstSource = RunToCompletion(propertiesFirst);

            // Both shapes choose constructor activation; the annotated property is
            // not part of the model either way.
            Assert.Contains("new global::Models.MixedModel(", constructorFirstSource);
            Assert.Equal(constructorFirstSource, propertiesFirstSource);
        }

        [Fact]
        public void SyntaxTreeOrderDoesNotChangeGeneratedSource()
        {
            const string firstModel = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class FirstModel
                {
                    [Constructor]
                    internal FirstModel(string alpha)
                    {
                    }
                }
                """;
            const string secondModel = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class SecondModel
                {
                    [Constructor]
                    internal SecondModel(string beta)
                    {
                    }
                }
                """;

            const string singleTreeSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class FirstModel
                {
                    [Constructor]
                    internal FirstModel(string alpha)
                    {
                    }
                }

                internal sealed class SecondModel
                {
                    [Constructor]
                    internal SecondModel(string beta)
                    {
                    }
                }
                """;

            var combined = RunToCompletion([firstModel, secondModel]);
            var swapped = RunToCompletion([secondModel, firstModel]);
            var singleTree = RunToCompletion(singleTreeSource);

            Assert.NotEmpty(combined);
            Assert.Equal(combined, swapped);
            Assert.Equal(combined, singleTree);
        }

        [Fact]
        public void SameNameInDifferentNamespaceProducesFreshOutput()
        {
            const string alphaSource = """
                using MaxMind.Db;

                namespace Alpha;

                internal sealed class SharedModel
                {
                    [Constructor]
                    internal SharedModel(string value)
                    {
                    }
                }
                """;
            const string betaSource = """
                using MaxMind.Db;

                namespace Beta;

                internal sealed class SharedModel
                {
                    [Constructor]
                    internal SharedModel(string value)
                    {
                    }
                }
                """;

            var driver = MaxMindDbSourceGeneratorTest.CreateDriver(aotDiagnostics: true);
            var alphaCompilation = MaxMindDbSourceGeneratorTest.CreateCompilation([alphaSource]);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                alphaCompilation, out _, out _, TestContext.Current.CancellationToken);
            var alphaOutput = SingleGeneratedSource(driver);
            Assert.Contains("global::Alpha.SharedModel", alphaOutput);

            var betaCompilation = alphaCompilation.ReplaceSyntaxTree(
                alphaCompilation.SyntaxTrees.Single(),
                MaxMindDbSourceGeneratorTest.CreateCompilation([betaSource]).SyntaxTrees.Single());
            driver = driver.RunGeneratorsAndUpdateCompilation(
                betaCompilation, out _, out _, TestContext.Current.CancellationToken);
            var betaOutput = SingleGeneratedSource(driver);

            Assert.Contains("global::Beta.SharedModel", betaOutput);
            Assert.DoesNotContain("global::Alpha.SharedModel", betaOutput);
        }

        [Fact]
        public void PartialGenericModelChangesPropagateAcrossCompilations()
        {
            const string genericPartOne = """
                using MaxMind.Db;

                namespace Models;

                internal sealed partial class PartialModel<T>
                {
                    [MapKey("alpha")]
                    internal string? Alpha { get; set; }
                }
                """;
            const string genericPartTwo = """
                using MaxMind.Db;

                namespace Models;

                internal sealed partial class PartialModel<T>
                {
                    [MapKey("beta")]
                    internal string? Beta { get; set; }
                }
                """;
            var concretePartOne = genericPartOne.Replace("<T>", string.Empty);
            var concretePartTwo = genericPartTwo.Replace("<T>", string.Empty);

            var driver = MaxMindDbSourceGeneratorTest.CreateDriver(aotDiagnostics: true);
            var genericCompilation = MaxMindDbSourceGeneratorTest.CreateCompilation(
                [genericPartOne, genericPartTwo]);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                genericCompilation, out _, out _, TestContext.Current.CancellationToken);
            var genericResult = driver.GetRunResult();

            Assert.Contains(
                genericResult.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG004");
            Assert.Empty(genericResult.GeneratedTrees);

            // Replacing the generic partial parts with non-generic ones must
            // produce a registration with the members from both parts.
            var concreteCompilation = MaxMindDbSourceGeneratorTest.CreateCompilation(
                [concretePartOne, concretePartTwo]);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                concreteCompilation, out _, out _, TestContext.Current.CancellationToken);
            var concreteResult = driver.GetRunResult();
            var concreteOutput = SingleGeneratedSource(driver);

            Assert.DoesNotContain(
                concreteResult.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG004");
            Assert.Contains("new global::Models.PartialModel", concreteOutput);
            Assert.Contains("Alpha = ", concreteOutput);
            Assert.Contains("Beta = ", concreteOutput);
        }

        [Fact]
        public void PropertyDeletionRegeneratesOutput()
        {
            const string withBeta = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class ShrinkingModel
                {
                    [MapKey("alpha")]
                    internal string? Alpha { get; set; }

                    [MapKey("beta")]
                    internal string? Beta { get; set; }
                }
                """;
            const string withoutBeta = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class ShrinkingModel
                {
                    [MapKey("alpha")]
                    internal string? Alpha { get; set; }
                }
                """;

            var driver = MaxMindDbSourceGeneratorTest.CreateDriver(aotDiagnostics: true);
            var fullCompilation = MaxMindDbSourceGeneratorTest.CreateCompilation([withBeta]);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                fullCompilation, out _, out _, TestContext.Current.CancellationToken);
            var fullOutput = SingleGeneratedSource(driver);
            Assert.Contains("Beta = ", fullOutput);

            var shrunkCompilation = fullCompilation.ReplaceSyntaxTree(
                fullCompilation.SyntaxTrees.Single(),
                MaxMindDbSourceGeneratorTest.CreateCompilation([withoutBeta]).SyntaxTrees.Single());
            driver = driver.RunGeneratorsAndUpdateCompilation(
                shrunkCompilation, out _, out _, TestContext.Current.CancellationToken);
            var shrunkOutput = SingleGeneratedSource(driver);

            Assert.Contains("Alpha = ", shrunkOutput);
            Assert.DoesNotContain("Beta", shrunkOutput);
        }

        private static string RunToCompletion(params string[] sources)
        {
            var driver = MaxMindDbSourceGeneratorTest.CreateDriver(aotDiagnostics: true);
            var compilation = MaxMindDbSourceGeneratorTest.CreateCompilation(sources);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation, out _, out _, TestContext.Current.CancellationToken);
            return SingleGeneratedSource(driver);
        }

        private static string SingleGeneratedSource(GeneratorDriver driver)
        {
            var runResult = driver.GetRunResult();
            var tree = Assert.Single(runResult.GeneratedTrees);
            return tree.GetText(TestContext.Current.CancellationToken).ToString();
        }
    }
}
