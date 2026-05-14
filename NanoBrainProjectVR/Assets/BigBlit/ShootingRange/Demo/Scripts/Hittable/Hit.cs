using UnityEngine;

namespace BigBlit.ShootingRange
{
    public class Hit : IHit
    {
        float _time;
        Vector3 _position;
        Vector3 _normal;
        Vector3 _velocity;
        Vector2 _uv0;
        Vector2 _uv1;
        Vector2 _uv2;
        int _triangleId;
        Collider _collider;


        public float Time => _time;
        public Vector3 Position => _position;
        public Vector3 Normal => _normal;
        public Vector3 Velocity => _velocity;
        public Vector2 UV0 => _uv0;
        public Vector2 UV1 => _uv1;
        public Vector2 UV2 => _uv2;
        public int TriangleId => _triangleId;
        public Collider Collider => _collider;

        public Hit(float time, Vector3 position, Vector3 normal, Vector3 velocity, Vector2 uv0, Vector2 uv1, Vector2 uv2, int triangleId, Collider collider) {
            _time = time;
            _position = position;
            _normal = normal;
            _velocity = velocity;
            _uv0 = uv0;
            _uv1 = uv1;
            _uv2 = uv2;
            _triangleId = triangleId;
            _collider = collider;
        }
    }
}
