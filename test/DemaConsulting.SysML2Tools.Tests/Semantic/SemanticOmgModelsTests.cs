// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Level 10 gate: all OMG example models and custom test models resolve with zero errors.
/// </summary>
public sealed class SemanticOmgModelsTests
{
    /// <summary>
    ///     OMG corpus models (path suffixes) whose strict-mode gaps were all dotted feature chains,
    ///     anonymous action nodes or satisfy declarations, and which therefore report no unresolved
    ///     reference any more.
    /// </summary>
    private static readonly string[] ChainResolvedModels =
    [
        "SimpleTests/ConnectionTest.sysml",
        "SimpleTests/AliasTest.sysml",
        "SimpleTests/AllocationTest.sysml",
        "SimpleTests/FeaturePathTest.sysml",
        "SimpleTests/ActionTest.sysml",
        "/TimeVaryingAttribute.sysml",
        "RequirementsExamples/RequirementDerivationExample.sysml",
        "8.4.1WheelHubAssembly/WheelPackage.sysml",
        "8.4.1WheelHubAssembly/WheelPackage-Updated.sysml",
        "17.Control/DecisionExample.sysml",
        "17.Control/ForkJoinExample.sysml",
        "19.TerminateActions/TerminateActionsExample-1.sysml",
        "27.Occurrences/InteractionExample-1.sysml",
        "27.Occurrences/MessagePayloadExample.sysml",
        "32.Requirements/RequirementSatisfaction.sysml",
        "7a1-VariantConfiguration-GeneralConcept-a.sysml",
        "8-Requirements.sysml",
        "13a-ModelContainment.sysml",
        "InteractionSequencingExamples/ServerSequenceRealization-2.sysml",
        "InteractionSequencingExamples/ServerSequenceOutsideRealization-2.sysml",
        "VehicleExample/SysMLV2SpecAnnexASimpleVehicleModel.sysml",
        "VehicleExample/VehicleIndividuals.sysml",
        "AnalysisExamples/TurbojetStageAnalysis.sysml",
        "SimpleTests/ConstraintTest.sysml",
        "03-Function-basedBehavior/3e-Function-basedBehavior-item.sysml",
        "14.ActionDefinitions/ActionDefinitionExample.sysml",
        "14.ActionDefinitions/ActionSuccessionExample-1.sysml",
        "14.ActionDefinitions/ActionSuccessionExample-2.sysml",
        "IndividualsExamples/JohnIndividualExample.sysml",
        "27.Occurrences/TimeSliceAndSnapshotExample.sysml",
        "27.Occurrences/InteractionRealization-2.sysml",
        "GeometryExamples/CarWithShapeAndCSG.sysml",
        "GeometryExamples/SimpleQuadcopter.sysml",
    ];

    /// <summary>
    ///     The unresolved references the OMG corpus still reports in strict mode, as (path suffix,
    ///     referenced name, reason). Each one is explained here; none is a name-resolution bug.
    ///     <list type="bullet">
    ///         <item>
    ///             <c>Camera.sysml</c> (training): the corpus is loaded as one workspace, and the
    ///             root-level <c>part def Camera</c> of <c>CameraExample/Camera.sysml</c> collides
    ///             with this file's root-level <c>package Camera</c> (the first one registered wins),
    ///             so <c>Camera::Scene</c> is looked up in the part definition. Each file is valid
    ///             on its own: a genuine corpus artifact, see
    ///             <see cref="SemanticOmgModels_CameraPackage_ResolvesInIsolation"/>.
    ///         </item>
    ///         <item>
    ///             <c>AHFNorwayTopics.sysml</c>, <c>AHFSequences.sysml</c>: <c>#service def</c> and
    ///             <c>#cloud X</c> are user-defined keywords whose members come from semantic
    ///             metadata (<c>SemanticMetadata::baseType</c>), which is not modeled.
    ///         </item>
    ///         <item>
    ///             <c>14c-LanguageExtensions.sysml</c>: the members of <c>#cause occurrence</c>
    ///             (<c>occurs</c>, <c>detected</c>, <c>severity</c>) also come from semantic
    ///             metadata.
    ///         </item>
    ///     </list>
    /// </summary>
    private static readonly (string FileSuffix, string Name)[] ExpectedStrictErrors =
    [
        ("ArrowheadFrameworkExample/AHFNorwayTopics.sysml", "APISService"),
        ("ArrowheadFrameworkExample/AHFSequences.sysml", "AHFNorway_LocalCloudDD.APISConsumer"),
        ("17.Control/Camera.sysml", "Scene"),
        ("17.Control/Camera.sysml", "Picture"),
        ("17.Control/Camera.sysml", "Image"),
        ("14-LanguageExtensions/14c-LanguageExtensions.sysml", "occurs"),
        ("14-LanguageExtensions/14c-LanguageExtensions.sysml", "detected"),
        ("14-LanguageExtensions/14c-LanguageExtensions.sysml", "severity"),
    ];

