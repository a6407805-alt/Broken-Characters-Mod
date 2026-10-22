using System.Collections.Generic;
using EntityStates;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    public enum StructureKind { Tower, Nest, Wall, Depot }

    // Marks a structure body (on the prefab, so every machine has it) and keeps lists for the HUD and director.
    public class SFStructure : MonoBehaviour
    {
        public StructureKind kind;
        public static readonly List<SFStructure> All = new List<SFStructure>();
        public static SFStructure Tower;
        [System.NonSerialized] public string marker;          // server: which nest marker / which builder
        [System.NonSerialized] public int tier;
        [System.NonSerialized] public NetworkInstanceId owner;

        public CharacterBody Body { get; private set; }
        public HealthComponent Health { get; private set; }

        private void Awake()
        {
            Body = GetComponent<CharacterBody>();
            Health = GetComponent<HealthComponent>();
            All.Add(this);
            if (kind == StructureKind.Tower) Tower = this;
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (Tower == this) Tower = null;
        }
    }

    // Structures are characters that can't move: a body (health, team, hurtboxes, a model from the bundle) driven by
    // a master without AI (so they level up with their team like everything else and can hold items). Made in code:
    //   SFControlTowerBody  player team, the objective      SFStormNestBody   monster team, spawns guards
    //   SFBarrierWallBody   player team, blocks the way     SFSupplyDepotBody player team, pays its builder
    internal static class SFStructures
    {
        public static GameObject TowerMaster, NestMaster, WallMaster, DepotMaster;
        private static readonly List<GameObject> models = new List<GameObject>();

        public static void Init()
        {
            ContentAddition.AddEntityState<StructureIdle>(out _);
            ContentAddition.AddEntityState<StructureDeath>(out _);
            TowerMaster = Make("SFControlTower", StructureKind.Tower, SFAssets.Tower, 1f, SFConfig.TowerHealth.Value, SFConfig.TowerArmor.Value,
                new Vector3(0, 15f, 0), new Vector3(9f, 30f, 9f), true, new Color(0.35f, 0.85f, 1f));
            NestMaster = Make("SFStormNest", StructureKind.Nest, SFAssets.Nest, 1.1f, SFConfig.NestHealthPerTier.Value, 20f,
                new Vector3(0, 5f, 0), new Vector3(8f, 10f, 8f), false, new Color(0.4f, 0.5f, 0.9f));
            WallMaster = Make("SFBarrierWall", StructureKind.Wall, SFAssets.Wall, 1f, SFConfig.WallHealth.Value, 100f,
                new Vector3(0, 1.75f, 0), new Vector3(8.2f, 3.6f, 1.3f), true, new Color(0.7f, 0.7f, 0.65f));
            DepotMaster = Make("SFSupplyDepot", StructureKind.Depot, SFAssets.Depot, 1f, 1500f, 30f,
                new Vector3(0, 2f, 0), new Vector3(6f, 4f, 6f), true, new Color(0.4f, 1f, 0.5f));
            foreach (var m in models) if (m) SFAssets.RegisterForConversion(m);
        }

        private static GameObject Make(string name, StructureKind kind, GameObject modelPrefab, float scale, float health, float armor,
            Vector3 center, Vector3 size, bool solid, Color color)
        {
            // ---- body
            var body = new GameObject(name + "Body");
            body.AddComponent<NetworkIdentity>().localPlayerAuthority = false;
            var team = body.AddComponent<TeamComponent>();
            team.hideAllyCardDisplay = true;
            var cb = body.AddComponent<CharacterBody>();
            cb.baseNameToken = Tokens.Prefix + name.ToUpperInvariant() + "_NAME";
            cb.subtitleNameToken = "";
            cb.bodyFlags = CharacterBody.BodyFlags.IgnoreFallDamage | CharacterBody.BodyFlags.Mechanical | CharacterBody.BodyFlags.ImmuneToExecutes |
                           CharacterBody.BodyFlags.IgnoreKnockback | CharacterBody.BodyFlags.ImmuneToGoo | CharacterBody.BodyFlags.ImmuneToLava;
            cb.baseMaxHealth = health;
            cb.levelMaxHealth = health * 0.3f;
            cb.baseArmor = armor;
            cb.baseRegen = 0f;
            cb.baseMoveSpeed = 0f;
            cb.baseAcceleration = 0f;
            cb.baseJumpCount = 0;
            cb.baseDamage = 0f;
            cb.levelDamage = 0f;
            cb.hullClassification = HullClassification.BeetleQueen;
            cb.bodyColor = color;
            cb.portraitIcon = IconFactory.Solid(color);
            var hc = body.AddComponent<HealthComponent>();
            hc.globalDeathEventChanceCoefficient = 1f;
            body.AddComponent<SkillLocator>();
            var rb = body.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var marker = body.AddComponent<SFStructure>();
            marker.kind = kind;

            var esm = body.AddComponent<EntityStateMachine>();
            esm.customName = "Body";
            esm.initialStateType = new SerializableEntityStateType(typeof(StructureIdle));
            esm.mainStateType = new SerializableEntityStateType(typeof(StructureIdle));
            var nsm = body.AddComponent<NetworkStateMachine>();
            SFUtil.SetField(nsm, "stateMachines", new[] { esm });
            var death = body.AddComponent<CharacterDeathBehavior>();
            death.deathStateMachine = esm;
            death.deathState = new SerializableEntityStateType(typeof(StructureDeath));
            death.idleStateMachine = new EntityStateMachine[0];

            // ---- model
            var modelBase = new GameObject("ModelBase").transform;
            modelBase.SetParent(body.transform, false);
            // 1.0.1: the tower lay on its side. The ModelLocator copies ModelBase's rotation onto its model every frame,
            // which wiped the FBX root's Blender-to-Unity rotation. The model is now an upright container holding the
            // prefab instance, which keeps its own rotation.
            GameObject model = SFModelContainer.Make("mdl" + name, modelBase);
            if (modelPrefab)
            {
                var visual = Object.Instantiate(modelPrefab, model.transform, false);
                visual.transform.localScale = modelPrefab.transform.localScale * scale;
            }
            else
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);   // no bundle: a plain block stands in
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(model.transform, false);
                cube.transform.localPosition = center;
                cube.transform.localScale = size;
            }
            var characterModel = model.AddComponent<CharacterModel>();
            characterModel.body = cb;
            var infos = new List<CharacterModel.RendererInfo>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                infos.Add(new CharacterModel.RendererInfo { renderer = r, defaultMaterial = r.sharedMaterial, defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On });
            characterModel.baseRendererInfos = infos.ToArray();
            characterModel.autoPopulateLightInfos = true;

            var hurtObject = new GameObject("Hurtbox");
            hurtObject.transform.SetParent(model.transform, false);
            hurtObject.transform.localPosition = center;
            hurtObject.layer = LayerIndex.entityPrecise.intVal;
            var hurtCollider = hurtObject.AddComponent<BoxCollider>();
            hurtCollider.size = size;
            var hurtBox = hurtObject.AddComponent<HurtBox>();
            hurtBox.healthComponent = hc;
            hurtBox.isBullseye = true;
            hurtBox.isSniperTarget = kind != StructureKind.Wall;
            hurtBox.damageModifier = HurtBox.DamageModifier.Normal;
            var group = model.AddComponent<HurtBoxGroup>();
            group.hurtBoxes = new[] { hurtBox };
            group.mainHurtBox = hurtBox;
            group.bullseyeCount = 1;
            SFUtil.SetField(hurtBox, "hurtBoxGroup", group);
            SFUtil.SetField(hurtBox, "indexInGroup", (short)0);

            if (solid)
            {
                // A world-layer block so characters can't walk through (walls exist to block).
                var block = new GameObject("Solid");
                block.transform.SetParent(body.transform, false);
                block.transform.localPosition = center;
                block.layer = LayerIndex.world.intVal;
                block.AddComponent<BoxCollider>().size = size;
                SFScene.SetSurface(block, Keys.SdMetal);
            }

            var locator = body.AddComponent<ModelLocator>();
            locator.modelTransform = model.transform;
            locator.modelBaseTransform = modelBase;
            locator.autoUpdateModelTransform = true;
            locator.dontDetatchFromParent = true;
            locator.noCorpse = true;
            locator.normalizeToFloor = false;

            GameObject bodyPrefab = PrefabAPI.InstantiateClone(body, name + "Body", true);
            Object.DestroyImmediate(body);
            models.Add(bodyPrefab);
            ContentAddition.AddBody(bodyPrefab);

            // ---- master: Engineer's turret master without its AI
            GameObject turretMaster = SFUtil.Load<GameObject>(Keys.EngiTurretMaster, "EngiTurretMaster");
            if (!turretMaster) return null;
            GameObject master = PrefabAPI.InstantiateClone(turretMaster, name + "Master", true);
            // Strip the AI in dependency order (Unity refuses to remove BaseAI while AIOwnership requires it).
            foreach (var type in new[] { typeof(RoR2.CharacterAI.AIOwnership), typeof(RoR2.CharacterAI.AISkillDriver), typeof(RoR2.CharacterAI.BaseAI),
                                         typeof(Deployable), typeof(NetworkStateMachine), typeof(EntityStateMachine) })
                foreach (var c in master.GetComponents(type)) Object.DestroyImmediate(c);
            foreach (var c in master.GetComponents<Component>())
                if (c is RoR2.CharacterAI.BaseAI) SpecialForcesPlugin.Log.LogWarning(name + "Master still has its AI: " + c.GetType().Name);
            master.GetComponent<CharacterMaster>().bodyPrefab = bodyPrefab;
            ContentAddition.AddMaster(master);
            return master;
        }

        // Server: put a structure down. Team and owner decide who it fights for.
        public static CharacterMaster Spawn(GameObject masterPrefab, Vector3 position, Quaternion rotation, TeamIndex team, GameObject summoner = null)
        {
            if (!NetworkServer.active || !masterPrefab) return null;
            var summon = new MasterSummon
            {
                masterPrefab = masterPrefab,
                position = position,
                rotation = rotation,
                summonerBodyObject = summoner,
                teamIndexOverride = team,
                ignoreTeamMemberLimit = true,
                useAmbientLevel = team == TeamIndex.Monster,
            };
            return summon.Perform();
        }
    }

    // A structure just stands there.
    public class StructureIdle : EntityState { }

    // Blown apart: an explosion, then the body is removed.
    public class StructureDeath : EntityState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            Vector3 at = characterBody ? characterBody.corePosition : transform.position;
            GameObject boom = SFUtil.Load<GameObject>(Keys.OmniExplosion, "OmniExplosionVFX");
            if (boom) EffectManager.SpawnEffect(boom, new EffectData { origin = at, scale = 10f }, false);
            var marker = GetComponent<SFStructure>();
            if (marker && marker.kind == StructureKind.Nest) SFAudio.PlayNear(SFAudio.Clip.NestDeath, at, 150f);
            if (modelLocator && modelLocator.modelTransform) modelLocator.modelTransform.gameObject.SetActive(false);
            if (NetworkServer.active) UnityEngine.Object.Destroy(gameObject, 0.3f);
        }
    }

    internal static class IconFactory
    {
        public static Texture2D Solid(Color c)
        {
            var tex = new Texture2D(16, 16);
            var pixels = new Color[256];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
