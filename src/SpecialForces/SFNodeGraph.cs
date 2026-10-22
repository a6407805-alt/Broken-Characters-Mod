using System.Collections.Generic;
using System.Diagnostics;
using RoR2;
using RoR2.Navigation;
using UnityEngine;

namespace SpecialForces
{
    // Monster pathing for Stormreach Valley, built when the map loads (the game bakes vanilla graphs in the editor).
    // Ground: a grid of raycasts onto the terrain; each node records which hull sizes fit there, each link which sizes
    // can walk between neighbours (slope and a sphere cast for trees and rocks). Air: three layers of nodes above it.
    // Written straight into NodeGraph's (private) node and link arrays, without line-of-sight data (nothing in this
    // mode needs it; every node shares one empty bit array).
    internal static class SFNodeGraph
    {
        private static readonly HullClassification[] Hulls = { HullClassification.Human, HullClassification.Golem, HullClassification.BeetleQueen };
        private const float MaxSlope = 50f;

        private struct Pending
        {
            public Vector3 position;
            public HullMask forbidden;
            public NodeFlags flags;
        }

        public static NodeGraph BuildGround(Bounds bounds, float spacing)
        {
            var timer = Stopwatch.StartNew();
            int mask = LayerIndex.world.mask;
            int nx = Mathf.CeilToInt(bounds.size.x / spacing), nz = Mathf.CeilToInt(bounds.size.z / spacing);
            var index = new int[nx, nz];
            var nodes = new List<Pending>();
            float top = bounds.max.y + 30f;
            for (int i = 0; i < nx; i++)
            {
                for (int k = 0; k < nz; k++)
                {
                    index[i, k] = -1;
                    var origin = new Vector3(bounds.min.x + (i + 0.5f) * spacing, top, bounds.min.z + (k + 0.5f) * spacing);
                    if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, bounds.size.y + 60f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < Mathf.Cos(MaxSlope * Mathf.Deg2Rad)) continue;
                    Vector3 p = hit.point;
                    HullMask forbidden = HullMask.None;
                    for (int h = 0; h < Hulls.Length; h++)
                    {
                        HullDef def = HullDef.Find(Hulls[h]);
                        float r = def.radius;
                        Vector3 a = p + Vector3.up * (r + 0.3f), b = p + Vector3.up * Mathf.Max(r + 0.35f, def.height - r);
                        if (Physics.CheckCapsule(a, b, r * 0.9f, mask, QueryTriggerInteraction.Ignore)) forbidden |= (HullMask)(1 << h);
                    }
                    if ((forbidden & HullMask.Human) != 0) continue;   // not even a human fits: no node
                    NodeFlags flags = NodeFlags.None;
                    if (!Physics.Raycast(p + Vector3.up, Vector3.up, 50f, mask, QueryTriggerInteraction.Ignore)) flags |= NodeFlags.NoCeiling;
                    index[i, k] = nodes.Count;
                    nodes.Add(new Pending { position = p, forbidden = forbidden, flags = flags });
                }
            }

