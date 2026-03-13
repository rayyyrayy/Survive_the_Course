using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSetup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        Debug.Log("Network Player Spawned! IsOwner: " + IsOwner);
    }
}

