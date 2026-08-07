using UnityEngine;

namespace Agrestis.World
{
    [DisallowMultipleComponent]
    public class DropTable : MonoBehaviour
    {
        [Tooltip("Prefab dropped for health.")]
        public GameObject HeartPickupPrefab;

        [Tooltip("Prefab dropped for stamina.")]
        public GameObject StaminaPickupPrefab;

        private void Awake()
        {
            Pickup.HeartDropPrefab = HeartPickupPrefab;
            Pickup.StaminaDropPrefab = StaminaPickupPrefab;

            if (HeartPickupPrefab == null)
            {
                Debug.LogWarning("DropTable has no Heart Pickup Prefab.", this);
            }
        }
    }
}
