using Sandbox;

namespace Facepunch.Pool;

public class BallPocket : Component, Component.ITriggerListener
{
	public void OnTriggerEnter( Collider other )
	{
		var ball = other.GameObject.Components.GetInParentOrSelf<PoolBall>();
		
		if ( ball.IsValid() )
		{
			ball.OnEnterPocket( this );
		}
	}

	public void OnTriggerExit( Collider other )
	{
		
	}
}
