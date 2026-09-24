#region

using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

#endregion

namespace MaxMind.Db.SourceGenerator.Test
{
    /// <summary>
    ///     Pins the compile-time half of the activator contract: when a model
    ///     cannot be source-generated, the AOT diagnostic must name the exact
    ///     model and point at its declaration, so it can be matched against the
    ///     runtime <see cref="DeserializationException" /> the reflection
    ///     activator raises for the same shape.
    /// </summary>
    public class AotDiagnosticContractTest
    {
        [Theory]
        [InlineData(StructModel, "MMDBSG012", "Models.StructModel", "StructModel")]
        [InlineData(OpenGenericModel, "MMDBSG004", "Models.GenericModel", "GenericModel")]
        [InlineData(AmbiguousModel, "MMDBSG007", "Models.AmbiguousModel", "AmbiguousModel")]
        [InlineData(DuplicateKeyModel, "MMDBSG005", "Models.DuplicateModel", "DuplicateModel")]
        [InlineData(InaccessibleConstructorModel, "MMDBSG002", "Models.InaccessibleModel", "InaccessibleModel")]
        [InlineData(RequiredMemberModel, "MMDBSG010", "Models.RequiredModel", "Value")]
        [InlineData(UnsupportedArrayModel, "MMDBSG008", "long[]", "ArrayModel")]
        public void AotDiagnosticsNameTheExactModelAndLocation(
            string modelSource,
            string expectedId,
            string expectedMessageFragment,
            string expectedLocationText
            )
        {
            var run = GeneratorHarness.Run(
                GeneratorHarness.CreateDriver(aotDiagnostics: true),
                GeneratorHarness.CreateCompilation(modelSource));

            var diagnostic = Assert.Single(
                run.Diagnostics,
                candidate => candidate.Id == expectedId);
            Assert.Contains(expectedMessageFragment, diagnostic.GetMessage());
            Assert.True(diagnostic.Location.IsInSource);
            var locationText = diagnostic.Location.SourceTree!
                .GetText(TestContext.Current.CancellationToken)
                .GetSubText(diagnostic.Location.SourceSpan)
                .ToString();
            Assert.Contains(expectedLocationText, locationText);
            Assert.Empty(run.Errors);
        }

        [Theory]
        [InlineData(StructModel, "MMDBSG012")]
        [InlineData(OpenGenericModel, "MMDBSG004")]
        [InlineData(AmbiguousModel, "MMDBSG007")]
        [InlineData(DuplicateKeyModel, "MMDBSG005")]
        [InlineData(InaccessibleConstructorModel, "MMDBSG002")]
        [InlineData(RequiredMemberModel, "MMDBSG010")]
        [InlineData(UnsupportedArrayModel, "MMDBSG008")]
        public void UnsupportedShapesStaySilentWithoutAotDiagnostics(
            string modelSource,
            string diagnosticId
            )
        {
            var run = GeneratorHarness.Run(
                GeneratorHarness.CreateDriver(aotDiagnostics: false),
                GeneratorHarness.CreateCompilation(modelSource));

            Assert.DoesNotContain(
                run.Diagnostics,
                candidate => candidate.Id == diagnosticId);
            Assert.Empty(run.Errors);
        }

        private const string StructModel = """
            using MaxMind.Db;

            namespace Models;

            internal struct StructModel
            {
                [Constructor]
                internal StructModel([MapKey("utf8_string")] string value)
                {
                }
            }
            """;

        private const string OpenGenericModel = """
            using MaxMind.Db;

            namespace Models;

            internal sealed class GenericModel<T>
            {
                [Constructor]
                internal GenericModel(T value)
                {
                }
            }
            """;

        private const string AmbiguousModel = """
            using MaxMind.Db;

            namespace Models;

            internal sealed class AmbiguousModel
            {
                [Constructor]
                internal AmbiguousModel(string value)
                {
                }

                [Constructor]
                internal AmbiguousModel(long value)
                {
                }
            }
            """;

        private const string DuplicateKeyModel = """
            using MaxMind.Db;

            namespace Models;

            internal sealed class DuplicateModel
            {
                [Constructor]
                internal DuplicateModel(
                    [MapKey("value")] string first,
                    [MapKey("value")] string second)
                {
                }
            }
            """;

        private const string InaccessibleConstructorModel = """
            using MaxMind.Db;

            namespace Models;

            internal sealed class InaccessibleModel
            {
                [Constructor]
                private InaccessibleModel(string value)
                {
                }
            }
            """;

        private const string RequiredMemberModel = """
            using MaxMind.Db;

            namespace Models;

            internal sealed class RequiredModel
            {
                [MapKey("value")]
                public required string Value { get; init; }
            }
            """;

        private const string UnsupportedArrayModel = """
            using MaxMind.Db;

            namespace Models;

            internal sealed class ArrayModel
            {
                [Constructor]
                internal ArrayModel(long[] values)
                {
                }
            }
            """;
    }
}
