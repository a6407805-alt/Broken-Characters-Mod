using RoR2;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Surveyor
{
    /// <summary>
    /// Shimmer Demon raid-boss rules (v0.6.8):
    /// - Elemental phase shield: only the matching minigun ammo cracks it. The boss bar takes that element's colour.
    ///   Fight 1 of a run needs 1 element, fight 2 rotates between 2, fight 3+ rotates through 3.
    /// - Spectral teleportation: every 8-13 s, or under heavy fire, it blinks (0.7 s telegraph, then a pop) to flank
    ///   its victim or to a high perch. The LiDAR marks the new spot through walls. No blinking while the shield is cracked.
    /// - Loot: red (Tier 3) items, one per element cracked during the fight (1 to 3).
    /// </summary>
    public partial class SvrDemon
    {
        public static int FightNumber;
        private static Run _fightRun;

        public static readonly string[] ElementNames = { "IRIDIUM AP", "CRYO", "INCENDIARY", "PLASMA" };
        public static readonly Color[] ElementColors =
        {
            new Color(0.86f, 0.88f, 0.92f, 0.98f),   // grey / white = Iridium AP
            new Color(0.2f, 0.92f, 1f, 0.98f),       // cyan = Cryo
            new Color(1f, 0.42f, 0.16f, 0.98f),      // orange / red = Incendiary
            new Color(0.62f, 0.38f, 1f, 0.98f)       // purple / blue = Plasma
        };

        public int[] Elements = { 0 };
        public int ElementIdx;
        public float Crack;                 // 0..1 progress of the current shield crack
        private int _masteredMask;

        private float _blinkTimer = 10f, _blinkWind, _heavyFire, _heavyFireT;
        private Vector3 _blinkTo;
        public Vector3 LastBlinkFrom, LastBlinkTo;
        public float LastBlinkAt = -10f;
        public float BlinkWindup { get { return _blinkWind > 0f ? 1f - _blinkWind / 0.7f : 0f; } }
        public Vector3 BlinkTarget { get { return _blinkTo; } }

        public static string ElementName(int e) { return ElementNames[((e % 4) + 4) % 4]; }

        /// <summary>The element the shield needs right now. A locked ammo module falls back to one she can use.</summary>
        public int RequiredElement
        {
            get
            {
                if (Elements == null || Elements.Length == 0) return 0;
                for (int k = 0; k < Elements.Length; k++)
                {
                    int e = Elements[(ElementIdx + k) % Elements.Length];
                    if (SvrUpgrades.IsMinigunUnlocked(e)) return e;
                }
                return 0;
            }
        }

        private void BeginFight()
        {
            if (_fightRun != Run.instance) { _fightRun = Run.instance; FightNumber = 0; }
            FightNumber++;
            int n = Mathf.Clamp(FightNumber, 1, 3);
            int[] pool = { 0, 1, 2, 3 };
            for (int i = pool.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); int t = pool[i]; pool[i] = pool[j]; pool[j] = t; }
            Elements = new int[n];
            for (int i = 0; i < n; i++) Elements[i] = pool[i];
            ElementIdx = 0;
            Crack = 0f;
            _masteredMask = 0;
            _blinkTimer = Random.Range(9f, 14f);
            _blinkWind = 0f;
            _heavyFire = 0f;
        }

        /// <summary>A spooled minigun hit with the right ammo.</summary>
        private void ElementHit(bool anchored)
        {
            Crack += anchored ? 0.07f : 0.045f;
            if (Crack < 1f) return;
            Crack = 0f;
            _masteredMask |= 1 << RequiredElement;
            VulnerableTimer = 7f;
            _blinkWind = 0f;
            Util.PlaySound("Play_item_proc_shieldBreak", PhysicalGo);
            LastRestoredSense = ElementName(RequiredElement) + " SHIELD CRACKED — ALL PLAYERS ATTACK NOW!";
            LastRestoredUntil = Time.time + 3.8f;
            try { Chat.AddMessage("<color=#ffdd33>[BOSS EXPOSED]</color> <color=#ff3b30>The Shimmer Demon's " + ElementName(RequiredElement) + " shield shattered!</color> <color=#90e0ef>Everyone attack!</color>"); }
            catch { }
        }

        private void RotateElement()
        {
            Crack = 0f;
            if (Elements != null && Elements.Length > 1) ElementIdx = (ElementIdx + 1) % Elements.Length;
        }

        private void NoteHeavyFire()
        {
            _heavyFire += 1f;
            _heavyFireT = 2f;
        }

        // ---------------------------------------------------------------- spectral teleportation

        private void BlinkTick(float dt, Vector3 targetPos)
        {
            if (_heavyFireT > 0f) { _heavyFireT -= dt; if (_heavyFireT <= 0f) _heavyFire = 0f; }
            if (Weakened) { _blinkWind = 0f; return; }           // fair window: no teleporting while the shield is cracked
            if (_blinkWind > 0f)
            {
                _blinkWind -= dt;
                if (_blinkWind <= 0f) DoBlink();
                return;
            }
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f || _heavyFire >= 14f) StartBlink(targetPos);
        }

        private void StartBlink(Vector3 targetPos)
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                float ang = Random.value * 6.283f;
                Vector3 c = targetPos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(14f, 28f);
                RaycastHit h;
                if (!Physics.Raycast(c + Vector3.up * 30f, Vector3.down, out h, 80f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) continue;
                float y = h.point.y;
                if (y > targetPos.y + 20f) continue;
                float score = (y - targetPos.y) * 0.6f + Random.value * 4f;     // likes high perches, a bit random
                if (score > bestScore) { bestScore = score; best = h.point; }
            }
            if (bestScore == float.MinValue)
            {
                float ang = Random.value * 6.283f;
                best = targetPos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 18f;
                best.y = GroundY(best, targetPos.y);
            }
            _blinkTo = best;
            _blinkWind = 0.7f;
            _heavyFire = 0f;
        }

        private void DoBlink()
        {
            LastBlinkFrom = Pos;
            Pos = _blinkTo;
            _lastPos = Pos;
            _center = Pos + Vector3.up * 1.4f;
            LastBlinkTo = Pos;
            LastBlinkAt = Time.time;
            _blinkTimer = Random.Range(8f, 13f);
            State = Mode.Stalk;
            _modeTimer = Random.Range(6f, 10f);
            if (PhysicalGo) PhysicalGo.transform.position = Pos + Vector3.up * 1.0f;
            Util.PlaySound("Play_voidman_shift_pop", PhysicalGo);
            LastRestoredSense = "DEMON BLINKED — LiDAR TRACKING NEW POSITION";
            LastRestoredUntil = Time.time + 1.8f;
        }

        /// <summary>The Black Hole drags it in too (unless it is being captured).</summary>
        public void PullToward(Vector3 at, float metresPerSecond, float dt)
        {
            if (!Active || TetherActive) return;
            Pos = Vector3.MoveTowards(Pos, at - Vector3.up * 1.0f, metresPerSecond * dt);
            _center = Pos + Vector3.up * 1.4f;
        }

        // ---------------------------------------------------------------- loot

        private void DropLoot()
        {
            try
            {
                if (!UnityEngine.Networking.NetworkServer.active || !Run.instance) return;
                int mastered = 0;
                for (int i = 0; i < 4; i++) if ((_masteredMask & (1 << i)) != 0) mastered++;
                int count = Mathf.Clamp(Mathf.Max(1, mastered), 1, 3);
                var pool = Run.instance.availableTier3DropList;
                for (int i = 0; i < count; i++)
                {
                    PickupIndex drop = (pool != null && pool.Count > 0) ? pool[Random.Range(0, pool.Count)] : PickupCatalog.FindPickupIndex(ItemTier.Tier3);
                    float ang = (i - (count - 1) * 0.5f) * 0.9f;
                    Vector3 v = Vector3.up * 16f + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 6f;
                    PickupDropletController.CreatePickupDroplet(drop, Pos + Vector3.up * 1.6f, v);
                }
            }
            catch { }
        }
    }
}
