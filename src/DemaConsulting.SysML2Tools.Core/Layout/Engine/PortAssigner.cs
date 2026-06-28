// <copyright file="PortAssigner.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A request to place a port on a box, identified by the box rectangle and the point the port's
/// connection travels toward (typically the centre of the connected box).
/// </summary>
/// <param name="Box">The bounding rectangle of the box that owns the port.</param>
/// <param name="Toward">The point the port's connection heads toward; selects the box side.</param>
internal readonly record struct PortRequest(Rect Box, Point2D Toward);

/// <summary>
/// The computed placement of a port: its absolute centre and the side of the box it sits on.
/// </summary>
/// <param name="CentreX">Absolute X coordinate of the port centre in logical pixels.</param>
/// <param name="CentreY">Absolute Y coordinate of the port centre in logical pixels.</param>
/// <param name="Side">The box side the port is attached to.</param>
internal readonly record struct PortPlacement(double CentreX, double CentreY, PortSide Side);

/// <summary>
/// Assigns ports to box sides and distributes multiple ports evenly along each side.
/// </summary>
/// <remarks>
/// Each port is first assigned to the box side whose outward normal best points toward its
/// connection target (a directional heuristic). Ports sharing a side are then spread out at evenly
/// spaced slots, ordered by their target coordinate so connections cross as little as possible. The
/// assigner is deterministic and independent of the SysML model.
/// </remarks>
internal static class PortAssigner
{
    /// <summary>
    /// Computes placements for a set of ports that all belong to the same box.
    /// </summary>
    /// <param name="requests">
    /// The ports to place. Every request should reference the same <see cref="PortRequest.Box"/>.
    /// </param>
    /// <returns>One <see cref="PortPlacement"/> per request, in the same order.</returns>
    public static IReadOnlyList<PortPlacement> Assign(IReadOnlyList<PortRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return [];
        }

        // Group port indices by the side chosen from each port's target direction.
        var bySide = new Dictionary<PortSide, List<int>>();
        for (var i = 0; i < requests.Count; i++)
        {
            var side = ChooseSide(requests[i].Box, requests[i].Toward);
            if (!bySide.TryGetValue(side, out var list))
            {
                list = [];
                bySide[side] = list;
            }

            list.Add(i);
        }

        var placements = new PortPlacement[requests.Count];
        foreach (var (side, indices) in bySide)
        {
            DistributeAlongSide(requests, side, indices, placements);
        }

        return placements;
    }

    /// <summary>Chooses the box side whose outward normal best points toward the target.</summary>
    private static PortSide ChooseSide(Rect box, Point2D toward)
    {
        var cx = box.X + (box.Width / 2.0);
        var cy = box.Y + (box.Height / 2.0);
        var dx = toward.X - cx;
        var dy = toward.Y - cy;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0 ? PortSide.Right : PortSide.Left;
        }

        return dy >= 0 ? PortSide.Bottom : PortSide.Top;
    }

    /// <summary>
    /// Places the given port indices at evenly spaced slots along the specified box side, ordered
    /// by their target coordinate along that side so connections cross as little as possible.
    /// </summary>
    private static void DistributeAlongSide(
        IReadOnlyList<PortRequest> requests,
        PortSide side,
        List<int> indices,
        PortPlacement[] placements)
    {
        var box = requests[indices[0]].Box;
        var horizontal = side is PortSide.Top or PortSide.Bottom;

        // Order ports by their target's coordinate along the edge to reduce crossings.
        indices.Sort((a, b) => horizontal
            ? requests[a].Toward.X.CompareTo(requests[b].Toward.X)
            : requests[a].Toward.Y.CompareTo(requests[b].Toward.Y));

        var count = indices.Count;
        for (var slot = 0; slot < count; slot++)
        {
            var fraction = (slot + 1.0) / (count + 1.0);
            var (x, y) = side switch
            {
                PortSide.Top => (box.X + (fraction * box.Width), box.Y),
                PortSide.Bottom => (box.X + (fraction * box.Width), box.Y + box.Height),
                PortSide.Left => (box.X, box.Y + (fraction * box.Height)),
                _ => (box.X + box.Width, box.Y + (fraction * box.Height)),
            };

            placements[indices[slot]] = new PortPlacement(x, y, side);
        }
    }
}
