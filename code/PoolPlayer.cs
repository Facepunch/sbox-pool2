using System;
using System.Linq;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Network;

namespace Facepunch.Pool;

public class PoolPlayer : Component
{
	public static PoolPlayer LocalPlayer =>
		GameManager.Instance.Players.FirstOrDefault( p => p.IsLocalPlayer );
	
	public TimeSince TimeSinceWhiteStruck { get; set; }
	public EloScore Elo { get; private set; }
	public bool IsLocalPlayer => Network.IsOwner;
	
	[HostSync] public PoolBallType BallType { get; set; }
	[HostSync] public FoulReason FoulReason { get; private set; }
	[HostSync] public bool HasStruckWhiteBall { get; set; }
	[HostSync] public bool IsPlacingWhiteBall { get; private set; }
	[HostSync] public bool HasSecondShot { get; set; }
	[HostSync] public bool DidHitOwnBall { get; set; }
	[HostSync] public bool DidPotBall { get; set; }
	[HostSync] public bool IsTurn { get; private set; }
	[HostSync] public string SteamName { get; set; }
	[HostSync] public ulong SteamId { get; set; }
	[HostSync] public int Score { get; set; }
	
	public int BallsLeft
	{
		get
		{
			var balls = GameManager.Instance.Balls.Where( ( e ) => e.Type == BallType );
			return balls.Count();
		}
	}
	
	public void StartTurn( bool hasSecondShot = false, bool showMessage = true )
	{
		Assert.True( Networking.IsHost );
		
		if ( showMessage )
			GameManager.Instance.AddToast( SteamId, $"{ SteamName } has started their turn" );

		SendSoundToOwner( "ding" );

		PoolCue.Instance.Network.AssignOwnership( Networking.FindConnection( Network.OwnerId ) );
		GameState.Instance.SetCurrentPlayer( this );

		HasStruckWhiteBall = false;
		HasSecondShot = hasSecondShot;
		FoulReason = FoulReason.None;
		DidHitOwnBall = false;
		DidPotBall = false;
		IsTurn = true;

		if ( hasSecondShot )
			StartPlacingWhiteBall();
	}
	
	[Broadcast]
	public void SendSoundToOwner( string soundName )
	{
		if ( IsLocalPlayer )
		{
			Sound.Play( soundName );
		}
	}
	
	[Broadcast]
	public void SendSound( string soundName )
	{
		Sound.Play( soundName );
	}

	public void StartPlacingWhiteBall()
	{
		Assert.True( Networking.IsHost );
		
		var whiteBall = GameManager.Instance.WhiteBall;

		if ( whiteBall != null && whiteBall.IsValid() )
		{
			whiteBall.StartPlacing();
		}

		_ = GameManager.Instance.RespawnBallAsync( whiteBall );

		IsPlacingWhiteBall = true;
	}

	[Broadcast]
	public void StopPlacingWhiteBall()
	{
		if ( !Networking.IsHost ) return;
		
		var whiteBall = GameManager.Instance.WhiteBall;

		if ( whiteBall.IsValid() )
		{
			whiteBall.StopPlacing();
		}

		IsPlacingWhiteBall = false;
	}

	public void Foul( FoulReason reason )
	{
		Assert.True( Networking.IsHost );
		
		if ( FoulReason != FoulReason.None ) return;

		Log.Info( $"{SteamName} has fouled (reason: {reason})" );

		GameManager.Instance.AddToast( SteamId, reason.ToMessage( SteamName ), "foul" );

		SendSound( "foul" );

		HasSecondShot = false;
		FoulReason = reason;
	}

	public void FinishTurn()
	{
		HasStruckWhiteBall = false;
		IsTurn = false;
	}

	protected override void OnUpdate()
	{
		if ( IsLocalPlayer && IsPlacingWhiteBall )
		{
			Log.Info( "Why do we wanna move the white ball?" );
			var whiteBall = GameManager.Instance.WhiteBall;
			if ( whiteBall.IsValid() )
			{
				var camera = Scene.Camera;
				var cursorDirection = Mouse.Visible ? camera.ScreenPixelToRay( Mouse.Position ).Forward : camera.Transform.Rotation.Forward;
				var cursorTrace = Scene.Trace.Ray( camera.Transform.Position, camera.Transform.Position + cursorDirection * 1000f ).Run();

				/*
				var whiteArea = PoolGame.Entity.WhiteArea;
				var whiteAreaWorldOBB = whiteArea.CollisionBounds.ToWorldSpace( whiteArea );
				*/
				
				whiteBall.TryMoveTo( cursorTrace.EndPosition );

				if ( Input.Released( "attack1" ) )
					StopPlacingWhiteBall();
			}
		}
		
		base.OnUpdate();
	}

	protected override void OnEnabled()
	{
		Elo = new();
		Score = 0;
		BallType = PoolBallType.White;
		
		base.OnEnabled();
	}
}
