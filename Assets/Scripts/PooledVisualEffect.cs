using UnityEngine;

// Attached by ObjectPooler to one-shot FX. Inactive prefabs are explicitly activated,
// and particle stop actions cannot destroy pooled roots or leave clones alive forever.
public class PooledVisualEffect : MonoBehaviour
{
    ParticleSystem[] _particles;
    Animator[] _animators;
    Vector3 _baseScale;
    float _remaining;
    bool _pooled;

    public void Play(Vector3 position, Quaternion rotation, float scale, bool pooled)
    {
        if (_particles == null)
        {
            _baseScale = transform.localScale;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _animators = GetComponentsInChildren<Animator>(true);
            foreach (var timer in GetComponentsInChildren<SelfDestruct>(true)) timer.enabled = false;
        }
        _pooled = pooled;
        _remaining = .5f;
        foreach (var particle in _particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.None;
            _remaining = Mathf.Max(_remaining, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }
        _remaining = Mathf.Min(10, _remaining + .1f);
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = _baseScale * scale;
        gameObject.SetActive(true);
        foreach (var animator in _animators)
            if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            { animator.Rebind(); animator.Update(0); }
        foreach (var particle in _particles)
            if (particle.gameObject.activeInHierarchy) particle.Play(false);
    }

    void Update()
    {
        _remaining -= Time.deltaTime;
        if (_remaining > 0) return;
        if (_pooled) gameObject.SetActive(false);
        else Destroy(gameObject);
    }
}
