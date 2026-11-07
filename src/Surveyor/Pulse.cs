using System.Collections.Generic;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Surveyor
{
    /// <summary>
    /// R: LiDAR Pulse (v0.7.0). A 360 shockwave: damage and a stun close by, a dense LiDAR flood for the Surveyor, and
    /// every enemy within range is marked for the WHOLE TEAM through walls for a few seconds (this state runs on every
    /// client, so each player draws the marks on their own screen). The Shimmer Stalker is marked at any range.
    /// </summary>
    public class SvrLidarPulseState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            Vector3 at = characterBody ? characterBody.corePosition : transform.position;
            SvrTeamReveal.Pulse(at, SvrPlugin.PulseRevealRange.Value, SvrPlugin.PulseRevealSeconds.Value);
            Util.PlaySound("Play_captain_drone_zap", gameObject);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            if (!isAuthority) return;

            SvrLidar lidar = GetComponent<SvrLidar>();
            if (lidar) { try { lidar.TriggerOverdrivePulse(); } catch { } }
            AddRecoil(-1f, -1.5f, -0.5f, 0.5f);
            new BlastAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = TeamComponent.GetObjectTeam(gameObject),
                baseDamage = damageStat * SvrPlugin.PulseDamage.Value,
                baseForce = 1200f,
                position = at,
                radius = SvrPlugin.PulseRadius.Value,
                falloffModel = BlastAttack.FalloffModel.None,
                crit = RollCrit(),
                procCoefficient = 1f,
                damageType = DamageType.Stun1s
            }.Fire();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.3f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }

    /// <summary>On every player's screen: red brackets around marked enemies (through walls) until the mark fades.</summary>
    public class SvrTeamReveal : MonoBehaviour
    {
        private static SvrTeamReveal _inst;
        private struct Mark { public CharacterBody Body; public float Until; }
        private readonly List<Mark> _marks = new List<Mark>();
        private GUIStyle _style;

        public static void Pulse(Vector3 at, float range, float seconds)
        {
            if (!_inst)
            {
                GameObject g = new GameObject("SvrTeamReveal");
                DontDestroyOnLoad(g);
                _inst = g.AddComponent<SvrTeamReveal>();
            }
            float until = Time.time + seconds;
            var all = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < all.Count; i++)
            {
                CharacterBody b = all[i];
                if (!b || !b.healthComponent || !b.healthComponent.alive) continue;
                if (!b.teamComponent || b.teamComponent.teamIndex == TeamIndex.Player) continue;
                bool stalker = SvrStalker.IsStalker(b);
                if (!stalker && (b.corePosition - at).sqrMagnitude > range * range) continue;
                _inst._marks.Add(new Mark { Body = b, Until = until + (stalker ? 4f : 0f) });
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _marks.Count == 0) return;
            LocalUser lu = LocalUserManager.GetFirstLocalUser();
            Camera cam = lu != null && lu.cameraRigController ? lu.cameraRigController.sceneCam : null;
            if (!cam) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            float scale = Screen.height / 1080f;
            Texture2D white = Texture2D.whiteTexture;
            float now = Time.time;
            Color old = GUI.color;
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                Mark m = _marks[i];
                if (!m.Body || now > m.Until || !m.Body.healthComponent || !m.Body.healthComponent.alive) { _marks.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(m.Body.corePosition);
                if (sp.z <= 0.3f) continue;
                bool stalker = SvrStalker.IsStalker(m.Body);
                float fade = Mathf.Clamp01((m.Until - now) / 1.5f);
                float size = Mathf.Clamp((stalker ? 900f : 380f) * Mathf.Max(0.5f, m.Body.radius) / sp.z, 16f, stalker ? 220f : 120f) * scale;
                float x = sp.x, y = Screen.height - sp.y;
                Color c = stalker ? new Color(0.85f, 0.25f, 1f, 0.95f * fade) : new Color(1f, 0.22f, 0.15f, 0.9f * fade);
                GUI.color = c;
                float t = 2f * scale, k = size * 0.32f, h = size * 0.5f;
                // four corner brackets
                GUI.DrawTexture(new Rect(x - h, y - h, k, t), white); GUI.DrawTexture(new Rect(x - h, y - h, t, k), white);
                GUI.DrawTexture(new Rect(x + h - k, y - h, k, t), white); GUI.DrawTexture(new Rect(x + h - t, y - h, t, k), white);
                GUI.DrawTexture(new Rect(x - h, y + h - t, k, t), white); GUI.DrawTexture(new Rect(x - h, y + h - k, t, k), white);
                GUI.DrawTexture(new Rect(x + h - k, y + h - t, k, t), white); GUI.DrawTexture(new Rect(x + h - t, y + h - k, t, k), white);
                if (stalker)
                {
                    _style.fontSize = Mathf.Max(10, Mathf.RoundToInt(14f * scale));
                    float d = lu.cachedBody ? Vector3.Distance(lu.cachedBody.corePosition, m.Body.corePosition) : 0f;
                    GUI.Label(new Rect(x - 150f * scale, y - h - 24f * scale, 300f * scale, 20f * scale), "SHIMMER STALKER  " + Mathf.RoundToInt(d) + "m", _style);
                }
            }
            GUI.color = old;
        }
    }
}
