using System;
using System.Collections.Generic;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    // Light base building around the control tower. Build mode (B): pick with 1-5, turn with R, place with F.
    //   1 Auto Turret     an Engineer turret that copies your items (like Engineer's)
    //   2 Barrier Wall    blocks the way; monsters shoot it
    //   3 Supply Depot    pays you money every few seconds
    //   4 Defender Squad  3 marines that guard the tower
    //   5 Escort Squad    2 marines that follow you into battle (your army)
    // Placement (inside the build radius, on flat ground, nothing in the way) is shown with a hologram; the
    // host checks it again, takes the money (costs scale with difficulty like chests) and builds it.
    internal static class SFBuilding
    {
        public enum Kind { Turret, Wall, Depot, Defenders, Escort }
        public static readonly Kind[] Kinds = { Kind.Turret, Kind.Wall, Kind.Depot, Kind.Defenders, Kind.Escort };

        public static int BaseCost(Kind k) => k switch
        {
            Kind.Turret => SFConfig.TurretCost.Value, Kind.Wall => SFConfig.WallCost.Value, Kind.Depot => SFConfig.DepotCost.Value,
            Kind.Defenders => SFConfig.DefenderCost.Value, _ => SFConfig.EscortCost.Value,
        };

        public static int Limit(Kind k) => k switch
        {
            Kind.Turret => SFConfig.TurretLimit.Value, Kind.Wall => SFConfig.WallLimit.Value, Kind.Depot => SFConfig.DepotLimit.Value,
            Kind.Defenders => SFConfig.DefenderLimit.Value, _ => SFConfig.EscortLimit.Value,
        };

        public static int Cost(Kind k) => Run.instance ? Run.instance.GetDifficultyScaledCost(BaseCost(k)) : BaseCost(k);
        public static bool Placed(Kind k) => k == Kind.Turret || k == Kind.Wall || k == Kind.Depot;

        public static void Init() { }

        // ================================================================== client

        public static bool Active { get; private set; }
        public static Kind Selected { get; private set; } = Kind.Turret;
        public static bool Valid { get; private set; }
        public static string Problem { get; private set; } = "";
        public static readonly int[] MyCounts = new int[5];
        private static float yaw;
        private static Vector3 placement;
        private static GameObject ghost;
        private static Kind ghostKind = (Kind)(-1);
        private static Material ghostMaterial;

        private static KeyCode Key(string name, KeyCode fallback)
        {
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), name, true); }
            catch { return fallback; }
        }

        public static void ClientUpdate()
        {
            if (!SFScene.Active || SFCutscene.Playing)
            {
                if (Active) Close();
                return;
            }
            var user = LocalUserManager.GetFirstLocalUser();
            CharacterBody body = user?.cachedBody;
            if (!body || !body.healthComponent || !body.healthComponent.alive)
            {
                if (Active) Close();
                return;
            }
            if (Input.GetKeyDown(Key(SFConfig.BuildKey.Value, KeyCode.B))) { if (Active) Close(); else Active = true; }
            if (!Active) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            for (int i = 0; i < Kinds.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Selected = Kinds[i];
            if (Input.GetKeyDown(Key(SFConfig.RotateKey.Value, KeyCode.R))) yaw = (yaw + 45f) % 360f;

            Evaluate(user, body);
            UpdateGhost();
            if (Input.GetKeyDown(Key(SFConfig.PlaceKey.Value, KeyCode.F)))
            {
                if (!Valid)
                {
                    SFHud.Notify(Problem, false);
                    return;
                }
                new BuildRequest { master = user.cachedMaster.netId, kind = (int)Selected, position = placement, yaw = yaw }.Send(NetworkDestination.Server);
            }
        }

        private static void Close()
        {
            Active = false;
            if (ghost) Object.Destroy(ghost);
            ghostKind = (Kind)(-1);
        }

        private static void Evaluate(LocalUser user, CharacterBody body)
        {
            Valid = false;
            Problem = "";
            var tower = SFStructure.Tower;
            if (!tower || !tower.Health || !tower.Health.alive) { Problem = Tokens.Get("BUILD_NO_TOWER"); return; }
            int cost = Cost(Selected);
            if (MyCounts[(int)Selected] >= Limit(Selected)) { Problem = Tokens.Get("BUILD_LIMIT"); return; }
            if (user.cachedMaster && user.cachedMaster.money < cost) { Problem = Tokens.Get("BUILD_MONEY"); }

            if (!Placed(Selected))
            {
                placement = Selected == Kind.Defenders ? tower.Body.footPosition : body.footPosition;
                Valid = Problem == "";
                return;
            }
            Camera cam = user.cameraRigController ? user.cameraRigController.sceneCam : Camera.main;
            if (!cam) return;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, 70f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                Problem = Tokens.Get("BUILD_AIM");
                return;
            }
            placement = hit.point;
            float r = SFConfig.BuildRadius.Value;
            Vector3 flat = placement - tower.Body.footPosition;
            flat.y = 0;
            if (flat.magnitude > r) { Problem = Tokens.Get("BUILD_RANGE"); return; }
            if (hit.normal.y < 0.8f) { Problem = Tokens.Get("BUILD_SLOPE"); return; }
            Vector3 half = Selected == Kind.Wall ? new Vector3(4f, 1.5f, 0.6f) : Selected == Kind.Depot ? new Vector3(3f, 1.8f, 3f) : new Vector3(1f, 1f, 1f);
            if (Physics.CheckBox(placement + Vector3.up * (half.y + 0.4f), half, Quaternion.Euler(0, yaw, 0),
                                 LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore))
            {
                Problem = Tokens.Get("BUILD_BLOCKED");
                return;
            }
            Valid = Problem == "";
        }

        private static void UpdateGhost()
        {
            if (!Placed(Selected))
            {
                if (ghost) Object.Destroy(ghost);
                ghostKind = (Kind)(-1);
                return;
            }
            if (ghostKind != Selected || !ghost)
            {
                if (ghost) Object.Destroy(ghost);
                ghostKind = Selected;
                GameObject prefab = Selected == Kind.Wall ? SFAssets.Wall : Selected == Kind.Depot ? SFAssets.Depot : null;
                ghost = prefab ? Object.Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                foreach (var c in ghost.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                if (!prefab) ghost.transform.localScale = new Vector3(1.6f, 1.2f, 1.6f);
                if (!ghostMaterial && SFEffects.LightningMaterial) ghostMaterial = new Material(SFEffects.LightningMaterial);
                foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            ghost.transform.position = placement + (ghostKind == Kind.Turret ? Vector3.up * 1.2f : Vector3.zero);
            ghost.transform.rotation = Quaternion.Euler(0, yaw, 0) * (SFAssets.Wall ? SFAssets.Wall.transform.rotation : Quaternion.identity);
            if (ghostMaterial && ghostMaterial.HasProperty("_TintColor"))
                ghostMaterial.SetColor("_TintColor", Valid ? new Color(0.3f, 1f, 0.45f, 0.8f) : new Color(1f, 0.25f, 0.2f, 0.8f));
            if (ghostMaterial) ghostMaterial.color = Valid ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.25f, 0.2f);
        }

        public static void ClientResult(BuildResult result)
        {
            var user = LocalUserManager.GetFirstLocalUser();
            if (user == null || !user.cachedMaster || user.cachedMaster.netId != result.master) return;
            if (result.token.StartsWith("COUNT:"))
            {
                var parts = result.token.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[1], out int k) && int.TryParse(parts[2], out int n) && k >= 0 && k < MyCounts.Length)
                    MyCounts[k] = n;
                return;
            }
            SFHud.Notify(Tokens.Get(result.token), result.ok);
            if (result.ok) SFAudio.Play(SFAudio.Clip.Build);
        }

        // ================================================================== server

        private class Built
        {
            public NetworkInstanceId owner;
            public Kind kind;
            public CharacterMaster master;
        }

        private static readonly List<Built> built = new List<Built>();

        public static void ServerHandle(BuildRequest req)
        {
            GameObject masterObject = Util.FindNetworkObject(req.master);
            var master = masterObject ? masterObject.GetComponent<CharacterMaster>() : null;
            if (!master || req.kind < 0 || req.kind >= Kinds.Length) return;
            Kind kind = (Kind)req.kind;
            string fail = Check(master, kind, req.position);
            if (fail != null)
            {
                Reply(master, fail, false);
                return;
            }
            int cost = Cost(kind);
            master.money = (uint)Mathf.Max(0, (int)master.money - cost);
            CharacterBody builder = master.GetBody();
            Quaternion rot = Quaternion.Euler(0, req.yaw, 0);
            var made = new List<CharacterMaster>();
            switch (kind)
            {
                case Kind.Turret:
                {
                    GameObject turret = SFUtil.Load<GameObject>(Keys.EngiTurretMaster, "EngiTurretMaster");
                    var m = new MasterSummon
                    {
                        masterPrefab = turret, position = req.position + Vector3.up * 0.2f, rotation = rot,
                        summonerBodyObject = builder ? builder.gameObject : null, teamIndexOverride = TeamIndex.Player,
                        ignoreTeamMemberLimit = true, inventoryToCopy = master.inventory,
                        inventoryItemCopyFilter = index => !ItemCatalog.GetItemDef(index) || !ItemCatalog.GetItemDef(index).ContainsTag(ItemTag.CannotCopy),
                    }.Perform();
                    made.Add(m);
                    break;
                }
                case Kind.Wall:
                    made.Add(SFStructures.Spawn(SFStructures.WallMaster, req.position, rot, TeamIndex.Player));
                    break;
                case Kind.Depot:
                {
                    var m = SFStructures.Spawn(SFStructures.DepotMaster, req.position, rot, TeamIndex.Player);
                    var marker = m ? m.GetBody()?.GetComponent<SFStructure>() : null;
                    if (marker) marker.owner = master.netId;
                    made.Add(m);
                    break;
                }
                case Kind.Defenders:
                case Kind.Escort:
                {
                    GameObject marine = SFUtil.Load<GameObject>(Keys.CommandoMonsterMaster, "CommandoMonsterMaster");
                    CharacterBody towerBody = SFStructure.Tower ? SFStructure.Tower.Body : null;
                    Vector3 around = kind == Kind.Defenders && towerBody ? towerBody.footPosition : req.position;
                    int count = kind == Kind.Defenders ? 3 : 2;
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 off = UnityEngine.Random.insideUnitCircle.normalized * (kind == Kind.Defenders ? 14f : 4f);
                        var m = new MasterSummon
                        {
                            masterPrefab = marine, position = SFUtil.Ground(around + new Vector3(off.x, 0, off.y), 10f) + Vector3.up * 0.5f, rotation = rot,
                            summonerBodyObject = kind == Kind.Escort && builder ? builder.gameObject : null,
                            teamIndexOverride = TeamIndex.Player, ignoreTeamMemberLimit = true,
                        }.Perform();
                        if (m && kind == Kind.Defenders && towerBody)
                            foreach (var ai in m.GetComponents<BaseAI>()) ai.leader.gameObject = towerBody.gameObject;
                        made.Add(m);
                    }
                    break;
                }
            }
            made.RemoveAll(m => !m);
            if (made.Count == 0)
            {
                master.GiveMoney((uint)cost);
                Reply(master, "BUILD_FAILED", false);
                return;
            }
            // A squad counts once: track only its first member.
            built.Add(new Built { owner = master.netId, kind = kind, master = made[0] });
            Reply(master, "BUILD_OK_" + kind.ToString().ToUpperInvariant(), true);
            SendCounts(master);
        }

        private static string Check(CharacterMaster master, Kind kind, Vector3 position)
        {
            var tower = SFStructure.Tower;
            if (!tower || !tower.Health || !tower.Health.alive) return "BUILD_NO_TOWER";
            if (Count(master.netId, kind) >= Limit(kind)) return "BUILD_LIMIT";
            if (master.money < Cost(kind)) return "BUILD_MONEY";
            if (Placed(kind))
            {
                Vector3 flat = position - tower.Body.footPosition;
                flat.y = 0;
                if (flat.magnitude > SFConfig.BuildRadius.Value + 5f) return "BUILD_RANGE";
            }
            return null;
        }

        private static int Count(NetworkInstanceId owner, Kind kind)
        {
            built.RemoveAll(b => !b.master || (!b.master.GetBody() && b.master.IsDeadAndOutOfLivesServer()));
            int n = 0;
            foreach (var b in built) if (b.owner == owner && b.kind == kind) n++;
            return n;
        }

        private static void Reply(CharacterMaster master, string token, bool ok) =>
            new BuildResult { master = master.netId, token = token, ok = ok }.Send(NetworkDestination.Clients);

        // The client keeps its own counts for the build panel: sent back as tokens "COUNT:k:n".
        private static void SendCounts(CharacterMaster master)
        {
            for (int k = 0; k < Kinds.Length; k++)
                new BuildResult { master = master.netId, token = $"COUNT:{k}:{Count(master.netId, (Kind)k)}", ok = true }.Send(NetworkDestination.Clients);
        }

        public static void ServerDepotIncome()
        {
            if (!Run.instance) return;
            uint pay = (uint)Mathf.Max(1, Run.instance.GetDifficultyScaledCost(Mathf.RoundToInt(SFConfig.DepotIncome.Value)));
            foreach (var s in SFStructure.All)
            {
                if (!s || s.kind != StructureKind.Depot || !s.Health || !s.Health.alive) continue;
                GameObject owner = Util.FindNetworkObject(s.owner);
                var m = owner ? owner.GetComponent<CharacterMaster>() : null;
                if (m) m.GiveMoney(pay);
            }
            // Refresh counts now and then (things die).
            foreach (var pc in PlayerCharacterMasterController.instances) if (pc.master) SendCounts(pc.master);
        }
    }
}
