// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     Classifies the kind of documentation text a <see cref="SysmlAnnotation"/> represents.
/// </summary>
public enum SysmlAnnotationKind
{
    /// <summary>
    ///     Free text from a <c>comment</c> annotating element (the <c>REGULAR_COMMENT</c> body
    ///     of a SysML/KerML <c>comment</c> member).
    /// </summary>
    Comment,

    /// <summary>
    ///     Free text from a <c>doc</c> annotating element (the <c>REGULAR_COMMENT</c> body of a
    ///     SysML/KerML <c>doc</c> member).
    /// </summary>
    Documentation,
}

/// <summary>
///     Captured free text from a <c>comment</c> or <c>doc</c> annotating element, attached to
///     the AST node it lexically annotates.
/// </summary>
/// <param name="Kind">The kind of annotation this text was captured from.</param>
/// <param name="Text">
///     The raw annotation text with the surrounding <c>/*</c>/<c>*/</c> (or <c>//*</c>/<c>*/</c>)
///     comment delimiters removed, preserved verbatim otherwise (no re-indentation or trimming).
/// </param>
public sealed record SysmlAnnotation(SysmlAnnotationKind Kind, string Text);
