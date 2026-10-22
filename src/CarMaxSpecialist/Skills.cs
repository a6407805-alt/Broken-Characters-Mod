using System.Collections.Generic;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace CarMaxSpecialist
{
    // ================================================================ M1 / M2: the combos
    //
    // M1 (light, hold to chain), 4 hits:  paper  ->  office stuff (stapler / phone / keyboard)  ->  the whole computer  ->  filing cabinet.
    // M2 (heavy, hold to chain), 4 hits:  lug nuts  ->  wrenches and tools  ->  jack and jack stands  ->  the car, then the building.
    // Stop for a moment and the combo starts over. Every prop is a visual; the damage is a bullet or blast from the owner's client.

    public abstract class CmxComboState : BaseSkillState
    {
        protected abstract bool Heavy { get; }
        protected int Step;
        private float _duration;
        private bool _crit;

        private static readonly float[] LightMult = { 0.6f, 0.8f, 1.2f, 2.4f };
        private static readonly float[] HeavyMult = { 1f, 1.3f, 1.8f, 2.5f };

        public override void OnEnter()
        {
            base.OnEnter();
            CmxShift shift = GetComponent<CmxShift>();
            if (isAuthority) Step = shift ? shift.NextStep(Heavy) : 0;
            bool last = Step == 3;
            float baseDur = Heavy ? (last ? 1.0f : 0.6f) : (last ? 0.62f : 0.32f);
            _duration = baseDur / Mathf.Max(0.1f, attackSpeedStat);

            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound(Heavy ? "Play_commando_shift" : (last ? "Play_mage_m1_shoot" : "Play_huntress_m1_shoot"), gameObject);
            _crit = isAuthority && RollCrit();

            if (Heavy) FireHeavy(aim); else FireLight(aim);
            characterBody.AddSpreadBloom(0.4f);
        }

        // ---------------------------------------------------------------- light

        private void FireLight(Ray aim)
        {
            Vector3 from = CmxFx.Hand(characterBody, aim);
            Vector3 dir = aim.direction;
            float dmg = damageStat * CmxPlugin.EmailDamage.Value * LightMult[Step];
            if (isAuthority) CmxAudio.Play(Step == 0 ? "email" : Step == 1 ? "typing" : Step == 2 ? "beep" : "slam");

            switch (Step)
            {
                case 0:
                    CmxFx.Paper(from, dir, 90f, false);
                    Bullet(aim, dir, dmg, 0.6f, 150f, 90f);
                    break;
                case 1:
                    CmxFx.Office(from, dir, 70f);
                    Bullet(aim, dir, dmg, 0.9f, 350f, 70f);
                    break;
                case 2:
                    CmxFx.Computer(from, dir, 60f);
                    Bullet(aim, dir, dmg, 1.4f, 700f, 60f);
                    break;
                default:
                {
                    Vector3 at = CmxFx.Target(aim, 32f);
                    CmxFx.Cabinet(from, at);
                    if (isAuthority)
                    {
                        CmxHud.Bubble(transform, transform.position, "<color=#FFCA26>FILED.</color>", Style.Small);
                        Blast(at, 6.5f, dmg, 2500f, 0.1f, 5f, false);
                    }
                    break;
                }
            }
        }

        private void Bullet(Ray aim, Vector3 dir, float damage, float radius, float force, float range)
        {
            if (!isAuthority) return;
            new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = aim.origin,
                aimVector = dir,
                minSpread = 0f,
                maxSpread = 0f,
                damage = damage,
                force = force,
                tracerEffectPrefab = null,
                hitEffectPrefab = CmxFx.HitSpark,
                isCrit = _crit,
                radius = radius,
                smartCollision = true,
                maxDistance = range,
                procCoefficient = 1f,
                stopperMask = LayerIndex.world.mask
            }.Fire();
        }

        // ---------------------------------------------------------------- heavy

        private void FireHeavy(Ray aim)
        {
            Vector3 from = CmxFx.Hand(characterBody, aim);
            Vector3 at = CmxFx.Ground(CmxFx.Target(aim, 40f));
            float dmg = damageStat * CmxPlugin.HeavyDamage.Value * HeavyMult[Step];
            if (isAuthority) CmxAudio.Play(Step == 0 ? "typing" : Step == 1 ? "stamp" : Step == 2 ? "clock" : "slam");
            float fly;

            switch (Step)
            {
                case 0:
                    fly = CmxFx.LugNuts(from, at);
                    Blast(at, 5f, dmg, 1200f, fly, 4f, false);
                    break;
                case 1:
                    fly = CmxFx.Tools(from, at);
                    Blast(at, 6f, dmg, 1600f, fly, 5f, false);
                    break;
                case 2:
                    fly = CmxFx.Jack(from, at);
                    Blast(at, 7f, dmg, 2200f, fly, 6f, false);
                    break;
                default:
                {
                    Vector3 farAt = at + dir2(aim) * 3f;
                    fly = CmxFx.Car(from, at);
                    float building = CmxFx.Building(from, farAt, 0.35f);
                    if (isAuthority)
                    {
                        CmxHud.Bubble(transform, transform.position, "<color=#FFCA26>TOTALED.</color>", Style.Small);
                        Blast(at, 9f, dmg, 3000f, fly, 8f, true);
                        Blast(farAt, 15f, damageStat * CmxPlugin.HeavyDamage.Value * 3f, 5000f, building, 10f, true);
                    }
                    break;
                }
            }
        }

        private static Vector3 dir2(Ray aim)
        {
            Vector3 d = aim.direction;
            d.y = 0f;
            return d.sqrMagnitude < 0.01f ? Vector3.forward : d.normalized;
        }

        // ---------------------------------------------------------------- damage

        private void Blast(Vector3 at, float radius, float damage, float force, float delay, float shake, bool big)
        {
            if (!isAuthority) return;
            GameObject owner = gameObject;
            TeamIndex team = TeamComponent.GetObjectTeam(owner);
            bool crit = _crit;
            CmxDelay.Run(delay, () =>
            {
                if (!owner) return;
                new BlastAttack
                {
                    attacker = owner,
                    inflictor = owner,
                    baseDamage = damage,
                    baseForce = force,
                    position = at,
                    radius = radius,
                    falloffModel = BlastAttack.FalloffModel.None,
                    crit = crit,
                    procCoefficient = 1f,
                    teamIndex = team
                }.Fire();
                CmxFx.Slam(at, radius * 0.45f, big);
            });
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= _duration && isAuthority) outer.SetNextStateToMain();
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write((byte)Step);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            Step = reader.ReadByte();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    public class CmxLightState : CmxComboState { protected override bool Heavy => false; }
    public class CmxHeavyState : CmxComboState { protected override bool Heavy => true; }

    /// <summary>Runs an action after a delay (a falling prop's landing).</summary>
    public class CmxDelay : MonoBehaviour
    {
        private float _t;
        private System.Action _a;

        public static void Run(float delay, System.Action a)
        {
            if (delay <= 0.01f) { a(); return; }
            GameObject go = new GameObject("CmxDelay");
            CmxDelay d = go.AddComponent<CmxDelay>();
            d._t = delay;
            d._a = a;
        }

        private void Update()
        {
            _t -= Time.deltaTime;
            if (_t > 0f) return;
            try { _a?.Invoke(); } catch (System.Exception e) { CmxPlugin.Log.LogWarning("Combo hit failed: " + e.Message); }
            Destroy(gameObject);
        }
    }

    // ================================================================ Shift: Order Your Own Parts

    public class CmxPartsState : BaseSkillState
    {
        public static float Duration = 0.32f;
        private bool _backordered;
        private Vector3 _start, _dir;

        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority)
            {
                _backordered = Random.value < CmxPlugin.BackorderChance.Value;
                _start = characterBody.footPosition;
            }
            else if (_start == Vector3.zero) _start = characterBody.footPosition;

            Vector3 move = inputBank ? inputBank.moveVector : Vector3.zero;
            move.y = 0f;
            if (move.sqrMagnitude < 0.01f) move = characterDirection ? characterDirection.forward : transform.forward;
            move.y = 0f;
            _dir = move.normalized;

            Util.PlaySound("Play_commando_shift", gameObject);
            if (NetworkServer.active) characterBody.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, Duration + 0.1f);
            if (isAuthority) CmxAudio.Play("typing");
            CmxPallet.Drop(characterBody, _start, _backordered, isAuthority);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && characterMotor)
            {
                characterMotor.rootMotion += _dir * (moveSpeedStat * 3.8f * Time.fixedDeltaTime);
                if (characterDirection) characterDirection.forward = _dir;
            }
            if (fixedAge >= Duration && isAuthority) outer.SetNextStateToMain();
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(_backordered);
            writer.Write(_start);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            _backordered = reader.ReadBoolean();
            _start = reader.ReadVector3();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Frozen;
    }

    // ================================================================ R: Lazy Coworkers

    /// <summary>While coworkers are out, the key is always ready (to send them home).</summary>
    public class CmxCoworkerSkillDef : SkillDef
    {
        private static bool Out(GenericSkill s)
        {
            CmxShift shift = s && s.characterBody ? s.characterBody.GetComponent<CmxShift>() : null;
            return shift && shift.Coworkers > 0;
        }

        public override bool IsReady(GenericSkill skillSlot) => base.IsReady(skillSlot) || Out(skillSlot);

        public override void OnExecute(GenericSkill skillSlot)
        {
            bool free = Out(skillSlot);
            int stock = skillSlot.stock;
            base.OnExecute(skillSlot);
            if (free) skillSlot.stock = stock;
        }
    }

    public class CmxCoworkersState : BaseSkillState
    {
        public static float Duration = 0.4f;

        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_UI_menuClick", gameObject);
            if (isAuthority) CmxTasks.ClearRequest = true;
            if (NetworkServer.active)
            {
                CmxShift shift = GetComponent<CmxShift>();
                if (shift) shift.ServerCoworkers();
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= Duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // ================================================================ Burnout

    /// <summary>He slams the desk and can't do anything for a few seconds (both his body and his hands).</summary>
    public class CmxBurnoutState : BaseState
    {
        private bool _isBody;

        public static void Begin(CharacterBody b)
        {
            if (!b || !b.hasEffectiveAuthority) return;
            EntityStateMachine body = EntityStateMachine.FindByCustomName(b.gameObject, "Body");
            EntityStateMachine weapon = EntityStateMachine.FindByCustomName(b.gameObject, "Weapon");
            if (body) body.SetInterruptState(new CmxBurnoutState(), InterruptPriority.Frozen);
            if (weapon) weapon.SetInterruptState(new CmxBurnoutState(), InterruptPriority.Frozen);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isBody = outer && outer.customName == "Body";
            if (_isBody)
            {
                Util.PlaySound("Play_captain_shift_impact", gameObject);
                CmxFx.Slam(characterBody ? characterBody.footPosition : transform.position);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (_isBody && isAuthority && characterMotor)
            {
                characterMotor.moveDirection = Vector3.zero;
                Vector3 v = characterMotor.velocity;
                characterMotor.velocity = new Vector3(0f, Mathf.Min(v.y, 0f), 0f);
            }
            if (fixedAge >= CmxPlugin.BurnoutSeconds.Value && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Frozen;
    }

    // ================================================================ visuals

    public static class CmxFx
    {
        private static GameObject _hitSpark, _boom, _boomBig;
        private static Material _paper, _paperBlue, _stamp, _beam, _wood, _box, _tape, _label;

        public static GameObject HitSpark
        {
            get { if (!_hitSpark) _hitSpark = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/HitsparkCommando"); return _hitSpark; }
        }

        public static GameObject Boom(bool big)
        {
            if (!_boom) _boom = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");
            if (!_boomBig) _boomBig = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX");
            return big ? _boomBig : _boom;
        }

        private static Shader _std;
        private static bool _stdLooked;

        // RoR2 only allows its own shaders by name (Shader.Find("Sprites/Default") throws), so everything uses the
        // game's standard shader, and lines use the Commando tracer's material.
        private static Shader Std()
        {
            if (_stdLooked) return _std;
            _stdLooked = true;
            try { _std = Shader.Find("Hopoo Games/Deferred/Standard"); } catch { _std = null; }
            if (!_std) { try { _std = LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/HGStandard"); } catch { _std = null; } }
            if (!_std) CmxPlugin.Log.LogWarning("Game shader not found; CarMax props may look plain.");
            return _std;
        }

        public static Material Lit(Color c, float glow = 0f)
        {
            Shader sh = Std();
            Material m = sh ? new Material(sh) : new Material(Commando());
            m.color = c;
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (glow > 0f)
            {
                if (m.HasProperty("_EmColor")) m.SetColor("_EmColor", c);
                if (m.HasProperty("_EmPower")) m.SetFloat("_EmPower", glow);
            }
            return m;
        }

        private static Material Commando()
        {
            try
            {
                GameObject body = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
                Renderer r = body ? body.GetComponentInChildren<Renderer>(true) : null;
                if (r && r.sharedMaterial) return r.sharedMaterial;
            }
            catch { }
            return null;
        }

        private static Material LineMat()
        {
            try
            {
                GameObject tracer = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerCommandoDefault");
                LineRenderer lr = tracer ? tracer.GetComponentInChildren<LineRenderer>(true) : null;
                if (lr && lr.sharedMaterial) return lr.sharedMaterial;
            }
            catch { }
            return Lit(new Color(1f, 0.2f, 0.15f), 1f);
        }

        private static void Mats()
        {
            if (_paper) return;
            _paper = Lit(new Color(0.97f, 0.97f, 0.95f), 0.35f);
            _paperBlue = Lit(new Color(0.75f, 0.75f, 0.75f));
            _stamp = Lit(new Color(0.1f, 0.65f, 0.2f), 0.6f);
            _beam = LineMat();
            _wood = Lit(new Color(0.55f, 0.38f, 0.2f));
            _box = Lit(new Color(0.8f, 0.63f, 0.4f));
            _tape = Lit(new Color(1f, 0.79f, 0.15f), 0.3f);
            _label = Lit(new Color(0f, 0.23f, 0.46f), 0.2f);
        }

        public static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default(Vector3))
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static Vector3 Hand(CharacterBody body, Ray aim)
        {
            try
            {
                Transform model = body && body.modelLocator ? body.modelLocator.modelTransform : null;
                ChildLocator cl = model ? model.GetComponent<ChildLocator>() : null;
                Transform t = cl ? (cl.FindChild("MuzzleRight") ?? cl.FindChild("HandR")) : null;
                if (t) return t.position;
            }
            catch { }
            return aim.origin + aim.direction * 0.8f;
        }

        /// <summary>A completed repair order flying through the air: header bar, printed lines, green COMPLETED stamp.</summary>
        public static void Paper(Vector3 from, Vector3 dir, float range, bool big)
        {
            Mats();
            GameObject go = new GameObject("CmxRepairOrder");
            go.transform.position = from;
            Transform t = go.transform;
            float k = big ? 1.5f : 1f;
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 0.015f, 0.65f) * k, _paper);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.27f) * k, new Vector3(0.5f, 0.006f, 0.1f) * k, _label);
            for (int i = 0; i < 4; i++)
                Part(t, PrimitiveType.Cube, new Vector3(-0.05f, 0.01f, 0.12f - i * 0.09f) * k, new Vector3(0.36f, 0.005f, 0.025f) * k, _paperBlue);
            Part(t, PrimitiveType.Cube, new Vector3(0.12f, 0.014f, -0.2f) * k, new Vector3(0.2f, 0.006f, 0.12f) * k, _stamp, new Vector3(0f, -18f, 0f));
            float dist = range;
            if (Physics.Raycast(from, dir, out RaycastHit hit, range, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) dist = hit.distance;
            go.AddComponent<CmxFlyer>().Init(from, from + dir.normalized * dist, 75f);
        }

        /// <summary>A red scanner fan from the barcode gun.</summary>
        public static void Scan(Vector3 from, Vector3 dir, float range, float angle)
        {
            Mats();
            GameObject go = new GameObject("CmxScan");
            go.transform.position = from;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-angle, angle, i / 8f);
                Vector3 d = Quaternion.AngleAxis(a, Vector3.up) * dir;
                LineRenderer lr = new GameObject("beam").AddComponent<LineRenderer>();
                lr.transform.SetParent(go.transform, false);
                lr.sharedMaterial = _beam;
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, from);
                lr.SetPosition(1, from + d * range);
                lr.startWidth = 0.06f;
                lr.endWidth = 0.6f;
                lr.startColor = new Color(1f, 0.2f, 0.15f, 0.9f);
                lr.endColor = new Color(1f, 0.2f, 0.15f, 0f);
            }
            go.AddComponent<CmxFade>().Life = 0.35f;
        }

        // ---------------------------------------------------------------- combo props

        private static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        private static Material M(string key, Color c, float glow = 0f)
        {
            if (MatCache.TryGetValue(key, out Material m) && m) return m;
            m = Lit(c, glow);
            MatCache[key] = m;
            return m;
        }

        private static Material Steel() { return M("steel", new Color(0.72f, 0.74f, 0.78f)); }
        private static Material Dark() { return M("dark", new Color(0.14f, 0.15f, 0.18f)); }

        /// <summary>Where the aim ray lands (world or characters), or a point range metres out.</summary>
        public static Vector3 Target(Ray aim, float range)
        {
            if (Physics.Raycast(aim.origin, aim.direction, out RaycastHit hit, range, LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return aim.origin + aim.direction * range;
        }

        public static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 120f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return p;
        }

        private static GameObject Prop(string name, Vector3 at)
        {
            GameObject go = new GameObject(name);
            go.transform.position = at;
            return go;
        }

        private static void Fly(GameObject go, Vector3 from, Vector3 to, float speed, float spin = 1f)
        {
            go.transform.position = from;
            go.AddComponent<CmxFlyer>().Init(from, to, speed, spin);
        }

        private static float Fall(GameObject go, Vector3 land, float height, float speed, float spin = 0f)
        {
            Vector3 from = land + Vector3.up * height;
            Fly(go, from, land, speed, spin);
            return height / speed;
        }

        private static GameObject Stapler(Transform parent, Vector3 pos, float k)
        {
            GameObject g = new GameObject("stapler");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            Part(g.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.14f, 0.05f, 0.42f) * k, Navy());
            Part(g.transform, PrimitiveType.Cube, new Vector3(0f, 0.05f, -0.02f) * k, new Vector3(0.15f, 0.05f, 0.38f) * k, M("gold", new Color(1f, 0.79f, 0.15f), 0.2f), new Vector3(-6f, 0f, 0f));
            return g;
        }

        /// <summary>M1 hit 2: a random piece of office junk (stapler with loose staples, desk phone, or keyboard).</summary>
        public static void Office(Vector3 from, Vector3 dir, float range)
        {
            Mats();
            GameObject go = Prop("CmxOfficeItem", from);
            Transform t = go.transform;
            switch (Random.Range(0, 3))
            {
                case 0:
                    Stapler(t, Vector3.zero, 1.2f);
                    for (int i = 0; i < 5; i++)
                        Part(t, PrimitiveType.Cube, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.15f, 0.15f), Random.Range(-0.35f, 0.35f)), new Vector3(0.12f, 0.015f, 0.03f), Steel(), new Vector3(0f, Random.Range(0f, 180f), 0f));
                    break;
                case 1: // desk phone
                    Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.38f, 0.1f, 0.34f), M("navy", new Color(0.04f, 0.17f, 0.4f)));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.12f, 0.07f, 0.55f), Dark(), new Vector3(0f, 0f, 0f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0.25f), new Vector3(0.16f, 0.1f, 0.12f), Dark());
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.1f, -0.25f), new Vector3(0.16f, 0.1f, 0.12f), Dark());
                    for (int i = 0; i < 6; i++)
                        Part(t, PrimitiveType.Cube, new Vector3(-0.1f + (i % 3) * 0.1f, 0.06f, 0.02f + (i / 3) * 0.08f), new Vector3(0.06f, 0.03f, 0.05f), M("white", Color.white));
                    break;
                default: // keyboard
                    Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.95f, 0.05f, 0.32f), M("kb", new Color(0.2f, 0.2f, 0.24f)));
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 9; c++)
                            Part(t, PrimitiveType.Cube, new Vector3(-0.4f + c * 0.1f, 0.04f, -0.09f + r * 0.09f), new Vector3(0.075f, 0.03f, 0.07f), M("key", new Color(0.88f, 0.88f, 0.9f)));
                    break;
            }
            float dist = range;
            if (Physics.Raycast(from, dir, out RaycastHit hit, range, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) dist = hit.distance;
            Fly(go, from, from + dir.normalized * dist, 65f);
        }

        /// <summary>M1 hit 3: the whole computer: monitor, tower, keyboard.</summary>
        public static void Computer(Vector3 from, Vector3 dir, float range)
        {
            Mats();
            GameObject go = Prop("CmxComputer", from);
            Transform t = go.transform;
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.7f, 0.12f), Dark());
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.35f, 0.07f), new Vector3(0.9f, 0.6f, 0.02f), M("screen", new Color(0.2f, 0.45f, 0.95f), 1.2f));
            Part(t, PrimitiveType.Cube, new Vector3(0f, -0.05f, 0f), new Vector3(0.12f, 0.2f, 0.1f), Dark());
            Part(t, PrimitiveType.Cube, new Vector3(0f, -0.15f, 0f), new Vector3(0.45f, 0.04f, 0.3f), Dark());
            Part(t, PrimitiveType.Cube, new Vector3(0.75f, 0.1f, 0f), new Vector3(0.38f, 0.85f, 0.75f), M("navy", new Color(0.04f, 0.17f, 0.4f)));
            Part(t, PrimitiveType.Cube, new Vector3(0.75f, 0.35f, 0.38f), new Vector3(0.3f, 0.03f, 0.02f), Dark());
            Part(t, PrimitiveType.Cube, new Vector3(-0.1f, -0.2f, 0.55f), new Vector3(0.8f, 0.04f, 0.28f), M("kb", new Color(0.2f, 0.2f, 0.24f)));
            float dist = range;
            if (Physics.Raycast(from, dir, out RaycastHit hit, range, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) dist = hit.distance;
            Fly(go, from, from + dir.normalized * dist, 60f);
        }

        /// <summary>M1 finisher: a filing cabinet, thrown.</summary>
        public static void Cabinet(Vector3 from, Vector3 at)
        {
            Mats();
            GameObject go = Prop("CmxCabinet", from);
            Transform t = go.transform;
            Material grey = M("cabinet", new Color(0.12f, 0.27f, 0.55f));
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.9f, 2f, 0.75f), grey);
            for (int i = 0; i < 4; i++)
            {
                float y = -0.75f + i * 0.5f;
                Part(t, PrimitiveType.Cube, new Vector3(0f, y, 0.38f), new Vector3(0.82f, 0.42f, 0.03f), M("drawer", new Color(0.2f, 0.38f, 0.7f)));
                Part(t, PrimitiveType.Cube, new Vector3(0f, y + 0.08f, 0.41f), new Vector3(0.3f, 0.04f, 0.04f), Steel());
            }
            Fly(go, from, at, 58f, 0.5f);
        }

        private static float Throw(GameObject go, Vector3 from, Vector3 to, float speed, float arc, float spin, float startScale = 1f)
        {
            go.transform.position = from;
            go.AddComponent<CmxFlyer>().Init(from, to, speed, spin, arc, startScale);
            return Mathf.Max(0.1f, Vector3.Distance(from, to) / speed);
        }

        private static Material Gold() { return M("gold", new Color(1f, 0.79f, 0.15f), 0.2f); }
        private static Material Navy() { return M("navy", new Color(0.04f, 0.17f, 0.4f)); }

        /// <summary>M2 hit 1: a handful of lug nuts flung from his hand. Returns the longest flight time.</summary>
        public static float LugNuts(Vector3 from, Vector3 at)
        {
            Mats();
            float longest = 0.2f;
            for (int i = 0; i < 8; i++)
            {
                GameObject go = Prop("CmxLugNut", from);
                Part(go.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.28f, 0.1f, 0.28f), Steel());
                Part(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.02f, 0.14f), Dark());
                Vector2 o = Random.insideUnitCircle * 3f;
                longest = Mathf.Max(longest, Throw(go, from, at + new Vector3(o.x, 0f, o.y), 38f, 1.5f, 0.8f));
            }
            return longest;
        }

        /// <summary>M2 hit 2: wrenches and a hammer, thrown spinning.</summary>
        public static float Tools(Vector3 from, Vector3 at)
        {
            Mats();
            float longest = 0.2f;
            for (int i = 0; i < 5; i++)
            {
                GameObject go = Prop("CmxTool", from);
                Transform t = go.transform;
                if (i == 4)
                {
                    Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.1f, 0.1f, 1.2f), Navy());
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, 0.6f), new Vector3(0.22f, 0.22f, 0.5f), Steel());
                }
                else
                {
                    Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.12f, 0.05f, 1.3f), Steel());
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.65f), new Vector3(0.34f, 0.05f, 0.34f), Steel());
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0.65f), new Vector3(0.16f, 0.06f, 0.16f), Dark());
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.65f), new Vector3(0.3f, 0.05f, 0.3f), Steel());
                }
                t.localScale = Vector3.one * 1.5f;
                Vector2 o = Random.insideUnitCircle * 3.5f;
                longest = Mathf.Max(longest, Throw(go, from, at + new Vector3(o.x, 0f, o.y), 34f, 2.5f, 1.2f));
            }
            return longest;
        }

        /// <summary>M2 hit 3: a floor jack and two jack stands, lobbed.</summary>
        public static float Jack(Vector3 from, Vector3 at)
        {
            Mats();
            GameObject jack = Prop("CmxJack", from);
            Transform t = jack.transform;
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.7f, 0.25f, 1.6f), Gold());
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.45f, -0.5f), new Vector3(0.3f, 0.5f, 0.3f), Gold());
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.65f, 0.1f), new Vector3(0.12f, 0.12f, 1.3f), Steel(), new Vector3(-18f, 0f, 0f));
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0.7f), new Vector3(0.4f, 0.05f, 0.4f), Dark());
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.2f, 1.25f), new Vector3(0.1f, 0.1f, 1f), Steel());
            t.localScale = Vector3.one * 1.6f;
            float longest = Throw(jack, from, at, 30f, 4f, 0.7f);
            for (int i = 0; i < 2; i++)
            {
                GameObject stand = Prop("CmxJackStand", from);
                Transform s = stand.transform;
                Part(s, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 0.9f), Dark());
                Part(s, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.22f, 1.1f, 0.22f), Navy());
                Part(s, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.4f, 0.12f, 0.4f), Gold());
                s.localScale = Vector3.one * 1.4f;
                longest = Mathf.Max(longest, Throw(stand, from, at + new Vector3(i == 0 ? -2.5f : 2.5f, 0f, Random.Range(-1.5f, 1.5f)), 30f, 5f, 0.6f));
            }
            return longest;
        }

        /// <summary>M2 hit 4: the car. It starts small in his hand and grows as it flies. Returns the flight time.</summary>
        public static float Car(Vector3 from, Vector3 at)
        {
            Mats();
            GameObject go = Prop("CmxCar", from);
            Transform t = go.transform;
            Material body = M("carbody", new Color(0.1f, 0.28f, 0.72f));
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.7f, 3.9f), body);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f), new Vector3(1.82f, 0.08f, 0.5f), Gold());
            Part(t, PrimitiveType.Cube, new Vector3(0f, 1.15f, -0.2f), new Vector3(1.55f, 0.55f, 2.0f), M("cabin", new Color(0.35f, 0.5f, 0.85f)));
            Part(t, PrimitiveType.Cube, new Vector3(0f, 1.18f, -0.2f), new Vector3(1.6f, 0.4f, 1.7f), M("glass", new Color(0.08f, 0.1f, 0.2f)));
            Part(t, PrimitiveType.Cube, new Vector3(0.6f, 0.62f, 1.97f), new Vector3(0.3f, 0.15f, 0.04f), M("lamp", new Color(1f, 0.9f, 0.5f), 1f));
            Part(t, PrimitiveType.Cube, new Vector3(-0.6f, 0.62f, 1.97f), new Vector3(0.3f, 0.15f, 0.04f), M("lamp", new Color(1f, 0.9f, 0.5f), 1f));
            for (int i = 0; i < 4; i++)
                Part(t, PrimitiveType.Cylinder, new Vector3(i % 2 == 0 ? 0.95f : -0.95f, 0.3f, i < 2 ? 1.25f : -1.25f), new Vector3(0.7f, 0.12f, 0.7f), Dark(), new Vector3(0f, 0f, 90f));
            t.localScale = Vector3.one * 1.6f;
            return Throw(go, from, at, 26f, 7f, 0.25f, 0.3f);
        }

        /// <summary>M2 finale: the whole dealership, thrown a beat after the car. Returns the time until it lands.</summary>
        public static float Building(Vector3 from, Vector3 at, float delay)
        {
            Mats();
            GameObject go = Prop("CmxBuilding", from);
            Transform t = go.transform;
            Part(t, PrimitiveType.Cube, new Vector3(0f, 6f, 0f), new Vector3(10f, 12f, 8f), Navy());
            Part(t, PrimitiveType.Cube, new Vector3(0f, 12.4f, 0f), new Vector3(10.6f, 0.8f, 8.6f), Gold());
            Part(t, PrimitiveType.Cube, new Vector3(0f, 10.8f, 4.1f), new Vector3(6f, 1.4f, 0.2f), M("sign", new Color(1f, 0.79f, 0.15f), 1.2f));
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 4; c++)
                    Part(t, PrimitiveType.Cube, new Vector3(-3.6f + c * 2.4f, 2.2f + r * 2.6f, 4.05f), new Vector3(1.4f, 1.4f, 0.12f), M("win", new Color(1f, 0.9f, 0.5f), 0.8f));
            go.SetActive(false);
            float flight = Vector3.Distance(from, at) / 30f;
            CmxDelay.Run(delay, () =>
            {
                if (!go) return;
                go.SetActive(true);
                go.AddComponent<CmxFlyer>().Init(from, at, 30f, 0f, 10f, 0.06f);
            });
            go.transform.position = from;
            return delay + flight;
        }

        public static void Slam(Vector3 at, float scale, bool big)
        {
            GameObject fx = Boom(big);
            if (fx) EffectManager.SpawnEffect(fx, new EffectData { origin = at, scale = Mathf.Max(1f, scale) }, false);
        }

        public static void Slam(Vector3 at)
        {
            GameObject fx = Boom(false);
            if (fx) EffectManager.SpawnEffect(fx, new EffectData { origin = at, scale = 2f }, false);
        }

        /// <summary>The pallet: slats, three boxes taped in yellow, a blue label.</summary>
        public static GameObject BuildPallet(float size)
        {
            Mats();
            GameObject root = new GameObject("CmxPallet");
            Transform t = root.transform;
            for (int i = 0; i < 5; i++) Part(t, PrimitiveType.Cube, new Vector3(-0.8f + i * 0.4f, 0.12f, 0f), new Vector3(0.3f, 0.06f, 1.9f), _wood);
            for (int i = 0; i < 3; i++) Part(t, PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.8f + i * 0.8f), new Vector3(1.9f, 0.1f, 0.2f), _wood);
            Part(t, PrimitiveType.Cube, new Vector3(-0.45f, 0.6f, -0.4f), new Vector3(0.85f, 0.85f, 0.85f), _box);
            Part(t, PrimitiveType.Cube, new Vector3(0.45f, 0.55f, 0.35f), new Vector3(0.85f, 0.75f, 0.9f), _box);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(0.9f, 0.7f, 0.8f), _box, new Vector3(0f, 18f, 0f));
            Part(t, PrimitiveType.Cube, new Vector3(-0.45f, 0.6f, -0.4f), new Vector3(0.2f, 0.87f, 0.87f), _tape);
            Part(t, PrimitiveType.Cube, new Vector3(0.45f, 0.55f, 0.35f), new Vector3(0.87f, 0.2f, 0.92f), _tape);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(0.2f, 0.72f, 0.82f), _tape, new Vector3(0f, 18f, 0f));
            Part(t, PrimitiveType.Cube, new Vector3(0.45f, 0.6f, 0.81f), new Vector3(0.45f, 0.3f, 0.02f), _label);
            root.transform.localScale = Vector3.one * size;
            return root;
        }

        /// <summary>A yellow circle on the ground where the pallet will land.</summary>
        public static GameObject Ring(Vector3 at, float radius)
        {
            Mats();
            GameObject go = new GameObject("CmxDropZone");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _beam;
            lr.useWorldSpace = true;
            lr.loop = true;
            const int n = 40;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                lr.SetPosition(i, at + new Vector3(Mathf.Cos(a) * radius, 0.15f, Mathf.Sin(a) * radius));
            }
            lr.startWidth = lr.endWidth = 0.25f;
            lr.startColor = lr.endColor = new Color(1f, 0.79f, 0.15f, 1f);
            return go;
        }
    }

    /// <summary>Moves a visual from A to B, tumbling, then removes it.</summary>
    public class CmxFlyer : MonoBehaviour
    {
        private Vector3 _a, _b;
        private float _t, _len, _speed, _arc, _s0 = 1f;
        private Vector3 _spin, _base;

        public void Init(Vector3 a, Vector3 b, float speed, float spin = 1f, float arc = 0f, float startScale = 1f)
        {
            _a = a; _b = b; _speed = speed; _arc = arc; _s0 = startScale;
            _base = transform.localScale;
            _len = Mathf.Max(0.1f, Vector3.Distance(a, b));
            _spin = new Vector3(Random.Range(-900f, 900f), Random.Range(-300f, 300f), Random.Range(-900f, 900f)) * spin;
            if (_s0 != 1f) transform.localScale = _base * _s0;
        }

        private void Update()
        {
            _t += Time.deltaTime * _speed / _len;
            float k = Mathf.Clamp01(_t);
            Vector3 p = Vector3.Lerp(_a, _b, k);
            p.y += _arc * 4f * k * (1f - k);
            transform.position = p;
            if (_s0 != 1f) transform.localScale = _base * Mathf.Lerp(_s0, 1f, k);
            transform.Rotate(_spin * Time.deltaTime, Space.Self);
            if (_t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Fades line renderers out, then removes the object.</summary>
    public class CmxFade : MonoBehaviour
    {
        public float Life = 0.4f;
        private float _age;
        private LineRenderer[] _lines;

        private void Start() { _lines = GetComponentsInChildren<LineRenderer>(); }

        private void Update()
        {
            _age += Time.deltaTime;
            float k = Mathf.Clamp01(1f - _age / Life);
            foreach (LineRenderer l in _lines)
            {
                if (!l) continue;
                Color s = l.startColor; s.a = 0.9f * k; l.startColor = s;
            }
            if (_age >= Life) Destroy(gameObject);
        }
    }

    /// <summary>
    /// The parts pallet. Everyone sees it fall; only the player who ordered it deals the damage.
    /// Backordered: a yellow drop zone with a countdown, then it lands 4 s late, bigger and harder.
    /// </summary>
    public class CmxPallet : MonoBehaviour
    {
        private CharacterBody _owner;
        private bool _authority, _backordered, _landed;
        private Vector3 _ground;
        private float _wait, _fall = 0.75f, _age, _size;
        private GameObject _visual, _ring;
        private const float Height = 38f;
        private int _lastCount = -1;

        public static void Drop(CharacterBody owner, Vector3 at, bool backordered, bool authority)
        {
            GameObject go = new GameObject("CmxPalletDrop");
            CmxPallet p = go.AddComponent<CmxPallet>();
            p._owner = owner;
            p._authority = authority;
            p._backordered = backordered;
            p._size = backordered ? 2f : 1.2f;
            p._wait = backordered ? 4f : 0.25f;
            p._ground = at;
            if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 60f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                p._ground = hit.point;
            float radius = CmxPlugin.PalletRadius.Value * (backordered ? 1.4f : 1f);
            p._ring = CmxFx.Ring(p._ground, radius);
            p._visual = CmxFx.BuildPallet(p._size);
            p._visual.transform.position = p._ground + Vector3.up * Height;
            p._visual.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            p._visual.SetActive(false);
            if (backordered && authority) CmxHud.Bubble(null, p._ground + Vector3.up * 2f, "<color=#FFD040>" + Lines.Pick(Lines.Backordered) + "</color>", Style.Customer);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age < _wait)
            {
                int left = Mathf.CeilToInt(_wait - _age);
                if (_backordered && _authority && left != _lastCount && left <= 3)
                {
                    _lastCount = left;
                    CmxHud.Bubble(null, _ground + Vector3.up * 1.5f, "<color=#FFD040>ETA " + left + "...</color>", Style.Small);
                }
                return;
            }
            if (!_landed)
            {
                if (_visual && !_visual.activeSelf) _visual.SetActive(true);
                float k = Mathf.Clamp01((_age - _wait) / _fall);
                if (_visual) _visual.transform.position = _ground + Vector3.up * (Height * (1f - k * k));
                if (k >= 1f) Land();
                return;
            }
            if (_age > _wait + _fall + 3f)
            {
                if (_visual) _visual.transform.position += Vector3.down * Time.deltaTime * 2f;
                if (_age > _wait + _fall + 4.5f) Destroy(gameObject);
            }
        }

        private void Land()
        {
            _landed = true;
            if (_ring) Destroy(_ring);
            float radius = CmxPlugin.PalletRadius.Value * (_backordered ? 1.4f : 1f);
            GameObject fx = CmxFx.Boom(_backordered);
            if (fx) EffectManager.SpawnEffect(fx, new EffectData { origin = _ground, scale = radius }, false);
            if (_owner) Util.PlaySound("Play_captain_shift_impact", _owner.gameObject);
            if (_authority) CmxAudio.Play("stamp");

            if (_authority && _owner && _owner.teamComponent)
            {
                new BlastAttack
                {
                    attacker = _owner.gameObject,
                    inflictor = _owner.gameObject,
                    teamIndex = _owner.teamComponent.teamIndex,
                    position = _ground + Vector3.up * 0.5f,
                    radius = radius,
                    baseDamage = _owner.damage * CmxPlugin.PalletDamage.Value * (_backordered ? 2f : 1f),
                    baseForce = 1200f,
                    bonusForce = Vector3.up * 900f,
                    crit = Util.CheckRoll(_owner.crit, _owner.master),
                    falloffModel = BlastAttack.FalloffModel.None,
                    procCoefficient = 1f
                }.Fire();
            }
        }

        private void OnDestroy()
        {
            if (_visual) Destroy(_visual);
            if (_ring) Destroy(_ring);
        }
    }
}
