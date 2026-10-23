// SPAWN tab (enemies, interactables, ally turrets) and WORLD tab (walls, ramps, stairs, terrain, water, jump pads).
// Everything is placed in front of you. Singleplayer host only (panel is hidden otherwise).
using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BCMDevTools
{
    public partial class DevToolsPlugin
    {
        // ================================================================ SPAWN tab
        static readonly (string label, string card)[] Interactables =
        {
            ("Gunner turret (buy)", "iscBrokenTurret1"), ("Gunner drone (buy)", "iscBrokenDrone1"),
            ("Healing drone (buy)", "iscBrokenDrone2"), ("Small chest", "iscChest1"), ("Large chest", "iscChest2"),
            ("Multishop", "iscTripleShop"), ("Printer", "iscDuplicator"), ("Scrapper", "iscScrapper"),
            ("Shrine of Chance", "iscShrineChance"), ("Shrine of Blood", "iscShrineBlood"),
            ("Equipment barrel", "iscEquipmentBarrel"), ("Lunar pod", "iscLunarChest"), ("Barrel (gold)", "iscBarrel1"),
        };
        readonly List<GameObject> _spawned = new List<GameObject>();

        void DrawSpawn(CharacterBody me)
        {
            GUILayout.Label("<b>ENEMIES</b>", _label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(26))) _spawnPick = (_spawnPick + SpawnNames.Length - 1) % SpawnNames.Length;
            if (GUILayout.Button("Spawn " + SpawnNames[_spawnPick] + " [F5]")) SpawnEnemy();
            if (GUILayout.Button(">", GUILayout.Width(26))) _spawnPick = (_spawnPick + 1) % SpawnNames.Length;
            if (GUILayout.Button(_dummyAi ? "AI: on" : "AI: off (dummy)", GUILayout.Width(110))) _dummyAi = !_dummyAi;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Kill all enemies [F7]")) KillAll();

            GUILayout.Space(6);
            GUILayout.Label("<b>ALLIES</b>", _label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ally turret (Engineer)")) SpawnAlly("EngiTurretMaster");
            if (GUILayout.Button("Ally drone")) SpawnAlly("Drone1Master");
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("<b>INTERACTABLES</b>  <color=#aaaaaa>(use + $1000 gold in ITEMS to buy)</color>", _label);
            for (int i = 0; i < Interactables.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(i + 2, Interactables.Length); j++)
                    if (GUILayout.Button(Interactables[j].label)) SpawnInteractable(Interactables[j].card);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Remove spawned interactables")) ClearSpawned();
        }

        /// <summary>A spot in front of you, dropped onto whatever ground is there.</summary>
        static bool InFront(float dist, out Vector3 pos, out Vector3 fwd)
        {
            pos = default; fwd = Vector3.forward;
            CharacterBody me = Me;
            if (!me) return false;
            fwd = me.inputBank ? me.inputBank.aimDirection : me.gameObject.transform.forward;
            fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            pos = me.footPosition + fwd * dist;
            if (Physics.Raycast(pos + Vector3.up * 15f, Vector3.down, out RaycastHit hit, 60f, LayerIndex.world.mask)) pos = hit.point;
            return true;
        }

        void SpawnInteractable(string card)
        {
            if (!NetworkServer.active || !InFront(8f, out Vector3 pos, out Vector3 fwd)) return;
            try
            {
                var isc = LegacyResourcesAPI.Load<InteractableSpawnCard>("SpawnCards/InteractableSpawnCard/" + card);
                if (!isc) { Logger.LogWarning("Interactable not found: " + card); return; }
                var rule = new DirectorPlacementRule { placementMode = DirectorPlacementRule.PlacementMode.Direct, position = pos };
                var req = new DirectorSpawnRequest(isc, rule, new Xoroshiro128Plus((ulong)DateTime.Now.Ticks));
                SpawnCard.SpawnResult res = isc.DoSpawn(pos, Quaternion.LookRotation(-fwd), req);
                if (res.spawnedInstance) _spawned.Add(res.spawnedInstance);
                else Logger.LogWarning("Interactable didn't spawn: " + card);
            }
            catch (Exception e) { Logger.LogError("Interactable " + card + ": " + e); }
        }

        void SpawnAlly(string masterName)
        {
            CharacterBody me = Me;
            if (!me || !NetworkServer.active || !InFront(5f, out Vector3 pos, out Vector3 fwd)) return;
            try
            {
                GameObject prefab = MasterCatalog.FindMasterPrefab(masterName);
                if (!prefab) { Logger.LogWarning("No master " + masterName); return; }
                new MasterSummon
                {
                    masterPrefab = prefab, position = pos + Vector3.up * 0.5f, rotation = Quaternion.LookRotation(fwd),
                    summonerBodyObject = me.gameObject, teamIndexOverride = TeamIndex.Player, ignoreTeamMemberLimit = true
                }.Perform();
            }
            catch (Exception e) { Logger.LogError("Ally " + masterName + ": " + e); }
        }

        void ClearSpawned()
        {
            foreach (GameObject g in _spawned) if (g) { if (NetworkServer.active) NetworkServer.Destroy(g); else Destroy(g); }
            _spawned.Clear();
        }

        // ================================================================ WORLD tab
        GameObject _props;   // parent of every block, cleared with "Remove props"
        Material _propMat, _waterMat, _padMat;

        void DrawWorld(CharacterBody me)
        {
            GUILayout.Label("<b>TEST FLOOR</b>", _label);
            if (GUILayout.Button("Go to flat world [F10]")) GoFlat();

            GUILayout.Space(6);
            GUILayout.Label("<b>COLLISION</b>  <color=#aaaaaa>(placed in front of you)</color>", _label);
            Row(("Wall", () => Block("Wall", 12f, new Vector3(12f, 6f, 1f), Vector3.zero, 0f)),
                ("Tall wall", () => Block("Tall wall", 12f, new Vector3(16f, 30f, 1f), Vector3.zero, 0f)),
                ("Pillar", () => Block("Pillar", 10f, new Vector3(2f, 12f, 2f), Vector3.zero, 0f)));
            Row(("Ramp (gentle)", () => Block("Ramp", 12f, new Vector3(6f, 0.5f, 16f), new Vector3(-15f, 0f, 0f), 2f)),
                ("Ramp (steep)", () => Block("Steep ramp", 12f, new Vector3(6f, 0.5f, 16f), new Vector3(-40f, 0f, 0f), 5f)),
                ("Stairs", Stairs));
            Row(("Ledge (8 m up)", () => Block("Ledge", 12f, new Vector3(8f, 1f, 8f), Vector3.zero, 8f)),
                ("Tunnel", Tunnel),
                ("Crates", Crates));

            GUILayout.Space(6);
            GUILayout.Label("<b>TERRAIN</b>", _label);
            Row(("Hill", () => Sphere("Hill", 20f, 30f, -11f)),
                ("Bumpy ground", Bumps),
                ("Water pool", Water));
            Row(("Jump pad", JumpPadProp), ("Remove props", ClearProps));
        }

        void Row(params (string label, Action act)[] buttons)
        {
            GUILayout.BeginHorizontal();
            foreach (var b in buttons)
                if (GUILayout.Button(b.label))
                {
                    try { b.act(); } catch (Exception e) { Logger.LogError(b.label + ": " + e); }
                }
            GUILayout.EndHorizontal();
        }

        Transform PropRoot()
        {
            if (!_props) _props = new GameObject("BCMDevTools_Props");
            return _props.transform;
        }

        Material MakeMat(Color tint, bool grid)
        {
            Shader sh = Shader.Find("Hopoo Games/Deferred/Standard");
            if (!sh) sh = LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/HGStandard");
            if (!sh) return null;
            var m = new Material(sh) { color = tint };
            if (grid) { m.mainTexture = GridTexture(); m.mainTextureScale = new Vector2(2f, 2f); }
            return m;
        }

        /// <summary>A solid box in front of you. tiltX tilts it (ramps); lift raises it off the ground.</summary>
        GameObject Block(string name, float dist, Vector3 size, Vector3 tilt, float lift)
        {
            if (!InFront(dist, out Vector3 pos, out Vector3 fwd)) return null;
            Quaternion rot = Quaternion.LookRotation(fwd) * Quaternion.Euler(tilt);
            return Box(name, pos + Vector3.up * (size.y * 0.5f + lift), size, rot);
        }

        GameObject Box(string name, Vector3 center, Vector3 size, Quaternion rot)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "BCM_" + name;
            g.layer = LayerIndex.world.intVal;
            g.transform.SetParent(PropRoot(), true);
            g.transform.position = center;
            g.transform.rotation = rot;
            g.transform.localScale = size;
            if (!_propMat) _propMat = MakeMat(new Color(0.85f, 0.75f, 0.6f), true);
            if (_propMat) g.GetComponent<Renderer>().sharedMaterial = _propMat;
            return g;
        }

        void Sphere(string name, float dist, float diameter, float sink)
        {
            if (!InFront(dist, out Vector3 pos, out _)) return;
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = "BCM_" + name;
            g.layer = LayerIndex.world.intVal;
            g.transform.SetParent(PropRoot(), true);
            g.transform.position = pos + Vector3.up * sink;
            g.transform.localScale = Vector3.one * diameter;
            if (!_propMat) _propMat = MakeMat(new Color(0.85f, 0.75f, 0.6f), true);
            if (_propMat) g.GetComponent<Renderer>().sharedMaterial = _propMat;
        }

        void Stairs()
        {
            if (!InFront(8f, out Vector3 pos, out Vector3 fwd)) return;
            Quaternion rot = Quaternion.LookRotation(fwd);
            for (int i = 0; i < 10; i++)
            {
                float h = 0.6f * (i + 1);
                Box("Step", pos + fwd * (i * 1.2f) + Vector3.up * (h * 0.5f), new Vector3(6f, h, 1.2f), rot);
            }
        }

        void Tunnel()
        {
            if (!InFront(12f, out Vector3 pos, out Vector3 fwd)) return;
            Quaternion rot = Quaternion.LookRotation(fwd);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Box("Tunnel wall", pos + right * 3f + Vector3.up * 2f, new Vector3(1f, 4f, 16f), rot);
            Box("Tunnel wall", pos - right * 3f + Vector3.up * 2f, new Vector3(1f, 4f, 16f), rot);
            Box("Tunnel roof", pos + Vector3.up * 4.5f, new Vector3(7f, 1f, 16f), rot);
        }

        void Crates()
        {
            if (!InFront(10f, out Vector3 pos, out Vector3 fwd)) return;
            Quaternion rot = Quaternion.LookRotation(fwd);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            for (int x = -1; x <= 1; x++)
                for (int y = 0; y < 3 - Math.Abs(x); y++)
                    Box("Crate", pos + right * (x * 2.2f) + Vector3.up * (1f + y * 2f), new Vector3(2f, 2f, 2f), rot);
        }

        void Bumps()
        {
            if (!InFront(20f, out Vector3 pos, out Vector3 fwd)) return;
            var rng = new System.Random();
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = pos + new Vector3((float)rng.NextDouble() * 30f - 15f, 0f, (float)rng.NextDouble() * 30f - 15f);
                float d = 6f + (float)rng.NextDouble() * 8f;
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "BCM_Bump"; g.layer = LayerIndex.world.intVal;
                g.transform.SetParent(PropRoot(), true);
                g.transform.position = p + Vector3.up * (-d * 0.3f);
                g.transform.localScale = new Vector3(d, d * 0.6f, d);
                if (!_propMat) _propMat = MakeMat(new Color(0.85f, 0.75f, 0.6f), true);
                if (_propMat) g.GetComponent<Renderer>().sharedMaterial = _propMat;
            }
        }

        /// <summary>A 16 m pool: blue surface you can walk into, slows you while you're in it (no collision).</summary>
        void Water()
        {
            if (!InFront(14f, out Vector3 pos, out _)) return;
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "BCM_Water";
            g.transform.SetParent(PropRoot(), true);
            g.transform.position = pos + Vector3.up * 0.75f;
            g.transform.localScale = new Vector3(16f, 1.5f, 16f);
            SetTrigger(g);
            if (!_waterMat) _waterMat = MakeMat(new Color(0.2f, 0.45f, 0.95f), false);
            if (_waterMat) g.GetComponent<Renderer>().sharedMaterial = _waterMat;
            g.AddComponent<DevWater>();
        }

        void JumpPadProp()
        {
            if (!InFront(8f, out Vector3 pos, out _)) return;
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "BCM_JumpPad";
            g.transform.SetParent(PropRoot(), true);
            g.transform.position = pos + Vector3.up * 0.3f;
            g.transform.localScale = new Vector3(3f, 0.3f, 3f);
            SetTrigger(g);
            if (!_padMat) _padMat = MakeMat(new Color(1f, 0.55f, 0.1f), false);
            if (_padMat) g.GetComponent<Renderer>().sharedMaterial = _padMat;
            g.AddComponent<DevJumpPad>();
        }

        static void SetTrigger(GameObject g)
        {
            Collider c = g.GetComponent<Collider>();
            if (c is MeshCollider) { Destroy(c); c = g.AddComponent<BoxCollider>(); }
            c.isTrigger = true;
            g.layer = LayerIndex.collideWithCharacterHullOnly.intVal;   // characters touch it, bullets don't
        }

        void ClearProps()
        {
            if (_props) Destroy(_props);
            _props = null;
        }
    }

    /// <summary>Slows any character standing in the water.</summary>
    public class DevWater : MonoBehaviour
    {
        void OnTriggerStay(Collider other)
        {
            if (!NetworkServer.active) return;
            CharacterBody b = other ? other.GetComponent<CharacterBody>() : null;
            if (b) b.AddTimedBuff(RoR2Content.Buffs.Slow50, 0.25f);
        }
    }

    /// <summary>Launches characters straight up.</summary>
    public class DevJumpPad : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            CharacterBody b = other ? other.GetComponent<CharacterBody>() : null;
            if (!b || !b.characterMotor || !b.hasEffectiveAuthority) return;
            CharacterMotor m = b.characterMotor;
            try { m.Motor.ForceUnground(); } catch { }
            m.velocity = new Vector3(m.velocity.x, 45f, m.velocity.z);
        }
    }
}
