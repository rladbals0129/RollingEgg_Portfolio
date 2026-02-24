using UnityEngine;

namespace RollingEgg
{
    public class EndPoint : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D coll)
        {
            var player = coll.GetComponent<PlayerController>();
            if (player != null)
            {
                player.OnEndPointReached();
            }
        }
    }
}
