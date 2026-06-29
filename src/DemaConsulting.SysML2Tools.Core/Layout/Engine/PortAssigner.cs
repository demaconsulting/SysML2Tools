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
/// A highway-aware port request: a connection that should be aligned to a committed corridor instead
/// of placed independently, so wires sharing a corridor leave a box as one trunk.
/// </summary>
/// <param name="Box">The bounding rectangle of the box that owns the port.</param>
/// <param name="Toward">The point the port's connection heads toward; selects the box side.</param>
/// <param name="ConnectorType">Connector category; only ports of the same type bundle together.</param>
/// <param name="CorridorId">Committed corridor id; -1 means the port joins no corridor and stays independent.</param>
/// <param name="IsOutgoing">True for source-side fan-out, false for destination-side fan-in.</param>
internal readonly record struct HighwayPortRequest(Rect Box, Point2D Toward, string ConnectorType, int CorridorId, bool IsOutgoing);

/// <summary>
/// A highway-aware port placement: absolute centre and side, plus a trunk group shared by ports that
/// merged into a common corridor-facing point. A group id of -1 marks an independent port.
/// </summary>
/// <param name="CentreX">Absolute X of the port centre in logical pixels.</param>
/// <param name="CentreY">Absolute Y of the port centre in logical pixels.</param>
/// <param name="Side">The box side the port is attached to.</param>
/// <param name="TrunkGroupId">Shared trunk id for merged ports; -1 when the port stands alone.</param>
internal readonly record struct HighwayPortPlacement(double CentreX, double CentreY, PortSide Side, int TrunkGroupId);

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

    /// <summary>
    /// Computes highway-aware placements: ports that share a box face, direction, committed corridor,
    /// and connector type collapse into a single trunk point so wires bound for the same corridor leave
    /// the box as one bundle; all other ports stay independent.
    /// </summary>
    /// <remarks>
    /// Merging the corridor-facing stubs is what makes a highway look like a trunk: rather than N
    /// parallel slots, the merged ports share one centre and a positive <see cref="HighwayPortPlacement.TrunkGroupId"/>,
    /// and the downstream comb fan-out separates them into individual lanes. Ports with corridor id -1
    /// receive group -1 and an independently distributed slot.
    /// </remarks>
    /// <param name="requests">The highway port requests; all should reference boxes in the same diagram.</param>
    /// <param name="approachZone">Off-face merge distance and slot inset; the trunk forms one approach zone off the face.</param>
    /// <returns>One <see cref="HighwayPortPlacement"/> per request, in input order.</returns>
    public static IReadOnlyList<HighwayPortPlacement> AssignHighway(IReadOnlyList<HighwayPortRequest> requests, double approachZone)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return [];
        }

        var placements = new HighwayPortPlacement[requests.Count];

        // Choose a side per request and group merge candidates by (side, outgoing, corridor, type).
        var sides = new PortSide[requests.Count];
        var groups = new Dictionary<(PortSide, bool, int, string), List<int>>();
        for (var i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            sides[i] = ChooseSide(r.Box, r.Toward);
            if (r.CorridorId == -1)
            {
                continue;
            }

            var key = (sides[i], r.IsOutgoing, r.CorridorId, r.ConnectorType);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(i);
        }

        // Assign a stable trunk group id per merged corridor group; collapse each to a shared merge point
        // a single connector falls back to a face slot, multiple connectors merge off-face into a trunk.
        var nextTrunk = 0;
        foreach (var (_, indices) in groups.OrderBy(g => g.Value[0]))
        {
            var trunk = nextTrunk++;
            var box = requests[indices[0]].Box;
            var side = sides[indices[0]];
            var (x, y) = indices.Count > 1
                ? MergePoint(box, side, indices.Select(i => requests[i].Toward).ToList(), approachZone)
                : MidpointOnSide(box, side);
            foreach (var idx in indices)
            {
                placements[idx] = new HighwayPortPlacement(x, y, sides[idx], trunk);
            }
        }

        // Independent ports (corridor id -1) sit at the midpoint of their chosen side, group -1.
        for (var i = 0; i < requests.Count; i++)
        {
            if (requests[i].CorridorId == -1)
            {
                var (x, y) = MidpointOnSide(requests[i].Box, sides[i]);
                placements[i] = new HighwayPortPlacement(x, y, sides[i], -1);
            }
        }

        return placements;
    }

    /// <summary>Returns the centre point of the given box side.</summary>
    private static (double X, double Y) MidpointOnSide(Rect box, PortSide side) => side switch
    {
        PortSide.Top => (box.X + (box.Width / 2.0), box.Y),
        PortSide.Bottom => (box.X + (box.Width / 2.0), box.Y + box.Height),
        PortSide.Left => (box.X, box.Y + (box.Height / 2.0)),
        _ => (box.X + box.Width, box.Y + (box.Height / 2.0)),
    };

    /// <summary>
    /// Computes the off-face trunk merge point for a group of connectors sharing one box face: a point
    /// stepped one approach zone outward from the face, aligned to the mean of the sources along the
    /// face axis. Bundled wires reach this point first, then share a single stub into the face.
    /// </summary>
    private static (double X, double Y) MergePoint(Rect box, PortSide side, IReadOnlyList<Point2D> sources, double approachZone)
    {
        var horizontal = side is PortSide.Top or PortSide.Bottom;
        var faceCoord = side switch
        {
            PortSide.Top => box.Y,
            PortSide.Bottom => box.Y + box.Height,
            PortSide.Left => box.X,
            _ => box.X + box.Width,
        };

        // Clamp the outward offset to half the closest source's distance from the face (stub logic).
        var minDist = sources.Min(s => Math.Abs((horizontal ? s.Y : s.X) - faceCoord));
        var mergeOffset = Math.Max(approachZone, Math.Min(approachZone, minDist / 2.0));

        // Mean source position projected onto the face axis, clamped one approach zone in from each end.
        var mean = horizontal ? sources.Average(s => s.X) : sources.Average(s => s.Y);
        var (start, len) = horizontal ? (box.X, box.Width) : (box.Y, box.Height);
        var along = Math.Clamp(mean, start + approachZone, start + len - approachZone);

        return side switch
        {
            PortSide.Top => (along, box.Y - mergeOffset),
            PortSide.Bottom => (along, box.Y + box.Height + mergeOffset),
            PortSide.Left => (box.X - mergeOffset, along),
            _ => (box.X + box.Width + mergeOffset, along),
        };
    }

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
    /// <summary>Minimum spacing between adjacent port slots so connectors stay individually traceable.</summary>
    private const double MinPortSlot = 11.0;

    private static void DistributeAlongSide(
        IReadOnlyList<PortRequest> requests,
        PortSide side,
        List<int> indices,
        PortPlacement[] placements)
    {
        var box = requests[indices[0]].Box;
        var horizontal = side is PortSide.Top or PortSide.Bottom;
        var faceLen = horizontal ? box.Width : box.Height;

        // Order ports by their target's coordinate along the edge to reduce crossings.
        indices.Sort((a, b) => horizontal
            ? requests[a].Toward.X.CompareTo(requests[b].Toward.X)
            : requests[a].Toward.Y.CompareTo(requests[b].Toward.Y));

        var count = indices.Count;
        var even = count * MinPortSlot <= faceLen;
        var faceStart = horizontal ? box.X : box.Y;
        for (var slot = 0; slot < count; slot++)
        {
            // Even spacing when the face is wide enough; otherwise compress to a centred minimum-slot band.
            var offset = even
                ? (slot + 1.0) / (count + 1.0) * faceLen
                : (faceLen / 2.0) - ((count - 1) * MinPortSlot / 2.0) + (slot * MinPortSlot);
            var (x, y) = side switch
            {
                PortSide.Top => (faceStart + offset, box.Y),
                PortSide.Bottom => (faceStart + offset, box.Y + box.Height),
                PortSide.Left => (box.X, faceStart + offset),
                _ => (box.X + box.Width, faceStart + offset),
            };

            placements[indices[slot]] = new PortPlacement(x, y, side);
        }
    }
}
