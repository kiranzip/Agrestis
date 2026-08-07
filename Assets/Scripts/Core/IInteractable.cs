using UnityEngine;

namespace Agrestis.Core
{
    public interface IInteractable
    {
        string Prompt { get; }

        bool IsAvailable { get; }

        Vector3 InteractPoint { get; }

        void Interact(GameObject interactor);
    }

    public interface IDamageable
    {
        void ApplyDamage(float quarterHearts, Vector3 fromPosition);
    }
}
