using R2API;
using RoR2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    // Effects made in code (registered, so EffectManager sends them to every player):
    //   Bolt     a jagged lightning bolt from EffectData.start (sky) to origin (ground) with a flash of light
    //   Warning  a pulsing ring on the ground (radius = EffectData.scale) where a strike will land
    internal static class SFEffects
    {
        public static GameObject Bolt, Warning;
        private static Material lightning;

        public static Material LightningMaterial
        {
            get
            {
                if (!lightning) lightning = SFUtil.Load<Material>(Keys.MatLightning, "matLightningLongBlue");
                return lightning;
            }
        }

        public static void Init()
        {
            Bolt = Make("SFLightningBolt", 0.45f, root => root.AddComponent<BoltVisual>());
            Warning = Make("SFStrikeWarning", 1.25f, root => root.AddComponent<WarningRing>());
        }

        private static GameObject Make(string name, float life, System.Action<GameObject> setup)
        {
            var root = new GameObject(name);
            var effect = root.AddComponent<EffectComponent>();
            effect.applyScale = false;
            effect.soundName = "";
            var vfx = root.AddComponent<VFXAttributes>();
            vfx.vfxPriority = VFXAttributes.VFXPriority.Always;
            vfx.vfxIntensity = VFXAttributes.VFXIntensity.Low;
            root.AddComponent<DestroyOnTimer>().duration = life;
            setup(root);
            GameObject prefab = PrefabAPI.InstantiateClone(root, name, false);
            Object.DestroyImmediate(root);
            ContentAddition.AddEffect(prefab);
            return prefab;
        }

        // Draws a jagged bolt between two points (also used directly by the weather, without the effect system).
        public static LineRenderer DrawBolt(GameObject host, Vector3 from, Vector3 to, float width, int segments = 14)
        {
            var line = host.GetComponent<LineRenderer>();
            if (!line) line = host.AddComponent<LineRenderer>();
            line.material = LightningMaterial;
            line.useWorldSpace = true;
            line.positionCount = segments + 1;
            line.widthMultiplier = width;
            line.startColor = line.endColor = new Color(0.75f, 0.9f, 1f);
            float jag = (to - from).magnitude * 0.06f;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < segments) p += Random.insideUnitSphere * jag;
                line.SetPosition(i, p);
            }
            return line;
        }

        private class BoltVisual : MonoBehaviour
        {
            private LineRenderer line;
            private Light flash;
            private float age;

            private void Start()
            {
                var data = GetComponent<EffectComponent>().effectData;
                Vector3 ground = data != null ? data.origin : transform.position;
                Vector3 sky = data != null && data.start != Vector3.zero ? data.start : ground + Vector3.up * 120f;
                line = DrawBolt(gameObject, sky, ground, 1.6f);
                flash = new GameObject("Flash").AddComponent<Light>();
                flash.transform.SetParent(transform, false);
                flash.transform.position = ground + Vector3.up * 4f;
                flash.type = LightType.Point;
                flash.color = new Color(0.6f, 0.8f, 1f);
                flash.range = 45f;
                flash.intensity = 12f;
            }

            private void Update()
            {
                age += Time.deltaTime;
                float k = Mathf.Clamp01(1f - age / 0.4f);
                if (line)
                {
                    line.widthMultiplier = 1.6f * k * (0.6f + 0.4f * Mathf.PerlinNoise(age * 40f, 0f));
                    line.enabled = k > 0.05f;
                }
                if (flash) flash.intensity = 12f * k * k;
            }
        }

        private class WarningRing : MonoBehaviour
        {
            private LineRenderer ring;
            private float radius, age;

            private void Start()
            {
                var data = GetComponent<EffectComponent>().effectData;
                radius = data != null && data.scale > 0 ? data.scale : 6f;
                Vector3 c = data != null ? data.origin : transform.position;
                ring = gameObject.AddComponent<LineRenderer>();
                ring.material = LightningMaterial;
                ring.useWorldSpace = true;
                ring.loop = true;
                ring.positionCount = 40;
                for (int i = 0; i < 40; i++)
                {
                    float a = i / 40f * Mathf.PI * 2f;
                    ring.SetPosition(i, SFUtil.Ground(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, 8f) + Vector3.up * 0.3f);
                }
            }

            private void Update()
            {
                age += Time.deltaTime;
                if (ring) ring.widthMultiplier = 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(age * 14f)) + age * 0.3f;
            }
        }
    }
}
