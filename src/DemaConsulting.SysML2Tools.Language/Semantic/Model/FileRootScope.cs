// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     What one source file writes at its root, outside any package, and how a
///     <see cref="NameResolver"/> must be told about it: root-level aliases are memberships of the
///     global namespace, and root-level imports apply to the declarations of that file.
/// </summary>
/// <remarks>
///     Kept so that the resolver built while the workspace is loaded and every resolver built
///     from the loaded workspace afterwards are set up identically
///     (<see cref="NameResolver.RegisterFileRoots"/>), rather than the later ones silently
///     missing the file-root context.
/// </remarks>
/// <param name="Aliases">The alias nodes written at the file root.</param>
/// <param name="Imports">The import nodes written at the file root.</param>
/// <param name="DeclarationNames">
///     The qualified names of the file's declarations, which the file-root imports apply to;
///     empty when the file has no root imports.
/// </param>
internal sealed record FileRootScope(
    IReadOnlyList<SysmlAliasNode> Aliases,
    IReadOnlyList<SysmlImportNode> Imports,
    IReadOnlyList<string> DeclarationNames);
