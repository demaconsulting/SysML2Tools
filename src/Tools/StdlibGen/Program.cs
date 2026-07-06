// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

// StdlibGen: Build-time tool to pre-compile SysML v2 stdlib files into stdlib.bin
// Usage: StdlibGen --stdlib-dir <path> --output <path>

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

// Parse arguments — extracted to keep the top-level program's cognitive complexity within limits
var (stdlibDir, outputPath) = ParseArgs(args);

if (stdlibDir is null || outputPath is null)
{
    await Console.Error.WriteLineAsync("Usage: StdlibGen --stdlib-dir <path> --output <path>").ConfigureAwait(false);
    return 1;
}

if (!Directory.Exists(stdlibDir))
{
    await Console.Error.WriteLineAsync($"ERROR: Stdlib directory not found: {stdlibDir}").ConfigureAwait(false);
    return 1;
}

await Console.Out.WriteLineAsync($"StdlibGen: Scanning {stdlibDir}").ConfigureAwait(false);

var allDiagnostics = new List<SysmlDiagnostic>();
var symbolTable = new SymbolTable();
var astRoots = new List<(string Path, SysmlNode? Root)>();

// Enumerate all stdlib source files
var sysmlFiles = Directory.EnumerateFiles(stdlibDir, "*.sysml", SearchOption.AllDirectories);
var kermlFiles = Directory.EnumerateFiles(stdlibDir, "*.kerml", SearchOption.AllDirectories);
var allFiles = sysmlFiles.Concat(kermlFiles).OrderBy(f => f, StringComparer.Ordinal).ToList();

await Console.Out.WriteLineAsync($"StdlibGen: Found {allFiles.Count} files").ConfigureAwait(false);

foreach (var filePath in allFiles)
{
    var content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
    var fileDiagnostics = new List<SysmlDiagnostic>();
    var cst = WorkspaceParser.ParseSourceToCst(filePath, content, fileDiagnostics);
    var root = new AstBuilder().Build(cst);

    // KerML files may produce parse errors with the SysML v2 grammar — downgrade to Warning
    if (filePath.EndsWith(".kerml", StringComparison.OrdinalIgnoreCase))
    {
        fileDiagnostics = fileDiagnostics
            .Select(d => d.Severity == DiagnosticSeverity.Error
                ? d with { Severity = DiagnosticSeverity.Warning }
                : d)
            .ToList();
    }

    allDiagnostics.AddRange(fileDiagnostics);
    symbolTable.RegisterAll(root);
    astRoots.Add((filePath, root));
}

// Run reference resolution
var resolver = new ReferenceResolver(symbolTable, allDiagnostics);
resolver.ResolveAll(astRoots);

// Run supertype walking
var supertypeWalker = new SupertypeWalker(symbolTable, allDiagnostics);
supertypeWalker.WalkAll();

// Serialize and write output
var bytes = AstSerializer.Serialize(symbolTable, allDiagnostics);

// Ensure output directory exists
var outputDir = Path.GetDirectoryName(outputPath);
if (outputDir is { Length: > 0 })
{
    Directory.CreateDirectory(outputDir);
}

// Write via a unique temp file, then atomically rename into place. This is required
// for safety under concurrent invocations: MSBuild's multi-targeted/solution builds can
// invoke StdlibGen more than once concurrently for the same --output path (e.g. once per
// top-level project that references DemaConsulting.SysML2Tools.Stdlib). Writing directly
// via File.WriteAllBytesAsync(outputPath, ...) opens the destination with FileMode.Create
// (truncate-in-place) and FileShare.Read, which does not permit a second concurrent
// writer - a second invocation racing on the same path can throw a sharing-violation
// IOException. Each invocation instead writes its own uniquely-named temp file (no shared
// handle, no contention), then File.Move(..., overwrite: true) atomically renames it onto
// the destination (a single filesystem rename, not a truncate-and-copy) - safe regardless
// of how many invocations race, with the last rename to complete winning deterministically.
var tempPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);
File.Move(tempPath, outputPath, overwrite: true);

var errorCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
var warnCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
await Console.Out.WriteLineAsync($"StdlibGen: Wrote {bytes.Length:N0} bytes to {outputPath}").ConfigureAwait(false);
await Console.Out.WriteLineAsync($"StdlibGen: {symbolTable.Symbols.Count} symbols, {errorCount} errors, {warnCount} warnings").ConfigureAwait(false);

return errorCount > 0 ? 1 : 0;

/// <summary>
/// Parses the command-line arguments and returns the stdlib directory and output path.
/// Extracted to keep the top-level program within the cognitive-complexity limit.
/// </summary>
/// <param name="args">Raw command-line arguments.</param>
/// <returns>
/// A tuple of (<c>StdlibDir</c>, <c>OutputPath</c>), either of which may be <see langword="null"/>
/// when the corresponding flag is absent.
/// </returns>
static (string? StdlibDir, string? OutputPath) ParseArgs(string[] args)
{
    string? stdlibDir = null;
    string? outputPath = null;
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--stdlib-dir")
        {
            stdlibDir = args[i + 1];
        }
        else if (args[i] == "--output")
        {
            outputPath = args[i + 1];
        }
    }

    return (stdlibDir, outputPath);
}
