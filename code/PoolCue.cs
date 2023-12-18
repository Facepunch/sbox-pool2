using System;
using System.Linq;
using Sandbox;
using Sandbox.Network;

namespace Facepunch.Pool;

public class PoolCue : Component, INetworkSerializable
{
	public static PoolCue Instance { get; private set; }
	
	private float CuePullBackOffset { get; set; }
	private float LastPowerDistance { get; set; }
	private float MaxCuePitch { get; set; } = 17f;
	private float MinCuePitch { get; set; } = 5f;
	private bool IsMakingShot { get; set; }
	private float CuePitch { get; set; }
	private float CueYaw { get; set; }
	private float ShotPower { get; set; }
	
	void INetworkSerializable.Write( ref ByteStream stream )
	{
		
	}

	void INetworkSerializable.Read( ByteStream stream )
	{
		
	}

	protected override void OnEnabled()
	{
		Instance = this;
		base.OnEnabled();
	}

	protected override void OnUpdate()
	{
		var whiteBall = Scene.GetAllComponents<PoolBall>().FirstOrDefault( b => b.Type == PoolBallType.White );
		if ( !whiteBall.IsValid() ) return;

		if ( !Network.IsOwner )
		{
			// If we don't own the cue right now (it isn't our turn), we can't control it.
			return;
		}

		var player = PoolPlayer.LocalPlayer;
		if ( !player.IsValid() || player.IsPlacingWhiteBall ) return;

		if ( Input.Down( "attack1" ) )
		{
			UpdatePowerSelection();
		}
		else
		{
			if ( !IsMakingShot )
			{
				UpdateDirection( whiteBall.Transform.Position );
			}
			else
			{
				if ( ShotPower >= 5f )
				{
					TakeShot( Transform.World, ShotPower );
				}
			
				CuePullBackOffset = 0f;
				IsMakingShot = false;
				ShotPower = 0f;
			}
		}
			
		Transform.Position = whiteBall.Transform.Position - Transform.Rotation.Forward * (1f + CuePullBackOffset + (CuePitch * 0.04f));
	}
	
	private Vector3 DirectionTo( PoolBall ball )
	{
		return (ball.Transform.Position - Transform.Position.WithZ( ball.Transform.Position.z )).Normal;
	}

	[Broadcast]
	private void TakeShot( Transform transform, float power )
	{
		Network.DropOwnership();
		
		var whiteBall = Scene.GetAllComponents<PoolBall>().FirstOrDefault( b => b.Type == PoolBallType.White );
		if ( !whiteBall.IsValid() ) return;
		if ( !GameNetworkSystem.IsHost ) return;

		var player = GameManager.Instance.Players.FirstOrDefault( p => p.IsTurn );
			
		Transform.World = transform;
		
		var direction = DirectionTo( whiteBall );
		var body = whiteBall.Components.Get<Rigidbody>();
		body.ApplyImpulse( direction * power * 6f * body.PhysicsBody.Mass );

		player.StikeWhiteBall();
	}

	private void UpdatePowerSelection()
	{
		var cursorDirection = Mouse.Visible ? Screen.GetDirection( Mouse.Position ) : Camera.Rotation.Forward;
		var cursorPlaneEndPos = Camera.Position + cursorDirection * 350f;
		var distanceToCue = cursorPlaneEndPos.Distance( Transform.Position - Transform.Rotation.Forward * 100f );
		var cuePullBackDelta = (LastPowerDistance - distanceToCue) * Time.Delta * 20f;

		if ( !IsMakingShot )
		{
			LastPowerDistance = 0f;
			cuePullBackDelta = 0f;
		}

		CuePullBackOffset = Math.Clamp( CuePullBackOffset + cuePullBackDelta, 0f, 8f );
		LastPowerDistance = distanceToCue;
		ShotPower = CuePullBackOffset.AsPercentMinMax( 0f, 8f );
		IsMakingShot = true;
	}

	private void UpdateDirection( Vector3 centerPosition )
	{
		var cursorDirection = Mouse.Visible ? Screen.GetDirection( Mouse.Position ) : Camera.Rotation.Forward;
		var tablePlane = new Plane( centerPosition, Vector3.Up );
		var hitPos = tablePlane.Trace( new( Camera.Position, cursorDirection ) );
		if ( !hitPos.HasValue ) return;

		var aimDir = (hitPos.Value - centerPosition).WithZ( 0 ).Normal;
		var rollTrace = Scene.Trace.Ray( centerPosition, centerPosition - aimDir * 100f )
			.WithoutTags( "white", "cue" )
			.Run();

		var aimRotation = Rotation.LookAt( aimDir, Vector3.Forward );

		CuePullBackOffset = CuePullBackOffset.LerpTo( 0f, Time.Delta * 10f );
		CuePitch = CuePitch.LerpTo( MinCuePitch + ((MaxCuePitch - MinCuePitch) * (1f - rollTrace.Fraction)), Time.Delta * 10f );
		CueYaw = aimRotation.Yaw().NormalizeDegrees();
			
		Transform.Rotation = Rotation.From(
			Transform.Rotation.Angles()
				.WithYaw( CueYaw )
				.WithPitch( CuePitch )
		);
	}
}
