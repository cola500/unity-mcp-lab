using UnityEngine;

[ExecuteAlways]
public class FaceTarget : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private bool yAxisOnly = true;

    void OnEnable()
    {
        if (target == null)
        {
            var go = GameObject.Find("Flame");
            if (go != null) target = go.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 to = target.position - transform.position;
        if (yAxisOnly) to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }
}
