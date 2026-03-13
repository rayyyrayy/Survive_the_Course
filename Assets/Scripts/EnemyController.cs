using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 2f;

    [SerializeField]
    private float speedIncrease = 0.5f;

    [SerializeField]
    private float increaseInterval = 10f;

    private Transform player;
    private NavMeshAgent agent;
    private float nextIncreaseTime;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.angularSpeed = 720f;
        agent.acceleration = 16f;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        nextIncreaseTime = Time.time + increaseInterval;
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if (player == null || agent == null)
            return;

        // If the agent has been warped or spawned off the NavMesh, avoid calling SetDestination until it's valid.
        if (!agent.isOnNavMesh)
            return;

        agent.SetDestination(player.position);

        if (Time.time >= nextIncreaseTime)
        {
            moveSpeed += speedIncrease;
            agent.speed = moveSpeed;
            nextIncreaseTime += increaseInterval;
        }
    }
}
