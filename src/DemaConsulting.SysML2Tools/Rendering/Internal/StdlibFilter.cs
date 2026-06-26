// <copyright file="StdlibFilter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Rendering.Internal;

/// <summary>
/// Provides a utility method for identifying elements that belong to the embedded
/// SysML v2 / KerML standard library, so that renderers can exclude them from
/// user-facing diagram output.
/// </summary>
/// <remarks>
/// Filtering is performed by checking whether an element's fully-qualified name starts
/// with any known stdlib root-package prefix followed by <c>::</c>, or equals the
/// prefix exactly. This list covers all packages shipped in the OMG SysML v2 stdlib
/// bundle embedded in <c>DemaConsulting.SysML2Tools</c>, including the KerML packages,
/// the SysML systems library packages, and the SysML domain library packages.
/// </remarks>
internal static class StdlibFilter
{
    /// <summary>
    /// Known SysML v2 and KerML standard-library root-package prefixes.
    /// </summary>
    private static readonly IReadOnlyList<string> StdlibPrefixes =
    [
        // SysML v2 core
        "SysML", "KerML",
        // KerML packages
        "BaseFunctions", "Occurrences", "Core", "Links", "Performances",
        "Blocks", "Rendering", "Systems", "SystemsModelingLibrary",
        "Metaobjects", "ScalarValues", "TransferPerformances", "ControlPerformances",
        "VectorValues",
        // SysML systems library
        "Actions", "Allocations", "AnalysisCases", "AnalysisTooling",
        "Attributes", "Calculations", "Cases", "CausationConnections",
        "CauseAndEffect", "Connections", "Constraints", "DerivationConnections",
        "Flows", "ImageMetadata", "Interfaces", "Items", "MeasurementRefCalculations",
        "MeasurementReferences", "Metadata", "ModelingMetadata",
        "ParametersOfInterestMetadata", "Parts", "Ports", "Quantities",
        "QuantityCalculations", "RequirementDerivation", "Requirements",
        "RiskMetadata", "SampledFunctions", "ShapeItems", "SI", "SIPrefixes",
        "SpatialItems", "StandardViewDefinitions", "States", "StateSpaceRepresentation",
        "TensorCalculations", "Time", "TradeStudies", "UseCases",
        "VectorCalculations", "VerificationCases", "Views",
        // ISQ domain libraries
        "ISQ", "ISQAcoustics", "ISQAtomicNuclear", "ISQBase",
        "ISQCharacteristicNumbers", "ISQChemistryMolecular", "ISQCondensedMatter",
        "ISQElectromagnetism", "ISQInformation", "ISQLight", "ISQMechanics",
        "ISQSpaceTime", "ISQThermodynamics"
    ];

    /// <summary>
    /// Returns <see langword="true"/> when the qualified name belongs to a known
    /// SysML v2 / KerML standard-library package.
    /// </summary>
    /// <param name="qualifiedName">Fully-qualified element name to test.</param>
    /// <returns><see langword="true"/> if the element is part of the stdlib.</returns>
    public static bool IsStdlibElement(string qualifiedName) =>
        StdlibPrefixes.Any(prefix =>
            qualifiedName == prefix ||
            qualifiedName.StartsWith(prefix + "::", StringComparison.Ordinal));
}
