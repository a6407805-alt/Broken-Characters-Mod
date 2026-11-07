using System.Collections.Generic;
using RoR2;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace Surveyor
{
    // v0.7.1: views 1/2/3 pulse the whole map; radar bottom-right with labels. v0.7.0: the LiDAR shows the whole stage's objectives: through-wall markers for chests, the teleporter, shrines,
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
            public int Cat;             // LiDAR view: 2 = items (chests, printers, drones, pickups), 3 = objectives
        }

        // ---------------------------------------------------------------- LiDAR views (1 monsters, 2 items, 3 objectives)

        public int View;                                   // 0 = mixed (until you pick one)
        public static readonly string[] ViewNames = { "ALL", "MONSTERS", "ITEMS", "OBJECTIVES" };
        public static readonly string[] ViewDescs = { "", "EVERY MONSTER ON THE MAP, THROUGH WALLS", "EVERY CHEST, PRINTER, DRONE AND ITEM", "TELEPORTER, SHRINES, PORTALS, ALTARS" };
        public static readonly Color[] ViewColors =
        {
            new Color(0.3f, 0.95f, 1f, 1f),
            new Color(1f, 0.22f, 0.18f, 1f),
            new Color(1f, 0.82f, 0.2f, 1f),
            new Color(0.78f, 0.38f, 1f, 1f)
        };
        private float _viewFlash = -10f, _waveStart = -100f;
        private int _waveView;
        private Vector3 _waveAt;
        private const float WaveSpeed = 140f, WaveMax = 650f, RevealHold = 30f;
        private readonly Dictionary<Object, float> _revealed = new Dictionary<Object, float>();

        private void ViewKeys()
        {
            int pick = 0;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) pick = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) pick = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) pick = 3;
            if (pick == 0) return;
            if (Peeking) EndPeek();
            View = pick;
            _viewFlash = Time.time;
            _waveStart = Time.time;
            _waveView = pick;
            _waveAt = _body.footPosition;
            _revealed.Clear();
            RefreshMap();
            Util.PlaySound(pick == 1 ? "Play_huntress_R_snipe_shoot" : pick == 2 ? "Play_item_proc_crit_cooldown" : "Play_item_use_EMPScanner_findChest", gameObject);
            Util.PlaySound("Play_item_use_EMPScanner_findChest", gameObject);
        }

        /// <summary>The pulse: a ring of coloured LiDAR dots rolls out across the whole stage; whatever it passes lights up.</summary>
        private void ViewTick(float dt)
        {
            float r = (Time.time - _waveStart) * WaveSpeed;
            if (r > WaveMax || _peekK > 0.65f) return;
            Color c = ViewColors[_waveView];
            Color hot = Color.Lerp(c, Color.white, 0.55f);
            int n = Mathf.Clamp(Mathf.RoundToInt(r * 0.9f), 60, 200);
            float spin = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = spin + i * Mathf.PI * 2f / n;
                Vector3 p = _waveAt + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                RaycastHit h;
                if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out h, 140f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    AddDot(h.point, (i & 3) == 0 ? hot : c, 1.1f, true);
                    if ((i & 7) == 0) AddDot(h.point + Vector3.up * Random.Range(0.6f, 2.5f), c, 0.6f, true);   // a thin wall of light
                }
            }
            float now = Time.time;
            Vector3 at = _waveAt;
            float r2 = r * r;
            if (_waveView == 1)
            {
                var all = CharacterBody.readOnlyInstancesList;
                for (int i = 0; i < all.Count; i++)
                {
                    CharacterBody b = all[i];
                    if (!b || !b.healthComponent || !b.healthComponent.alive || !b.teamComponent || b.teamComponent.teamIndex == TeamIndex.Player) continue;
                    if (_revealed.ContainsKey(b) || Flat2(b.corePosition - at) > r2) continue;
                    _revealed[b] = now;
                    Burst(b.corePosition, c, Mathf.Max(0.8f, b.radius));
                }
            }
            else
            {
                for (int i = 0; i < _map.Count; i++)
                {
                    MapPoi p = _map[i];
                    if (!p.Tr || p.Cat != _waveView || _revealed.ContainsKey(p.Tr) || Flat2(p.Pos - at) > r2) continue;
                    _revealed[p.Tr] = now;
                    Burst(p.Pos, c, p.Rank == 0 ? 3f : 0.9f);
                }
            }
        }

        private static float Flat2(Vector3 d) { d.y = 0f; return d.sqrMagnitude; }

        private void Burst(Vector3 at, Color c, float r)
        {
            for (int i = 0; i < 40; i++)
                AddDot(at + Random.onUnitSphere * r, (i & 1) == 0 ? c : Color.white, 2.2f, true);
        }

        private bool Revealed(Object o) => o && _revealed.TryGetValue(o, out float t) && Time.time - t < RevealHold;

        private static string Short(string label)
        {
            if (label == "TELEPORTER") return "TP";
            if (label.StartsWith("SHRINE")) return "SHRINE";
            if (label.StartsWith("PRINTER")) return "PRINTER";
            if (label == "LARGE CHEST") return "BIG CHEST";
            if (label == "LEGENDARY CHEST") return "LEGENDARY";
            if (label == "EQUIPMENT BARREL") return "EQUIP";
            return label;
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
                        _map.Add(new MapPoi { Tr = tp.transform, Pos = tp.transform.position + Vector3.up * 2f, Label = "TELEPORTER", Col = new Color(1f, 0.3f, 0.95f), Rank = 0, Cat = 3 });
                foreach (PurchaseInteraction pi in FindObjectsOfType<PurchaseInteraction>())
                {
                    if (!pi || !pi.gameObject.activeInHierarchy || !pi.available) continue;
                    string n = pi.gameObject.name.ToLowerInvariant();
                    if (n.Contains("teleporter")) continue;
                    Color c = MapCol(n, out string label, out int rank);
                    if (label == null) continue;
                    bool obj = label.Contains("SHRINE") || label.Contains("PORTAL") || label.Contains("NEWT") || label.Contains("CLEANSING");
                    _map.Add(new MapPoi { Tr = pi.transform, Pos = pi.transform.position + Vector3.up * 1f, Label = label, Col = c, Rank = rank, Cat = obj ? 3 : 2 });
                }
                foreach (BarrelInteraction bi in FindObjectsOfType<BarrelInteraction>())
                    if (bi && bi.gameObject.activeInHierarchy)
                        _map.Add(new MapPoi { Tr = bi.transform, Pos = bi.transform.position + Vector3.up * 0.6f, Label = "BARREL", Col = new Color(1f, 0.82f, 0.25f), Rank = 3, Cat = 2 });
            }
            catch { }
            try { AddPickups(); } catch { }
            try
            {
                foreach (SceneExitController se in FindObjectsOfType<SceneExitController>())
                    if (se && se.gameObject.activeInHierarchy && !se.GetComponent<TeleporterInteraction>())
                        _map.Add(new MapPoi { Tr = se.transform, Pos = se.transform.position + Vector3.up * 1.5f, Label = "PORTAL", Col = new Color(0.4f, 0.8f, 1f), Rank = 1, Cat = 3 });
            }
            catch { }
        }

        private static System.Reflection.FieldInfo _pickupField, _pickupInner;

        /// <summary>The pickup on a ground item, by reflection (the field moved between game versions).</summary>
        private static PickupIndex PickupOf(GenericPickupController g)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            if (_pickupField == null)
                _pickupField = typeof(GenericPickupController).GetField("pickupIndex", F) ?? typeof(GenericPickupController).GetField("_pickupIndex", F) ?? typeof(GenericPickupController).GetField("pickup", F) ?? typeof(GenericPickupController).GetField("_pickup", F);
            if (_pickupField == null) return PickupIndex.none;
            object v = _pickupField.GetValue(g);
            if (v is PickupIndex pi) return pi;
            if (v != null)
            {
                if (_pickupInner == null) _pickupInner = v.GetType().GetField("pickupIndex", F);
                if (_pickupInner != null && _pickupInner.GetValue(v) is PickupIndex pj) return pj;
            }
            return PickupIndex.none;
        }

        /// <summary>Items lying on the ground (their own method: if the game hides a field, only this part is skipped).</summary>
        private void AddPickups()
        {
            foreach (GenericPickupController g in FindObjectsOfType<GenericPickupController>())
            {
                if (!g || !g.gameObject.activeInHierarchy) continue;
                PickupDef pd = PickupCatalog.GetPickupDef(PickupOf(g));
                string name = pd != null ? Language.GetString(pd.nameToken) : "ITEM";
                Color col = pd != null ? pd.baseColor : Color.white;
                _map.Add(new MapPoi { Tr = g.transform, Pos = g.transform.position + Vector3.up * 0.5f, Label = name.ToUpperInvariant(), Col = col, Rank = 2, Cat = 2 });
            }
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
            // sick (RAD) enemies leak green dots, frozen ones crackle with ice-blue dots
            if (SvrAmmo.RadBuff != null)
            {
                var bodies = CharacterBody.readOnlyInstancesList;
                for (int i = 0; i < bodies.Count; i++)
                {
                    CharacterBody b = bodies[i];
                    if (!b || (b.corePosition - _body.corePosition).sqrMagnitude > 80f * 80f) continue;
                    if (b.HasBuff(SvrAmmo.RadBuff)) AddDot(b.corePosition + Random.insideUnitSphere * Mathf.Max(0.6f, b.radius), new Color(0.5f, 1f, 0.2f, 1f), 0.5f, true);
                    if (b.HasBuff(SvrAmmo.FrostBuff) || b.HasBuff(SvrAmmo.FrostLockBuff)) AddDot(b.corePosition + Random.insideUnitSphere * Mathf.Max(0.6f, b.radius), new Color(0.6f, 0.95f, 1f, 1f), 0.5f, true);
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

            _mapStyle.clipping = TextClipping.Overflow;
            _mapStyle.wordWrap = false;

            if (lidarOn && View == 0) DrawMixedMarkers(cam, scale, me, white);
            if (lidarOn && View > 0) DrawViewMarkers(cam, scale, me, white);

            // radar map (always): bottom right, just above the skill icons. You in the middle, facing up. Dots labelled.
            float R = 110f * scale;
            float cx = Screen.width - 70f * scale - R, cy = Screen.height - 200f * scale - R;
            float range = 90f;
            GUI.color = new Color(0.02f, 0.05f, 0.08f, 0.75f);
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
            _mapStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(10f * scale));
            _mapStyle.alignment = TextAnchor.MiddleLeft;
            int labels = 0;
            for (int i = 0; i < _map.Count; i++)
            {
                MapPoi p = _map[i];
                if (!p.Tr) continue;
                Vector2 rp = RadarPos(p.Pos - me, fwd, right, range, R, p.Rank == 0, out bool inside);
                if (!inside) continue;
                bool dim = View > 0 && p.Cat != View;
                GUI.color = dim ? new Color(p.Col.r, p.Col.g, p.Col.b, 0.3f) : p.Col;
                float sz = (p.Rank == 0 ? 6f : p.Rank == 3 ? 2.5f : 3.5f) * scale;
                if (p.Rank <= 1) Diamond(cx + rp.x, cy + rp.y, sz, white);
                else GUI.DrawTexture(new Rect(cx + rp.x - sz, cy + rp.y - sz, sz * 2f, sz * 2f), white);
                // labels: every objective, plus chests/items when that view is on (capped so the radar stays readable)
                bool label = !dim && p.Rank < 3 && (p.Rank <= 1 || View == 2) && labels < 14;
                if (label)
                {
                    labels++;
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.Label(new Rect(cx + rp.x + sz + 2f * scale + 1f, cy + rp.y - 7f * scale + 1f, 120f * scale, 14f * scale), Short(p.Label), _mapStyle);
                    GUI.color = Color.Lerp(p.Col, Color.white, 0.35f);
                    GUI.Label(new Rect(cx + rp.x + sz + 2f * scale, cy + rp.y - 7f * scale, 120f * scale, 14f * scale), Short(p.Label), _mapStyle);
                }
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
                    GUI.Label(new Rect(cx + rp.x + 9f * scale, cy + rp.y - 7f * scale, 120f * scale, 14f * scale), "STALKER", _mapStyle);
                    continue;
                }
                GUI.color = ally ? new Color(0.4f, 1f, 0.5f, 0.95f) : new Color(1f, 0.25f, 0.2f, 0.85f);
                float s2 = (ally ? 3f : 2f) * scale;
                if (!ally && b.isBoss) s2 = 4f * scale;
                GUI.DrawTexture(new Rect(cx + rp.x - s2, cy + rp.y - s2, s2 * 2f, s2 * 2f), white);
                if (ally || b.isBoss || b.isElite)
                {
                    string n = ally ? PlayerName(b) : (b.isBoss ? "BOSS" : "ELITE");
                    if (!string.IsNullOrEmpty(n))
                        GUI.Label(new Rect(cx + rp.x + s2 + 2f * scale, cy + rp.y - 7f * scale, 120f * scale, 14f * scale), n, _mapStyle);
                }
            }
            GUI.color = Color.white;
            Diamond(cx, cy, 4f * scale, white);

            // title + legend under the radar
            _mapStyle.alignment = TextAnchor.MiddleCenter;
            _mapStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(11f * scale));
            GUI.color = new Color(0.6f, 0.9f, 1f, 0.85f);
            GUI.Label(new Rect(cx - R, cy - R - 20f * scale, R * 2f, 18f * scale), "LIDAR MAP · 90m" + (View > 0 ? " · " + ViewNames[View] : ""), _mapStyle);
            _mapStyle.alignment = TextAnchor.MiddleLeft;
            _mapStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(10f * scale));
            float lx = cx - R, ly = cy + R + 5f * scale;
            Legend(ref lx, ly, new Color(1f, 0.25f, 0.2f), "ENEMY", false, scale, white);
            Legend(ref lx, ly, new Color(0.4f, 1f, 0.5f), "ALLY", false, scale, white);
            Legend(ref lx, ly, new Color(0.95f, 0.95f, 0.95f), "CHEST", false, scale, white);
            Legend(ref lx, ly, new Color(0.4f, 1f, 0.6f), "OBJECTIVE", true, scale, white);
            GUI.color = Color.white;
        }

        private void Legend(ref float x, float y, Color c, string text, bool diamond, float scale, Texture2D white)
        {
            GUI.color = c;
            if (diamond) Diamond(x + 4f * scale, y + 7f * scale, 4f * scale, white);
            else GUI.DrawTexture(new Rect(x + 1f * scale, y + 4f * scale, 6f * scale, 6f * scale), white);
            GUI.color = new Color(0.85f, 0.92f, 1f, 0.85f);
            Vector2 sz = _mapStyle.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(x + 10f * scale, y, sz.x + 4f, 14f * scale), text, _mapStyle);
            x += 10f * scale + sz.x + 8f * scale;
        }

        private static string PlayerName(CharacterBody b)
        {
            try { return b.master && b.master.playerCharacterMasterController ? b.GetUserName() : ""; } catch { return ""; }
        }

        /// <summary>No view picked: nearby things, ranked (the teleporter always, with an edge arrow).</summary>
        private void DrawMixedMarkers(Camera cam, float scale, Vector3 me, Texture2D white)
        {
            _mapStyle.alignment = TextAnchor.MiddleCenter;
            _mapStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(11f * scale));
            for (int i = 0; i < _map.Count; i++)
            {
                MapPoi p = _map[i];
                if (!p.Tr) continue;
                float d = Vector3.Distance(me, p.Pos);
                float maxD = p.Rank == 0 ? 9999f : p.Rank == 1 ? 140f : p.Rank == 2 ? 110f : 45f;
                if (d > maxD) continue;
                float fade = p.Rank == 0 ? 1f : Mathf.Clamp01((maxD - d) / 20f);
                bool label = p.Rank == 0 || d < 40f || (p.Rank == 1 && d < 80f);
                Marker(cam, scale, p.Pos, p.Col, fade, p.Rank == 0 ? 9f : 5f, label ? p.Label + "  " + Mathf.RoundToInt(d) + "m" : null, p.Rank == 0, white);
            }
        }

        /// <summary>A view is on: everything of that kind the pulse has passed, anywhere on the map, in the view's colour.</summary>
        private void DrawViewMarkers(Camera cam, float scale, Vector3 me, Texture2D white)
        {
            _mapStyle.alignment = TextAnchor.MiddleCenter;
            _mapStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(11f * scale));
            Color vc = ViewColors[View];
            if (View == 1)
            {
                var all = CharacterBody.readOnlyInstancesList;
                for (int i = 0; i < all.Count; i++)
                {
                    CharacterBody b = all[i];
                    if (!Revealed(b) || !b.healthComponent || !b.healthComponent.alive) continue;
                    float d = Vector3.Distance(me, b.corePosition);
                    bool big = b.isBoss || SvrStalker.IsStalker(b);
                    string label = (big || b.isElite || d < 45f) ? b.GetDisplayName().ToUpperInvariant() + "  " + Mathf.RoundToInt(d) + "m" : null;
                    Marker(cam, scale, b.corePosition, big ? new Color(1f, 0.1f, 0.6f) : vc, 1f, big ? 9f : 5f, label, big, white);
                }
                return;
            }
            for (int i = 0; i < _map.Count; i++)
            {
                MapPoi p = _map[i];
                if (!p.Tr || p.Cat != View || !Revealed(p.Tr)) continue;
                float d = Vector3.Distance(me, p.Pos);
                bool label = View == 3 || d < 70f || p.Rank <= 1;
                Color c = View == 2 ? Color.Lerp(vc, p.Col, 0.45f) : vc;
                Marker(cam, scale, p.Pos, c, 1f, p.Rank == 0 ? 9f : 5.5f, label ? p.Label + "  " + Mathf.RoundToInt(d) + "m" : null, View == 3, white);
            }
        }

        private void Marker(Camera cam, float scale, Vector3 pos, Color col, float alpha, float size, string label, bool edgeArrow, Texture2D white)
        {
            Vector3 sp = cam.WorldToScreenPoint(pos);
            bool behind = sp.z <= 0.3f;
            float x = sp.x, y = Screen.height - sp.y;
            if (behind || x < 0f || x > Screen.width || y < 0f || y > Screen.height)
            {
                if (!edgeArrow) return;
                Vector2 dir = new Vector2(x - Screen.width * 0.5f, y - Screen.height * 0.5f);
                if (behind) dir = -dir;
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                dir.Normalize();
                float ex = Screen.width * 0.5f + dir.x * Screen.width * 0.44f, ey = Screen.height * 0.5f + dir.y * Screen.height * 0.4f;
                GUI.color = col;
                Diamond(ex, ey, 8f * scale, white);
                if (label != null) GUI.Label(new Rect(ex - 110f * scale, ey + 10f * scale, 220f * scale, 18f * scale), label, _mapStyle);
                return;
            }
            GUI.color = new Color(col.r, col.g, col.b, 0.92f * alpha);
            Diamond(x, y, size * scale, white);
            if (label != null)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
                GUI.Label(new Rect(x - 110f * scale + 1f, y - 26f * scale + 1f, 220f * scale, 18f * scale), label, _mapStyle);
                GUI.color = new Color(Mathf.Lerp(col.r, 1f, 0.3f), Mathf.Lerp(col.g, 1f, 0.3f), Mathf.Lerp(col.b, 1f, 0.3f), alpha);
                GUI.Label(new Rect(x - 110f * scale, y - 26f * scale, 220f * scale, 18f * scale), label, _mapStyle);
            }
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
