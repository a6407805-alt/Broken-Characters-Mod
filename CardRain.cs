using System;
using R2API;
using RoR2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DuelistMod
{
    /// <summary>
    /// What everyone else sees while you duel in the sky: every hit in the duel drops a card out of
    /// the sky onto the enemies it damaged. It is a registered effect sent through EffectManager, so
    /// it plays on every player's machine (they all have the mod, as with any modded survivor).
    /// effectData.genericUInt carries the item index + 1 of the card that was played (0 = plain card).
    /// </summary>
    public static class CardRain
    {
        public static GameObject EffectPrefab;
        internal static GameObject Impact;

        public static void Create()
        {
            try
            {
                // Built under an inactive holder so nothing on it runs before it becomes a prefab.
                GameObject holder = new GameObject("DuelistCardRainHolder");
                holder.SetActive(false);
                GameObject tmp = new GameObject("DuelistCardStrike");
                tmp.transform.SetParent(holder.transform, false);
                tmp.AddComponent<EffectComponent>();
                VFXAttributes vfx = tmp.AddComponent<VFXAttributes>();
                vfx.vfxPriority = VFXAttributes.VFXPriority.Always;
                vfx.vfxIntensity = VFXAttributes.VFXIntensity.Low;
                tmp.AddComponent<CardStrikeEffect>();

                EffectPrefab = PrefabAPI.InstantiateClone(tmp, "DuelistCardStrike", false);
                Object.Destroy(holder);
                ContentAddition.AddEffect(EffectPrefab);
                DuelistPlugin.Log.LogInfo("Card rain effect registered.");
            }
            catch (Exception e)
            {
                EffectPrefab = null;
                DuelistPlugin.Log.LogError("Card rain effect failed to register:\n" + e);
            }
        }

        /// <summary>Drops one card onto a spot on the map, for every player. Server only.</summary>
        public static void Drop(Vector3 target, ItemIndex item)
        {
            if (!EffectPrefab || !DuelConfig.CardRain.Value) return;
            try
            {
                EffectManager.SpawnEffect(EffectPrefab, new EffectData
                {
                    origin = target,
                    genericUInt = item == ItemIndex.None ? 0u : (uint)((int)item + 1),
                    scale = 1f
                }, true);
            }
            catch { }
        }
    }

    /// <summary>Animates one falling card: spins down from high above, burst on impact, then fades.</summary>
    public class CardStrikeEffect : MonoBehaviour
    {
        private const float FallTime = 0.5f;
        private const float StayTime = 0.7f;

        private Transform _card;
        private Vector3 _start, _target, _spinAxis;
        private float _t;
        private bool _landed;

        private void Start()
        {
            _target = transform.position;
            uint data = 0;
            EffectComponent ec = GetComponent<EffectComponent>();
            if (ec && ec.effectData != null) data = ec.effectData.genericUInt;

            Texture art = null;
            if (data > 0)
            {
                try
                {
                    ItemDef def = ItemCatalog.GetItemDef((ItemIndex)((int)data - 1));
                    if (def) art = def.pickupIconTexture;
                }
                catch { }
            }

            GameObject card = new GameObject("FallingCard");
            card.transform.SetParent(transform, false);
            Material back = DuelistVisuals.MakeMaterial(new Color(0.5f, 0.16f, 0.4f), new Color(0.6f, 0.2f, 0.5f) * 0.8f);
            Material face = DuelistVisuals.MakeMaterial(new Color(0.95f, 0.78f, 0.3f), new Color(0.9f, 0.7f, 0.2f) * 0.8f);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "Back", card.transform, Vector3.zero, new Vector3(1.1f, 0.04f, 1.6f), back);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "Face", card.transform, new Vector3(0f, 0.025f, 0f), new Vector3(0.95f, 0.02f, 1.45f), face);
            if (art)
            {
                Shader unlit = DuelistVisuals.UnlitShader;
                Material m = unlit ? new Material(unlit) : DuelistVisuals.MakeMaterial(Color.white, Color.white * 0.4f);
                m.mainTexture = art;
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Collider c = q.GetComponent<Collider>();
                if (c) Object.DestroyImmediate(c);
                q.transform.SetParent(card.transform, false);
                q.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                q.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
                q.GetComponent<Renderer>().sharedMaterial = m;
            }
            card.transform.localScale = Vector3.one * 1.4f;
            _card = card.transform;

            Vector2 drift = UnityEngine.Random.insideUnitCircle * 3f;
            _start = _target + new Vector3(drift.x, 24f, drift.y);
            _spinAxis = UnityEngine.Random.onUnitSphere;
            _card.position = _start;
            _card.rotation = UnityEngine.Random.rotation;
        }

        private void Update()
        {
            if (!_card) { Destroy(gameObject); return; }
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / FallTime);

            if (!_landed)
            {
                _card.position = Vector3.Lerp(_start, _target, k * k);
                _card.Rotate(_spinAxis, 900f * Time.deltaTime, Space.World);
                if (k >= 1f)
                {
                    _landed = true;
                    _card.position = _target;
                    _card.rotation = Quaternion.Euler(75f, UnityEngine.Random.Range(0f, 360f), 0f); // stuck in the ground at an angle
                    try
                    {
                        if (!CardRain.Impact) CardRain.Impact = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");
                        if (CardRain.Impact) EffectManager.SpawnEffect(CardRain.Impact, new EffectData { origin = _target, scale = 1.6f }, false);
                    }
                    catch { }
                }
            }
            else
            {
                float fade = Mathf.Clamp01((_t - FallTime) / StayTime);
                _card.localScale = Vector3.one * 1.4f * (1f - fade * fade);
                if (_t > FallTime + StayTime) Destroy(gameObject);
            }
        }
    }
}
