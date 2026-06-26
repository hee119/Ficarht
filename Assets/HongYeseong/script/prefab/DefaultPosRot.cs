using System;
using UnityEngine;

public class DefaultPosRot : MonoBehaviour
{
    public Vector3 defaultLocalPosition;
    public Quaternion defaultLocalRotation;

    private void Awake()
    {
        defaultLocalPosition = transform.localPosition;
        defaultLocalRotation = transform.localRotation;
    }
}