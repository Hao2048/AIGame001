using UnityEngine;

/// <summary>
/// 让一个物体（世界空间 Canvas）永远正对相机。
/// </summary>
public class Billboard : MonoBehaviour
{
    public Camera cam;

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        var dir = transform.position - cam.transform.position;
        if (dir.sqrMagnitude < 1e-6f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
