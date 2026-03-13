using UnityEngine;
using Unity.Netcode;

public class FollowCharacter : MonoBehaviour
{
    [SerializeField] public Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -10f);
    [SerializeField] private float followLerp = 12f;

    private void Start()
    {
        // Offline-safe fallback: if no target assigned, find the Player by tag.
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
    }

    private void LateUpdate()
    {
        // In online mode, the owner player will set the target via NetworkPlayer.
        // Avoid "find by tag" when multiple networked Players exist.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (target == null) return;
        }

        if (target == null)
        {
            // Offline-safe fallback: if a player spawns after Start(), reacquire.
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
        if (target == null) return;

        // Camera follows position only; does not inherit rolling/physics rotation.
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
    }
}