            var links = new List<NodeGraph.Link>();
            var linkStart = new int[nodes.Count];
            var linkCount = new int[nodes.Count];
            for (int i = 0; i < nx; i++)
            {
                for (int k = 0; k < nz; k++)
                {
                    int a = index[i, k];
                    if (a < 0) continue;
                    linkStart[a] = links.Count;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int ii = i + dx, kk = k + dz;
                            if (ii < 0 || kk < 0 || ii >= nx || kk >= nz) continue;
                            int b = index[ii, kk];
                            if (b < 0) continue;
                            if (TryGroundLink(nodes[a], nodes[b], a, b, mask, out NodeGraph.Link link)) links.Add(link);
                        }
                    }
                    linkCount[a] = links.Count - linkStart[a];
                }
            }
            NodeGraph graph = Finish(nodes, links, linkStart, linkCount, "SFGroundGraph");
            SpecialForcesPlugin.Log.LogInfo($"Ground pathing: {nodes.Count} nodes, {links.Count} links ({timer.ElapsedMilliseconds} ms).");
            return graph;
        }

        private static bool TryGroundLink(Pending a, Pending b, int ia, int ib, int mask, out NodeGraph.Link link)
        {
            link = default;
            Vector3 d = b.position - a.position;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            float slope = Mathf.Atan2(Mathf.Abs(d.y), horizontal) * Mathf.Rad2Deg;
            if (slope > MaxSlope) return false;
            int hullMask = 0;
            for (int h = 0; h < Hulls.Length; h++)
            {
                int bit = 1 << h;
                if (((int)a.forbidden & bit) != 0 || ((int)b.forbidden & bit) != 0) continue;
                HullDef def = HullDef.Find(Hulls[h]);
                float r = def.radius * 0.8f;
                Vector3 from = a.position + Vector3.up * (r + 0.6f), to = b.position + Vector3.up * (r + 0.6f);
                Vector3 dir = to - from;
                if (Physics.SphereCast(from, r, dir.normalized, out _, dir.magnitude, mask, QueryTriggerInteraction.Ignore)) continue;
                hullMask |= bit;
            }
            if (hullMask == 0) return false;
            link = new NodeGraph.Link
            {
                nodeIndexA = new NodeGraph.NodeIndex(ia),
                nodeIndexB = new NodeGraph.NodeIndex(ib),
                distanceScore = d.magnitude,
                maxSlope = slope,
                minJumpHeight = 0f,
                hullMask = hullMask,
                jumpHullMask = 0,
                gateIndex = 0,
            };
            return true;
        }

        public static NodeGraph BuildAir(Bounds bounds, float spacing)
        {
            var timer = Stopwatch.StartNew();
            int mask = LayerIndex.world.mask;
            float[] heights = { 10f, 28f, 50f };
            int nx = Mathf.CeilToInt(bounds.size.x / spacing), nz = Mathf.CeilToInt(bounds.size.z / spacing), ny = heights.Length;
            var index = new int[nx, ny, nz];
            var nodes = new List<Pending>();
            float top = bounds.max.y + 30f;
            for (int i = 0; i < nx; i++)
            {
                for (int k = 0; k < nz; k++)
                {
                    var origin = new Vector3(bounds.min.x + (i + 0.5f) * spacing, top, bounds.min.z + (k + 0.5f) * spacing);
                    bool ground = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, bounds.size.y + 60f, mask, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < ny; j++)
                    {
                        index[i, j, k] = -1;
                        if (!ground) continue;
                        Vector3 p = hit.point + Vector3.up * heights[j];
                        if (p.y > bounds.max.y + 10f) continue;
                        if (Physics.CheckSphere(p, 3f, mask, QueryTriggerInteraction.Ignore)) continue;
                        HullMask forbidden = HullMask.None;
                        if (Physics.CheckSphere(p, 8f, mask, QueryTriggerInteraction.Ignore)) forbidden |= HullMask.BeetleQueen;
                        index[i, j, k] = nodes.Count;
                        nodes.Add(new Pending { position = p, forbidden = forbidden, flags = NodeFlags.NoCeiling });
                    }
                }
            }
            var links = new List<NodeGraph.Link>();
            var linkStart = new int[nodes.Count];
            var linkCount = new int[nodes.Count];
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            for (int k = 0; k < nz; k++)
            {
                int a = index[i, j, k];
                if (a < 0) continue;
                linkStart[a] = links.Count;
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    int ii = i + dx, jj = j + dy, kk = k + dz;
                    if (ii < 0 || jj < 0 || kk < 0 || ii >= nx || jj >= ny || kk >= nz) continue;
                    int b = index[ii, jj, kk];
                    if (b < 0) continue;
                    Vector3 d = nodes[b].position - nodes[a].position;
                    if (Physics.SphereCast(nodes[a].position, 1.5f, d.normalized, out _, d.magnitude, mask, QueryTriggerInteraction.Ignore)) continue;
                    int hullMask = (int)(HullMask.Human | HullMask.Golem);
                    if (((nodes[a].forbidden | nodes[b].forbidden) & HullMask.BeetleQueen) == 0) hullMask |= (int)HullMask.BeetleQueen;
                    links.Add(new NodeGraph.Link
                    {
                        nodeIndexA = new NodeGraph.NodeIndex(a), nodeIndexB = new NodeGraph.NodeIndex(b),
                        distanceScore = d.magnitude, maxSlope = 90f, minJumpHeight = 0f, hullMask = hullMask, jumpHullMask = 0, gateIndex = 0,
                    });
                }
                linkCount[a] = links.Count - linkStart[a];
            }
            NodeGraph graph = Finish(nodes, links, linkStart, linkCount, "SFAirGraph");
            SpecialForcesPlugin.Log.LogInfo($"Air pathing: {nodes.Count} nodes, {links.Count} links ({timer.ElapsedMilliseconds} ms).");
            return graph;
        }

        private static NodeGraph Finish(List<Pending> pending, List<NodeGraph.Link> links, int[] linkStart, int[] linkCount, string name)
        {
            var graph = ScriptableObject.CreateInstance<NodeGraph>();
            graph.name = name;
            var los = new SerializableBitArray(pending.Count);
            var nodes = new NodeGraph.Node[pending.Count];
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new NodeGraph.Node
                {
                    position = pending[i].position,
                    linkListIndex = new NodeGraph.LinkListIndex { index = linkStart[i], size = (uint)linkCount[i] },
                    forbiddenHulls = pending[i].forbidden,
                    flags = pending[i].flags,
                    lineOfSightMask = los,
                    gateIndex = 0,
                };
            }
            SFUtil.SetField(graph, "nodes", nodes);
            SFUtil.SetField(graph, "links", links.ToArray());
            SFUtil.Call(graph, "OnNodeCountChanged");
            SFUtil.Call(graph, "RebuildBlockMap");
            return graph;
        }
    }
}
