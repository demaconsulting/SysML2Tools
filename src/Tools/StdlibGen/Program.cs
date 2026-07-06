// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

// StdlibGen: pre-compiles SysML v2 stdlib source files into a gzip-compressed UTF-8 JSON
// blob (stdlib.json.gz), embedded as a resource in DemaConsulting.SysML2Tools.Stdlib.
// Invoked as a plain sequential step by build.ps1 before the solution build - not as a
// ProjectReference or nested MSBuild/Exec call from Stdlib.csproj - so there is no
// MSBuild-in-MSBuild coordination with projects it shares dependencies with. It only
// overwrites its output when content actually changed, so running it on every build is
// cheap and keeps stdlib.json.gz from ever silently going stale.
// Usage: StdlibGen --stdlib-dir <path> --output <path-to-.gz>

using System.IO.Compression;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

// Parse arguments — extracted to keep the top-level program's cognitive complexity within limits
var (stdlibDir, outputPath) = ParseArgs(args);

if (stdlibDir is null || outputPath is null)
{
    await Console.Error.WriteLineAsync("Usage: StdlibGen --stdlib-dir <path> --output <path-to-.gz>").ConfigureAwait(false);
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
    // Diagnostics carry this path into the committed stdlib.json.gz artifact, so it must be
    // reproducible across machines/checkouts/CI runners regardless of --stdlib-dir being
    // absolute or relative, or of OS path separators - otherwise the artifact would differ
    // on every machine/invocation even when the stdlib source content is unchanged,
    // defeating StdlibGen's skip-if-unchanged check and leaking build-machine paths into a
    // shipped NuGet package.
    var relativePath = Path.GetRelativePath(stdlibDir, filePath).Replace(Path.DirectorySeparatorChar, '/');

    var content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
    var fileDiagnostics = new List<SysmlDiagnostic>();
    var cst = WorkspaceParser.ParseSourceToCst(relativePath, content, fileDiagnostics);
    var root = new AstBuilder().Build(cst);

    // KerML files may produce parse errors with the SysML v2 grammar — downgrade to Warning
    if (relativePath.EndsWith(".kerml", StringComparison.OrdinalIgnoreCase))
    {
        fileDiagnostics = fileDiagnostics
            .Select(d => d.Severity == DiagnosticSeverity.Error
                ? d with { Severity = DiagnosticSeverity.Warning }
                : d)
            .ToList();
    }

    allDiagnostics.AddRange(fileDiagnostics);
    symbolTable.RegisterAll(root);
    astRoots.Add((relativePath, root));
}

// Run reference resolution
var resolver = new ReferenceResolver(symbolTable, allDiagnostics);
resolver.ResolveAll(astRoots);

// Run supertype walking
var supertypeWalker = new SupertypeWalker(symbolTable, allDiagnostics);
supertypeWalker.WalkAll();

// Serialize the symbol table. The uncompressed form is ~7.7 MB (mostly repetitive
// symbol/type name strings), which gzip-compresses to well under 500 KB — small enough
// to commit directly to source control as the embedded resource that StdlibProvider
// decompresses at runtime.
var bytes = AstSerializer.Serialize(symbolTable, allDiagnostics);

// Ensure output directory exists
var outputDir = Path.GetDirectoryName(outputPath);
if (outputDir is { Length: > 0 })
{
    Directory.CreateDirectory(outputDir);
}

// Write to a uniquely-named temp file, then binary-compare it against the existing output
// (if any). This is run from build.ps1 on every build (so nobody can forget to regenerate it
// after editing stdlib source files), so avoiding a no-op write matters: it keeps the
// checked-in file's mtime/git status untouched on every build where the stdlib sources are
// unchanged. Comparing compressed bytes directly (rather than decompressing first) is safe
// because .NET's GZipStream writes a fixed zero MTIME header field, so compressing identical
// input always produces byte-identical output.
var tempPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
try
{
    await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
    await using (var gzipStream = new GZipStream(fileStream, CompressionLevel.SmallestSize))
    {
        await gzipStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    if (File.Exists(outputPath) &&
        BytesEqual(
            await File.ReadAllBytesAsync(tempPath).ConfigureAwait(false),
            await File.ReadAllBytesAsync(outputPath).ConfigureAwait(false)))
    {
        await Console.Out.WriteLineAsync($"StdlibGen: {outputPath} is already up to date; skipping write").ConfigureAwait(false);
    }
    else
    {
        File.Move(tempPath, outputPath, overwrite: true);
    }
}
finally
{
    File.Delete(tempPath);
}

var errorCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
var warnCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
var compressedLength = new FileInfo(outputPath).Length;
await Console.Out.WriteLineAsync(
    $"StdlibGen: Wrote {compressedLength:N0} compressed bytes ({bytes.Length:N0} uncompressed) to {outputPath}")
    .ConfigureAwait(false);
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

/// <summary>
/// Compares two byte arrays for equality.
/// </summary>
/// <param name="left">First byte array.</param>
/// <param name="right">Second byte array.</param>
/// <returns><see langword="true"/> if the arrays have identical content.</returns>
static bool BytesEqual(byte[] left, byte[] right) => left.AsSpan().SequenceEqual(right);
