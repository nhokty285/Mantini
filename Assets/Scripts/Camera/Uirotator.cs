using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private Transform cam;
    void Start()
    {
        Debug.Log($"Script này vừa được thêm vào object: {gameObject.name}", this);
        cam = GameObject.Find("GO_Camera").GetComponent<Transform>();
    } // cache để tối ưu
    void LateUpdate()
    {
        transform.LookAt(cam);
        transform.Rotate(0, 180, 0);
    }
}
