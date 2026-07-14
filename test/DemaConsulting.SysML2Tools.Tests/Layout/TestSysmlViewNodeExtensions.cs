// <copyright file="TestSysmlViewNodeExtensions.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Test-only helper reconstructing <see cref="SysmlViewNode.ResolvedExposeMembers"/> for
///     hand-built <see cref="SysmlViewNode"/> test fixtures across this directory that manually set
///     <see cref="SysmlViewNode.ExposeMembers"/> and <see cref="SysmlNode.ResolvedEdges"/>
///     directly (bypassing <c>ReferenceResolver</c>, which normally populates
///     <see cref="SysmlViewNode.ResolvedExposeMembers"/> itself). Pairs each
///     <see cref="ExposeMember"/> with its corresponding resolved <see cref="SysmlEdgeKind.Expose"/>
///     edge target by position, valid only for the simple 1:1 fixtures used throughout these
///     layout-strategy tests (every member resolves, in source order, with no gaps) — the dedicated
///     re-pairing regression test in <c>ExposeScopeResolverTests</c> builds its own mismatched
///     pairing explicitly instead of using this helper.
/// </summary>
internal static class TestSysmlViewNodeExtensions
{
    /// <summary>
    ///     Populates <paramref name="view"/>'s <see cref="SysmlViewNode.ResolvedExposeMembers"/> by
    ///     pairing its <see cref="SysmlViewNode.ExposeMembers"/> (in order) with its resolved
    ///     <see cref="SysmlEdgeKind.Expose"/> edge targets (in order), and returns the same instance
    ///     for fluent chaining in object-initializer-style test setup.
    /// </summary>
    /// <param name="view">The view node to populate.</param>
    /// <returns>The same <paramref name="view"/> instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the number of <see cref="SysmlViewNode.ExposeMembers"/> does not exactly
    ///     match the number of resolved <see cref="SysmlEdgeKind.Expose"/> edges — this helper's
    ///     documented 1:1 contract is violated, and silently pairing a mismatched or truncated
    ///     count via <c>Zip</c> could mask a broken test fixture behind an incorrect pairing rather
    ///     than failing fast.
    /// </exception>
    public static SysmlViewNode WithResolvedExposeMembers(this SysmlViewNode view)
    {
        var targets = view.ResolvedEdges
            .Where(edge => edge.Kind == SysmlEdgeKind.Expose)
            .Select(edge => edge.TargetQualifiedName)
            .ToList();

        if (targets.Count != view.ExposeMembers.Count)
        {
            throw new InvalidOperationException(
                $"WithResolvedExposeMembers requires a 1:1 pairing: {view.ExposeMembers.Count} " +
                $"ExposeMembers but {targets.Count} resolved Expose edges. Use a mismatched " +
                "fixture built by hand (see ExposeScopeResolverTests) instead of this helper if " +
                "that is the scenario under test.");
        }

        view.ResolvedExposeMembers = view.ExposeMembers
            .Zip(targets, (member, target) => (Member: member, ResolvedQualifiedName: target))
            .ToList();

        return view;
    }
}
