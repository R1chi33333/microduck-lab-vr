using MicroduckVR.Core;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>
    /// On a duck's mover capsule: walking into a loose toy nudges it along. The duck has a small, steady push, so
    /// light toys (a football, a block) roll away at about walking pace and heavy ones (a laptop) barely shift.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PropPusher : MonoBehaviour
    {
        public float pushForce = 0.5f;       // newtons the duck leans into a toy with
        public float speedFactor = 1.3f;     // a pushed toy tops out a bit faster than the duck walks
        CharacterController cc;

        void Awake() { cc = GetComponent<CharacterController>(); }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.collider.attachedRigidbody;
            if (rb == null) return;
            // playing on a joining PC, the host simulates the toys (here they are kinematic): ask it to push
            if (rb.isKinematic && !Coop.Joined) return;
            if (hit.moveDirection.y < -0.5f) return;                         // standing on it, not walking into it
            var dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            float speed = Mathf.Max(new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude, 0.06f);
            if (rb.isKinematic)
            {
                Vector3 hp = new Vector3(hit.point.x, Mathf.Max(hit.point.y, rb.worldCenterOfMass.y), hit.point.z);
                Coop.QueuePush(rb, dir, speed * speedFactor, pushForce * Time.deltaTime / Mathf.Max(rb.mass, 0.01f), hp);
                return;
            }
            float along = Vector3.Dot(rb.linearVelocity, dir), want = speed * speedFactor;
            if (along >= want) return;
            float dv = Mathf.Min(want - along, pushForce * Time.deltaTime / Mathf.Max(rb.mass, 0.01f));
            // push a little above the contact's height so round things roll instead of skidding
            Vector3 at = new Vector3(hit.point.x, Mathf.Max(hit.point.y, rb.worldCenterOfMass.y), hit.point.z);
            rb.AddForceAtPosition(dir * dv, at, ForceMode.VelocityChange);
            rb.WakeUp();
        }
    }
}
