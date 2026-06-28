// <copyright file="ContainmentPacker.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A single item to be packed, identified only by its size. Callers map the packed
/// rectangles back to their model elements by index.
/// </summary>
/// <param name="Width">Required width of the item in logical pixels.</param>
/// <param name="Height">Required height of the item in logical pixels.</param>
internal readonly record struct PackItem(double Width, double Height);

/// <summary>
/// A packed rectangle: the position assigned to the item at the same index in the input list.
/// </summary>
/// <param name="X">Absolute X coordinate of the left edge in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the top edge in logical pixels.</param>
/// <param name="Width">Width of the item in logical pixels.</param>
/// <param name="Height">Height of the item in logical pixels.</param>
internal readonly record struct PackedRect(double X, double Y, double Width, double Height);

/// <summary>
/// The result of a packing operation.
/// </summary>
/// <param name="Width">Total width of the packed region (including outer padding) in logical pixels.</param>
/// <param name="Height">Total height of the packed region (including outer padding) in logical pixels.</param>
/// <param name="Rects">
/// Packed rectangles, one per input item in the same order. Each rectangle is positioned
/// relative to the region origin (0, 0).
/// </param>
internal sealed record PackResult(double Width, double Height, IReadOnlyList<PackedRect> Rects);

/// <summary>
/// A shelf (row) bin-packing engine. Places a sequence of variable-size items left to right,
/// wrapping to a new row when the next item would exceed the maximum content width, and sizes
/// the enclosing region to fit all items plus uniform outer padding.
/// </summary>
/// <remarks>
/// The algorithm is deterministic and preserves input order. It guarantees that no two packed
/// rectangles overlap and that every rectangle lies within the returned region bounds. An item
/// wider than the available content width is placed alone on its own row at the content width's
/// left edge (it may extend the region width).
/// </remarks>
internal static class ContainmentPacker
{
    /// <summary>
    /// Packs the given items into rows within <paramref name="maxContentWidth"/>.
    /// </summary>
    /// <param name="items">Items to pack, in the desired visual order.</param>
    /// <param name="maxContentWidth">
    /// Maximum width of the content area (excluding outer padding). Rows wrap when exceeded.
    /// Must be positive.
    /// </param>
    /// <param name="horizontalGap">Gap between adjacent items in the same row.</param>
    /// <param name="verticalGap">Gap between adjacent rows.</param>
    /// <param name="padding">Uniform padding added around the entire packed region.</param>
    /// <returns>A <see cref="PackResult"/> describing item positions and the region size.</returns>
    public static PackResult Pack(
        IReadOnlyList<PackItem> items,
        double maxContentWidth,
        double horizontalGap,
        double verticalGap,
        double padding)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Empty input yields a zero-content region consisting only of padding on both axes.
        if (items.Count == 0)
        {
            return new PackResult(2.0 * padding, 2.0 * padding, []);
        }

        var rects = new PackedRect[items.Count];

        var cursorX = padding;
        var rowTopY = padding;
        var rowHeight = 0.0;
        var widestContentRight = padding;
        var isFirstInRow = true;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            // Determine whether this item starts a new row: it does not fit in the current row
            // and the current row already has at least one item.
            var prospectiveRight = cursorX + item.Width;
            var contentRightLimit = padding + maxContentWidth;
            if (!isFirstInRow && prospectiveRight > contentRightLimit)
            {
                // Wrap to a new row below the tallest item of the current row. The wrapped item
                // is placed immediately below as the sole occupant of the new row; isFirstInRow is
                // reset to false at the end of this iteration once the item has been placed.
                rowTopY += rowHeight + verticalGap;
                cursorX = padding;
                rowHeight = 0.0;
            }

            rects[i] = new PackedRect(cursorX, rowTopY, item.Width, item.Height);

            // Advance the horizontal cursor past this item plus a trailing gap.
            cursorX += item.Width + horizontalGap;
            rowHeight = Math.Max(rowHeight, item.Height);
            widestContentRight = Math.Max(widestContentRight, rects[i].X + item.Width);
            isFirstInRow = false;
        }

        // Total size: widest row's right edge + padding; last row's bottom + padding.
        var totalWidth = widestContentRight + padding;
        var totalHeight = rowTopY + rowHeight + padding;

        return new PackResult(totalWidth, totalHeight, rects);
    }
}
