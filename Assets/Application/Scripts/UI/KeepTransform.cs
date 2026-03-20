using UnityEngine;

[DisallowMultipleComponent]
public class KeepTransform : MonoBehaviour
{
    public bool keepPosition = true;
    public bool keepRotation = true;
    public bool keepScale = true;

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _initialScale = Vector3.one;
    private Vector3 _oldParentScale = Vector3.one;
    private Vector3 _oldParentScale2 = Vector3.one;
    private Vector3 _oldParentScale3 = Vector3.one;
    private Vector3 _oldParentScale4 = Vector3.one;
    private Vector3 _oldParentScale5 = Vector3.one;
    private Vector3 _oldParentScale6 = Vector3.one;

    void Awake()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        _initialScale = transform.localScale;
        CacheParentScale();
    }

    void CacheParentScale()
    {
        if (transform.parent != null)
        {
            _oldParentScale = transform.parent.localScale;
            if (transform.parent.parent != null)
            {
                _oldParentScale2 = transform.parent.parent.localScale;
                if (transform.parent.parent.parent != null)
                {
                    _oldParentScale3 = transform.parent.parent.parent.localScale;
                    if (transform.parent.parent.parent.parent != null)
                    {
                        _oldParentScale4 = transform.parent.parent.parent.parent.localScale;
                        if (transform.parent.parent.parent.parent.parent != null)
                        {
                            _oldParentScale5 = transform.parent.parent.parent.parent.parent.localScale;
                            if (transform.parent.parent.parent.parent.parent.parent != null)
                                _oldParentScale6 = transform.parent.parent.parent.parent.parent.parent.localScale;
                        }
                    }
                }
            }
        }
    }

    void KeepScale(Vector3 parentLocalScale)
    {
        Vector3 scale = transform.localScale;
        scale.x = _initialScale.x / Mathf.Max(parentLocalScale.x, 0.01f);
        scale.y = _initialScale.y / Mathf.Max(parentLocalScale.y, 0.01f);
        scale.z = _initialScale.z / Mathf.Max(parentLocalScale.z, 0.01f);
        transform.localScale = scale;
    }

    bool CheckParentScaleChanged(Vector3 curParentScale, Vector3 oldParentScale)
    {
        return Mathf.Abs(curParentScale.x - oldParentScale.x) > 0.0001f ||
               Mathf.Abs(curParentScale.y - oldParentScale.y) > 0.0001f ||
               Mathf.Abs(curParentScale.z - oldParentScale.z) > 0.0001f;
    }

    void LateUpdate()
    {
        if (transform.parent == null)
            return;

        if (keepRotation)
            transform.rotation = _initialRotation;

        if (keepPosition)
            transform.position = _initialPosition;

        if (keepScale)
        {
            // Awake/Start 시점에 lossyScale이 부정확한 Unity 버그로 인해
            // 부모를 최대 6단계까지 직접 순회하며 스케일 변화를 감지합니다.
            Transform parent = transform.parent;
            Vector3 parentScale = parent.localScale;

            if (CheckParentScaleChanged(parentScale, _oldParentScale))
            {
                _oldParentScale = parentScale;
                KeepScale(parentScale);
            }
            else if (parent.parent != null)
            {
                parentScale = parent.parent.localScale;
                if (CheckParentScaleChanged(parentScale, _oldParentScale2))
                {
                    _oldParentScale2 = parentScale;
                    KeepScale(parentScale);
                }
                else if (parent.parent.parent != null)
                {
                    parentScale = parent.parent.parent.localScale;
                    if (CheckParentScaleChanged(parentScale, _oldParentScale3))
                    {
                        _oldParentScale3 = parentScale;
                        KeepScale(parentScale);
                    }
                    else if (parent.parent.parent.parent != null)
                    {
                        parentScale = parent.parent.parent.parent.localScale;
                        if (CheckParentScaleChanged(parentScale, _oldParentScale4))
                        {
                            _oldParentScale4 = parentScale;
                            KeepScale(parentScale);
                        }
                        else if (parent.parent.parent.parent.parent != null)
                        {
                            parentScale = parent.parent.parent.parent.parent.localScale;
                            if (CheckParentScaleChanged(parentScale, _oldParentScale5))
                            {
                                _oldParentScale5 = parentScale;
                                KeepScale(parentScale);
                            }
                            else if (parent.parent.parent.parent.parent.parent != null)
                            {
                                parentScale = parent.parent.parent.parent.parent.parent.localScale;
                                if (CheckParentScaleChanged(parentScale, _oldParentScale6))
                                {
                                    _oldParentScale6 = parentScale;
                                    KeepScale(parentScale);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void OnValidate()
    {
        CacheParentScale();
    }
}
