using UnityEngine;

public class RootController : MonoBehaviour
{
    public GameObject rootPrefab;

    public KeyCode upKey;
    public KeyCode downKey;
    public KeyCode leftKey;
    public KeyCode rightKey;

    public float step = 0.5f;

    private Vector3 headPos;

    void Start()
    {
        headPos = transform.position;
        Instantiate(rootPrefab, headPos, Quaternion.identity);
    }

    void Update()
    {
        if (Input.GetKeyDown(upKey))
            Grow(Vector3.up);

        if (Input.GetKeyDown(downKey))
            Grow(Vector3.down);

        if (Input.GetKeyDown(leftKey))
            Grow(Vector3.left);

        if (Input.GetKeyDown(rightKey))
            Grow(Vector3.right);
    }

    void Grow(Vector3 dir)
    {
        Vector3 nextPos = headPos + dir * step;

        // 只能在地下
        if (nextPos.y > 0)
            return;

        headPos = nextPos;
        Instantiate(rootPrefab, headPos, Quaternion.identity);
    }
}