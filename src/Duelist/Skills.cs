using System;
using System.Collections.Generic;
using System.Reflection;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace DuelistMod
{
    internal static class SkillAssets
    {
        private static bool _loaded;
        public static GameObject Tracer;
        public static GameObject HitEffect;
        public static GameObject MuzzleFlash;
        public static string ShootSound = "Play_commando_M1";

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Tracer = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerCommandoDefault");
            HitEffect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/HitsparkCommando");
            MuzzleFlash = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/MuzzleFlashes/MuzzleflashCommando");

            // Vanilla states keep their assets in static fields filled at load; prefer those.
            try
            {
                System.Type t = typeof(EntityStates.Commando.CommandoWeapon.FirePistol2);
                object v;
                v = t.GetField("firePistolSoundString", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (v is string s && !string.IsNullOrEmpty(s)) ShootSound = s;
                v = t.GetField("tracerEffectPrefab", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (v is GameObject g1 && g1) Tracer = g1;
                v = t.GetField("hitEffectPrefab", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (v is GameObject g2 && g2) HitEffect = g2;
                v = t.GetField("muzzleEffectPrefab", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (v is GameObject g3 && g3) MuzzleFlash = g3;
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ thrown cards (what everyone sees)

    /// <summary>
    /// A card flying from the Duelist to its mark: it can rise and hover first, then curves in and TRACKS
    /// its target while it flies, spinning, with a light trail. When it arrives, onArrive runs (the owner's
    /// machine deals the hit there). Spawned on every client from the same aim, so everyone sees it.
    /// </summary>
    public class FlyingCard : MonoBehaviour
    {
        private Vector3 _from, _to, _bend;
        private Transform _target;
        private float _t, _dur, _hover;
        private Transform _card;
        private bool _trump;
        private Color _color;
        private Action<Vector3> _onArrive;
        private static Material _violet, _gold, _trail;

        /// <param name="hover">seconds it rises and hangs in the air before it flies (0 = thrown straight away)</param>
        public static void Throw(Vector3 from, Vector3 to, Transform target, bool trump, bool golden, float hover, Vector3 launchDir, Action<Vector3> onArrive)
        {
            try
            {
                GameObject go = new GameObject(trump ? "TrumpCard" : "ThrownCard");
                go.transform.position = from;
                FlyingCard fc = go.AddComponent<FlyingCard>();
                fc._from = from; fc._to = to; fc._target = target; fc._trump = trump; fc._hover = hover; fc._onArrive = onArrive;
                float dist = Vector3.Distance(from, target ? target.position : to);
                fc._dur = Mathf.Clamp(dist / (trump ? 75f : 95f), 0.08f, 0.9f);
                // the curve: leaves along launchDir, then bends into the target
                fc._bend = from + (launchDir.sqrMagnitude > 0.01f ? launchDir.normalized : (to - from).normalized) * Mathf.Min(dist * 0.45f, 9f);
                fc._color = trump || golden ? new Color(1f, 0.8f, 0.3f) : new Color(0.65f, 0.4f, 1f);
                fc.Build(golden || trump);
            }
            catch { }
        }

        private void Build(bool golden)
        {
            if (!_violet) _violet = DuelistVisuals.MakeMaterial(new Color(0.42f, 0.14f, 0.62f), new Color(0.6f, 0.3f, 1f) * 1.4f);
            if (!_gold) _gold = DuelistVisuals.MakeMaterial(new Color(1f, 0.82f, 0.32f), new Color(1f, 0.72f, 0.2f) * 2.2f);
            float s = _trump ? 2.1f : golden ? 1.4f : 1f;
            _card = new GameObject("Card").transform;
            _card.SetParent(transform, false);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "Face", _card, Vector3.zero, new Vector3(0.24f, 0.012f, 0.34f) * s, golden ? _gold : _violet);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "Inlay", _card, new Vector3(0f, 0.008f, 0f) * s, new Vector3(0.14f, 0.012f, 0.22f) * s, golden ? _violet : _gold);
            try
            {
                Shader sh = DuelistVisuals.UnlitShader;
                if (!_trail && sh) _trail = new Material(sh);
                TrailRenderer tr = gameObject.AddComponent<TrailRenderer>();
                tr.material = _trail;
                tr.time = _trump ? 0.25f : 0.14f;
                tr.startWidth = 0.16f * s;
                tr.endWidth = 0f;
                tr.startColor = new Color(_color.r, _color.g, _color.b, 0.8f);
                tr.endColor = new Color(_color.r, _color.g, _color.b, 0f);
                tr.minVertexDistance = 0.1f;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.emitting = _hover <= 0f;
            }
            catch { }
        }

        private Vector3 End => _target ? _target.position : _to;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_hover > 0f)
            {
                // rising out of the sand, hanging in the air, slowly turning to face its mark
                _hover -= dt;
                transform.position += Vector3.up * dt * 1.6f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation((End - transform.position).sqrMagnitude > 0.01f ? End - transform.position : Vector3.forward), dt * 8f);
                if (_card) _card.localRotation = Quaternion.Euler(-70f, 0f, Mathf.Sin(Time.time * 6f) * 10f);
                if (_hover <= 0f)
                {
                    _from = transform.position;
                    _bend = _from + Vector3.up * 1.5f + (End - _from) * 0.25f;
                    TrailRenderer tr = GetComponent<TrailRenderer>();
                    if (tr) tr.emitting = true;
                }
                return;
            }
            _t += dt;
            float k = Mathf.Clamp01(_t / _dur);
            Vector3 end = End;
            Vector3 a = Vector3.Lerp(_from, _bend, k), b = Vector3.Lerp(_bend, end, k);
            Vector3 p = Vector3.Lerp(a, b, k);
            Vector3 vel = b - a;
            transform.position = p;
            if (vel.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(vel);
            if (_card) _card.localRotation = Quaternion.Euler(0f, 0f, _t * (_trump ? 900f : 1400f));
            if (k >= 1f)
            {
                try { _onArrive?.Invoke(end); } catch (Exception e) { DuelistPlugin.Log.LogWarning("Card hit failed: " + e.Message); }
                Partners.Impact(end, _color, _trump ? 1.1f : 0.35f, false);
                if (_card) Destroy(_card.gameObject);
                enabled = false;
                Destroy(gameObject, 0.3f);
            }
        }

        /// <summary>Where a shot from the aim ray lands (skipping your own hurtboxes).</summary>
        public static Vector3 LandPoint(Ray aim, float max, CharacterBody self)
        {
            float best = max;
            foreach (RaycastHit h in Physics.RaycastAll(aim, max, LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore))
            {
                HurtBox hb = h.collider ? h.collider.GetComponent<HurtBox>() : null;
                if (hb && self && hb.healthComponent == self.healthComponent) continue;
                if (h.distance < best) best = h.distance;
            }
            return aim.origin + aim.direction * best;
        }

        /// <summary>The enemy nearest your crosshair within a cone (what a thrown card will track).</summary>
        public static HurtBox Seek(CharacterBody body, Ray aim, float range, float angle)
        {
            if (!body || !body.teamComponent) return null;
            BullseyeSearch s = new BullseyeSearch
            {
                searchOrigin = aim.origin,
                searchDirection = aim.direction,
                maxDistanceFilter = range,
                maxAngleFilter = angle,
                teamMaskFilter = TeamMask.GetEnemyTeams(body.teamComponent.teamIndex),
                sortMode = BullseyeSearch.SortMode.Angle,
                filterByLoS = true
            };
            s.RefreshCandidates();
            foreach (HurtBox hb in s.GetResults()) if (hb && hb.healthComponent && hb.healthComponent.alive) return hb;
            return null;
        }

        /// <summary>Up to n different enemies around a point, nearest first.</summary>
        public static List<HurtBox> SeekMany(CharacterBody body, Vector3 at, float radius, int n)
        {
            List<HurtBox> list = new List<HurtBox>();
            if (!body || !body.teamComponent) return list;
            SphereSearch s = new SphereSearch { origin = at, radius = radius, mask = LayerIndex.entityPrecise.mask };
            s.RefreshCandidates();
            s.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(body.teamComponent.teamIndex));
            s.OrderCandidatesByDistance();
            s.FilterCandidatesByDistinctHurtBoxEntities();
            foreach (HurtBox hb in s.GetHurtBoxes())
                if (hb && hb.healthComponent && hb.healthComponent.alive && list.Count < n) list.Add(hb);
            return list;
        }

        /// <summary>The hand the card leaves from.</summary>
        public static Vector3 Hand(CharacterBody body)
        {
            try
            {
                ModelLocator ml = body ? body.modelLocator : null;
                ChildLocator cl = ml && ml.modelTransform ? ml.modelTransform.GetComponent<ChildLocator>() : null;
                Transform m = cl ? cl.FindChild("MuzzleRight") : null;
                if (m) return m.position;
            }
            catch { }
            return body ? body.corePosition + Vector3.up * 0.3f : Vector3.zero;
        }

        /// <summary>The owner's hit when a card arrives: a short shot into the mark (so procs and crits work).</summary>
        public static void Strike(GameObject owner, Vector3 from, Vector3 at, float damage, bool crit, float force, bool pierce, float radius, float proc)
        {
            if (!owner) return;
            Vector3 dir = at - from;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            BulletAttack b = new BulletAttack
            {
                owner = owner,
                weapon = owner,
                origin = at - dir * 2.5f,
                aimVector = dir,
                minSpread = 0f,
                maxSpread = 0f,
                damage = damage,
                force = force,
                tracerEffectPrefab = null,
                hitEffectPrefab = SkillAssets.HitEffect,
                isCrit = crit,
                radius = radius,
                smartCollision = true,
                maxDistance = pierce ? 60f : 5f,
                procCoefficient = proc
            };
            if (pierce) b.stopperMask = LayerIndex.world.mask;
            b.Fire();
        }
    }

    /// <summary>Counts M1 throws; every third is the Trump Card.</summary>
    public class DuelistTossCombo : MonoBehaviour
    {
        public int Count;
        public float Last;
    }

    // ------------------------------------------------------------------ M1

    /// <summary>
    /// Card Toss: a razor card that curves into the enemy nearest your crosshair and tracks it. Every third
    /// throw in a row is the TRUMP CARD: a huge golden card that pierces the whole line for 2.5x damage.
    /// </summary>
    public class CardTossState : BaseSkillState
    {
        public static float BaseDuration = 0.3f;
        private float _duration;

        public override void OnEnter()
        {
            base.OnEnter();
            SkillAssets.Load();
            _duration = BaseDuration / attackSpeedStat;
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");

            DuelistTossCombo combo = gameObject.GetComponent<DuelistTossCombo>() ?? gameObject.AddComponent<DuelistTossCombo>();
            if (Time.time - combo.Last > 1.6f) combo.Count = 0;
            combo.Last = Time.time;
            combo.Count++;
            bool trump = combo.Count % 3 == 0;

            Util.PlaySound(trump ? "Play_mage_m2_shoot" : SkillAssets.ShootSound, gameObject);
            float range = trump ? 200f : 150f;
            Vector3 hand = FlyingCard.Hand(characterBody);
            HurtBox target = FlyingCard.Seek(characterBody, aim, range, trump ? 14f : 10f);
            Vector3 land = target ? target.transform.position : FlyingCard.LandPoint(aim, range, characterBody);
            Vector3 side = Vector3.Cross(Vector3.up, aim.direction).normalized * (combo.Count % 2 == 0 ? 1f : -1f);

            GameObject owner = gameObject;
            float dmg = damageStat * DuelConfig.TossDamage.Value * (trump ? 2.5f : 1f);
            bool authority = isAuthority, crit = isAuthority && RollCrit();
            FlyingCard.Throw(hand, land, target ? target.transform : null, trump, trump, 0f, aim.direction + side * 0.35f + Vector3.up * 0.15f,
                at => { if (authority) FlyingCard.Strike(owner, hand, at, dmg, crit, trump ? 600f : 120f, trump, trump ? 0.8f : 0.35f, 1f); });
            characterBody.AddSpreadBloom(trump ? 1.2f : 0.5f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= _duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    // ------------------------------------------------------------------ M2

    /// <summary>
    /// Pharaoh's Judgment: the Duelist raises a hand and four golden cards rise from the ground around him,
    /// hang in the air, turn toward their marks, then each hunts down a different enemy.
    /// </summary>
    public class CardFanState : BaseSkillState
    {
        public static float BaseDuration = 0.5f;
        public const int Cards = 4;
        private float _duration;

        public override void OnEnter()
        {
            base.OnEnter();
            SkillAssets.Load();
            _duration = BaseDuration / attackSpeedStat;
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_mage_m1_shoot", gameObject);

            Vector3 mark = FlyingCard.LandPoint(aim, 120f, characterBody);
            List<HurtBox> targets = FlyingCard.SeekMany(characterBody, mark, 22f, Cards);
            HurtBox aimed = FlyingCard.Seek(characterBody, aim, 120f, 12f);
            if (aimed && !targets.Contains(aimed)) targets.Insert(0, aimed);

            Vector3 fwd = aim.direction; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            GameObject owner = gameObject;
            bool authority = isAuthority, crit = isAuthority && RollCrit();
            float dmg = damageStat * DuelConfig.FanDamage.Value * 1.75f;
            for (int i = 0; i < Cards; i++)
            {
                // a crescent behind the shoulders, like a pharaoh's fan
                float a = Mathf.Lerp(-70f, 70f, i / (float)(Cards - 1));
                Vector3 spot = characterBody.corePosition + Quaternion.AngleAxis(a, Vector3.up) * (-fwd) * 1.3f + Vector3.up * 0.2f + right * 0f;
                HurtBox t = targets.Count > 0 ? targets[i % targets.Count] : null;
                Vector3 land = t ? t.transform.position : mark + right * (i - 1.5f) * 2f;
                Vector3 from = spot;
                FlyingCard.Throw(spot, land, t ? t.transform : null, false, true, 0.22f + i * 0.07f, Vector3.up,
                    at => { if (authority) FlyingCard.Strike(owner, from + Vector3.up * 1.5f, at, dmg, crit, 300f, false, 0.6f, 0.8f); });
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= _duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // (levitation and the hoverboard live in Hoverboard.cs)

    // ------------------------------------------------------------------ Shift

    public class SleightOfHandState : BaseSkillState
    {
        public static float Duration = 0.22f;
        public static float SpeedMultiplier = 5.5f;
        private Vector3 _dir;

        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound("Play_huntress_shift_mini_blink", gameObject);

            Vector3 move = inputBank ? inputBank.moveVector : Vector3.zero;
            _dir = move.sqrMagnitude > 0.01f ? move.normalized : (characterDirection ? characterDirection.forward : transform.forward);
            _dir.y = 0f;
            _dir.Normalize();

            if (characterMotor) characterMotor.Motor.ForceUnground();
            if (NetworkServer.active) characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (characterMotor && characterDirection)
            {
                characterMotor.velocity = _dir * (moveSpeedStat * SpeedMultiplier);
                characterDirection.forward = _dir;
            }
            if (fixedAge >= Duration && isAuthority) outer.SetNextStateToMain();
        }

        public override void OnExit()
        {
            if (characterMotor) characterMotor.velocity *= 0.25f;
            if (NetworkServer.active && characterBody.HasBuff(RoR2Content.Buffs.HiddenInvincibility))
                characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // ------------------------------------------------------------------ Special

    /// <summary>
    /// Runs for the whole duel on the Body state machine (so normal movement is off) and can only be
    /// left by finishing the duel or dying. Host only in v0.1.
    /// </summary>
    public class DuelDimensionState : BaseState
    {
        public static readonly HashSet<CharacterBody> ActiveDuelists = new HashSet<CharacterBody>();

        private DuelSession _session;
        private DuelUI _ui;

        public override void OnEnter()
        {
            base.OnEnter();
            if (!isAuthority)
            {
                // a friend is dueling on their machine: the host still marks them as busy
                if (NetworkServer.active && characterBody) ActiveDuelists.Add(characterBody);
                return;
            }

            _session = new DuelSession(characterBody);
            bool ok;
            try
            {
                ok = _session.Begin();
            }
            catch (Exception e)
            {
                // Never leave the player stranded on the board: log, send them back, end the state.
                DuelistPlugin.Log.LogError("Duel failed to start:\n" + e);
                try { Chat.AddMessage("<color=#ff6666>Duel failed to start (see LogOutput.log). Sending you back.</color>"); } catch { }
                try { _session.End(); } catch (Exception e2) { DuelistPlugin.Log.LogError("Cleanup also failed:\n" + e2); }
                ok = false;
            }
            if (!ok)
            {
                _session = null;
                return;
            }

            ActiveDuelists.Add(characterBody);
            try
            {
                _ui = gameObject.AddComponent<DuelUI>();
                _ui.Session = _session;
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogError("Duel UI failed to attach:\n" + e);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;

            if (_session == null)
            {
                outer.SetNextStateToMain();
                return;
            }

            // Emergency exit that doesn't depend on the UI working.
            if (Input.GetKeyDown(KeyCode.Backspace)) _session.Retreat();

            try
            {
                _session.Tick(Time.fixedDeltaTime);
            }
            catch (Exception e)
            {
                _tickErrors++;
                if (_tickErrors <= 3) DuelistPlugin.Log.LogError($"Duel tick error #{_tickErrors}:\n" + e);
                if (_tickErrors >= 30)
                {
                    DuelistPlugin.Log.LogError("Too many duel errors; ending the duel.");
                    try { Chat.AddMessage("<color=#ff6666>The duel broke (see LogOutput.log). Sending you back.</color>"); } catch { }
                    outer.SetNextStateToMain();
                    return;
                }
            }

            if (_session.Finished || (DuelConfig.MaxDuelSeconds.Value > 0f && fixedAge > DuelConfig.MaxDuelSeconds.Value)) outer.SetNextStateToMain();
        }

        private int _tickErrors;

        public override void OnExit()
        {
            if (_ui) Object.Destroy(_ui);
            if (_session != null)
            {
                try { _session.End(); }
                catch (Exception e) { DuelistPlugin.Log.LogError("Duel cleanup failed:\n" + e); }
                _session = null;
            }
            if (characterBody) ActiveDuelists.Remove(characterBody);
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Death;
    }
}
