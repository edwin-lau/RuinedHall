using UnityEngine;

public class HeroFollowCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset;
    [SerializeField] float lookAtHeight = 1.1f;
    [SerializeField] float followSmoothing = 12f;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
        CaptureOffsetFromCurrentView();
    }

    void Start()
    {
        if (target != null && offset.sqrMagnitude < 0.01f)
            CaptureOffsetFromCurrentView();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desired = target.position + offset;
        float t = followSmoothing <= 0f
            ? 1f
            : 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
        transform.LookAt(target.position + Vector3.up * lookAtHeight);
    }

    void CaptureOffsetFromCurrentView()
    {
        if (target == null)
            return;

        offset = transform.position - target.position;
        if (offset.y < 6f)
            offset.y = 14.7f;
        if (offset.sqrMagnitude < 4f)
            offset = new Vector3(0f, 14.7f, -12.8f);
    }
}
