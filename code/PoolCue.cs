using System;
using System.Diagnostics.Metrics;
using System.Linq;
using Sandbox;

namespace Facepunch.Pool;

public class PoolCue : Component
{
	private float CuePullBackOffset { get; set; }
	private float LastPowerDistance { get; set; }
	private float MaxCuePitch { get; set; } = 17f;
	private float MinCuePitch { get; set; } = 5f;
	private bool IsMakingShot { get; set; }
	private float CuePitch { get; set; }
	private float CueYaw { get; set; }
	private float ShotPower { get; set; }
	
	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		
		var whiteBall = Scene.GetAllComponents<PoolBallSpawn>().FirstOrDefault( b => b.Type == PoolBallType.White );
		if ( !whiteBall.IsValid() ) return;
		
		if ( !Input.Down( "attack1" ) )
		{
			if ( !IsMakingShot )
			{
				UpdateDirection( whiteBall.Transform.Position );
			}
			else
			{
				TakeShot( Transform.World, ShotPower );
				CuePullBackOffset = 0f;
				IsMakingShot = false;
				ShotPower = 0f;
			}
		}
		else
		{
			UpdatePowerSelection();
			IsMakingShot = true;
		}
		
		Transform.Position = whiteBall.Transform.Position - Transform.Rotation.Forward * (1f + CuePullBackOffset + (CuePitch * 0.04f));
	}

	[Authority]
	private void TakeShot( Transform transform, float power )
	{
		Log.Info( "Wants to take a shot: " + transform.Position );
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
