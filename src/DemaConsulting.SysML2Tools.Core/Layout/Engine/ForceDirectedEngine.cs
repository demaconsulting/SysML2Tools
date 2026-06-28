// <copyright file="ForceDirectedEngine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A node to be placed by the <see cref="ForceDirectedEngine"/>, identified by its size. Callers
/// map results back to model elements by index.
/// </summary>
/// <param name="Width">Width of the node's bounding box in logical pixels.</param>
/// <param name="Height">Height of the node's bounding box in logical pixels.</param>
internal readonly record struct ForceNode(double Width, double Height);

/// <summary>
/// An undirected edge between two nodes (by index) that contributes an attractive spring force.
/// </summary>
/// <param name="A">Index of the first node.</param>
/// <param name="B">Index of the second node.</param>
internal readonly record struct ForceEdge(int A, int B);

/// <summary>
/// The result of a force-directed placement.
/// </summary>
/// <param name="Width">Total width of the placed region (including padding) in logical pixels.</param>
/// <param name="Height">Total height of the placed region (including padding) in logical pixels.</param>
/// <param name="Rects">Placed rectangles, one per input node in the same order.</param>
internal sealed record ForceResult(double Width, double Height, IReadOnlyList<PackedRect> Rects);

/// <summary>
/// A deterministic Fruchterman-Reingold force-directed layout engine. Nodes repel one another and
/// edges act as springs; after a fixed number of cooling iterations the node centres settle into a
/// spread-out arrangement. A final overlap-removal pass guarantees non-overlapping bounding boxes.
/// </summary>
/// <remarks>
/// The initial placement is seeded deterministically (a golden-angle spiral) so that results are
/// reproducible across runs and platforms. The engine returns absolute rectangles translated so the
/// region origin is (0, 0) plus a uniform padding margin.
/// </remarks>
internal static class ForceDirectedEngine
{
    /// <summary>Maximum number of force-application iterations.</summary>
    private const int Iterations = 300;

    /// <summary>Consecutive low-energy iterations required before early termination.</summary>
    private const int KineticEnergyWindow = 5;

    /// <summary>Total squared displacement below which an iteration counts as settled.</summary>
    private const double KineticEnergyThreshold = 300.0;

    /// <summary>Golden angle in radians, used to spread the deterministic initial seed.</summary>
    private const double GoldenAngle = 2.399963229728653;

    /// <summary>
    /// Computes a force-directed placement for the given nodes and edges. Hierarchy gravity is
    /// disabled, matching the original flat (free-2D) behaviour.
    /// </summary>
    /// <param name="nodes">Nodes to place, in caller order.</param>
    /// <param name="edges">Edges contributing attractive forces (indices into <paramref name="nodes"/>).</param>
    /// <param name="spacing">Nominal spacing between adjacent node centres (the spring rest length).</param>
    /// <param name="padding">Uniform padding added around the placed region.</param>
    /// <returns>A <see cref="ForceResult"/> with one rectangle per node and the region size.</returns>
    public static ForceResult Place(
        IReadOnlyList<ForceNode> nodes,
        IReadOnlyList<ForceEdge> edges,
        double spacing,
        double padding) =>
        Place(nodes, edges, spacing, padding, kHier: 0.0, layerHints: null);

    /// <summary>
    /// Computes a force-directed placement with an optional anisotropic vertical hierarchy gravity.
    /// When <paramref name="kHier"/> is positive and per-node layer hints are supplied, each node is
    /// biased toward a reading-direction y-band proportional to its layer, producing a flow that is
    /// flat for <c>kHier = 0</c> and increasingly columnar as <c>kHier</c> rises. A wire-pressure
    /// outward force (annealed with the cooling temperature) opens dense regions early, and the
    /// simulation terminates early once kinetic energy stays low for several consecutive iterations.
    /// </summary>
    /// <param name="nodes">Nodes to place, in caller order.</param>
    /// <param name="edges">Edges contributing attractive forces (indices into <paramref name="nodes"/>).</param>
    /// <param name="spacing">Nominal spacing between adjacent node centres (the spring rest length).</param>
    /// <param name="padding">Uniform padding added around the placed region.</param>
    /// <param name="kHier">Hierarchy/affinity ratio: 0 = flat, 1 = near-hard reading direction.</param>
    /// <param name="layerHints">Optional per-node layer (0 = top); null disables hierarchy gravity.</param>
    /// <returns>A <see cref="ForceResult"/> with one rectangle per node and the region size.</returns>
    public static ForceResult Place(
        IReadOnlyList<ForceNode> nodes,
        IReadOnlyList<ForceEdge> edges,
        double spacing,
        double padding,
        double kHier,
        IReadOnlyList<int>? layerHints)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var n = nodes.Count;
        if (n == 0)
        {
            return new ForceResult(2.0 * padding, 2.0 * padding, []);
        }

        if (n == 1)
        {
            var only = new[] { new PackedRect(padding, padding, nodes[0].Width, nodes[0].Height) };
            return new ForceResult(nodes[0].Width + (2.0 * padding), nodes[0].Height + (2.0 * padding), only);
        }

        // Deterministic spiral seed positions centred on the origin.
        var px = new double[n];
        var py = new double[n];
        for (var i = 0; i < n; i++)
        {
            var radius = spacing * Math.Sqrt(i + 1);
            var angle = i * GoldenAngle;
            px[i] = radius * Math.Cos(angle);
            py[i] = radius * Math.Sin(angle);
        }

        // Optimal distance between nodes (Fruchterman-Reingold "k").
        var k = spacing;
        var area = k * k * n;
        var temperature = Math.Sqrt(area) / 2.0;
        var cooling = temperature / (Iterations + 1);

