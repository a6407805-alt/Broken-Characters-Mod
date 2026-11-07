using System.Collections.Generic;
using RoR2;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Surveyor
{
    // v0.7.0: the LiDAR shows the whole stage's objectives: through-wall markers for chests, the teleporter, shrines,
    // printers, drones and more (labelled when close), and a radar map on the left. Black holes and the Shimmer Stalker
    // are painted into the point cloud.
    public partial class SvrLidar
    {
        private struct MapPoi
        {
            public Transform Tr;
            public Vector3 Pos;
            public string Label;
            public Color Col;
            public int Rank;            // 0 = teleporter, 1 = objective (shrine/portal/printer), 2 = chest/drone, 3 = barrel/minor
        }

        private readonly List<MapPoi> _map = new List<MapPoi>(128);
        private float _nextMapScan;
        private GUIStyle _mapStyle;

        private static Color MapCol(string n, out string label, out int rank)
        {
            rank = 2;
            if (n.Contains("teleporter")) { label = "TELEPORTER"; rank = 0; return new Color(1f, 0.3f, 0.95f); }
            if (n.Contains("portal")) { label = "PORTAL"; rank = 1; return new Color(0.4f, 0.8f, 1f); }
            if (n.Contains("shrine")) { label = n.Contains("chance") ? "SHRINE OF CHANCE" : n.Contains("blood") ? "SHRINE OF BLOOD" : n.Contains("boss") ? "SHRINE OF THE MOUNTAIN" : n.Contains("combat") ? "SHRINE OF COMBAT" : n.Contains("heal") ? "SHRINE OF THE WOODS" : n.Contains("order") ? "SHRINE OF ORDER" : n.Contains("cleanse") ? "CLEANSING POOL" : "SHRINE"; rank = 1; return new Color(0.4f, 1f, 0.6f); }
            if (n.Contains("duplicator")) { label = n.Contains("large") ? "PRINTER (GREEN)" : n.Contains("military") ? "PRINTER (RED)" : n.Contains("wild") ? "PRINTER (YELLOW)" : "PRINTER (WHITE)"; rank = 1; return new Color(1f, 0.85f, 0.3f); }
            if (n.Contains("scrapper")) { label = "SCRAPPER"; rank = 1; return new Color(0.85f, 0.85f, 0.85f); }
            if (n.Contains("lunarchest")) { label = "LUNAR POD"; return new Color(0.45f, 0.7f, 1f); }
            if (n.Contains("goldchest")) { label = "LEGENDARY CHEST"; return new Color(1f, 0.3f, 0.25f); }
            if (n.Contains("chest2") || n.Contains("largechest")) { label = "LARGE CHEST"; return new Color(0.45f, 1f, 0.35f); }
            if (n.Contains("equipmentbarrel")) { label = "EQUIPMENT BARREL"; return new Color(1f, 0.6f, 0.15f); }
            if (n.Contains("voidchest") || n.Contains("voidtriple")) { label = "VOID CRADLE"; return new Color(0.85f, 0.4f, 1f); }
            if (n.Contains("lockbox")) { label = "LOCKBOX"; return new Color(0.45f, 1f, 0.35f); }
            if (n.Contains("chest") || n.Contains("casino")) { label = "CHEST"; return new Color(0.95f, 0.95f, 0.95f); }
            if (n.Contains("multishop") || n.Contains("terminal")) { label = "TERMINAL"; return new Color(0.95f, 0.95f, 0.95f); }
            if (n.Contains("drone") || n.Contains("turret")) { label = n.Contains("turret") ? "TURRET" : "DRONE"; return new Color(0.4f, 0.9f, 1f); }
            if (n.Contains("barrel")) { label = "BARREL"; rank = 3; return new Color(1f, 0.82f, 0.25f); }
            if (n.Contains("newt") || n.Contains("altar")) { label = "NEWT ALTAR"; rank = 1; return new Color(0.45f, 0.7f, 1f); }
            label = null;
            return Color.white;
        }

        private void RefreshMap()
        {
            _map.Clear();
            try
            {
                foreach (TeleporterInteraction tp in FindObjectsOfType<TeleporterInteraction>())
                    if (tp && tp.gameObject.activeInHierarchy)
                        _map.Add(new MapPoi { Tr = tp.transform, Pos = tp.transform.position + Vector3.up * 2f, Label = "TELEPORTER", Col = new Color(1f, 0.3f, 0.95f), Rank = 0 });
                foreach (PurchaseInteraction pi in FindObjectsOfType<PurchaseInteraction>())
                {
                    if (!pi || !pi.gameObject.activeInHierarchy || !pi.available) continue;
                    string n = pi.gameObject.name.ToLowerInvariant();
                    if (n.Contains("teleporter")) continue;
                    Color c = MapCol(n, out string label, out int rank);
                    if (label == null) continue;
                    _map.Add(new MapPoi { Tr = pi.transform, Pos = pi.transform.position + Vector3.up * 1f, Label = label, Col = c, Rank = rank });
                }
                foreach (BarrelInteraction bi in FindObjectsOfType<BarrelInteraction>())
                    if (bi && bi.gameObject.activeInHierarchy)
                        _map.Add(new MapPoi { Tr = bi.transform, Pos = bi.transform.position + Vector3.up * 0.6f, Label = "BARREL", Col = new Color(1f, 0.82f, 0.25f), Rank = 3 });
            }
            catch { }
        }

        // ---------------------------------------------------------------- point-cloud extras (called from Update)

        private void MiscLidarFx(float dt)
        {
            if (Time.time >= _nextMapScan) { _nextMapScan = Time.time + 2f; RefreshMap(); }
            if (_peekK > 0.65f) return;
            float now = Time.time;
            // black holes: dots spiral into them
            for (int h = 0; h < SvrBlackHoleFx.Live.Count; h++)
            {
                Transform t = SvrBlackHoleFx.Live[h];
                if (!t) continue;
                Vector3 c = t.position;
                float r = SvrPlugin.BlackHoleRadius.Value;
                for (int i = 0; i < 30; i++)
                {
                    float prog = (now * 0.9f + i / 30f) % 1f;
                    float rad = Mathf.Lerp(r * 0.55f, 0.35f, prog);
                    float ang = i * 2.39996f + now * 3.5f + prog * 7f;
                    AddDot(c + new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang * 0.5f) * rad * 0.18f, Mathf.Sin(ang) * rad), (i & 1) == 0 ? new Color(0.68f, 0.32f, 1f, 1f) : Color.white, 0.12f, true);
                }
            }
            // the Shimmer Stalker: always outlined in the cloud, brighter when close
            var all = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < all.Count; i++)
                if (SvrStalker.IsStalker(all[i]) && all[i].healthComponent && all[i].healthComponent.alive)
                    SweepBody(all[i], 0.5f, 26);
        }

        // ---------------------------------------------------------------- markers + radar (called from OnGUI)

        private void DrawMap(Camera cam, float scale)
        {
            if (_body == null) return;
            if (_mapStyle == null) _mapStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Texture2D white = Texture2D.whiteTexture;
            Vector3 me = _body.corePosition;
            bool lidarOn = !Peeking && _peekK < 0.5f;

            // through-wall markers (LiDAR mode only): diamond + label when close, teleporter always with distance
            if (lidarOn)
            {
                _mapStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(11f * scale));
                for (int i = 0; i < _map.Count; i++)
                {
                    MapPoi p = _map[i];
                    if (!p.Tr) continue;
                    float d = Vector3.Distance(me, p.Pos);
                    float maxD = p.Rank == 0 ? 9999f : p.Rank == 1 ? 140f : p.Rank == 2 ? 110f : 45f;
                    if (d > maxD) continue;
                    Vector3 sp = cam.WorldToScreenPoint(p.Pos);
                    bool behind = sp.z <= 0.3f;
                    float x = sp.x, y = Screen.height - sp.y;
                    if (behind || x < 0f || x > Screen.width || y < 0f || y > Screen.height)
                    {
                        if (p.Rank != 0) continue;
                        // teleporter off screen: arrow on the screen edge
                        Vector2 dir = new Vector2(x - Screen.width * 0.5f, y - Screen.height * 0.5f);
                        if (behind) dir = -dir;
                        if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                        dir.Normalize();
                        float ex = Screen.width * 0.5f + dir.x * Screen.width * 0.44f, ey = Screen.height * 0.5f + dir.y * Screen.height * 0.4f;
                        GUI.color = p.Col;
                        Diamond(ex, ey, 9f * scale, white);
                        GUI.Label(new Rect(ex - 90f * scale, ey + 10f * scale, 180f * scale, 16f * scale), "TELEPORTER " + Mathf.RoundToInt(d) + "m", _mapStyle);
                        continue;
                    }
                    float fade = p.Rank == 0 ? 1f : Mathf.Clamp01((maxD - d) / 20f);
                    GUI.color = new Color(p.Col.r, p.Col.g, p.Col.b, 0.9f * fade);
                    Diamond(x, y, (p.Rank == 0 ? 9f : 5f) * scale, white);
                    if (p.Rank == 0 || d < 40f || (p.Rank == 1 && d < 80f))
                        GUI.Label(new Rect(x - 100f * scale, y - 22f * scale, 200f * scale, 16f * scale), p.Label + "  " + Mathf.RoundToInt(d) + "m", _mapStyle);
                }
            }

            // radar map (always): you in the middle, facing up
            float R = 92f * scale;
            float cx = 20f * scale + R, cy = 330f * scale + R;
            float range = 90f;
            GUI.color = new Color(0.02f, 0.05f, 0.08f, 0.72f);
            GUI.DrawTexture(new Rect(cx - R, cy - R, R * 2f, R * 2f), white);
            GUI.color = new Color(0.2f, 0.85f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(cx - R, cy, R * 2f, 1f), white);
            GUI.DrawTexture(new Rect(cx, cy - R, 1f, R * 2f), white);
            GUI.DrawTexture(new Rect(cx - R, cy - R, R * 2f, 1.5f * scale), white);
            GUI.DrawTexture(new Rect(cx - R, cy + R, R * 2f, 1.5f * scale), white);
            GUI.DrawTexture(new Rect(cx - R, cy - R, 1.5f * scale, R * 2f), white);
            GUI.DrawTexture(new Rect(cx + R, cy - R, 1.5f * scale, R * 2f), white);
            float sweep = (Time.time * 1.6f) % (Mathf.PI * 2f);
            GUI.color = new Color(0.3f, 1f, 0.9f, 0.25f);
            for (int k = 0; k < 12; k++)
            {
                float rr = R * k / 12f;
                GUI.DrawTexture(new Rect(cx + Mathf.Sin(sweep) * rr - 1f, cy - Mathf.Cos(sweep) * rr - 1f, 2f * scale, 2f * scale), white);
            }

            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            for (int i = 0; i < _map.Count; i++)
            {
                MapPoi p = _map[i];
                if (!p.Tr) continue;
                Vector2 rp = RadarPos(p.Pos - me, fwd, right, range, R, p.Rank == 0, out bool inside);
                if (!inside) continue;
                GUI.color = p.Col;
                float sz = (p.Rank == 0 ? 6f : p.Rank == 3 ? 2.5f : 3.5f) * scale;
                if (p.Rank <= 1) Diamond(cx + rp.x, cy + rp.y, sz, white);
                else GUI.DrawTexture(new Rect(cx + rp.x - sz, cy + rp.y - sz, sz * 2f, sz * 2f), white);
            }
            var all = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < all.Count; i++)
            {
                CharacterBody b = all[i];
                if (!b || b == _body || !b.healthComponent || !b.healthComponent.alive || !b.teamComponent) continue;
                bool stalker = SvrStalker.IsStalker(b);
                bool ally = b.teamComponent.teamIndex == TeamIndex.Player;
                Vector2 rp = RadarPos(b.corePosition - me, fwd, right, range, R, stalker, out bool inside);
                if (!inside) continue;
                if (stalker)
                {
                    float pulse = 5f + 2.5f * Mathf.Sin(Time.time * 9f);
                    GUI.color = new Color(0.85f, 0.2f, 1f, 1f);
                    Diamond(cx + rp.x, cy + rp.y, pulse * scale, white);
                    continue;
                }
                GUI.color = ally ? new Color(0.4f, 1f, 0.5f, 0.95f) : new Color(1f, 0.25f, 0.2f, 0.85f);
                float s2 = (ally ? 3f : 2f) * scale;
                GUI.DrawTexture(new Rect(cx + rp.x - s2, cy + rp.y - s2, s2 * 2f, s2 * 2f), white);
            }
            GUI.color = Color.white;
            Diamond(cx, cy, 4f * scale, white);
            _mapStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(10f * scale));
            GUI.color = new Color(0.6f, 0.9f, 1f, 0.8f);
            GUI.Label(new Rect(cx - R, cy + R + 2f * scale, R * 2f, 14f * scale), "LIDAR MAP · 90m", _mapStyle);
        }

        private static Vector2 RadarPos(Vector3 d, Vector3 fwd, Vector3 right, float range, float R, bool clamp, out bool inside)
        {
            float x = Vector3.Dot(d, right) / range * R, y = -Vector3.Dot(d, fwd) / range * R;
            Vector2 v = new Vector2(x, y);
            inside = Mathf.Abs(x) <= R && Mathf.Abs(y) <= R;
            if (!inside && clamp)
            {
                float m = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                v *= (R - 4f) / m;
                inside = true;
            }
            return v;
        }

        private static void Diamond(float x, float y, float r, Texture2D white)
        {
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
            GUI.DrawTexture(new Rect(x - r * 0.7f, y - r * 0.7f, r * 1.4f, r * 1.4f), white);
            GUI.matrix = m;
        }
    }
}
