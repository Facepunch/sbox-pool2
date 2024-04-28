using System;
using System.Linq;
using Sandbox;
using Sandbox.Network;

namespace Facepunch.Pool;

public class PoolCue : Component
{
	public static PoolCue Instance { get; private set; }
	public float ShotPower { get; private set; }
	
	private TimeSince TimeSinceWhiteStruck { get; set; }
	private Vector3 LastWhiteBallPosition { get; set; }
	private float CuePullBackOffset { get; set; }
	private float LastPowerDistance { get; set; }
	private float MaxCuePitch { get; set; } = 17f;
	private float MinCuePitch { get; set; } = 5f;
	private bool IsMakingShot { get; set; }
	private float CuePitch { get; set; }
	private float CueYaw { get; set; }

	protected override void OnEnabled()
	{
		Instance = this;
		base.OnEnabled();
	}

	protected override void OnUpdate()
	{
		var currentPlayer = GameState.Instance.CurrentPlayer;
		if ( !currentPlayer.IsValid() ) return;
		
		var renderer = Components.Get<ModelRenderer>( true );

		if ( !currentPlayer.IsValid() || currentPlayer.IsPlacingWhiteBall || currentPlayer.HasStruckWhiteBall )
		{ 
			renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
			FadeTo( 0f, 4f );
		}
		else 
		{ 
			renderer.RenderType = ModelRenderer.ShadowRenderType.On;
			FadeTo( 1f, 8f );
		}
		
		// Don't render entirely if we're placing the white ball.
		renderer.SceneObject.RenderingEnabled = !currentPlayer.IsPlacingWhiteBall;
		
		if ( !Network.IsOwner )
		{
			// If we don't own the cue right now (it isn't our turn), we can't control it.
			return;
		}

		if ( TimeSinceWhiteStruck < 2f )
		{
			// We recently struck the white ball so let's just interpolate the cue forward.
			Transform.Position = LastWhiteBallPosition - Transform.Rotation.Forward * (1f + (CuePitch * 0.04f));
			return;
		}

		var localPlayer = PoolPlayer.LocalPlayer;
		if ( !localPlayer.IsValid() ) return;
		if ( localPlayer.IsPlacingWhiteBall ) return;
		
		var whiteBall = Scene.GetAllComponents<PoolBall>().FirstOrDefault( b => b.Type == PoolBallType.White );
		if ( !whiteBall.IsValid() ) return;

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
				TakeShot( Transform.World, ShotPower );
				return;
			}
		}
			
		Transform.Position = whiteBall.Transform.Position - Transform.Rotation.Forward * (1f + CuePullBackOffset + (CuePitch * 0.04f));
	}

	private void FadeTo( float opacity, float speed )
	{
		var renderer = Components.Get<ModelRenderer>( true );
		renderer.Tint = renderer.Tint.WithAlpha( renderer.Tint.a.LerpTo( opacity, Time.Delta * speed ) );
	}
	
	private Vector3 DirectionTo( PoolBall ball )
	{
		return (ball.Transform.Position - Transform.Position.WithZ( ball.Transform.Position.z )).Normal;
	}

	[Broadcast]
	private void TakeShot( Transform transform, float power )
	{
		if ( Network.IsOwner )
		{
			CuePullBackOffset = 0f;
			IsMakingShot = false;
			ShotPower = 0f;
		}
		
		var whiteBall = Scene.GetAllComponents<PoolBall>().FirstOrDefault( b => b.Type == PoolBallType.White );
		if ( !whiteBall.IsValid() ) return;
		
		var currentPlayer = GameState.Instance.CurrentPlayer;
		if ( !currentPlayer.IsValid() ) return;
		
		currentPlayer.TimeSinceWhiteStruck = 0f;
		currentPlayer.HasStruckWhiteBall = true;
		
		TimeSinceWhiteStruck = 0f;
		LastWhiteBallPosition = whiteBall.Transform.Position;
		
		if ( !Networking.IsHost ) return;
		
		Transform.World = transform;
		
		var direction = DirectionTo( whiteBall );
		var body = whiteBall.Components.Get<Rigidbody>();
		body.ApplyImpulse( direction * power * 6f * body.PhysicsBody.Mass );
	}

	private void UpdatePowerSelection()
	{
		var camera = Scene.Camera;
		var cursorDirection = Mouse.Visible ? camera.ScreenPixelToRay( Mouse.Position ).Forward : camera.Transform.Rotation.Forward;
		var cursorPlaneEndPos = camera.Transform.Position + cursorDirection * 350f;
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
		var camera = Scene.Camera;
		var cursorDirection = Mouse.Visible ? camera.ScreenPixelToRay( Mouse.Position ).Forward : camera.Transform.Rotation.Forward;
		var tablePlane = new Plane( centerPosition, Vector3.Up );
		var hitPos = tablePlane.Trace( new( camera.Transform.Position, cursorDirection ) );
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
