using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace Surveyor
{
    /// <summary>
    /// Heavy-gunner body language layered on top of Commando's animations (runs after the Animator, every frame):
    /// the left hand reaches for the minigun's foregrip (two-bone IK), the gun lags behind when she turns, the chest
    /// leans back under the weight (more while spooled), the hips sway and sink with each heavy step, the muzzle kicks
    /// and the torso shudders while firing. On her own client every step and landing thuds the camera.
    /// Purely visual and local: nothing here is networked.
    /// </summary>
    public class SvrHeavy : MonoBehaviour
    {
        private Transform _hips, _chest, _uaL, _faL, _hL, _uaR, _faR, _grip;
        private SvrSpool _spool;
        private CharacterBody _body;
        private CharacterMotor _motor;
        private CharacterDirection _dir;
        private float _phase, _moveK, _kick;
        private float _lagYaw, _lagYawV, _lagPitch, _lagPitchV, _lastYaw, _lastPitch;
        private bool _init, _wasGrounded = true;

        // Every bone we touch is restored in Update() BEFORE the Animator runs (so additive layers like AimPitch/AimYaw
        // and culled/unkeyed bones never compound our procedural offsets), then saved and posed in LateUpdate().
        private class Bone
        {
            public Transform T;
            private Quaternion _baseR;
            private Vector3 _baseP;
            private bool _saved;
            public Bone(Transform t) { T = t; }
            public void Restore()
            {
                if (!T || !_saved) return;
                T.localRotation = _baseR;
                T.localPosition = _baseP;
                _saved = false;
            }
            public void Save()
            {
                if (!T) return;
                _baseR = T.localRotation;
                _baseP = T.localPosition;
                _saved = true;
            }
        }
        private Bone[] _bones;

        public void Setup(Dictionary<string, Transform> game, Transform grip)
        {
            game.TryGetValue("hips", out _hips);
            if (!game.TryGetValue("chest", out _chest)) game.TryGetValue("spine", out _chest);
            game.TryGetValue("upper_arm.L", out _uaL);
            game.TryGetValue("forearm.L", out _faL);
            game.TryGetValue("hand.L", out _hL);
            game.TryGetValue("upper_arm.R", out _uaR);
            game.TryGetValue("forearm.R", out _faR);
            _grip = grip;
        }

        private void Awake()
        {
            _body = GetComponentInParent<CharacterBody>();
            if (!_body || transform.root.name.Contains("Display")) enabled = false;
        }

        private void Start()
        {
            _bones = new[] { new Bone(_hips), new Bone(_chest), new Bone(_uaR), new Bone(_faR), new Bone(_uaL), new Bone(_faL), new Bone(_hL) };
            _spool = GetComponentInParent<SvrSpool>();
            _body = GetComponentInParent<CharacterBody>();
            if (!_body || transform.root.name.Contains("Display")) { enabled = false; return; }       // character-select display: keep the plain pose
            _motor = _body.characterMotor;
            _dir = _body.characterDirection;
        }

        private void OnDisable()
        {
            if (_bones == null) return;
            foreach (Bone b in _bones) b.Restore();
        }

        private void Update()
        {
            if (_bones == null) return;
            foreach (Bone b in _bones) b.Restore();
        }

        private void LateUpdate()
        {
            if (_bones == null) return;
            foreach (Bone b in _bones) b.Restore();
            if (!SvrPlugin.HeavyMotion.Value || Time.deltaTime <= 0f) return;
            foreach (Bone b in _bones) b.Save();
            Pose(Time.deltaTime);
        }

        private void Pose(float dt)
        {
            Transform m = transform;
            Vector3 up = m.up, right = m.right, fwd = m.forward;
            float spool = _spool ? _spool.Spool : 0f;

            // ---- movement: step phase from distance travelled, one heavy footfall every 1.35 m
            Vector3 vel = _motor ? _motor.velocity : Vector3.zero;
            vel.y = 0f;
            float speed = vel.magnitude;
            bool grounded = !_motor || _motor.isGrounded;
            _moveK = Mathf.MoveTowards(_moveK, grounded ? Mathf.Clamp01(speed / 6f) : 0f, dt * 4f);
            float prev = _phase;
            _phase += speed * dt / 1.35f * Mathf.PI;
            if (grounded && speed > 1f && Mathf.Floor(prev / Mathf.PI) != Mathf.Floor(_phase / Mathf.PI)) Thud(0.25f + 0.35f * _moveK + 0.3f * spool, false);
            if (_phase > Mathf.PI * 200f) _phase -= Mathf.PI * 200f;
            if (_motor && grounded && !_wasGrounded) Thud(1.3f, true);
            _wasGrounded = grounded;

            // ---- the gun lags behind turns and aim changes (heavier when spooled)
            Vector3 aim = _body && _body.inputBank ? _body.inputBank.aimDirection : fwd;
            float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
            float yawRate = _init ? Mathf.DeltaAngle(_lastYaw, yaw) / dt : 0f;
            float pitchRate = _init ? (pitch - _lastPitch) / dt : 0f;
            _lastYaw = yaw; _lastPitch = pitch; _init = true;
            float lag = 0.05f * (1f + 1.2f * spool);
            _lagYaw = Mathf.SmoothDamp(_lagYaw, Mathf.Clamp(-yawRate * lag, -16f, 16f), ref _lagYawV, 0.14f);
            _lagPitch = Mathf.SmoothDamp(_lagPitch, Mathf.Clamp(pitchRate * lag, -10f, 10f), ref _lagPitchV, 0.14f);

            // ---- recoil pulses from the minigun
            if (_spool && _spool.KickPulse > 0f) { _kick = Mathf.Min(1.2f, _kick + _spool.KickPulse); _spool.KickPulse = 0f; }
            _kick *= Mathf.Exp(-11f * dt);

            // ---- turning is responsive
            if (_dir) _dir.turnSpeed = Mathf.Lerp(720f, 540f, spool);

            float roll = Mathf.Sin(_phase) * 3.2f * _moveK;
            float sink = 0.035f * _moveK * Mathf.Abs(Mathf.Cos(_phase)) + 0.012f * spool;

            if (_hips)
            {
                _hips.position -= up * sink;
                _hips.rotation = Quaternion.AngleAxis(roll, fwd) * _hips.rotation;
            }
            if (_chest)
            {
                float lean = 3f + 5f * spool + 2f * _moveK;
                Quaternion q = Quaternion.AngleAxis(-lean, right) * Quaternion.AngleAxis(-roll * 0.7f, fwd);
                if (_kick > 0.02f) q = Quaternion.AngleAxis(Random.Range(-1f, 1f) * 1.1f * _kick, up) * Quaternion.AngleAxis(-1.5f * _kick, right) * q;
                _chest.rotation = q * _chest.rotation;
            }
            if (_uaR)
            {
                float bob = Mathf.Sin(_phase * 2f) * 1.6f * _moveK;
                _uaR.rotation = Quaternion.AngleAxis(_lagYaw, up) * Quaternion.AngleAxis(-_lagPitch + bob, right) * _uaR.rotation;
            }
            if (_faR && _kick > 0.01f) _faR.rotation = Quaternion.AngleAxis(-7f * _kick, right) * _faR.rotation;   // the muzzle climbs

            // ---- left hand on the foregrip
            if (_grip && _uaL && _faL && _hL)
            {
                Vector3 pole = _uaL.position - right * 0.35f - up * 0.4f + fwd * 0.05f;
                TwoBone(_uaL, _faL, _hL, _grip.position, pole);
            }
        }

        private void Thud(float amp, bool landing)
        {
            if (!_body || !_body.hasEffectiveAuthority) return;
            amp *= SvrPlugin.HeavyStepShake.Value;
            if (amp <= 0.01f) return;
            try
            {
                ShakeEmitter.CreateSimpleShakeEmitter(_body.footPosition, new Wave { amplitude = amp, frequency = landing ? 16f : 24f, cycleOffset = 0f }, landing ? 0.35f : 0.16f, 10f, true);
            }
            catch { }
            if (landing) Util.PlaySound("Play_char_land", _body.gameObject);
        }

        /// <summary>Analytic two-bone IK: bends a (shoulder) and b (elbow) so c (hand) reaches the target, elbow toward the pole.</summary>
        private static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float lab = (pb - pa).magnitude, lbc = (pc - pb).magnitude;
            if (lab < 1e-4f || lbc < 1e-4f) return;
            Vector3 d = target - pa;
            float dist = Mathf.Clamp(d.magnitude, 0.05f, lab + lbc - 0.002f);
            Vector3 dn = d.normalized;
            Vector3 pv = pole - pa;
            Vector3 perp = pv - dn * Vector3.Dot(pv, dn);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dn, Vector3.up);
            perp.Normalize();
            float cosA = Mathf.Clamp((lab * lab + dist * dist - lbc * lbc) / (2f * lab * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            Vector3 elbow = pa + dn * (lab * cosA) + perp * (lab * sinA);
            a.rotation = Quaternion.FromToRotation(pb - pa, elbow - pa) * a.rotation;
            pb = b.position; pc = c.position;
            Vector3 wantC = pa + dn * dist;
            b.rotation = Quaternion.FromToRotation(pc - pb, wantC - pb) * b.rotation;
        }
    }
}
