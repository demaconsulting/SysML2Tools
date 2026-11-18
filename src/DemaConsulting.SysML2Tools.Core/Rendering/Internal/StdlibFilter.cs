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
/// Filtering is based on element <em>origin</em>, never on name: an element is a standard-library
/// element exactly when its fully-qualified name is in the workspace's
/// <see cref="Semantic.SysmlWorkspace.StdlibNames"/> set, which <c>WorkspaceLoader</c> derives from the
/// embedded standard-library seed symbol table. A user package that merely shares a root name with a
/// standard-library package (for example a user <c>package Parts</c>) is therefore not treated as
/// standard library, and its members (<c>Parts::Wheel</c>) stay visible.
/// </remarks>
internal static class StdlibFilter
{
    /// <summary>
    /// Returns <see langword="true"/> when the qualified name originates from the embedded
    /// standard-library seed, that is, when it is a member of <paramref name="stdlibNames"/>.
    /// </summary>
    /// <param name="qualifiedName">Fully-qualified element name to test.</param>
    /// <param name="stdlibNames">Set of qualified names that originate from the stdlib seed.</param>
    /// <returns><see langword="true"/> if the element is part of the stdlib.</returns>
    public static bool IsStdlibElement(string qualifiedName, IReadOnlySet<string> stdlibNames) =>
        stdlibNames.Contains(qualifiedName);
}