    /// <summary>
    ///     Finds the test/SysMLModels directory relative to the test assembly.
    /// </summary>
    private static string? FindSysMLModelsRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "test", "SysMLModels");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    /// <summary>
    ///     All OMG reference model files and custom test models should load with zero errors.
    /// </summary>
    [Fact]
    public async Task SemanticOmgModels_AllModels_ResolveWithZeroErrors()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var sysmlFiles = Directory.GetFiles(modelsRoot, "*.sysml", SearchOption.AllDirectories);
        if (sysmlFiles.Length == 0)
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync(sysmlFiles, stdlibTable);

        // Assert — no errors
        var errors = result.Diagnostics
            .Where(d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            var messages = string.Join("\n", errors.Select(e => $"  {e.FilePath}({e.Line},{e.Column}): {e.Message}"));
            Assert.Fail($"Expected zero errors but got {errors.Count}:\n{messages}");
        }
    }

    /// <summary>
    ///     The custom and test-case model fixtures load with zero diagnostics of any severity,
    ///     both by default and in strict unresolved-reference mode.
    /// </summary>
    /// <param name="strict">Whether strict unresolved-reference mode is enabled.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomAndTestCaseModels_AllModels_LoadWithZeroDiagnostics(bool strict)
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var sysmlFiles = new[] { "Custom", "TestCases" }
            .Select(folder => Path.Combine(modelsRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*.sysml", SearchOption.AllDirectories))
            .ToArray();
        if (sysmlFiles.Length == 0)
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync(sysmlFiles, stdlibTable, strict);

        // Assert
        if (result.Diagnostics.Count > 0)
        {
            var messages = string.Join(
                "\n",
                result.Diagnostics.Select(d => $"  {d.FilePath}({d.Line},{d.Column}): {d.Severity}: {d.Message}"));
            Assert.Fail($"Expected zero diagnostics but got {result.Diagnostics.Count}:\n{messages}");
        }
    }

    /// <summary>
    ///     The gallery models' resolved edges are exactly the recorded baseline: implicit
    ///     specializations and inherited lookup feed name resolution only and never add, drop or
    ///     redirect a relationship (the golden files were captured before they existed). Model 03
    ///     and 12 cover the legacy <c>Actions::Action::start</c>/<c>done</c> transition targets.
    /// </summary>
    /// <param name="model">The gallery model's file name without extension.</param>
    [Theory]
    [InlineData("01-drone-general")]
    [InlineData("03-elevator-state")]
    [InlineData("12-heater-state")]
    [InlineData("14-usages-typing-general")]
    public async Task SemanticGalleryModels_Edges_MatchBaselineGolden(string model)
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        Assert.NotNull(modelsRoot);
        var repoRoot = Directory.GetParent(Directory.GetParent(modelsRoot)!.FullName)!.FullName;
        var modelFile = Path.Combine(repoRoot, "docs", "gallery", "models", model + ".sysml");
        var goldenFile = Path.Combine(modelsRoot, "ExpectedEdges", model + ".edges.txt");
        Assert.True(File.Exists(modelFile), $"Missing gallery model {modelFile}");
        Assert.True(File.Exists(goldenFile), $"Missing golden edge file {goldenFile}");

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([modelFile], stdlibTable);
        var actual = result.Workspace!.Index.AllEdges
            .Select(e => $"{e.Kind}|{e.SourceQualifiedName}|{e.TargetQualifiedName}|{e.RawReference}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var expected = (await File.ReadAllLinesAsync(goldenFile, TestContext.Current.CancellationToken))
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     In strict mode the OMG corpus reports its known unresolved references (resolver gaps)
    ///     as Errors; the count must never grow beyond the recorded baseline. Lower the baseline
    ///     whenever resolver gaps are closed. The default run reports none of them as Errors.
    /// </summary>
    [Fact]
    public async Task SemanticOmgModels_StrictUnresolved_DoNotExceedBaseline()
    {
        // Arrange
        const int baseline = 8;
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var omgRoot = Path.Combine(modelsRoot, "OMG");
        if (!Directory.Exists(omgRoot))
        {
            return;
        }

        var sysmlFiles = Directory.GetFiles(omgRoot, "*.sysml", SearchOption.AllDirectories);
        if (sysmlFiles.Length == 0)
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var strictResult = await WorkspaceLoader.LoadAsync(sysmlFiles, stdlibTable, strictUnresolved: true);
        var defaultResult = await WorkspaceLoader.LoadAsync(sysmlFiles, stdlibTable);

        // Assert — in strict mode every unresolved reference is an Error, and there are no more
        // than the baseline
        var unresolved = strictResult.Diagnostics
            .Where(d => d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal))
            .ToList();
        Assert.All(
            unresolved,
            d => Assert.Equal(DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Error, d.Severity));
        Assert.True(
            unresolved.Count <= baseline,
            $"Expected at most {baseline} unresolved references in the OMG corpus but got {unresolved.Count}.");

        // Assert — the models whose only gaps were dotted feature chains (and anonymous action
        // nodes) now resolve completely
        var stillFailing = unresolved
            .Where(d => ChainResolvedModels.Any(
                model => d.FilePath.Replace('\\', '/').EndsWith(model, StringComparison.OrdinalIgnoreCase)))
            .Select(d => $"{d.FilePath}({d.Line},{d.Column}): {d.Message}")
            .ToList();
        Assert.True(stillFailing.Count == 0, "Unexpected unresolved references:\n" + string.Join("\n", stillFailing));

        // Assert — exactly the explained references remain, no others
        var actual = unresolved
            .Select(d => (File: d.FilePath.Replace('\\', '/'), Name: d.Message["Unresolved reference: '".Length..^1]))
            .OrderBy(e => e.File, StringComparer.Ordinal)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(ExpectedStrictErrors.Length, actual.Count);
        foreach (var (fileSuffix, name) in ExpectedStrictErrors)
        {
            Assert.Contains(actual, e => e.File.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase) && e.Name == name);
        }

        // Assert — by default none of them is an Error
        Assert.DoesNotContain(
            defaultResult.Diagnostics,
            d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Error &&
                 d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The training <c>Camera</c> package resolves completely on its own (with the package it
    ///     imports), and its three strict-mode errors in the whole corpus come back exactly when the
    ///     example whose root-level <c>part def Camera</c> shares its name is loaded too: the loader
    ///     processes files in canonical path order (<c>examples</c> before <c>training</c>) whatever
    ///     order they are supplied in, and the first declaration of a name wins, so the remaining
    ///     <c>Camera</c> errors are a name collision between two corpus files, not a gap.
    /// </summary>
    [Fact]
    public async Task SemanticOmgModels_CameraPackage_ResolvesInIsolation()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var omgRoot = Path.Combine(modelsRoot, "OMG");
        var package = Path.Combine(omgRoot, "training", "17.Control", "Camera.sysml");
        var imported = Path.Combine(omgRoot, "training", "15.Actions", "ActionDecomposition.sysml");
        var collidingPartDef = Path.Combine(omgRoot, "examples", "CameraExample", "Camera.sysml");
        var collidingImport = Path.Combine(omgRoot, "examples", "CameraExample", "PictureTaking.sysml");
        if (!File.Exists(package) || !File.Exists(imported) || !File.Exists(collidingPartDef) || !File.Exists(collidingImport))
        {
            return;
        }

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();

        // Act
        var alone = await WorkspaceLoader.LoadAsync([package, imported], stdlibTable, strictUnresolved: true);
        var together = await WorkspaceLoader.LoadAsync(
            [collidingPartDef, collidingImport, package, imported], stdlibTable, strictUnresolved: true);
        var togetherReversed = await WorkspaceLoader.LoadAsync(
            [imported, package, collidingImport, collidingPartDef], stdlibTable, strictUnresolved: true);

        // Assert
        Assert.Empty(alone.Diagnostics);
        Assert.Equal(
            ["Image", "Picture", "Scene"],
            together.Diagnostics
                .Select(d => d.Message["Unresolved reference: '".Length..^1])
                .Order(StringComparer.Ordinal));
        Assert.All(together.Diagnostics, d => Assert.EndsWith("Camera.sysml", d.FilePath, StringComparison.Ordinal));
        Assert.Equal(
            together.Diagnostics.Select(d => d.ToString()),
            togetherReversed.Diagnostics.Select(d => d.ToString()));
    }

    /// <summary>
    ///     The corpus diagnostics do not depend on the order the files are supplied in (or on the
    ///     platform's directory enumeration order): loading the whole corpus in ordinal order and in
    ///     reverse order produces the same diagnostics.
    /// </summary>
    [Fact]
    public async Task SemanticOmgModels_StrictUnresolved_IndependentOfFileOrder()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var omgRoot = Path.Combine(modelsRoot, "OMG");
        if (!Directory.Exists(omgRoot))
        {
            return;
        }

        var forward = Directory.GetFiles(omgRoot, "*.sysml", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (forward.Count == 0)
        {
            return;
        }

        var reversed = Enumerable.Reverse(forward).ToList();
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();

        // Act
        var forwardResult = await WorkspaceLoader.LoadAsync(forward, stdlibTable, strictUnresolved: true);
        var reversedResult = await WorkspaceLoader.LoadAsync(reversed, stdlibTable, strictUnresolved: true);

        // Assert
        Assert.Equal(
            forwardResult.Diagnostics.Select(d => d.ToString()),
            reversedResult.Diagnostics.Select(d => d.ToString()));
        Assert.Equal(forwardResult.Workspace!.Files, reversedResult.Workspace!.Files);
    }
}
