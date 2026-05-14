
namespace BigBlit.ShootingRange
{

public delegate void TimeChangeEvent(IAnimable valueable);

public interface IAnimable
{
    float Time { get; }

    event TimeChangeEvent timeChangeEvent;
}

}