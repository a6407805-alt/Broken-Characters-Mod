using UnityEngine;

namespace SpecialForces
{
    // The ModelLocator's model for structures and the Hurricane: an upright, unrotated object holding the bundle's
    // prefab (whose root carries the Blender-to-Unity rotation). EntityState.GetModelAnimator looks on the model
    // itself, so a hook hands it the Animator inside.
    public class SFModelContainer : MonoBehaviour
    {
        public Animator animator;

        public static GameObject Make(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<SFModelContainer>();
            return go;
        }

        public static void Init()
        {
            On.EntityStates.EntityState.GetModelAnimator += (orig, self) =>
            {
                Animator found = orig(self);
                if (found) return found;
                Transform model = self.outer && self.outer.commonComponents.modelLocator ? self.outer.commonComponents.modelLocator.modelTransform : null;
                var container = model ? model.GetComponent<SFModelContainer>() : null;
                return container ? container.animator : null;
            };
        }
    }
}
