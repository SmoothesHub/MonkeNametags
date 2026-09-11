using UnityEngine;

namespace MonkeNameplates
{
    internal sealed class Visibility
    {
        private readonly RaycastHit[] hits = new RaycastHit[128];
        private readonly Collider[] overlaps = new Collider[32];

        private bool OriginClear(Vector3 origin, Transform targetRig, Transform localRig, Transform localPhysicsRig)
        {
            // A ray starting inside a collider may miss that collider: fail closed there.
            int inside = Physics.OverlapSphereNonAlloc(origin, 0.01f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (inside >= overlaps.Length) return false;
            for (int i = 0; i < inside; i++)
                if (overlaps[i] && !IsAvatar(overlaps[i].transform, targetRig, localRig, localPhysicsRig)) return false;
            return true;
        }

        private bool Clear(Vector3 origin, Vector3 target, Transform targetRig, Transform localRig, Transform localPhysicsRig)
        {
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (distance < 0.015f) return false;
            int count = Physics.RaycastNonAlloc(origin, delta / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false; // Never assume an incomplete query is clear.
            for (int i = 0; i < count; i++)
                if (hits[i].collider && !IsAvatar(hits[i].collider.transform, targetRig, localRig, localPhysicsRig)) return false;
            return true;
        }

        private static bool IsAvatar(Transform value, Transform target, Transform local, Transform physics) =>
            (target && (value == target || value.IsChildOf(target))) ||
            (local && (value == local || value.IsChildOf(local))) ||
            (physics && (value == physics || value.IsChildOf(physics)));

        public bool CanSee(Camera camera, Vector3 head, Vector3 label, Vector3 fps,
            Transform target, Transform local, Transform localPhysicsRig, bool showFps)
        {
            if (camera.stereoEnabled)
            {
                // Require both eyes to see the head AND label anchors, conservatively.
                Vector3 left = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.GetColumn(3);
                Vector3 right = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.GetColumn(3);
                return Eye(left) && Eye(right);
            }
            return Eye(camera.transform.position);

            bool Eye(Vector3 origin) => OriginClear(origin, target, local, localPhysicsRig) &&
                Clear(origin, head, target, local, localPhysicsRig) &&
                Clear(origin, label, target, local, localPhysicsRig) &&
                (!showFps || Clear(origin, fps, target, local, localPhysicsRig));
        }
    }
}
