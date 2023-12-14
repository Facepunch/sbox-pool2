using Sandbox;

namespace Facepunch.Pool;

public class PoolBall : Component
{
	[Property] public PoolBallType Type { get; set; }
	[Property] public PoolBallNumber Number { get; set; }
}
