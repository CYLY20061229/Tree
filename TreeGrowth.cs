using UnityEngine;

public class TreeGrowth : MonoBehaviour
{
    public float growthSpeed = 2f;

    void Update()
    {
        if (Input.GetKey(KeyCode.W)) // °´WÉú³¤
        {
            Grow();
        }
    }

    void Grow()
    {
        transform.localScale += new Vector3(0, growthSpeed * Time.deltaTime, 0);
    }
}