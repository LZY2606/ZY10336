#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

#endregion

namespace MaxMind.Db.SourceGenerator.Test
{
    /// <summary>
    ///     Shared driver harness for generator contract tests. Unlike the
    ///     single-shot helper in <see cref="MaxMindDbSourceGeneratorTest" />, the
    ///     driver is threaded through successive runs so tests can prove that
    ///     incremental compilations never observe stale output.
    /// </summary>
    internal static class GeneratorHarness
    {
        private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp12);

        private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

        internal static CSharpCompilation CreateCompilation(params string[] sources)
        {
            return CSharpCompilation.Create(
                "GeneratorContractTests",
                sources.Select(source => CSharpSyntaxTree.ParseText(source, ParseOptions)),
                References,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));
        }

        internal static GeneratorDriver CreateDriver(bool aotDiagnostics)
        {
            return CSharpGeneratorDriver.Create(
                [new MaxMindDbSourceGenerator().AsSourceGenerator()],
                parseOptions: ParseOptions,
                optionsProvider: new AotDiagnosticsOptionsProvider(aotDiagnostics));
        }

        internal static GeneratorRun Run(GeneratorDriver driver, Compilation compilation)
        {
            var updatedDriver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out _);
            var result = updatedDriver.GetRunResult();
            var source = result.GeneratedTrees.Length == 0
                ? string.Empty
                : string.Join(
                    "\n",
                    result.GeneratedTrees.Select(tree => tree.GetText().ToString()));
            var errors = outputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();
            return new GeneratorRun(updatedDriver, source, result.Diagnostics, errors);
        }

        private static ImmutableArray<MetadataReference> CreateReferences()
        {
            var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
                .Split(Path.PathSeparator) ?? [];
            var references = new Dictionary<string, MetadataReference>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths.Append(typeof(ConstructorAttribute).Assembly.Location))
            {
                references[path] = MetadataReference.CreateFromFile(path);
            }
            return references.Values.ToImmutableArray();
        }

        internal sealed class GeneratorRun
        {
            internal GeneratorRun(
                GeneratorDriver driver,
                string source,
                ImmutableArray<Diagnostic> diagnostics,
                ImmutableArray<Diagnostic> errors
                )
            {
                Driver = driver;
                Source = source;
                Diagnostics = diagnostics;
                Errors = errors;
            }

            internal ImmutableArray<Diagnostic> Diagnostics { get; }
            internal GeneratorDriver Driver { get; }
            internal ImmutableArray<Diagnostic> Errors { get; }
            internal string Source { get; }
        }

        private sealed class AotDiagnosticsOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly AnalyzerConfigOptions _globalOptions;

            internal AotDiagnosticsOptionsProvider(bool aotDiagnostics)
            {
                _globalOptions = new AotDiagnosticsOptions(aotDiagnostics);
            }

            public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
                EmptyOptions.Instance;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
                EmptyOptions.Instance;
        }

        private sealed class AotDiagnosticsOptions : AnalyzerConfigOptions
        {
            private readonly bool _aotDiagnostics;

            internal AotDiagnosticsOptions(bool aotDiagnostics)
            {
                _aotDiagnostics = aotDiagnostics;
            }

            public override bool TryGetValue(string key, out string value)
            {
                if (key == "build_property.MaxMindDbAotDiagnostics")
                {
                    value = _aotDiagnostics ? "true" : "false";
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }

        private sealed class EmptyOptions : AnalyzerConfigOptions
        {
            internal static EmptyOptions Instance { get; } = new();

            public override bool TryGetValue(string key, out string value)
            {
                value = string.Empty;
                return false;
            }
        }
    }
}
