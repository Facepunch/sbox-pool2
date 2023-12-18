using System;
using System.Linq;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Network;

namespace Facepunch.Pool;

public class PoolPlayer : Component, INetworkSerializable
{
	public static PoolPlayer LocalPlayer =>
		GameManager.Instance.Players.FirstOrDefault( p => p.ConnectionId == Connection.Local.Id );
	
	public PoolBallType BallType { get; set; }
	public FoulReason FoulReason { get; set; }
	public Connection Connection { get; set; }
	public Guid ConnectionId { get; set; }
	public TimeSince TimeSinceWhiteStruck { get; private set; }
	public bool HasStruckWhiteBall { get; private set; }
	public EloScore Elo { get; private set; }
	public bool IsLocalPlayer => ConnectionId == Connection.Local.Id;
	public bool IsPlacingWhiteBall { get; private set; }
	public bool HasSecondShot { get; set; }
	public bool DidHitOwnBall { get; set; }
	public bool DidPotBall { get; set; }
	public bool IsTurn { get; private set; }
	public string SteamName { get; set; }
	public ulong SteamId { get; set; }
	public int Score { get; set; }
	
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
		Assert.True( GameNetworkSystem.IsHost );
		
		if ( showMessage )
			GameManager.Instance.AddToast( SteamId, $"{ SteamName } has started their turn" );

		SendSoundToOwner( "ding" );

		PoolCue.Instance.Network.AssignOwnership( Connection );

		GameState.Instance.CurrentPlayer = this;

		HasStruckWhiteBall = false;
		HasSecondShot = hasSecondShot;
		FoulReason = FoulReason.None;
		DidHitOwnBall = false;
		DidPotBall = false;
		IsTurn = true;

		if ( hasSecondShot )
			StartPlacingWhiteBall();
	}
	
	[Authority]
	public void SendSoundToOwner( string soundName )
	{
		Sound.Play( soundName );
	}
	
	[Broadcast]
	public void SendSound( string soundName )
	{
		Sound.Play( soundName );
	}

	public void StartPlacingWhiteBall()
	{
		Assert.True( GameNetworkSystem.IsHost );
		
		var whiteBall = GameManager.Instance.WhiteBall;

		if ( whiteBall != null && whiteBall.IsValid() )
		{
			whiteBall.StartPlacing();
		}

		_ = GameManager.Instance.RespawnBallAsync( whiteBall );

		IsPlacingWhiteBall = true;
	}

	[Authority]
	public void StopPlacingWhiteBall()
	{
		var whiteBall = GameManager.Instance.WhiteBall;

		if ( whiteBall.IsValid() )
		{
			whiteBall.StopPlacing();
		}

		IsPlacingWhiteBall = false;
	}

	public void Foul( FoulReason reason )
	{
		Assert.True( GameNetworkSystem.IsHost );
		
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

	public void StikeWhiteBall()
	{
		Assert.True( GameNetworkSystem.IsHost );
		TimeSinceWhiteStruck = 0f;
		HasStruckWhiteBall = true;
	}

	protected override void OnUpdate()
	{
		if ( IsLocalPlayer && IsPlacingWhiteBall )
		{
			var whiteBall = GameManager.Instance.WhiteBall;
			if ( whiteBall.IsValid() )
			{
				var cursorDirection = Mouse.Visible ? Screen.GetDirection( Mouse.Position ) : Camera.Rotation.Forward;
				var cursorTrace = Scene.Trace.Ray( Camera.Main.Position, Camera.Main.Position + cursorDirection * 1000f ).Run();

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
		base.OnEnabled();
	}

	void INetworkSerializable.Write( ref ByteStream stream )
	{
		stream.Write( Connection.Id );
		stream.Write( SteamName );
		stream.Write( SteamId );
		stream.Write( HasSecondShot );
		stream.Write( HasStruckWhiteBall );
		stream.Write( IsTurn );
		stream.Write( FoulReason );
		stream.Write( DidHitOwnBall );
		stream.Write( DidPotBall );
		stream.Write( IsPlacingWhiteBall );
	}

	void INetworkSerializable.Read( ByteStream stream )
	{
		ConnectionId = stream.Read<Guid>();
		SteamName = stream.Read<string>();
		SteamId = stream.Read<ulong>();
		HasSecondShot = stream.Read<bool>();
		HasStruckWhiteBall = stream.Read<bool>();
		IsTurn = stream.Read<bool>();
		FoulReason = stream.Read<FoulReason>();
		DidHitOwnBall = stream.Read<bool>();
		DidPotBall = stream.Read<bool>();
		IsPlacingWhiteBall = stream.Read<bool>();
	}
}
