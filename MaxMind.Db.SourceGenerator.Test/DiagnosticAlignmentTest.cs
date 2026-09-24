using System.Collections.Generic;
using System.Linq;
using MaxMind.Db.Test.Contract;
using Microsoft.CodeAnalysis;
using Xunit;

namespace MaxMind.Db.SourceGenerator.Test
{
    /// <summary>
    ///     The compile-time side of the shared diagnostic contract: every case in
    ///     MaxMind.Db.Test/Contract/DiagnosticContract.cs must produce its MMDBSG
    ///     diagnostic on the offending model or member. The runtime side of the
    ///     same cases lives in MaxMind.Db.Test.DiagnosticAlignmentTest; the two
    ///     sides align on error code and model path, not on timing.
    /// </summary>
    public class DiagnosticAlignmentTest
    {
        public static IEnumerable<object[]> Cases =>
            DiagnosticContract.Cases.Select(contractCase => new object[] { contractCase.Name });

        [Theory]
        [MemberData(nameof(Cases))]
        public void GeneratorReportsContractDiagnosticOnTheModel(string caseName)
        {
            var contractCase = DiagnosticContract.Cases.Single(
                candidate => candidate.Name == caseName);
            var compilation = MaxMindDbSourceGeneratorTest.CreateCompilation(
                [contractCase.ModelSource]);
            var driver = MaxMindDbSourceGeneratorTest.CreateDriver(aotDiagnostics: true);

            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out _,
                TestContext.Current.CancellationToken);
            var runResult = driver.GetRunResult();

            var diagnostic = Assert.Single(
                runResult.Diagnostics,
                candidate => candidate.Id == contractCase.DiagnosticId);
            Assert.True(
                diagnostic.Location.IsInSource,
                $"{contractCase.DiagnosticId} for {caseName} should have a source location");
            var locatedText = diagnostic.Location.SourceTree!
                .GetText(TestContext.Current.CancellationToken)
                .GetSubText(diagnostic.Location.SourceSpan)
                .ToString();
            Assert.Contains(contractCase.ExpectedLocationText, locatedText);
            Assert.Empty(outputCompilation
                .GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(candidate => candidate.Severity == DiagnosticSeverity.Error));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void GeneratorStaysSilentWithoutAotDiagnostics(string caseName)
        {
            var contractCase = DiagnosticContract.Cases.Single(
                candidate => candidate.Name == caseName);
            var compilation = MaxMindDbSourceGeneratorTest.CreateCompilation(
                [contractCase.ModelSource]);
            var driver = MaxMindDbSourceGeneratorTest.CreateDriver(aotDiagnostics: false);

            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out _,
                out _,
                TestContext.Current.CancellationToken);
            var runResult = driver.GetRunResult();

            Assert.DoesNotContain(
                runResult.Diagnostics,
                candidate => candidate.Id == contractCase.DiagnosticId);
        }
    }
}
