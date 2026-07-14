// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Tests for <c>AstBuilder</c>'s metadata-annotation capture (<see cref="SysmlMetadataNode"/>),
///     exercised indirectly through the public <see cref="WorkspaceLoader.LoadAsync"/> entry point
///     (mirroring the existing <c>WorkspaceLoaderTests</c> convention, since <c>AstBuilder</c> is
///     internal).
/// </summary>
public sealed class AstBuilderMetadataTests
{
    /// <summary>
    ///     A part def annotated with a bare <c>@Type;</c> metadata reference captures a
    ///     <see cref="SysmlMetadataNode"/> child with no attribute values.
    /// </summary>
    [Fact]
    public async Task AstBuilder_BareMetadataAnnotation_CapturesMetadataNode()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    metadata def Safety {
                        attribute isMandatory : Boolean;
                    }

                    part def Engine {
                        @Safety;
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::Engine", out var engine));
            var metadata = Assert.Single(engine!.Children.OfType<SysmlMetadataNode>());
            Assert.Equal("Safety", metadata.TypeReference);
            Assert.Empty(metadata.Attributes);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A part def annotated with <c>{@Type{attr = value;}}</c> captures the literal boolean
    ///     attribute value assigned in the annotation's body.
    /// </summary>
    [Fact]
    public async Task AstBuilder_MetadataAnnotationWithBooleanAttribute_CapturesLiteralValue()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    metadata def Safety {
                        attribute isMandatory : Boolean;
                    }

                    part def Engine {
                        @Safety {
                            isMandatory = true;
                        }
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::Engine", out var engine));
            var metadata = Assert.Single(engine!.Children.OfType<SysmlMetadataNode>());
            var attribute = Assert.Single(metadata.Attributes);
            Assert.Equal("isMandatory", attribute.Name);
            Assert.Equal(MetadataAttributeValueKind.Boolean, attribute.Kind);
            Assert.True(attribute.BooleanValue);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A metadata annotation's type reference resolves into a
    ///     <see cref="SysmlEdgeKind.MetadataType"/> edge when the referenced <c>metadata def</c>
    ///     exists in scope.
    /// </summary>
    [Fact]
    public async Task AstBuilder_MetadataAnnotation_ResolvesTypeReference()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    metadata def Safety {
                        attribute isMandatory : Boolean;
                    }

                    part def Engine {
                        @Safety;
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::Engine", out var engine));
            var metadata = Assert.Single(engine!.Children.OfType<SysmlMetadataNode>());
            var edge = Assert.Single(metadata.ResolvedEdges);
            Assert.Equal(SysmlEdgeKind.MetadataType, edge.Kind);
            Assert.Equal("P::Safety", edge.TargetQualifiedName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A metadata annotation whose type reference does not resolve produces an
    ///     "Unresolved reference" warning diagnostic, mirroring every other reference kind
    ///     <c>ReferenceResolver</c> handles.
    /// </summary>
    [Fact]
    public async Task AstBuilder_MetadataAnnotation_UnresolvedType_ProducesWarning()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    part def Engine {
                        @NoSuchMetadataType;
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.Contains(
                result.Diagnostics,
                d => d.Message.Contains("Unresolved reference") && d.Message.Contains("NoSuchMetadataType"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An <c>expose &lt;path&gt;::**[&lt;expr&gt;]</c> bracket-filter member captures its raw
    ///     expression text paired with its exposed path on <see cref="SysmlViewNode.ExposeMembers"/>
    ///     (Phase 2a fixes the earlier flattened, unpaired capture — see
    ///     <see cref="AstBuilder_MultipleExposeMembers_OnlyOneBracketed_PairsFilterWithCorrectPath"/>).
    /// </summary>
    [Fact]
    public async Task AstBuilder_ExposeBracketFilter_CapturesRawText()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    metadata def Safety {
                        attribute isMandatory : Boolean;
                    }

                    part def Engine {
                        @Safety;
                    }

                    view V {
                        expose P::**[@Safety];
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::V", out var view));
            var viewNode = Assert.IsType<SysmlViewNode>(view);
            var member = Assert.Single(viewNode.ExposeMembers);
            Assert.Equal("P", member.QualifiedName);
            Assert.Equal("@Safety", member.BracketFilterExpressionText);
            Assert.Equal(ExposeRecursionKind.NamespaceRecursive, member.RecursionKind);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A view with two <c>expose</c> members, only one of which carries a bracket filter, pairs
    ///     each entry with its own path and (possibly null) filter text independently — the Phase 2a
    ///     fix for the earlier flattened, unpaired <c>ExposedNames</c>/<c>ExposeBracketFilterTexts</c>
    ///     lists that made it impossible to tell which exposed path a given bracket filter belonged
    ///     to.
    /// </summary>
    [Fact]
    public async Task AstBuilder_MultipleExposeMembers_OnlyOneBracketed_PairsFilterWithCorrectPath()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    metadata def Safety {
                        attribute isMandatory : Boolean;
                    }

                    part def Engine {
                        @Safety;
                    }

                    part def Chassis;

                    view V {
                        expose Chassis;
                        expose Engine::**[@Safety];
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::V", out var view));
            var viewNode = Assert.IsType<SysmlViewNode>(view);

            Assert.Equal(2, viewNode.ExposeMembers.Count);
            var chassisMember = viewNode.ExposeMembers[0];
            Assert.Equal("Chassis", chassisMember.QualifiedName);
            Assert.Null(chassisMember.BracketFilterExpressionText);
            Assert.Equal(ExposeRecursionKind.MembershipExact, chassisMember.RecursionKind);

            var engineMember = viewNode.ExposeMembers[1];
            Assert.Equal("Engine", engineMember.QualifiedName);
            Assert.Equal("@Safety", engineMember.BracketFilterExpressionText);
            Assert.Equal(ExposeRecursionKind.NamespaceRecursive, engineMember.RecursionKind);

            // GetExposedNames() remains the flat qualified-name projection consumed by
            // ReferenceResolver, unaffected by which entries carry a bracket filter.
            Assert.Equal(["Chassis", "Engine"], viewNode.GetExposedNames());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A bare <c>expose X;</c> (MembershipExpose, non-recursive) captures
    ///     <see cref="ExposeRecursionKind.MembershipExact"/> — only <c>X</c> itself is in scope,
    ///     not its containment subtree.
    /// </summary>
    [Fact]
    public async Task AstBuilder_ExposeBareMembership_CapturesMembershipExact()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    part def Engine;

                    view V {
                        expose Engine;
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::V", out var view));
            var viewNode = Assert.IsType<SysmlViewNode>(view);
            var member = Assert.Single(viewNode.ExposeMembers);
            Assert.Equal("Engine", member.QualifiedName);
            Assert.Null(member.BracketFilterExpressionText);
            Assert.Equal(ExposeRecursionKind.MembershipExact, member.RecursionKind);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A recursive <c>expose X::**;</c> (MembershipExpose, recursive) captures
    ///     <see cref="ExposeRecursionKind.MembershipRecursive"/> — <c>X</c> and its entire
    ///     containment subtree are in scope.
    /// </summary>
    [Fact]
    public async Task AstBuilder_ExposeRecursiveMembership_CapturesMembershipRecursive()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    part def Engine;

                    view V {
                        expose Engine::**;
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::V", out var view));
            var viewNode = Assert.IsType<SysmlViewNode>(view);
            var member = Assert.Single(viewNode.ExposeMembers);
            Assert.Equal("Engine", member.QualifiedName);
            Assert.Null(member.BracketFilterExpressionText);
            Assert.Equal(ExposeRecursionKind.MembershipRecursive, member.RecursionKind);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A non-recursive namespace <c>expose X::*;</c> (NamespaceExpose, non-recursive) captures
    ///     <see cref="ExposeRecursionKind.NamespaceDirectChildren"/> — only <c>X</c>'s direct
    ///     children are in scope, not <c>X</c> itself and not deeper descendants.
    /// </summary>
    [Fact]
    public async Task AstBuilder_ExposeNamespaceDirectChildren_CapturesNamespaceDirectChildren()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    view V {
                        expose P::*;
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::V", out var view));
            var viewNode = Assert.IsType<SysmlViewNode>(view);
            var member = Assert.Single(viewNode.ExposeMembers);
            Assert.Equal("P", member.QualifiedName);
            Assert.Null(member.BracketFilterExpressionText);
            Assert.Equal(ExposeRecursionKind.NamespaceDirectChildren, member.RecursionKind);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A recursive namespace <c>expose X::*::**;</c> (NamespaceExpose, recursive) captures
    ///     <see cref="ExposeRecursionKind.NamespaceRecursive"/> — <c>X</c>'s entire containment
    ///     subtree is in scope, excluding <c>X</c> itself.
    /// </summary>
    [Fact]
    public async Task AstBuilder_ExposeNamespaceRecursive_CapturesNamespaceRecursive()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                package P {
                    view V {
                        expose P::*::**;
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace!.Declarations.TryGetValue("P::V", out var view));
            var viewNode = Assert.IsType<SysmlViewNode>(view);
            var member = Assert.Single(viewNode.ExposeMembers);
            Assert.Equal("P", member.QualifiedName);
            Assert.Null(member.BracketFilterExpressionText);
            Assert.Equal(ExposeRecursionKind.NamespaceRecursive, member.RecursionKind);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

