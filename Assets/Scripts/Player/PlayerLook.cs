using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerLook : MonoBehaviour
{
    [SerializeField, Min(0f)] private float sensitivity = 0.1f;
    [SerializeField, Range(0f, 180f)] private float yawLimit = 70f;
    [SerializeField, Range(-89f, 0f)] private float minimumPitch = -30f;
    [SerializeField, Range(0f, 89f)] private float maximumPitch = 45f;

    private float yaw;
    private float pitch;

    public void Initialize(Quaternion localRotation)
    {
        Vector3 angles = localRotation.eulerAngles;

        yaw = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.y), -yawLimit, yawLimit);

        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), minimumPitch, maximumPitch);
    }

    public Quaternion Evaluate(Vector2 mouseDelta)
    {
        yaw = Mathf.Clamp(yaw + mouseDelta.x * sensitivity, -yawLimit, yawLimit);

        pitch = Mathf.Clamp(pitch - mouseDelta.y * sensitivity, minimumPitch, maximumPitch);

        return Quaternion.Euler(pitch, yaw, 0f);
    }
}