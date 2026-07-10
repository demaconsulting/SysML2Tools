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
        var tempFile = Path.GetTempFileName() + ".sysml";
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
        var tempFile = Path.GetTempFileName() + ".sysml";
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
        var tempFile = Path.GetTempFileName() + ".sysml";
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
        var tempFile = Path.GetTempFileName() + ".sysml";
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
    ///     expression text on <see cref="SysmlViewNode.ExposeBracketFilterTexts"/>, without
    ///     evaluating it (Phase 1 capture-only per the ROADMAP).
    /// </summary>
    [Fact]
    public async Task AstBuilder_ExposeBracketFilter_CapturesRawText()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
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
            var bracketFilterText = Assert.Single(viewNode.ExposeBracketFilterTexts);
            Assert.Equal("@Safety", bracketFilterText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
