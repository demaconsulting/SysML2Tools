// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Represents a fully-loaded and semantically-resolved SysML/KerML workspace.
/// </summary>
public sealed class SysmlWorkspace
{
    /// <summary>
    ///     Gets the list of loaded source file paths (virtual paths for stdlib, real paths for user files).
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Gets the qualified-name registry mapping fully-qualified names to their declaration nodes.
    /// </summary>
    public IReadOnlyDictionary<string, object> Declarations { get; init; } =
        new Dictionary<string, object>();
}
