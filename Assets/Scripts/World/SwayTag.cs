using UnityEngine;

namespace Agrestis.World
{
    [DisallowMultipleComponent]
    public class SwayTag : MonoBehaviour
    {
        [Tooltip("How strongly this reacts to wind.")]
        [Range(0f, 3f)] public float Responsiveness = 1f;
    }
}
