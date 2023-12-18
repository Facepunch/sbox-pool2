using Sandbox;

namespace Facepunch.Pool;

public class BallPocket : Component, Component.ITriggerListener
{
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var ball = other.GameObject.Components.GetInParentOrSelf<PoolBall>();
		
		if ( ball.IsValid() )
		{
			ball.OnEnterPocket( this );
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		
	}
}
