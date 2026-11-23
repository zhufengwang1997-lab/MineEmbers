using UnityEngine;

namespace MineEmbers.Gameplay
{
    /// <summary>Receiver for the animation events baked into the Starter Assets clips (OnFootstep / OnLand).
    /// Without a receiver Unity logs "AnimationEvent has no receiver" on every step. Footstep audio is not
    /// wired yet; the methods only exist so the events resolve.</summary>
    public class PlayerAnimEvents : MonoBehaviour
    {
        void OnFootstep(AnimationEvent e) { }
        void OnLand(AnimationEvent e) { }
    }
}
