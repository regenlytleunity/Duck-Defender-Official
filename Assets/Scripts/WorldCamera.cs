using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WorldCamera : MonoBehaviour
{
    public static WorldCamera Instance { get; private set; }
    public const string ZoomPreference = "DuckDefender_CameraZoom";
    public float Left = -57, Right = 57, Floor = -2.3f, Ceiling = 18;
    public float MinimumZoom = 4, MaximumZoom = 7.5f, MaximumHalfWidth = 20;
    public float SpawnDistance = 23, FollowTime = .15f;
    Camera _camera;
    Vector3 _velocity;
    void Awake() { Instance = this; _camera = GetComponent<Camera>(); }
    void LateUpdate()
    {
        var session = LocalCoopSession.Instance;
        var primary = LocalCoopSession.NearestAlive(transform.position);
        if (primary == null) return;
        Vector3 min = primary.transform.position, max = min;
        if (session != null) foreach (var player in session.Players)
            if (player.Alive) { min = Vector3.Min(min, player.transform.position); max = Vector3.Max(max, player.transform.position); }
        float aspect = Mathf.Max(.1f, _camera.aspect);
        float limit = Mathf.Min(MaximumZoom, MaximumHalfWidth / aspect);
        float size = LocalCoopSession.Multiplayer ? Mathf.Max(5, (max.x - min.x + 5) / (2 * aspect), (max.y - min.y + 4) / 2) :
            Mathf.Lerp(MinimumZoom, MaximumZoom, Mathf.Clamp01(PlayerPrefs.GetFloat(ZoomPreference, 2f / 7f)));
        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, Mathf.Min(size, limit), 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
        float width = _camera.orthographicSize * aspect;
        Vector3 target = (min + max) * .5f + Vector3.up * 1.85f;
        target.x = Mathf.Clamp(target.x, Left + width, Right - width);
        target.y = Mathf.Clamp(target.y, Floor + _camera.orthographicSize - .6f, Ceiling - _camera.orthographicSize);
        target.z = transform.position.z;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, FollowTime, Mathf.Infinity, Time.unscaledDeltaTime);
        // Clamping after smoothing prevents exposing the void while changing zoom near a wall.
        var position = transform.position;
        position.x = Mathf.Clamp(position.x, Left + width, Right - width);
        transform.position = position;
    }

    public bool TrySpawnPosition(GameObject prefab, out Vector3 position)
    {
        position = Vector3.zero;
        var focus = LocalCoopSession.NearestAlive(transform.position);
        if (focus == null || _camera == null) return false;
        float margin = 1.5f;
        var sprite = prefab.GetComponent<SpriteRenderer>();
        if (sprite != null) margin = Mathf.Max(margin, sprite.bounds.extents.x * 1.2f + .5f);
        float left = Left + margin, right = Right - margin;
        float viewportLeft = _camera.transform.position.x - _camera.orthographicSize * _camera.aspect - margin;
        float viewportRight = _camera.transform.position.x + _camera.orthographicSize * _camera.aspect + margin;
        float distance = Mathf.Max(SpawnDistance, MaximumHalfWidth + margin);
        float firstSide = Random.value < .5f ? -1 : 1;
        for (int attempt = 0; attempt < 80; attempt++)
        {
            float x = attempt < 2 ? Mathf.Clamp(focus.transform.position.x + (attempt == 0 ? firstSide : -firstSide) * distance, left, right) : Random.Range(left, right);
            if (x >= viewportLeft && x <= viewportRight || Mathf.Abs(x - focus.transform.position.x) < distance) continue;
            var nearest = LocalCoopSession.NearestAlive(new Vector2(x, Floor));
            if (nearest != null && Mathf.Abs(nearest.transform.position.x - x) < margin + 2) continue;
            // Raycast the authored terrain so edge spawns cannot land in empty space.
            var hit = Physics2D.Raycast(new Vector2(x, Floor + 2), Vector2.down, 8, LayerMask.GetMask("Ground"));
            if (hit.collider == null || hit.normal.y < .5f) continue;
            float halfHeight = sprite != null ? Mathf.Max(.6f, sprite.bounds.extents.y * 1.2f) : .8f;
            position = new Vector3(x, hit.point.y + halfHeight + .15f, 0);
            return true;
        }
        return false;
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
