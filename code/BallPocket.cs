using System.Collections.Concurrent;
using Sandbox;
using Sandbox.Network;

namespace Facepunch.Pool;

public class BallPocket : Component, Component.ITriggerListener
{
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost ) return;
		
		var ball = other.GameObject.Components.GetInParentOrSelf<PoolBall>();
		if ( !ball.IsValid() ) return;
		if ( !ball.IsValid() ) return;
		if ( !ball.Physics.MotionEnabled ) return;
		if ( ball.IsAnimating ) return;
			
		ball.OnEnterPocket( this );
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		
	}
}
