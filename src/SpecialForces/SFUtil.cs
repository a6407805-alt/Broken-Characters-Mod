using System;
using System.Collections.Generic;
using System.Reflection;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    internal static class SFUtil
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // Private game members go through reflection: the publicized GameLibs let them compile, but direct access
        // throws in game (Gravitos / Infested General lessons).
        public static void SetField(object target, string field, object value)
        {
            FieldInfo info = FindField(target.GetType(), field);
            if (info == null) SpecialForcesPlugin.Log.LogError($"Field {target.GetType().Name}.{field} not found.");
            else info.SetValue(target, value);
        }

        // Property setters that may be private in game (e.g. SceneDef.cachedName): set through reflection.
        public static void SetProperty(object target, string property, object value)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty(property, AnyInstance | BindingFlags.DeclaredOnly);
                MethodInfo setter = p?.GetSetMethod(true);
                if (setter == null) continue;
                setter.Invoke(target, new[] { value });
                return;
            }
            SpecialForcesPlugin.Log.LogWarning($"Property {target.GetType().Name}.{property} has no setter.");
        }

        public static object GetField(object target, string field) => FindField(target.GetType(), field)?.GetValue(target);

        public static T GetStatic<T>(Type type, string field) where T : class => type.GetField(field, AnyStatic)?.GetValue(null) as T;

        public static void SetStatic(Type type, string field, object value)
        {
            FieldInfo info = type.GetField(field, AnyStatic);
            if (info == null) SpecialForcesPlugin.Log.LogWarning($"Static field {type.Name}.{field} not found.");
            else info.SetValue(null, value);
        }

        public static object Call(object target, string method, params object[] args)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                foreach (MethodInfo m in t.GetMethods(AnyInstance | BindingFlags.DeclaredOnly))
                    if (m.Name == method && m.GetParameters().Length == args.Length) return m.Invoke(target, args);
            }
            SpecialForcesPlugin.Log.LogWarning($"Method {target.GetType().Name}.{method}({args.Length}) not found.");
            return null;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        // Addressable assets by key (GUID keys from RoR2BepInExPack's GameAssetPathsBetter, copied as strings so a
        // newer pack can't break the build). Missing ones log once and return null; callers have fallbacks.
        private static readonly Dictionary<string, Object> loaded = new Dictionary<string, Object>();

        public static T Load<T>(string key, string what) where T : Object
        {
            if (loaded.TryGetValue(key, out Object o)) return o as T;
            T result = null;
            try { result = Addressables.LoadAssetAsync<T>(key).WaitForCompletion(); }
            catch (Exception e) { SpecialForcesPlugin.Log.LogWarning($"Could not load {what} ({key}): {e.Message}"); }
            if (!result) SpecialForcesPlugin.Log.LogWarning($"Game asset {what} not found ({key}).");
            loaded[key] = result;
            return result;
        }

        // TeleportHelper.TeleportBody changed between game versions (2 or 3 arguments): call whichever exists.
        public static void Teleport(CharacterBody body, Vector3 foot)
        {
            foreach (MethodInfo m in typeof(TeleportHelper).GetMethods(AnyStatic))
            {
                if (m.Name != "TeleportBody") continue;
                var p = m.GetParameters();
                if (p.Length == 2) { m.Invoke(null, new object[] { body, foot }); return; }
                if (p.Length == 3 && p[2].ParameterType == typeof(bool)) { m.Invoke(null, new object[] { body, foot, false }); return; }
            }
            if (body.characterMotor) body.characterMotor.Motor.SetPositionAndRotation(foot, ((Component)body).transform.rotation);
            else ((Component)body).transform.position = foot;
        }

        // DirectorCard's spawn card: game 1.5 loads it from an address (GetSpawnCard); older games have the field.
        private static readonly MethodInfo GetSpawnCardMethod = typeof(DirectorCard).GetMethod("GetSpawnCard", AnyInstance);

        public static SpawnCard CardOf(DirectorCard card)
        {
            if (card == null) return null;
            try
            {
                if (GetSpawnCardMethod != null) return GetSpawnCardMethod.Invoke(card, null) as SpawnCard;
            }
            catch (Exception e) { SpecialForcesPlugin.Log.LogWarning("GetSpawnCard failed: " + e.Message); }
            return card.spawnCard;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found) return found;
            }
            return null;
        }

        public static Vector3 Ground(Vector3 p, float up = 60f)
        {
            return Physics.Raycast(p + Vector3.up * up, Vector3.down, out RaycastHit hit, up + 200f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)
                ? hit.point : p;
        }

        public static int PlayerCount => Math.Max(1, Run.instance ? Run.instance.participatingPlayerCount : 1);
    }
}
