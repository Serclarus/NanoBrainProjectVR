namespace BigBlit.ShootingRange
{
    public interface IHitInfo
    {
        IShot Shot { get; }
        IHit Hit { get; }
        IHittableZone HittableZone { get; }

        float Distance { get; }
    }





}
