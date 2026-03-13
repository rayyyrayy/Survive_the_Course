using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Collections;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkPlayer : NetworkBehaviour
{
    public NetworkVariable<bool> isChaser = new NetworkVariable<bool>(false);
    public NetworkVariable<int> totalScore = new NetworkVariable<int>(0);
    // Name is written on the server (via ServerRpc), so write permission must be Server.
    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float chaserSpeedMultiplier = 1.1f;

    private Renderer _renderer;
    private Rigidbody _rb;
    private PlayerController _playerController;
    private Vector3 _serverMoveInput;
    private bool _didBindCamera;
    private VirtualJoystick _virtualJoystick;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _rb = GetComponent<Rigidbody>();
        _playerController = GetComponent<PlayerController>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner && NetworkManager.Singleton != null)
        {
            var name = PlayerPrefs.GetString("PlayerName", $"Player {OwnerClientId}");
            Debug.Log($"[NetworkPlayer] OnNetworkSpawn owner {OwnerClientId}, sending name '{name}' to server.");
            SubmitNameServerRpc(name);
            _virtualJoystick = Object.FindFirstObjectByType<VirtualJoystick>();
        }
        // Only override the scene camera in an active netcode session.
        if (IsLocalPlayer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            TryBindLocalCamera();

        // Disable the offline controller during multiplayer (prevents client/server fighting + jitter).
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && _playerController != null)
            _playerController.enabled = false;

        // Multiplayer physics smoothing:
        // - Server simulates Rigidbody motion
        // - Clients are kinematic to avoid physics fighting NetworkTransform updates (jitter)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && _rb != null)
        {
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.isKinematic = !IsServer;
        }

        // Ensure we sync position over the network but do NOT sync rolling rotation.
        // (NetworkTransform settings are authority-driven; set on server.)
        if (IsServer)
        {
            var nt = GetComponent<NetworkTransform>();
            if (nt == null)
                nt = gameObject.AddComponent<NetworkTransform>();

            nt.Interpolate = true;
            nt.SlerpPosition = false;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleY = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;
        }

        isChaser.OnValueChanged += OnIsChaserChanged;
        OnIsChaserChanged(false, isChaser.Value);
    }

    public override void OnNetworkDespawn()
    {
        isChaser.OnValueChanged -= OnIsChaserChanged;
    }

    private void OnIsChaserChanged(bool _, bool newValue)
    {
        if (_renderer == null)
            return;

        var c = newValue ? Color.red : Color.blue;
        if (_renderer.material != null)
            _renderer.material.color = c;
    }

    private void Update()
    {
        // Offline mode: do nothing (offline uses PlayerController, not NetworkPlayer).
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        // Only the local player reads input.
        if (!IsLocalPlayer)
            return;

        // Make camera binding resilient to spawn order.
        if (!_didBindCamera)
            TryBindLocalCamera();

        if (OnlineGameManager.Instance == null || !OnlineGameManager.Instance.IsPlaying)
        {
            SubmitMoveInputServerRpc(Vector2.zero);
            return;
        }

        Vector2 keyboardInput = Vector2.zero;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float h = 0f;
            float v = 0f;

            if (keyboard.leftArrowKey.isPressed) h -= 1f;
            if (keyboard.rightArrowKey.isPressed) h += 1f;
            if (keyboard.downArrowKey.isPressed) v -= 1f;
            if (keyboard.upArrowKey.isPressed) v += 1f;

            keyboardInput = new Vector2(h, v);
        }

        Vector2 joyInput = Vector2.zero;
        if (_virtualJoystick == null)
            _virtualJoystick = Object.FindFirstObjectByType<VirtualJoystick>();

        if (_virtualJoystick != null && _virtualJoystick.isActiveAndEnabled)
            joyInput = _virtualJoystick.InputVector;

        var combined = keyboardInput + joyInput;
        if (combined.sqrMagnitude > 1f)
            combined = combined.normalized;

        SubmitMoveInputServerRpc(combined);
    }

    private void TryBindLocalCamera()
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        var follower = cam.GetComponent<FollowCharacter>();
        if (follower == null)
            return;

        follower.target = transform;
        _didBindCamera = true;
    }

    private void FixedUpdate()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        // Server authoritative movement (prevents client/server transform fighting / camera shake).
        if (!IsServer)
            return;

        if (OnlineGameManager.Instance == null || !OnlineGameManager.Instance.IsPlaying)
            return;

        float speed = moveSpeed * (isChaser.Value ? chaserSpeedMultiplier : 1f);
        Vector3 move = _serverMoveInput * (speed * Time.fixedDeltaTime);
        _rb.MovePosition(_rb.position + move);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitMoveInputServerRpc(Vector2 input)
    {
        _serverMoveInput = new Vector3(input.x, 0f, input.y);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitNameServerRpc(string name)
    {
        Debug.Log($"[NetworkPlayer] SubmitNameServerRpc received on server from client {OwnerClientId}, name='{name}'.");
        playerName.Value = new FixedString32Bytes(name);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer)
            return;

        if (!isChaser.Value)
            return;

        var other = collision.collider.GetComponentInParent<NetworkPlayer>();
        if (other == null)
            return;

        if (!other.isChaser.Value)
            OnlineGameManager.Instance?.ChaserCaughtRunner();
    }
}