        ApplyForces(nodes, edges, px, py, k, temperature, cooling, kHier, layerHints, spacing);
        RemoveOverlaps(nodes, px, py, spacing);

        return BuildResult(nodes, px, py, padding);
    }

    /// <summary>Runs the iterative repulsion/attraction force simulation in place.</summary>
    private static void ApplyForces(
        IReadOnlyList<ForceNode> nodes,
        IReadOnlyList<ForceEdge> edges,
        double[] px,
        double[] py,
        double k,
        double temperature,
        double cooling,
        double kHier,
        IReadOnlyList<int>? layerHints,
        double spacing)
    {
        var n = nodes.Count;
        var dx = new double[n];
        var dy = new double[n];
        var initialTemp = temperature;
        var hierarchy = kHier > 0.0 && layerHints is not null && layerHints.Count == n;
        var settledRun = 0;

        for (var iter = 0; iter < Iterations; iter++)
        {
            Array.Clear(dx);
            Array.Clear(dy);

            // Repulsive forces between every pair of nodes, plus an annealed wire-pressure term that
            // pushes crowded pairs apart most strongly while the system is still hot.
            var pressure = temperature / Math.Max(initialTemp, 0.01);
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var deltaX = px[i] - px[j];
                    var deltaY = py[i] - py[j];
                    var dist = Math.Max(Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)), 0.01);
                    var repulse = ((k * k) / dist) + (pressure * k * 0.25);
                    var ux = deltaX / dist;
                    var uy = deltaY / dist;
                    dx[i] += ux * repulse;
                    dy[i] += uy * repulse;
                    dx[j] -= ux * repulse;
                    dy[j] -= uy * repulse;
                }
            }

            // Attractive forces along edges.
            foreach (var edge in edges)
            {
                var deltaX = px[edge.A] - px[edge.B];
                var deltaY = py[edge.A] - py[edge.B];
                var dist = Math.Max(Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)), 0.01);
                var attract = (dist * dist) / k;
                var ux = deltaX / dist;
                var uy = deltaY / dist;
                dx[edge.A] -= ux * attract;
                dy[edge.A] -= uy * attract;
                dx[edge.B] += ux * attract;
                dy[edge.B] += uy * attract;
            }

            // Anisotropic hierarchy gravity: pull each node toward its layer's vertical band.
            if (hierarchy)
            {
                for (var i = 0; i < n; i++)
                {
                    var targetY = layerHints![i] * spacing;
                    dy[i] += kHier * (targetY - py[i]);
                }
            }

            // Displace each node, capped by the current temperature, then cool down.
            var energy = 0.0;
            for (var i = 0; i < n; i++)
            {
                var disp = Math.Max(Math.Sqrt((dx[i] * dx[i]) + (dy[i] * dy[i])), 0.01);
                var capped = Math.Min(disp, temperature);
                px[i] += (dx[i] / disp) * capped;
                py[i] += (dy[i] / disp) * capped;
                energy += capped * capped;
            }

            temperature = Math.Max(temperature - cooling, 0.0);

            // Kinetic-energy termination: stop once the system stays nearly still for a few iterations.
            settledRun = energy < KineticEnergyThreshold ? settledRun + 1 : 0;
            if (settledRun >= KineticEnergyWindow)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Iteratively pushes apart any overlapping node bounding boxes (with a small gap) until no
    /// overlaps remain or an iteration cap is reached.
    /// </summary>
    private static void RemoveOverlaps(IReadOnlyList<ForceNode> nodes, double[] px, double[] py, double gap)
    {
        var n = nodes.Count;
        const int MaxPasses = 200;
        var margin = gap * 0.3;

        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var moved = false;
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var halfW = ((nodes[i].Width + nodes[j].Width) / 2.0) + margin;
                    var halfH = ((nodes[i].Height + nodes[j].Height) / 2.0) + margin;
                    var deltaX = px[j] - px[i];
                    var deltaY = py[j] - py[i];
                    var overlapX = halfW - Math.Abs(deltaX);
                    var overlapY = halfH - Math.Abs(deltaY);

                    if (overlapX <= 0 || overlapY <= 0)
                    {
                        continue;
                    }

                    // Resolve along the axis of least penetration.
                    if (overlapX < overlapY)
                    {
                        var shift = (overlapX / 2.0) * (deltaX < 0 ? -1.0 : 1.0);
                        px[i] -= shift;
                        px[j] += shift;
                    }
                    else
                    {
                        var shift = (overlapY / 2.0) * (deltaY < 0 ? -1.0 : 1.0);
                        py[i] -= shift;
                        py[j] += shift;
                    }

                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }
    }

    /// <summary>Translates centre positions to top-left rectangles and computes the region size.</summary>
    private static ForceResult BuildResult(IReadOnlyList<ForceNode> nodes, double[] px, double[] py, double padding)
    {
        var n = nodes.Count;
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        for (var i = 0; i < n; i++)
        {
            var left = px[i] - (nodes[i].Width / 2.0);
            var top = py[i] - (nodes[i].Height / 2.0);
            minX = Math.Min(minX, left);
            minY = Math.Min(minY, top);
            maxX = Math.Max(maxX, left + nodes[i].Width);
            maxY = Math.Max(maxY, top + nodes[i].Height);
        }

        var rects = new PackedRect[n];
        for (var i = 0; i < n; i++)
        {
            var left = px[i] - (nodes[i].Width / 2.0) - minX + padding;
            var top = py[i] - (nodes[i].Height / 2.0) - minY + padding;
            rects[i] = new PackedRect(left, top, nodes[i].Width, nodes[i].Height);
        }

        var width = (maxX - minX) + (2.0 * padding);
        var height = (maxY - minY) + (2.0 * padding);
        return new ForceResult(width, height, rects);
    }
}
