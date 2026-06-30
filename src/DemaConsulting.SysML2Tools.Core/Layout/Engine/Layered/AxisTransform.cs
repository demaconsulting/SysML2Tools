// <copyright file="AxisTransform.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that maps the abstract along/cross coordinates computed by the earlier stages
/// onto screen coordinates for the requested <see cref="LayoutDirection"/>.
/// </summary>
/// <remarks>
/// <para>
/// The direction-agnostic stages always compute in the <see cref="LayoutDirection.Right"/>-equivalent
/// abstract axes: <c>along = +X</c> (layer progression) and <c>cross = +Y</c> (within-layer order). A
/// node's along-extent is its <see cref="LayerNode.Width"/> and its cross-extent its
/// <see cref="LayerNode.Height"/>. This stage is the single place that converts those abstract
/// coordinates into screen coordinates, isolating all direction handling to one unit.
/// </para>
/// <para>
/// Mapping a layout top-to-bottom or bottom-to-top additionally requires the along-axis to be the node
/// <em>height</em> rather than its width. A purely-final coordinate remap is therefore insufficient
/// for <see cref="LayoutDirection.Down"/>/<see cref="LayoutDirection.Up"/>: the input node sizes must
/// first be normalized (width&#8596;height swapped) so the stages space layers by height. That
/// normalization is performed by <see cref="NormalizeInputAxes"/>, which the pipeline calls before the
/// stage loop. <see cref="LayoutDirection.Right"/> is a literal no-op here (output byte-identical) and
/// <see cref="LayoutDirection.Left"/> only reflects the X axis without swapping.
/// </para>
/// </remarks>
internal sealed class AxisTransform : ILayoutStage
{
    /// <summary>
    /// Normalizes the input node axes for the requested direction before the stages run.
    /// </summary>
    /// <param name="graph">The graph whose node sizes may be swapped in place.</param>
    /// <remarks>
    /// For <see cref="LayoutDirection.Down"/>/<see cref="LayoutDirection.Up"/> the node width and
    /// height are swapped so the along-axis (layer spacing) is driven by the node height. For
    /// <see cref="LayoutDirection.Right"/>/<see cref="LayoutDirection.Left"/> this is a no-op, which
    /// keeps those pipelines byte-identical.
    /// </remarks>
    public static void NormalizeInputAxes(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.Direction is LayoutDirection.Down or LayoutDirection.Up)
        {
            graph.SwapNodeAxes();
        }
    }

    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.Direction == LayoutDirection.Right)
        {
            // RIGHT identity: coordinates are already in screen space; nothing to transform.
            return;
        }

        // LEFT/UP reflect the along-axis about its maximum; DOWN is a pure transpose (max unused).
        var maxAlong = ComputeMaxAlong(graph);

        RemapNodes(graph, maxAlong);
        RemapWaypoints(graph, maxAlong);
    }

    /// <summary>
    /// Maps each augmented-node abstract top-left onto its screen top-left for the direction.
    /// </summary>
    /// <param name="graph">The graph whose node coordinates are replaced.</param>
    /// <param name="maxAlong">The maximum along (abstract +X) coordinate, used by LEFT/UP reflection.</param>
    private static void RemapNodes(LayeredGraph graph, double maxAlong)
    {
        var augX = graph.AugX;
        var augY = graph.AugY;
        var augNodes = graph.AugNodes;

        var newX = new double[augX.Length];
        var newY = new double[augY.Length];
        for (var i = 0; i < augX.Length; i++)
        {
            var alongExtent = i < augNodes.Count ? augNodes[i].Width : 0.0;
            (newX[i], newY[i]) = MapNodeTopLeft(graph.Direction, augX[i], augY[i], alongExtent, maxAlong);
        }

        graph.AugX = newX;
        graph.AugY = newY;
    }

    /// <summary>Maps every waypoint of every original edge onto screen space for the direction.</summary>
    /// <param name="graph">The graph whose waypoints are replaced.</param>
    /// <param name="maxAlong">The maximum along (abstract +X) coordinate, used by LEFT/UP reflection.</param>
    private static void RemapWaypoints(LayeredGraph graph, double maxAlong)
    {
        var waypoints = graph.Waypoints;
        var mapped = new IReadOnlyList<Point2D>[waypoints.Count];
        for (var e = 0; e < waypoints.Count; e++)
        {
            var polyline = waypoints[e];
            var points = new Point2D[polyline.Count];
            for (var p = 0; p < polyline.Count; p++)
            {
                points[p] = MapPoint(graph.Direction, polyline[p], maxAlong);
            }

            mapped[e] = points;
        }

        graph.Waypoints = mapped;
    }

    /// <summary>Maps a single abstract point onto screen space for the given direction.</summary>
    /// <param name="direction">The requested layout flow direction.</param>
    /// <param name="point">The abstract-space point (along = X, cross = Y).</param>
    /// <param name="maxAlong">The maximum along coordinate, used by LEFT/UP reflection.</param>
    /// <returns>The screen-space point.</returns>
    private static Point2D MapPoint(LayoutDirection direction, Point2D point, double maxAlong)
    {
        // S2234: the DOWN/UP transpose deliberately feeds the cross coordinate as screen X and the
        // along coordinate as screen Y; the "reversed" argument order is the intended axis swap.
#pragma warning disable S2234
        return direction switch
        {
            // DOWN: pure transpose (along -> screen Y, cross -> screen X).
            LayoutDirection.Down => new Point2D(point.Y, point.X),

            // LEFT: reflect the along-axis; cross is unchanged.
            LayoutDirection.Left => new Point2D(maxAlong - point.X, point.Y),

            // UP: transpose with the along-axis reflected into screen Y.
            LayoutDirection.Up => new Point2D(point.Y, maxAlong - point.X),

            _ => point,
        };
#pragma warning restore S2234
    }

    /// <summary>Maps an abstract node top-left onto its screen top-left for the given direction.</summary>
    /// <param name="direction">The requested layout flow direction.</param>
    /// <param name="alongTopLeft">The node's abstract along (X) coordinate.</param>
    /// <param name="crossTopLeft">The node's abstract cross (Y) coordinate.</param>
    /// <param name="alongExtent">The node's along-extent (its abstract width).</param>
    /// <param name="maxAlong">The maximum along coordinate, used by LEFT/UP reflection.</param>
    /// <returns>The node's screen top-left (drawn there with its intrinsic width and height).</returns>
    private static (double X, double Y) MapNodeTopLeft(
        LayoutDirection direction,
        double alongTopLeft,
        double crossTopLeft,
        double alongExtent,
        double maxAlong) => direction switch
        {
            LayoutDirection.Down => (crossTopLeft, alongTopLeft),
            LayoutDirection.Left => (maxAlong - (alongTopLeft + alongExtent), crossTopLeft),
            LayoutDirection.Up => (crossTopLeft, maxAlong - (alongTopLeft + alongExtent)),
            _ => (alongTopLeft, crossTopLeft),
        };

    /// <summary>
    /// Returns the maximum along (abstract +X) coordinate over all augmented-node far corners and all
    /// waypoint X values, which LEFT/UP reflect about.
    /// </summary>
    /// <param name="graph">The placed and routed graph.</param>
    /// <returns>The maximum along coordinate (0 when the graph is empty).</returns>
    private static double ComputeMaxAlong(LayeredGraph graph)
    {
        var max = 0.0;
        var augX = graph.AugX;
        var augNodes = graph.AugNodes;
        for (var i = 0; i < augX.Length && i < augNodes.Count; i++)
        {
            max = Math.Max(max, augX[i] + augNodes[i].Width);
        }

        foreach (var polyline in graph.Waypoints)
        {
            foreach (var point in polyline)
            {
                max = Math.Max(max, point.X);
            }
        }

        return max;
    }
}
