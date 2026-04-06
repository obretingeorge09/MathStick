using UnityEngine;
using System.Collections;

public class CameraResizer : MonoBehaviour
{
    public float resolution = 1.6f;
    public float cameraSize = 4.4f;

	// Use this for initialization
	void Start ()
    {
        float fw = cameraSize * 200f * resolution;
        float ar = (float)Screen.width / (float)Screen.height;
        Camera.main.orthographicSize = fw / (200f * ar);
	}
	
	// Update is called once per frame
	void Update ()
    {
	
	}
}
