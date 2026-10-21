using System.Collections.Generic;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace CarMaxSpecialist
{
    // ================================================================ M1: Completed Repair Orders

    public class CmxEmailState : BaseSkillState
    {
        public static float BaseDuration = 0.32f;
        private float _duration;

        public override void OnEnter()
        {
            base.OnEnter();
            _duration = BaseDuration / attackSpeedStat;
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");

            CmxShift shift = GetComponent<CmxShift>();
            bool replyAll = false;
            if (shift)
            {
                if (Time.time - shift.LastEmail > 1.5f) shift.EmailCount = 0;
                shift.LastEmail = Time.time;
                shift.EmailCount++;
                replyAll = shift.EmailCount % 3 == 0;
            }

            Util.PlaySound(replyAll ? "Play_mage_m1_shoot" : "Play_huntress_m1_shoot", gameObject);
            if (isAuthority) CmxAudio.Play(replyAll ? "stamp" : "email");

            Vector3 from = CmxFx.Hand(characterBody, aim);
            int count = replyAll ? 3 : 1;
            bool crit = isAuthority && RollCrit();
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = aim.direction;
                if (count > 1) dir = Quaternion.AngleAxis((i - 1) * 7f, Vector3.up) * dir;
                CmxFx.Paper(from, dir, 90f, replyAll);
                if (isAuthority)
                {
                    new BulletAttack
                    {
                        owner = gameObject,
                        weapon = gameObject,
                        origin = aim.origin,
                        aimVector = dir,
                        minSpread = 0f,
                        maxSpread = 0f,
                        damage = damageStat * CmxPlugin.EmailDamage.Value,
                        force = 150f,
                        tracerEffectPrefab = null,
                        hitEffectPrefab = CmxFx.HitSpark,
                        isCrit = crit,
                        radius = 0.6f,
                        smartCollision = true,
                        maxDistance = 90f,
                        procCoefficient = 1f,
                        stopperMask = LayerIndex.world.mask
                    }.Fire();
                }
            }
            if (replyAll && isAuthority) CmxHud.Bubble(transform, transform.position, "<color=#7CFF7C>RO STACK</color>", Style.Small);
            characterBody.AddSpreadBloom(0.4f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= _duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    // ================================================================ M2: Inventory Audit

    public class CmxAuditState : BaseSkillState
    {
        public static float BaseDuration = 0.45f;
        private float _duration;

        public override void OnEnter()
        {
            base.OnEnter();
            _duration = BaseDuration / attackSpeedStat;
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_UI_menuClick", gameObject);
            if (isAuthority) CmxAudio.Play("beep");
            CmxFx.Scan(CmxFx.Hand(characterBody, aim), aim.direction, CmxPlugin.AuditRange.Value, 32f);
            if (NetworkServer.active)
            {
                CmxShift shift = GetComponent<CmxShift>();
                if (shift) shift.ServerAudit(aim);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= _duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
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
        private float _t, _len, _speed;
        private Vector3 _spin;

        public void Init(Vector3 a, Vector3 b, float speed)
        {
            _a = a; _b = b; _speed = speed;
            _len = Mathf.Max(0.1f, Vector3.Distance(a, b));
            _spin = new Vector3(Random.Range(-900f, 900f), Random.Range(-300f, 300f), Random.Range(-900f, 900f));
        }

        private void Update()
        {
            _t += Time.deltaTime * _speed / _len;
            transform.position = Vector3.Lerp(_a, _b, _t);
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
