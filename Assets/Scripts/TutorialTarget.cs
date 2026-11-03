using UnityEngine;

public class TutorialTarget : MonoBehaviour
{
    public int Index;
    public void Hit() { TutorialDirector.Instance?.HitTarget(Index); }
}
