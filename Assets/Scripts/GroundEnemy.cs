using UnityEngine;

public class GroundEnemy : EnemyBase
{
    [Header("Ground AI")]
    public float JumpForce = 5f;
    public float WallCheckDistance = 1.0f;
    public LayerMask GroundLayer;
    public Transform WallCheckPoint;

    private bool _isGrounded;

    protected override void Move()
    {
        if (PlayerTarget == null) return;
        
        // 1. Calculate Horizontal Direction
        float direction = (PlayerTarget.position.x > transform.position.x) ? 1f : -1f;

        // 2. Apply Velocity at FIXED speed (ignore separation - that's handled by SwarmerEnemy)
        // FIX: Use assignment instead of additive forces to ensure consistent speed
        // The bug was that enemies could stack and push each other to extreme speeds
        Rb.linearVelocity = new Vector2(direction * CurrentSpeed, Rb.linearVelocity.y);

        // 3. Wall Detection (Auto-Jump)
        CheckForWalls(direction);
    }

    private void CheckForWalls(float direction)
    {
        Vector2 origin = WallCheckPoint != null ? WallCheckPoint.position : transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * direction, WallCheckDistance, GroundLayer);

        if (hit.collider != null)
        {
            if (Mathf.Abs(Rb.linearVelocity.y) < 0.01f)
            {
                Rb.AddForce(Vector2.up * JumpForce, ForceMode2D.Impulse);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (WallCheckPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(WallCheckPoint.position, WallCheckPoint.position + new Vector3(WallCheckDistance, 0, 0));
        }
    }
}