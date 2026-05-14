namespace BigBlit.ShootingRange
{
    public delegate void OnHit(IHitInfo hitInfo);

    public interface IHittable
    {
        event OnHit onHit;
        bool Hit(IShot shot, IHit hit);
    }





}
