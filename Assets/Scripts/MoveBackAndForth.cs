using UnityEngine;

public class MoveBackAndForth : MonoBehaviour
{
    public float speed = 3f;
    public float distance = 4f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * distance;
        transform.position = startPosition + new Vector3(offset, 0, 0);
    }
}
