﻿using System.Collections.Concurrent;
using Sandbox;
using Sandbox.Network;

namespace Facepunch.Pool;

public class BallPocket : Component, Component.ITriggerListener
{
	private ConcurrentQueue<PoolBall> EnteredQueue { get; set; } = new();
	
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost ) return;
		
		var ball = other.GameObject.Components.GetInParentOrSelf<PoolBall>();
		if ( !ball.IsValid() ) return;
	
		EnteredQueue.Enqueue( ball );
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		
	}

	protected override void OnUpdate()
	{
		while ( EnteredQueue.TryDequeue( out var ball ) )
		{
			if ( !ball.IsValid() ) continue;
			if ( !ball.Physics.MotionEnabled ) continue;
			if ( ball.IsAnimating ) continue;
			
			ball.OnEnterPocket( this );
		}
		
		base.OnUpdate();
	}
}
