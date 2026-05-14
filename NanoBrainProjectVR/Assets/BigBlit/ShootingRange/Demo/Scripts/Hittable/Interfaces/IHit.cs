using UnityEngine;

namespace BigBlit.ShootingRange
{
    public interface IHit {
        float Time { get; }
        Vector3 Position { get; }
        Vector3 Normal { get; }
        Vector3 Velocity { get; }
        Vector2 UV0 { get; }
        Vector2 UV1 { get; }
        Vector2 UV2 { get; }
        int TriangleId { get; }   
        Collider Collider { get; }
    }
}
