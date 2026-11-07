using System.Collections.Generic;
using R2API;
using R2API.Networking;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Navigation;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Surveyor
{
    /// <summary>
    /// The Shimmer Stalker (v0.7.0): a real, networked raid boss everyone sees and fights. Built on the game's Imp Overlord
    /// (so hitboxes, animations, AI and networking just work), turned into a tall pitch-black shadow with glowing eyes.
    /// Night-stalker event: a few minutes into a stage the lights dim and it arrives near whoever strayed from the group,
    /// then keeps hunting the most isolated player. Below half health it drops low and charges much faster.
    /// Drops red items for the team when it dies.
    /// </summary>
    public static class SvrStalker
    {
        public const string BodyName = "BCShimmerStalkerBody";
        public static GameObject BodyPrefab, MasterPrefab;

        public static bool IsStalker(CharacterBody b) { return b && b.name.StartsWith(BodyName); }

        public static void Create()
        {
            GameObject srcBody = null, srcMaster = null;
            try { srcBody = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/ImpBossBody"); } catch { }
            try { srcMaster = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterMasters/ImpBossMaster"); } catch { }
            if (!srcBody || !srcMaster) { SvrPlugin.Log.LogWarning("Shimmer Stalker: Imp Overlord prefabs not found; the event is off."); return; }

            LanguageAPI.Add("BC_STALKER_NAME", "The Shimmer Stalker");
            LanguageAPI.Add("BC_STALKER_SUBTITLE", "It Hunts the Stragglers");

            BodyPrefab = PrefabAPI.InstantiateClone(srcBody, BodyName, true);
            CharacterBody b = BodyPrefab.GetComponent<CharacterBody>();
            b.baseNameToken = "BC_STALKER_NAME";
            b.subtitleNameToken = "BC_STALKER_SUBTITLE";
            float hp = SvrPlugin.StalkerHealthMult.Value;
            b.baseMaxHealth *= hp;
            b.levelMaxHealth *= hp;
            b.baseMoveSpeed *= 1.15f;
            b.baseArmor += SvrPlugin.StalkerArmor.Value;
            b.isChampion = true;
            BodyPrefab.AddComponent<SvrStalkerFx>();

            MasterPrefab = PrefabAPI.InstantiateClone(srcMaster, "BCShimmerStalkerMaster", true);
            MasterPrefab.GetComponent<CharacterMaster>().bodyPrefab = BodyPrefab;
            foreach (AISkillDriver d in MasterPrefab.GetComponents<AISkillDriver>())
                if (d.skillSlot == SkillSlot.Special) Object.DestroyImmediate(d);          // no teleporting: it walks you down
            MasterPrefab.AddComponent<SvrStalkerHunter>();

            ContentAddition.AddBody(BodyPrefab);
            ContentAddition.AddMaster(MasterPrefab);

            NetworkingAPI.RegisterMessageType<SvrStalkerEventMessage>();
            RoR2Application.onFixedUpdate += DirectorTick;
            Stage.onServerStageBegin += OnStage;
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
        }

        // ---------------------------------------------------------------- night-stalker director (host only)

        private static float _spawnAt = -1f;
        private static bool _spawnedThisStage;

        private static void OnStage(Stage s)
        {
            _spawnedThisStage = false;
            _spawnAt = Time.time + Random.Range(SvrPlugin.StalkerDelayMin.Value, Mathf.Max(SvrPlugin.StalkerDelayMin.Value, SvrPlugin.StalkerDelayMax.Value));
        }

        private static void DirectorTick()
        {
            if (!NetworkServer.active || !Run.instance || _spawnedThisStage || _spawnAt < 0f || Time.time < _spawnAt) return;
            _spawnAt = Time.time + 10f;                   // re-check every 10 s until it can spawn
            if (!SvrPlugin.StalkerEnabled.Value || !MasterPrefab) return;
            SceneDef sd = SceneCatalog.mostRecentSceneDef;
            if (!sd || sd.sceneType != SceneType.Stage) return;
            List<CharacterBody> players = AlivePlayers();
            if (players.Count == 0) return;
            if (SvrPlugin.StalkerOnlyWithSurveyor.Value)
            {
                bool any = false;
                foreach (CharacterBody p in players) if (p.name.StartsWith("BCSurveyorBody")) any = true;
                if (!any) return;
            }
            CharacterBody target = MostIsolated(players);
            Vector3 pos;
            if (!FindSpawn(target.footPosition, out pos)) return;
            _spawnedThisStage = true;
            try
            {
                CharacterMaster cm = new MasterSummon
                {
                    masterPrefab = MasterPrefab,
                    position = pos,
                    rotation = Quaternion.LookRotation(Flat(target.footPosition - pos)),
                    summonerBodyObject = null,
                    ignoreTeamMemberLimit = true,
                    teamIndexOverride = TeamIndex.Monster
                }.Perform();
                if (cm && cm.inventory)
                {
                    cm.inventory.GiveItem(RoR2Content.Items.UseAmbientLevel);
                    int extra = Mathf.Max(0, players.Count - 1);
                    if (extra > 0) cm.inventory.GiveItem(RoR2Content.Items.BoostHp, Mathf.RoundToInt(10f * SvrPlugin.StalkerPerPlayer.Value * extra));
                }
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<color=#b14bff>The lights are failing. Something tall is hunting whoever strays from the group...</color>" });
            }
            catch (System.Exception e) { SvrPlugin.Log.LogError("Shimmer Stalker spawn failed: " + e); }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.01f ? v.normalized : Vector3.forward; }

        public static List<CharacterBody> AlivePlayers()
        {
            List<CharacterBody> list = new List<CharacterBody>();
            foreach (PlayerCharacterMasterController pc in PlayerCharacterMasterController.instances)
            {
                CharacterBody b = pc && pc.master ? pc.master.GetBody() : null;
                if (b && b.healthComponent && b.healthComponent.alive) list.Add(b);
            }
            return list;
        }

        /// <summary>The player furthest from everyone else (alone = the only player).</summary>
        public static CharacterBody MostIsolated(List<CharacterBody> players)
        {
            CharacterBody best = players[0];
            float bestD = -1f;
            foreach (CharacterBody p in players)
            {
                float nearest = float.MaxValue;
                foreach (CharacterBody q in players)
                    if (q != p) nearest = Mathf.Min(nearest, (q.corePosition - p.corePosition).sqrMagnitude);
                if (nearest == float.MaxValue) nearest = 0f;
                if (nearest > bestD) { bestD = nearest; best = p; }
            }
            return best;
        }

        private static bool FindSpawn(Vector3 near, out Vector3 pos)
        {
            pos = near;
            NodeGraph ng = SceneInfo.instance ? SceneInfo.instance.groundNodes : null;
            for (int i = 0; i < 12; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector3 c = near + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(35f, 55f);
                if (ng)
                {
                    NodeGraph.NodeIndex ni = ng.FindClosestNode(c, HullClassification.Golem);
                    if (ni != NodeGraph.NodeIndex.invalid && ng.GetNodePosition(ni, out Vector3 np) && (np - near).sqrMagnitude > 20f * 20f) { pos = np; return true; }
                }
                RaycastHit h;
                if (Physics.Raycast(c + Vector3.up * 40f, Vector3.down, out h, 120f, LayerIndex.world.mask)) { pos = h.point; return true; }
            }
            return false;
        }

        // ---------------------------------------------------------------- loot

        private static void OnDeath(DamageReport r)
        {
            if (!NetworkServer.active || r == null || !IsStalker(r.victimBody) || !Run.instance) return;
            int count = Mathf.Clamp(AlivePlayers().Count, 1, 4);
            var pool = Run.instance.availableTier3DropList;
            Vector3 at = r.victimBody.corePosition + Vector3.up * 2f;
            for (int i = 0; i < count; i++)
            {
                PickupIndex drop = (pool != null && pool.Count > 0) ? pool[Random.Range(0, pool.Count)] : PickupCatalog.FindPickupIndex(ItemTier.Tier3);
                float ang = (i - (count - 1) * 0.5f) * 0.8f;
                PickupDropletController.CreatePickupDroplet(drop, at, Vector3.up * 18f + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 7f);
            }
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<color=#b14bff>The Shimmer Stalker collapses into static. The lights come back.</color>" });
        }
    }

    /// <summary>Host only: keeps the Stalker locked on the most isolated player, and drags it back if it gets left far behind.</summary>
    public class SvrStalkerHunter : MonoBehaviour
    {
        private CharacterMaster _master;
        private BaseAI _ai;
        private float _next, _nextCatchUp, _nextPounce, _nextShriek, _pounceAt, _shriekHitAt, _pounceWindEnd, _shriekAt;
        private Vector3 _pounceSpot;
        private bool _raged, _pouncing;
        private CharacterBody _prey;

        private void Awake()
        {
            _master = GetComponent<CharacterMaster>();
            _ai = GetComponent<BaseAI>();
        }

        private bool _loggedAttack;

        // pounce: a huge leap onto its prey that smashes everyone where it lands; shriek: a scream that hurts and cripples
        private void Attacks(CharacterBody me)
        {
            float now = Time.time;
            CharacterMotor motor = me.characterMotor;
            if (_nextPounce <= 0f) { _nextPounce = now + Random.Range(8f, 12f); _nextShriek = now + Random.Range(10f, 16f); }

            if (_pouncing)
            {
                if (now - _pounceAt > 0.35f && (!motor || motor.isGrounded || now - _pounceAt > 2.5f))
                {
                    _pouncing = false;
                    new BlastAttack
                    {
                        attacker = me.gameObject, inflictor = me.gameObject, teamIndex = TeamIndex.Monster,
                        baseDamage = me.damage * 5f, baseForce = 3000f, bonusForce = Vector3.up * 1500f,
                        position = me.footPosition, radius = 11f, falloffModel = BlastAttack.FalloffModel.Linear,
                        procCoefficient = 1f, damageType = DamageType.Generic
                    }.Fire();
                    GameObject fx = null;
                    try { fx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/PodGroundImpact"); } catch { }
                    if (!fx) { try { fx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX"); } catch { } }
                    if (fx) { try { EffectManager.SpawnEffect(fx, new EffectData { origin = me.footPosition, scale = 11f }, true); } catch { } }
                    SvrStalkerEventMessage.Broadcast(me.gameObject, 2);
                }
                return;
            }

            CharacterBody prey = _prey;
            if (!prey || !prey.healthComponent || !prey.healthComponent.alive) return;
            Vector3 to = prey.footPosition - me.footPosition;
            float flat = new Vector2(to.x, to.z).magnitude;

            // shriek: rears back for 0.9 s (purple ring = how far it reaches), then screams
            if (now >= _nextShriek && flat < 35f && _pounceWindEnd <= 0f)
            {
                _nextShriek = now + Random.Range(16f, 24f);
                _shriekAt = now + 0.9f;
                SvrStalkerEventMessage.Broadcast(me.gameObject, 5, me.footPosition, 35f);
            }
            if (_shriekAt > 0f && now >= _shriekAt)
            {
                _shriekAt = 0f;
                _shriekHitAt = now + 0.1f;
                SvrStalkerEventMessage.Broadcast(me.gameObject, 3);
            }
            if (_shriekHitAt > 0f && now >= _shriekHitAt)
            {
                _shriekHitAt = 0f;
                foreach (CharacterBody p in SvrStalker.AlivePlayers())
                {
                    if ((p.corePosition - me.corePosition).sqrMagnitude > 35f * 35f) continue;
                    p.AddTimedBuff(RoR2Content.Buffs.Cripple, 4f);
                    try
                    {
                        p.healthComponent.TakeDamage(new DamageInfo
                        {
                            attacker = me.gameObject, inflictor = me.gameObject, damage = me.damage * 1.5f,
                            position = p.corePosition, procCoefficient = 0f, damageType = DamageType.NonLethal, damageColorIndex = DamageColorIndex.Default
                        });
                    }
                    catch { }
                }
            }

            // pounce: coils for 0.95 s while a red ring marks the spot it will land on (dodge it!), then leaps there
            if (_pounceWindEnd <= 0f && _shriekAt <= 0f && now >= _nextPounce && motor && motor.isGrounded && flat > 10f && flat < 45f)
            {
                _nextPounce = now + (_raged ? Random.Range(6f, 9f) : Random.Range(10f, 14f));
                _pounceSpot = prey.footPosition;
                _pounceWindEnd = now + 0.95f;
                SvrStalkerEventMessage.Broadcast(me.gameObject, 4, _pounceSpot, 11f);
            }
            if (_pounceWindEnd > 0f && now >= _pounceWindEnd && motor)
            {
                _pounceWindEnd = 0f;
                to = _pounceSpot - me.footPosition;
                const float T = 0.95f;
                float g = Mathf.Abs(Physics.gravity.y);
                Vector3 v = new Vector3(to.x, 0f, to.z) / T;
                v.y = to.y / T + 0.5f * g * T;
                try { motor.Motor.ForceUnground(); } catch { }
                motor.velocity = v;
                _pouncing = true;
                _pounceAt = now;
                SvrStalkerEventMessage.Broadcast(me.gameObject, 1);
            }
        }

        private void Retarget(CharacterBody t)
        {
            if (_ai == null || _ai.currentEnemy == null) return;
            _ai.currentEnemy.gameObject = t.gameObject;
            try { SetBestHurtBox(t); } catch { }
        }

        private void SetBestHurtBox(CharacterBody t) { _ai.currentEnemy.bestHurtBox = t.mainHurtBox; }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !_master) return;
            CharacterBody me = _master.GetBody();
            if (!me || !me.healthComponent || !me.healthComponent.alive) return;
            try { Attacks(me); } catch (System.Exception e) { if (!_loggedAttack) { _loggedAttack = true; SvrPlugin.Log.LogError("Stalker attacks: " + e); } }
            if (Time.time < _next) return;
            _next = Time.time + 2f;

            // crawler phase: much faster once it is below half health
            if (!_raged && me.healthComponent.combinedHealthFraction < 0.5f && _master.inventory)
            {
                _raged = true;
                _master.inventory.GiveItem(RoR2Content.Items.Hoof, 8);
                _master.inventory.GiveItem(RoR2Content.Items.Syringe, 5);
            }

            List<CharacterBody> players = SvrStalker.AlivePlayers();
            if (players.Count == 0) return;
            CharacterBody t = SvrStalker.MostIsolated(players);
            _prey = t;
            if (t) { try { Retarget(t); } catch { } }      // separate method: if the game blocks a setter, only this call fails
            // left far behind? it reappears behind its prey, out of sight (at most every 25 s)
            if (t && Time.time >= _nextCatchUp && (me.corePosition - t.corePosition).sqrMagnitude > 95f * 95f)
            {
                _nextCatchUp = Time.time + 25f;
                Vector3 back = t.footPosition - t.gameObject.transform.forward * 30f;
                NodeGraph ng = SceneInfo.instance ? SceneInfo.instance.groundNodes : null;
                if (ng)
                {
                    NodeGraph.NodeIndex ni = ng.FindClosestNode(back, HullClassification.Golem);
                    if (ni != NodeGraph.NodeIndex.invalid && ng.GetNodePosition(ni, out Vector3 np)) back = np;
                }
                try { TeleportHelper.TeleportBody(me, back); } catch { }
            }
        }
    }

    /// <summary>
    /// On every client: turns the Imp Overlord into a pitch-black shadow with glowing red eyes, dims the stage lights while
    /// it lives, shrieks when it arrives, drops low and roars below half health, fills your screen with static and a
    /// heartbeat vignette when it is close, and draws its boss bar.
    /// </summary>
    public class SvrStalkerFx : MonoBehaviour
    {
        private CharacterBody _body;
        private Transform _model, _head;
        private Vector3 _baseScale;
        private Transform[] _eyes = new Transform[2];
        private Light _eyeLight;
        private bool _looked, _raged, _custom;
        private SvrStalkerRig _rig;
        private float _jawOpen, _jawTarget, _nextSnap, _nextGrowl;
        private float _rageK, _dim;
        private GUIStyle _style;

        // lights we dimmed (restored when it dies or the stage ends)
        private static readonly List<Light> _lights = new List<Light>();
        private static readonly List<float> _lightBase = new List<float>();
        private static Color _ambBase;
        private static float _ambIntBase;
        private static string _dimScene;
        private static int _dimUsers;

        private void Start()
        {
            _body = GetComponent<CharacterBody>();
            Shriek();
            BeginDim();
        }

        private void OnDestroy() { EndDim(); }

        private void Shriek()
        {
            LocalUser lu = LocalUserManager.GetFirstLocalUser();
            GameObject at = lu != null && lu.cachedBody ? lu.cachedBody.gameObject : gameObject;
            Util.PlaySound("Play_imp_overlord_spawn", at);       // heard right next to you, wherever it is
            Util.PlaySound("Play_imp_overlord_spawn", gameObject);
        }

        private void Look()
        {
            _model = SvrPlugin.Model(gameObject);
            if (!_model) return;
            _looked = true;
            try { _rig = SvrStalkerRig.Build(gameObject, _model); }
            catch (System.Exception e) { SvrPlugin.Log.LogError("Stalker model: " + e); _rig = null; }
            _custom = _rig != null;
            if (!_custom) _model.localScale *= SvrPlugin.StalkerScale.Value;     // the rig sizes itself
            _baseScale = _model.localScale;
            CharacterModel cm = _model.GetComponent<CharacterModel>();
            if (!_custom && cm && cm.baseRendererInfos != null)
            {
                for (int i = 0; i < cm.baseRendererInfos.Length; i++)
                {
                    Material src = cm.baseRendererInfos[i].defaultMaterial;
                    if (!src) continue;
                    Material m = new Material(src) { name = "ShimmerStalkerShadow" };
                    if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.03f, 0.02f, 0.05f, 1f));
                    if (m.HasProperty("_EmColor")) m.SetColor("_EmColor", new Color(0.35f, 0.03f, 0.25f, 1f));
                    if (m.HasProperty("_EmPower")) m.SetFloat("_EmPower", 0.35f);
                    if (m.HasProperty("_SpecularStrength")) m.SetFloat("_SpecularStrength", 0f);
                    cm.baseRendererInfos[i].defaultMaterial = m;
                }
            }
            ChildLocator cl = _model.GetComponent<ChildLocator>();
            _head = _rig ? _rig.Head : (cl ? cl.FindChild("Head") : null);
            if (!_head)
                foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
                    if (t.name.ToLowerInvariant().Contains("head")) { _head = t; break; }
            Shader hg = Shader.Find("Hopoo Games/Deferred/Standard");
            Material eyeMat = hg ? new Material(hg) : null;
            if (eyeMat)
            {
                eyeMat.SetColor("_Color", new Color(1f, 0.08f, 0.05f));
                if (eyeMat.HasProperty("_EmColor")) eyeMat.SetColor("_EmColor", new Color(1f, 0.08f, 0.05f));
                if (eyeMat.HasProperty("_EmTex")) eyeMat.SetTexture("_EmTex", Texture2D.whiteTexture);
                if (eyeMat.HasProperty("_EmPower")) eyeMat.SetFloat("_EmPower", 14f);
            }
            for (int i = 0; i < 2 && !_custom; i++)          // the Blender model has its own six eyes
            {
                GameObject e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                e.name = "StalkerEye" + i;
                Object.Destroy(e.GetComponent<Collider>());
                if (eyeMat) e.GetComponent<Renderer>().sharedMaterial = eyeMat;
                e.transform.SetParent(_model, false);
                _eyes[i] = e.transform;
            }
            GameObject lg = new GameObject("StalkerEyeLight");
            lg.transform.SetParent(_model, false);
            _eyeLight = lg.AddComponent<Light>();
            _eyeLight.type = LightType.Point;
            _eyeLight.color = new Color(1f, 0.1f, 0.06f);
            _eyeLight.range = 7f;
            _eyeLight.intensity = 3f;
        }

        private void LateUpdate()
        {
            if (!_body) return;
            if (!_looked) Look();
            if (!_model) return;
            float dt = Time.deltaTime;
            bool rage = _body.healthComponent && _body.healthComponent.combinedHealthFraction < 0.5f;
            if (rage && !_raged)
            {
                _raged = true;
                Shriek();
                Util.PlaySound("Play_imp_overlord_attack1_throw", gameObject);
            }
            _rageK = Mathf.MoveTowards(_rageK, _raged ? 1f : 0f, dt * 1.5f);
            // crawler phase: the rig drops to all fours (only the old fallback body squashes)
            if (!_custom) _model.localScale = Vector3.Scale(_baseScale, new Vector3(1f + 0.1f * _rageK, 1f - 0.22f * _rageK, 1f + 0.2f * _rageK));

            // a growl now and then when it is near you
            LocalUser lu0 = LocalUserManager.GetFirstLocalUser();
            if (lu0 != null && lu0.cachedBody && Time.time >= _nextGrowl && (lu0.cachedBody.corePosition - _body.corePosition).sqrMagnitude < 70f * 70f)
            {
                _nextGrowl = Time.time + Random.Range(9f, 18f);
                Util.PlaySound("Play_imp_overlord_idle_VO", gameObject);
            }

            float s = Mathf.Abs(_model.lossyScale.x);
            if (_rig) s = 1f;
            Vector3 hp = _head ? _head.position + (_rig ? _head.forward * 0.6f * _model.lossyScale.x : Vector3.zero) : _model.position + _model.up * 3f * s;
            for (int i = 0; i < 2; i++)
            {
                if (!_eyes[i]) continue;
                _eyes[i].position = hp + _model.forward * (0.45f * s) + _model.right * ((i == 0 ? -0.14f : 0.14f) * s);
                float flick = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, i);
                _eyes[i].localScale = Vector3.one * (0.11f * flick);
            }
            if (_eyeLight)
            {
                _eyeLight.transform.position = hp + _model.forward * (0.6f * s);
                _eyeLight.intensity = (2.5f + 2f * _rageK) * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 4f, 7f));
            }
            TickDim(dt);
        }

        // ---------------------------------------------------------------- lights out

        private void BeginDim()
        {
            if (_dimUsers++ > 0) return;
            _dimScene = SceneManager.GetActiveScene().name;
            _lights.Clear(); _lightBase.Clear();
            foreach (Light l in Object.FindObjectsOfType<Light>())
                if (l && l.type == LightType.Directional) { _lights.Add(l); _lightBase.Add(l.intensity); }
            _ambBase = RenderSettings.ambientLight;
            _ambIntBase = RenderSettings.ambientIntensity;
        }

        private void TickDim(float dt)
        {
            _dim = Mathf.MoveTowards(_dim, 1f, dt / 4f);                 // the lights fail over ~4 s
            if (SceneManager.GetActiveScene().name != _dimScene) return;
            float k = Mathf.Lerp(1f, SvrPlugin.StalkerDarkness.Value, _dim);
            for (int i = 0; i < _lights.Count; i++) if (_lights[i]) _lights[i].intensity = _lightBase[i] * k;
            RenderSettings.ambientLight = _ambBase * k;
            RenderSettings.ambientIntensity = _ambIntBase * k;
        }

        private static void EndDim()
        {
            if (--_dimUsers > 0) return;
            _dimUsers = 0;
            if (SceneManager.GetActiveScene().name != _dimScene) { _lights.Clear(); _lightBase.Clear(); return; }
            for (int i = 0; i < _lights.Count; i++) if (_lights[i]) _lights[i].intensity = _lightBase[i];
            RenderSettings.ambientLight = _ambBase;
            RenderSettings.ambientIntensity = _ambIntBase;
            _lights.Clear(); _lightBase.Clear();
        }

        // ---------------------------------------------------------------- dread + boss bar (every player)

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !_body || !_body.healthComponent || !_body.healthComponent.alive) return;
            float scale = Screen.height / 1080f;
            Texture2D white = Texture2D.whiteTexture;
            Color old = GUI.color;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow, wordWrap = false };

            // dread: heartbeat vignette + static when it is close to you
            LocalUser lu = LocalUserManager.GetFirstLocalUser();
            CharacterBody me = lu != null ? lu.cachedBody : null;
            if (me)
            {
                float d = Vector3.Distance(me.corePosition, _body.corePosition);
                float k = Mathf.Clamp01(1f - (d - 6f) / 34f);
                if (k > 0.01f)
                {
                    float bpm = 70f + 90f * k;
                    float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * bpm / 60f * Mathf.PI)), 8f);
                    float a = (0.25f + 0.3f * beat) * k;
                    for (int i = 0; i < 6; i++)
                    {
                        float inset = i * 0.03f;
                        float bx = Screen.width * inset, by = Screen.height * inset;
                        GUI.color = new Color(0.05f, 0f, 0.02f, a * (1f - i / 6f));
                        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, by + 8f), white);
                        GUI.DrawTexture(new Rect(0f, Screen.height - by - 8f, Screen.width, by + 8f), white);
                        GUI.DrawTexture(new Rect(0f, 0f, bx + 8f, Screen.height), white);
                        GUI.DrawTexture(new Rect(Screen.width - bx - 8f, 0f, bx + 8f, Screen.height), white);
                    }
                    int noise = Mathf.RoundToInt(60f * k * k);
                    for (int i = 0; i < noise; i++)
                    {
                        GUI.color = new Color(Random.value, Random.value * 0.4f, Random.value, 0.25f * k);
                        GUI.DrawTexture(new Rect(Random.value * Screen.width, Random.value * Screen.height, Random.Range(20f, 220f) * scale, Random.Range(1f, 3f) * scale), white);
                    }
                }
            }

            GUI.color = old;
        }
    }
}
