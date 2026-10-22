using System;
using System.Collections.Generic;
using System.Reflection;
using EntityStates;
using KinematicCharacterController;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Skills;
using SpecialForces.States;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    // The Hurricane: a Stone Titan's body and AI (the game's giant-monster plumbing: big hull, motor, boss AI)
    // with the custom storm-beast model, its own skills, millions of health and a boss bar.
    internal static class SFBoss
    {
        public static GameObject BodyPrefab, MasterPrefab, BossGroupPrefab, CyclonePrefab;
        public const float AuthoredHeight = 6f;   // the model's height at scale 1 (sf_boss.py)
        public static float Scale => SFConfig.BossHeight.Value / AuthoredHeight;

        public static void Init()
        {
            foreach (var t in new[] { typeof(HurricaneSpawn), typeof(HurricaneClaw), typeof(HurricaneBarrage), typeof(HurricaneCyclone),
                                      typeof(HurricaneRoar), typeof(HurricaneSquall), typeof(HurricaneDeath) })
                ContentAddition.AddEntityState(t, out _);
            CreateBody();
            CreateMaster();
            CreateBossGroup();
            CreateCyclone();
        }

        // ------------------------------------------------------------------ body

        private static void CreateBody()
        {
            GameObject titan = SFUtil.Load<GameObject>(Keys.TitanBody, "TitanBody");
            if (!titan) return;
            BodyPrefab = PrefabAPI.InstantiateClone(titan, "SFHurricaneBody", true);
            var body = BodyPrefab.GetComponent<CharacterBody>();
            body.baseNameToken = Tokens.Prefix + "HURRICANE_NAME";
            body.subtitleNameToken = Tokens.Prefix + "HURRICANE_SUBTITLE";
            body.baseMaxHealth = SFConfig.BossHealth.Value;
            body.levelMaxHealth = SFConfig.BossHealthPerLevel.Value;
            body.baseArmor = SFConfig.BossArmor.Value;
            body.baseDamage = SFConfig.BossDamage.Value;
            body.levelDamage = SFConfig.BossDamagePerLevel.Value;
            body.baseMoveSpeed = SFConfig.BossMoveSpeed.Value;
            body.baseAcceleration = 25f;
            body.baseRegen = 0f;
            body.levelRegen = 0f;
            body.isChampion = true;
            body.hullClassification = HullClassification.BeetleQueen;
            body.bodyColor = new Color(0.45f, 0.75f, 1f);
            body.bodyFlags |= CharacterBody.BodyFlags.ImmuneToExecutes | CharacterBody.BodyFlags.IgnoreFallDamage | SFUtil.IgnoreKnockback;
            body.portraitIcon = IconFactory.Solid(new Color(0.25f, 0.4f, 0.6f));
            var motor = BodyPrefab.GetComponent<CharacterMotor>();
            if (motor) motor.mass = 5000f;
            foreach (var hurt in BodyPrefab.GetComponents<SetStateOnHurt>())
            {
                hurt.canBeStunned = false;
                hurt.canBeFrozen = false;
                hurt.canBeHitStunned = false;
            }

            // State machines: spawn by roaring, then walk; skills run on the Body machine (it stops to attack).
            foreach (var esm in BodyPrefab.GetComponents<EntityStateMachine>())
            {
                if (esm.customName != "Body") continue;
                esm.initialStateType = new SerializableEntityStateType(typeof(HurricaneSpawn));
                esm.mainStateType = new SerializableEntityStateType(typeof(GenericCharacterMain));
            }
            var death = BodyPrefab.GetComponent<CharacterDeathBehavior>();
            if (death) death.deathState = new SerializableEntityStateType(typeof(HurricaneDeath));

            CreateSkills(BodyPrefab);
            if (SFAssets.Hurricane) SwapModel(BodyPrefab);
            else SpecialForcesPlugin.Log.LogWarning("Hurricane model missing (bundle): it will look like a Stone Titan.");

            // A capsule sized for a 24 m beast (the motor and the monster pathing use it).
            var kcm = BodyPrefab.GetComponent<KinematicCharacterMotor>();
            float h = SFConfig.BossHeight.Value;
            if (kcm)
            {
                try { kcm.SetCapsuleDimensions(h * 0.3f, h * 0.9f, h * 0.45f); }
                catch
                {
                    // On a prefab the motor may not have its capsule yet: set the numbers it reads in Awake.
                    kcm.CapsuleRadius = h * 0.3f;
                    kcm.CapsuleHeight = h * 0.9f;
                    kcm.CapsuleYOffset = h * 0.45f;
                    var capsule = BodyPrefab.GetComponent<CapsuleCollider>();
                    if (capsule)
                    {
                        capsule.radius = h * 0.3f;
                        capsule.height = h * 0.9f;
                        capsule.center = new Vector3(0, h * 0.45f, 0);
                    }
                }
            }
            ContentAddition.AddBody(BodyPrefab);
        }

        private static void CreateSkills(GameObject prefab)
        {
            foreach (var old in prefab.GetComponents<GenericSkill>()) Object.DestroyImmediate(old);
            var locator = prefab.GetComponent<SkillLocator>();
            locator.primary = Skill(prefab, "HurricaneClaw", typeof(HurricaneClaw), 3f);
            locator.secondary = Skill(prefab, "HurricaneBarrage", typeof(HurricaneBarrage), 14f);
            locator.utility = Skill(prefab, "HurricaneCyclone", typeof(HurricaneCyclone), 20f);
            locator.special = Skill(prefab, "HurricaneRoar", typeof(HurricaneRoar), 26f);
        }

        private static GenericSkill Skill(GameObject prefab, string name, Type state, float cooldown)
        {
            var def = ScriptableObject.CreateInstance<SkillDef>();
            ((ScriptableObject)def).name = "SF" + name;
            def.skillName = name;
            def.skillNameToken = Tokens.Prefix + name.ToUpperInvariant() + "_NAME";
            def.skillDescriptionToken = Tokens.Prefix + name.ToUpperInvariant() + "_NAME";
            def.activationState = new SerializableEntityStateType(state);
            def.activationStateMachineName = "Body";
            def.baseRechargeInterval = cooldown;
            def.baseMaxStock = 1;
            def.rechargeStock = 1;
            def.requiredStock = 1;
            def.stockToConsume = 1;
            def.isCombatSkill = true;
            def.interruptPriority = InterruptPriority.Skill;
            def.beginSkillCooldownOnSkillEnd = false;
            def.fullRestockOnAssign = true;
            def.cancelSprintingOnActivation = true;
            ContentAddition.AddSkillDef(def);
            var family = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)family).name = "SF" + name + "Family";
            family.variants = new[] { new SkillFamily.Variant { skillDef = def, viewableNode = new ViewablesCatalog.Node(def.skillName, false, null) } };
            ContentAddition.AddSkillFamily(family);
            var gs = prefab.AddComponent<GenericSkill>();
            gs.skillName = name;
            SFUtil.SetField(gs, "_skillFamily", family);
            return gs;
        }

        // Replace the Titan's model under ModelBase with the storm beast (as the survivors' CustomModel does).
        private static void SwapModel(GameObject prefab)
        {
            var locator = prefab.GetComponent<ModelLocator>();
            Transform oldModel = locator.modelTransform;
            Transform modelBase = locator.modelBaseTransform ? locator.modelBaseTransform : oldModel.parent;
            var references = FindReferencesInto(prefab, oldModel);
            Vector3 oldPosition = oldModel.localPosition;
            Object.DestroyImmediate(oldModel.gameObject);

            // Upright container (the ModelLocator overwrites its rotation) holding the FBX, which keeps its own.
            GameObject model = SFModelContainer.Make("mdlHurricane", modelBase);
            model.transform.localPosition = oldPosition;
            model.transform.localScale = Vector3.one * (Scale / Mathf.Max(0.0001f, modelBase.lossyScale.x));
            GameObject visual = Object.Instantiate(SFAssets.Hurricane, model.transform, false);
            visual.name = "Hurricane";
            model.GetComponent<SFModelContainer>().animator = visual.GetComponentInChildren<Animator>(true);
            locator.modelTransform = model.transform;
            SFAssets.RegisterForConversion(model);

            var characterModel = model.AddComponent<CharacterModel>();
            characterModel.body = prefab.GetComponent<CharacterBody>();
            characterModel.autoPopulateLightInfos = true;
            var infos = new List<CharacterModel.RendererInfo>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                infos.Add(new CharacterModel.RendererInfo { renderer = r, defaultMaterial = r.sharedMaterial, defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On });
            characterModel.baseRendererInfos = infos.ToArray();

            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            var pairs = new List<ChildLocator.NameTransformPair>();
            void Add(string n, string bone) { if (bones.TryGetValue(bone, out var t)) pairs.Add(new ChildLocator.NameTransformPair { name = n, transform = t }); }
            Add("Head", "head"); Add("Jaw", "jaw"); Add("Chest", "chest"); Add("Base", "base"); Add("Pelvis", "pelvis");
            Add("HandL", "hand.l"); Add("HandR", "hand.r"); Add("TailTip", "tail.6"); Add("Muzzle", "head"); Add("MuzzleLaser", "head");
            var childLocator = model.AddComponent<ChildLocator>();
            SFUtil.SetField(childLocator, "transformPairs", pairs.ToArray());

            // Hurtboxes: the body mass and the head (a weak point for snipers).
            var health = prefab.GetComponent<HealthComponent>();
            var hurtBoxes = new List<HurtBox>();
            HurtBox Box(string name, Transform parent, Vector3 local, Vector3 size, bool main)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = local;
                go.layer = LayerIndex.entityPrecise.intVal;
                go.AddComponent<BoxCollider>().size = size;
                var hb = go.AddComponent<HurtBox>();
                hb.healthComponent = health;
                hb.isBullseye = main;
                hb.isSniperTarget = !main;
                hb.damageModifier = HurtBox.DamageModifier.Normal;
                hurtBoxes.Add(hb);
                return hb;
            }
            HurtBox mainBox = Box("HurtboxBody", model.transform, new Vector3(0, 3.6f, -0.6f), new Vector3(4.4f, 3.2f, 5.6f), true);
            if (bones.TryGetValue("head", out var head)) Box("HurtboxHead", head, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 1.8f, 1.6f), false);
            var group = model.AddComponent<HurtBoxGroup>();
            group.hurtBoxes = hurtBoxes.ToArray();
            group.mainHurtBox = mainBox;
            group.bullseyeCount = 1;
            for (short i = 0; i < hurtBoxes.Count; i++)
            {
                SFUtil.SetField(hurtBoxes[i], "hurtBoxGroup", group);
                SFUtil.SetField(hurtBoxes[i], "indexInGroup", i);
            }

            var direction = prefab.GetComponent<CharacterDirection>();
            if (direction) direction.modelAnimator = model.GetComponent<SFModelContainer>().animator;
            Redirect(references, model, childLocator);
            model.AddComponent<HurricaneVisuals>();
        }

        private class Reference
        {
            public Component owner;
            public FieldInfo field;
            public string targetName;
        }

        private static List<Reference> FindReferencesInto(GameObject prefab, Transform oldModel)
        {
            var found = new List<Reference>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (!component || component.transform.IsChildOf(oldModel)) continue;
                for (Type type = component.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(Component); type = type.BaseType)
                {
                    foreach (FieldInfo field in type.GetFields(flags))
                    {
                        if (!typeof(Object).IsAssignableFrom(field.FieldType)) continue;
                        object value;
                        try { value = field.GetValue(component); }
                        catch { continue; }
                        Transform target = null;
                        if (value is Component c && c) target = c.transform;
                        else if (value is GameObject g && g) target = g.transform;
                        if (!target || !target.IsChildOf(oldModel)) continue;
                        if (component is ModelLocator && field.Name == "_modelTransform") continue;
                        found.Add(new Reference { owner = component, field = field, targetName = target.name });
                    }
                }
            }
            return found;
        }

        private static void Redirect(List<Reference> references, GameObject model, ChildLocator locator)
        {
            foreach (var r in references)
            {
                Transform t = locator.FindChild("Chest") ?? model.transform;
                object replacement = null;
                Type type = r.field.FieldType;
                if (type == typeof(Transform)) replacement = t;
                else if (type == typeof(GameObject)) replacement = t.gameObject;
                else if (typeof(Component).IsAssignableFrom(type)) replacement = model.GetComponentInChildren(type, true);
                r.field.SetValue(r.owner, replacement);
            }
            if (references.Count > 0) SpecialForcesPlugin.Log.LogInfo($"Hurricane: redirected {references.Count} references from the Titan model.");
        }

        // ------------------------------------------------------------------ master and boss bar

        private static void CreateMaster()
        {
            GameObject titanMaster = SFUtil.Load<GameObject>(Keys.TitanMaster, "TitanMaster");
            if (!titanMaster || !BodyPrefab) return;
            MasterPrefab = PrefabAPI.InstantiateClone(titanMaster, "SFHurricaneMaster", true);
            MasterPrefab.GetComponent<CharacterMaster>().bodyPrefab = BodyPrefab;
            foreach (var d in MasterPrefab.GetComponents<AISkillDriver>()) Object.DestroyImmediate(d);
            float h = SFConfig.BossHeight.Value;
            Driver("Roar", SkillSlot.Special, 0f, h * 1.3f, AISkillDriver.MovementType.Stop);
            Driver("Claw", SkillSlot.Primary, 0f, h * 1.0f, AISkillDriver.MovementType.ChaseMoveTarget);
            Driver("Barrage", SkillSlot.Secondary, 0f, 170f, AISkillDriver.MovementType.ChaseMoveTarget);
            Driver("Cyclone", SkillSlot.Utility, h, 130f, AISkillDriver.MovementType.ChaseMoveTarget);
            Driver("Chase", SkillSlot.None, 0f, float.PositiveInfinity, AISkillDriver.MovementType.ChaseMoveTarget);
            foreach (var ai in MasterPrefab.GetComponents<BaseAI>()) ai.fullVision = true;
            ContentAddition.AddMaster(MasterPrefab);
        }

        private static void Driver(string name, SkillSlot slot, float min, float max, AISkillDriver.MovementType move)
        {
            var d = MasterPrefab.AddComponent<AISkillDriver>();
            d.customName = name;
            d.skillSlot = slot;
            d.requireSkillReady = slot != SkillSlot.None;
            d.minDistance = min;
            d.maxDistance = max;
            d.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            d.movementType = move;
            d.aimType = AISkillDriver.AimType.AtCurrentEnemy;
            d.selectionRequiresTargetLoS = false;
            d.activationRequiresTargetLoS = false;
            d.ignoreNodeGraph = false;
            d.shouldSprint = false;
        }

        private static void CreateBossGroup()
        {
            var go = new GameObject("SFBossGroup");
            go.AddComponent<NetworkIdentity>();
            go.AddComponent<CombatSquad>();
            var bg = go.AddComponent<BossGroup>();
            bg.bossDropChance = 0f;
            bg.shouldDisplayHealthBarOnHud = true;
            bg.scaleRewardsByPlayerCount = false;
            BossGroupPrefab = PrefabAPI.InstantiateClone(go, "SFBossGroup", true);
            Object.DestroyImmediate(go);
            // (InstantiateClone with registerNetwork already registers it as a networked object.)
        }

        // Server: spawn the Hurricane in its lair with a boss bar; onDefeated runs when it dies.
        public static CharacterMaster SpawnHurricane(Vector3 lair, Action onDefeated)
        {
            if (!MasterPrefab) return null;
            var master = new MasterSummon
            {
                masterPrefab = MasterPrefab,
                position = lair + Vector3.up * 2f,
                rotation = Quaternion.LookRotation(SFScene.TowerPosition - lair == Vector3.zero ? Vector3.back : new Vector3((SFScene.TowerPosition - lair).x, 0, (SFScene.TowerPosition - lair).z)),
                teamIndexOverride = TeamIndex.Monster,
                ignoreTeamMemberLimit = true,
                useAmbientLevel = true,
            }.Perform();
            if (!master) return null;
            master.isBoss = true;
            int extra = Mathf.RoundToInt(SFConfig.BossHealthPerExtraPlayer.Value * 10f * (SFUtil.PlayerCount - 1));
            if (extra > 0) master.inventory.GiveItem(RoR2Content.Items.BoostHp, extra);
            var group = Object.Instantiate(BossGroupPrefab);
            NetworkServer.Spawn(group);
            var squad = group.GetComponent<CombatSquad>();
            squad.AddMember(master);
            squad.onDefeatedServer += () => onDefeated?.Invoke();
            var body = master.GetBody();
            if (body) body.gameObject.AddComponent<HurricaneBrain>().lair = lair;
            return master;
        }

        // ------------------------------------------------------------------ cyclones

        private static void CreateCyclone()
        {
            var go = new GameObject("SFCyclone");
            go.AddComponent<NetworkIdentity>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.transformSyncMode = NetworkTransform.TransformSyncMode.SyncTransform;
            nt.sendInterval = 0.1f;
            go.AddComponent<Cyclone>();
            CyclonePrefab = PrefabAPI.InstantiateClone(go, "SFCyclone", true);
            Object.DestroyImmediate(go);
        }
    }

    // Glowing eyes and a light that pulses like the storm inside it.
    public class HurricaneVisuals : MonoBehaviour
    {
        private Light glow;

        private void Start()
        {
            glow = new GameObject("StormGlow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0, 4.5f, 0);
            glow.type = LightType.Point;
            glow.color = new Color(0.5f, 0.75f, 1f);
            glow.range = SFConfig.BossHeight.Value * 2.5f;
        }

        private void Update()
        {
            if (glow) glow.intensity = 1.5f + 6f * Mathf.Pow(Mathf.PerlinNoise(Time.time * 3f, 0.3f), 6f);
        }
    }

    // Server: phases (Squall at 75/50/25% health), enrage below 25%, and the leash to its lair.
    public class HurricaneBrain : MonoBehaviour
    {
        public Vector3 lair;
        private CharacterBody body;
        private int phase;
        private float nextCheck;

        private void Start() => body = GetComponent<CharacterBody>();

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !body || !body.healthComponent || !body.healthComponent.alive) return;
            if (Time.fixedTime < nextCheck) return;
            nextCheck = Time.fixedTime + 0.5f;
            float fraction = body.healthComponent.combinedHealthFraction;
            int target = fraction < 0.25f ? 3 : fraction < 0.5f ? 2 : fraction < 0.75f ? 1 : 0;
            if (target > phase)
            {
                phase = target;
                var esm = EntityStateMachine.FindByCustomName(gameObject, "Body");
                if (esm) esm.SetInterruptState(new HurricaneSquall(), InterruptPriority.Pain);
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = Tokens.Prefix + "HURRICANE_PHASE", paramTokens = new[] { phase.ToString() } });
            }
            if (phase >= 3) body.AddTimedBuff(RoR2Content.Buffs.WarCryBuff, 1f);
            float leash = SFConfig.BossLeash.Value;
            float distance = Vector3.Distance(body.footPosition, lair);
            if (distance > leash * 1.4f || (distance > leash && !PlayersWithin(60f)))
            {
                if (SFEffects.Bolt) EffectManager.SpawnEffect(SFEffects.Bolt, new EffectData { origin = body.footPosition, start = body.footPosition + Vector3.up * 200f }, true);
                SFUtil.Teleport(body, lair + Vector3.up * 2f);
                if (SFEffects.Bolt) EffectManager.SpawnEffect(SFEffects.Bolt, new EffectData { origin = lair, start = lair + Vector3.up * 200f }, true);
            }
        }

        private bool PlayersWithin(float range)
        {
            foreach (var pc in PlayerCharacterMasterController.instances)
            {
                var b = pc.master ? pc.master.GetBody() : null;
                if (b && (b.corePosition - body.corePosition).sqrMagnitude < range * range) return true;
            }
            return false;
        }
    }

    // A tornado that crawls across the ground, dragging and battering everything of the other team.
    public class Cyclone : MonoBehaviour
    {
        public GameObject owner;
        public TeamIndex team = TeamIndex.Monster;
        public float damage;
        public Vector3 direction = Vector3.forward;
        private float age, nextHit;
        private const float Life = 10f, Speed = 9f, Radius = 10f;

        private void Start()
        {
            Material smoke = SFUtil.Load<Material>(Keys.MatSmoke, "matGenericSmoke");
            if (!smoke) return;
            // A twisting column of dark cloud (rotating particles) around a lightning core.
            var ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 2.2f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startColor = new Color(0.25f, 0.28f, 0.33f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 120f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(3.5f);
            vel.y = new ParticleSystem.MinMaxCurve(9f);
            vel.radial = new ParticleSystem.MinMaxCurve(1.2f);
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.material = smoke;
            ps.Play();
            var light = new GameObject("Core").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = Vector3.up * 8f;
            light.color = new Color(0.55f, 0.8f, 1f);
            light.range = 30f;
            light.intensity = 3f;
            SFAudio.PlayNear(SFAudio.Clip.BossCyclone, transform.position, 120f);
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (!NetworkServer.active) return;
            if (age > Life) { NetworkServer.Destroy(gameObject); return; }
            Vector3 next = transform.position + direction * Speed * Time.fixedDeltaTime;
            transform.position = SFUtil.Ground(next, 15f);
            if (age < nextHit) return;
            nextHit = age + 0.5f;
            var victims = new List<HurtBox>();
            foreach (var tc in TeamComponent.GetTeamMembers(TeamIndex.Player))
            {
                if (!tc || !tc.body) continue;
                Vector3 to = transform.position - tc.body.corePosition;
                to.y = 0;
                if (to.magnitude > Radius * 2f) continue;
                // The pull: towards the centre and a little up.
                Vector3 pull = to.normalized * 900f + Vector3.up * 300f;
                if (tc.body.characterMotor) tc.body.healthComponent.TakeDamageForce(pull, true, false);
            }
            new BlastAttack
            {
                attacker = owner,
                inflictor = gameObject,
                teamIndex = team,
                position = transform.position + Vector3.up * 3f,
                radius = Radius,
                baseDamage = damage * 0.75f,
                baseForce = 0f,
                procCoefficient = 0.2f,
                falloffModel = BlastAttack.FalloffModel.None,
                attackerFiltering = AttackerFiltering.NeverHitSelf,
            }.Fire();
        }
    }
}
