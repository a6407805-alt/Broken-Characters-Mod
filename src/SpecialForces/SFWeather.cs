using RoR2;
using UnityEngine;

namespace SpecialForces
{
    // The storm, on every machine (local visuals only): rain that follows the camera, heavier and foggier away from
    // the control tower ("the only thing keeping this rainstorm at bay"), lightning in the distance with a flash of
    // the moon light and thunder arriving after the flash, and the rain/wind sound loop.
    public class SFWeather : MonoBehaviour
    {
        public const float ShieldRadius = 110f;
        private ParticleSystem rain;
        private ParticleSystem.EmissionModule emission;
        private Light moon;
        private float moonBase, nextStrike, flashUntil, baseFog;
        private Transform cameraTransform;

        private void Start()
        {
            foreach (Light l in FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) { moon = l; break; }
            if (moon) moonBase = moon.intensity;
            baseFog = RenderSettings.fogDensity;
            BuildRain();
            nextStrike = Time.time + 3f;
            SFAudio.SetRain(true);
        }

        private void OnDestroy() => SFAudio.SetRain(false);

        private void BuildRain()
        {
            Material mat = SFUtil.Load<Material>(Keys.MatRain, "matRain");
            if (!mat) return;
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>();
            rain.Stop();
            var main = rain.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(32f, 40f);
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
            main.startColor = new Color(0.65f, 0.72f, 0.85f, 0.55f);
            main.maxParticles = 12000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.4f;
            emission = rain.emission;
            emission.rateOverTime = 3000f * SFConfig.RainDensity.Value;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 1f, 70f);
            shape.rotation = new Vector3(90f, 0f, 0f);       // emit downwards from a horizontal slab
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4f;
            renderer.velocityScale = 0.05f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rain.Play();
        }

        private void LateUpdate()
        {
            if (!cameraTransform)
            {
                var rig = CameraRigController.readOnlyInstancesList.Count > 0 ? CameraRigController.readOnlyInstancesList[0] : null;
                if (rig && rig.sceneCam) cameraTransform = rig.sceneCam.transform;
                else return;
            }
            Vector3 cam = cameraTransform.position;
            if (rain) rain.transform.position = cam + Vector3.up * 22f + cameraTransform.forward * 12f;

            // Calm inside the tower's shield, heavier outside.
            float storm = SFConfig.HeavyRain.Value ? Mathf.InverseLerp(ShieldRadius * 0.8f, ShieldRadius * 2.5f, Vector3.Distance(cam, SFScene.TowerPosition)) : 0.5f;
            if (rain) emission.rateOverTime = Mathf.Lerp(1400f, 4200f, storm) * SFConfig.RainDensity.Value;
            RenderSettings.fogDensity = baseFog * Mathf.Lerp(0.75f, 1.6f, storm);

            if (Time.time >= nextStrike)
            {
                nextStrike = Time.time + Random.Range(SFConfig.LightningMinSeconds.Value, SFConfig.LightningMaxSeconds.Value) * Mathf.Lerp(1.3f, 0.7f, storm);
                Strike(cam);
            }
            if (moon) moon.intensity = Time.time < flashUntil ? moonBase * (2.5f + 2f * Mathf.PerlinNoise(Time.time * 30f, 1f)) : moonBase;
        }

        // A bolt somewhere in the distance (purely visual), the sky flashing, then thunder after the light.
        private void Strike(Vector3 cam)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(150f, 480f);
            Vector3 ground = SFUtil.Ground(cam + new Vector3(dir.x, 0, dir.y) * distance, 300f);
            var bolt = new GameObject("DistantBolt");
            SFEffects.DrawBolt(bolt, ground + new Vector3(Random.Range(-60f, 60f), 260f, Random.Range(-60f, 60f)), ground, 4f, 18);
            Destroy(bolt, 0.25f);
            flashUntil = Time.time + Random.Range(0.12f, 0.3f);
            float delay = Mathf.Min(2.5f, distance / 340f);
            int which = Random.Range(0, 3);
            Invoke(which == 0 ? nameof(Thunder1) : which == 1 ? nameof(Thunder2) : nameof(Thunder3), delay);
        }

        private void Thunder1() => SFAudio.Play(SFAudio.Clip.Thunder1);
        private void Thunder2() => SFAudio.Play(SFAudio.Clip.Thunder2);
        private void Thunder3() => SFAudio.Play(SFAudio.Clip.Thunder3);
    }

    // Static dressing: floodlights around the outpost, mist at the foot of each waterfall, the tower's light.
    internal static class SFDecor
    {
        public static void Build(GameObject map)
        {
            foreach (var pair in SFScene.MarkersStartingWith("SF_Floodlight_"))
            {
                Vector3 p = SFUtil.Ground(pair.Value, 20f);
                Vector3 look = SFScene.TowerPosition - p;
                look.y = 0;
                Quaternion rot = Quaternion.LookRotation(-look.normalized);    // the lamps face the -Y side of the model
                GameObject pole = SFAssets.Spawn(SFAssets.Floodlight, p, rot);
                Transform lamp = pole ? SFUtil.FindDeep(pole.transform, "Lamp") : null;
                var light = new GameObject("FloodLight").AddComponent<Light>();
                light.transform.position = lamp ? lamp.position : p + Vector3.up * 10f;
                light.transform.rotation = Quaternion.LookRotation((SFScene.TowerPosition - light.transform.position).normalized + Vector3.down * 0.6f);
                light.type = LightType.Spot;
                light.spotAngle = 70f;
                light.range = 70f;
                light.intensity = 3f;
                light.color = new Color(1f, 0.93f, 0.8f);
                if (pole) pole.AddComponent<MeshColliderAdder>();
            }
            foreach (var pair in SFScene.MarkersStartingWith("SF_Waterfall_"))
                Mist(pair.Value);
            var core = new GameObject("TowerGlow").AddComponent<Light>();
            core.transform.position = SFScene.TowerPosition + Vector3.up * 30f;
            core.type = LightType.Point;
            core.range = 80f;
            core.intensity = 4f;
            core.color = new Color(0.4f, 0.85f, 1f);
        }

        private static void Mist(Vector3 at)
        {
            Material smoke = SFUtil.Load<Material>(Keys.MatSmoke, "matGenericSmoke");
            if (!smoke) return;
            var go = new GameObject("WaterfallMist");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startColor = new Color(0.75f, 0.82f, 0.9f, 0.18f);
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 4f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = smoke;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        // Floodlight poles get a collider so players don't walk through them.
        private class MeshColliderAdder : MonoBehaviour
        {
            private void Start()
            {
                foreach (var f in GetComponentsInChildren<MeshFilter>())
                {
                    var mc = f.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = f.sharedMesh;
                    f.gameObject.layer = LayerIndex.world.intVal;
                }
                Destroy(this);
            }
        }
    }
}
