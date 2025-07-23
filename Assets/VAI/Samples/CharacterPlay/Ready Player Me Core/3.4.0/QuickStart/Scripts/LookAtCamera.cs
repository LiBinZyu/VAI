using UnityEngine;

namespace ReadyPlayerMe.Samples
{
    public class LookAtCamera : MonoBehaviour
    {
        [SerializeField] private GameObject cam;

        private void Update()
        {
            transform.LookAt(cam.transform);
        }
    }
}
