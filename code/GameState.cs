using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Network;

namespace Facepunch.Pool;

public enum RoundState
{
	Waiting,
	Playing,
	GameOver
}

public struct PotHistoryItem
{
	public PoolBallNumber Number { get; set; }
	public PoolBallType Type { get; set; }
}

public class GameState : Component
{
	public static GameState Instance { get; private set; }

	[Sync] public NetList<PotHistoryItem> PotHistory { get; private set; }

	[Sync] private Guid PlayerOneId { get; set; } 
	[Sync] private Guid PlayerTwoId { get; set; }
	[Sync] private Guid CurrentPlayerId { get; set; }

	public PoolPlayer PlayerOne => Scene?.Directory.FindByGuid( PlayerOneId )?.Components.Get<PoolPlayer>();
	public PoolPlayer PlayerTwo => Scene?.Directory.FindByGuid( PlayerTwoId )?.Components.Get<PoolPlayer>();
	public PoolPlayer CurrentPlayer => Scene?.Directory.FindByGuid( CurrentPlayerId )?.Components.Get<PoolPlayer>();

	public RealTimeUntil PlayerTurnEndTime { get; private set; }
	
	[Sync] public int TimeLeftSeconds { get; private set; }
	[Sync] public RoundState State { get; private set; }
	[Sync] public int RoundCount { get; private set; }
	
	private bool DidClaimThisTurn { get; set; }
	private bool HasPlayedFastForwardSound { get; set; }
	private bool IsFastForwarding { get; set; }

	public PoolPlayer GetBallPlayer( PoolBall ball )
	{
		if ( PlayerOne.BallType == ball.Type )
			return PlayerOne;

		return PlayerTwo.BallType == ball.Type ? PlayerTwo : null;
	}

	public PoolPlayer GetOtherPlayer( PoolPlayer player )
	{
		return player == PlayerOne ? PlayerTwo : PlayerOne;
	}

	public void SetCurrentPlayer( PoolPlayer player )
	{
		CurrentPlayerId = player.GameObject.Id;
	}

	public void OnBallEnterPocket( PoolBall ball, BallPocket pocket )
	{
		Assert.True( Networking.IsHost );

		ball.PlayPocketSound();

		if ( !ball.LastStriker.IsValid() )
		{
			if ( ball.Type == PoolBallType.White )
			{
				var currentPlayer = Instance.CurrentPlayer;
				currentPlayer?.Foul( FoulReason.PotWhiteBall );
				_ = GameManager.Instance.RespawnBallAsync( ball, true );
			}
			else if ( ball.Type == PoolBallType.Black )
			{
				_ = GameManager.Instance.RespawnBallAsync( ball, true );
			}
			else
			{
				var player = GetBallPlayer( ball );

				if ( player != null && player.IsValid() )
				{
					var currentPlayer = Instance.CurrentPlayer;

					if ( currentPlayer == player )
						player.HasSecondShot = true;

					DoPlayerPotBall( currentPlayer, ball, BallPotType.Silent );
				}

				_ = GameManager.Instance.RemoveBallAsync( ball, true );
			}

			return;
		}

		if ( ball.Type == PoolBallType.White )
		{
			ball.LastStriker.Foul( FoulReason.PotWhiteBall );
			_ = GameManager.Instance.RespawnBallAsync( ball, true );
		}
		else if ( ball.Type == ball.LastStriker.BallType )
		{
			if ( CurrentPlayer == ball.LastStriker )
			{
				ball.LastStriker.HasSecondShot = true;
				ball.LastStriker.DidHitOwnBall = true;
			}

			DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Normal );
			_ = GameManager.Instance.RemoveBallAsync( ball, true );
		}
		else if ( ball.Type == PoolBallType.Black )
		{
			DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Normal );
			_ = GameManager.Instance.RemoveBallAsync( ball, true );
		}
		else
		{
			if ( ball.LastStriker.BallType == PoolBallType.White )
			{
				// We only get a second shot if we didn't foul.
				if ( ball.LastStriker.FoulReason == FoulReason.None )
					ball.LastStriker.HasSecondShot = true;

				// This is our ball type now, we've claimed it.
				ball.LastStriker.DidHitOwnBall = true;
				ball.LastStriker.BallType = ball.Type;

				var otherPlayer = GetOtherPlayer( ball.LastStriker );
				otherPlayer.BallType =
					(ball.Type == PoolBallType.Spots ? PoolBallType.Stripes : PoolBallType.Spots);

				DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Claim );
				DidClaimThisTurn = true;
			}
			else
			{
				if ( !DidClaimThisTurn )
					ball.LastStriker.Foul( FoulReason.PotOtherBall );

				DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Normal );
			}

			_ = GameManager.Instance.RemoveBallAsync( ball, true );
		}
	}

	public void OnBallHitOtherBall( PoolBall ball, PoolBall other )
	{
		Assert.True( Networking.IsHost );

		if ( ball.Type != PoolBallType.White ) return;

		if ( ball.LastStriker.BallType == PoolBallType.White )
		{
			/* Questioning if this Foul should be here.
			 * If the 8 ball gets potted, the player should lose according to 8-ball pool rules.
			 * If the player pots the 8-ball on first strike, a re-rack should happen. */
			if ( other.Type == PoolBallType.Black )
			{
				// The player has somehow hit the black as their first strike.
				//ball.LastStriker.Foul( FoulReason.HitOtherBall );
			}
		}
		else if ( other.Type == PoolBallType.Black )
		{
			if ( ball.LastStriker.BallsLeft > 0 )
			{
				if ( !ball.LastStriker.DidHitOwnBall )
					ball.LastStriker.Foul( FoulReason.HitOtherBall );
			}
			else
			{
				ball.LastStriker.DidHitOwnBall = true;
			}
		}
		else if ( other.Type != ball.LastStriker.BallType )
		{
			if ( !ball.LastStriker.DidHitOwnBall )
				ball.LastStriker.Foul( FoulReason.HitOtherBall );
		}
		else if ( ball.LastStriker.FoulReason == FoulReason.None )
		{
			ball.LastStriker.DidHitOwnBall = true;
		}
	}
	public void IncrementRoundCount()
	{
		RoundCount += 1;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !Networking.IsHost || State != RoundState.Playing )
			return;

		if ( CurrentPlayer.IsValid() && CurrentPlayer.HasStruckWhiteBall &&
		     CurrentPlayer.TimeSinceWhiteStruck > 3f )
		{
			var whiteBall = GameManager.Instance.WhiteBall;
			if ( whiteBall.IsValid() && whiteBall.Network.IsOwner )
			{
				CheckForStoppedBalls();
			}
		}
			
		var timeLeft = MathF.Max( PlayerTurnEndTime, 0f );
			
		if ( !CurrentPlayer.IsValid() )
			return;

		if ( CurrentPlayer.HasStruckWhiteBall )
			return;

		TimeLeftSeconds = timeLeft.CeilToInt();

		/*
		if ( timeLeft <= 4f && ClockTickingSound == null )
		{
			ClockTickingSound = currentPlayer.PlaySound( "clock-ticking" );
			ClockTickingSound.Value.SetVolume( 0.5f );
		}
		*/

		if ( timeLeft <= 0f )
		{
			EndTurn();
		}
	}

	private bool ShouldIncreaseTimeScale()
	{
		var currentPlayer = CurrentPlayer;

		if ( currentPlayer.TimeSinceWhiteStruck >= 7f )
			return true;

		return currentPlayer.TimeSinceWhiteStruck >= 4f;
	}

	private void EndTurn()
	{
		Assert.True( Networking.IsHost );
		IncrementRoundCount();
		var currentPlayer = CurrentPlayer;

		foreach ( var ball in GameManager.Instance.Balls )
		{
			// Physics component is disabled, this means it's properties aren't set to a reference!
			if (currentPlayer.IsPlacingWhiteBall)
			{ break; }
			var physics = ball.Components.Get<Rigidbody>();
			if ( !physics.IsValid() ) continue;
			
			physics.AngularVelocity = Vector3.Zero;
			physics.Velocity = Vector3.Zero;
			physics.ClearForces();
		}

		PoolCue.Instance.Reset();

		var didHitAnyBall = currentPlayer.DidPotBall;

		if ( !didHitAnyBall )
		{
			if ( GameManager.Instance.Balls.Any( ball =>
				    ball.Type != PoolBallType.White && ball.LastStriker == currentPlayer ) )
			{
				didHitAnyBall = true;
			}
		}

		foreach ( var ball in GameManager.Instance.Balls )
		{
			ball.LastStriker = null;
		}

		if ( !didHitAnyBall )
			currentPlayer.Foul( FoulReason.HitNothing );

		if ( currentPlayer.IsPlacingWhiteBall )
		{
			_ = GameManager.Instance.RespawnBallAsync( GameManager.Instance.WhiteBall );
			currentPlayer.StopPlacingWhiteBall();
		}

		var otherPlayer = GetOtherPlayer( currentPlayer );
		var blackBall = GameManager.Instance.BlackBall;

		if ( blackBall == null || !blackBall.IsValid() )
		{
			if ( currentPlayer.FoulReason == FoulReason.None )
			{
				//if ( currentPlayer.BallsLeft == 0 )
				//DoPlayerWin( currentPlayer );
				//else
				//DoPlayerWin( otherPlayer );
			}
			else
			{
				//DoPlayerWin( otherPlayer );
			}
		}
		else
		{
			if ( !currentPlayer.HasSecondShot )
			{
				currentPlayer.FinishTurn();
				otherPlayer.StartTurn( currentPlayer.FoulReason != FoulReason.None );
			}
			else
			{
				currentPlayer.StartTurn( false, false );
			}
		}

		/*
		if ( ClockTickingSound != null )
		{
			ClockTickingSound.Value.Stop();
			ClockTickingSound = null;
		}
		*/

		IsFastForwarding = false;
		DidClaimThisTurn = false;
		PlayerTurnEndTime = 30f;
	}

	private void CheckForStoppedBalls()
	{
		if ( ShouldIncreaseTimeScale() && !IsFastForwarding )
		{
			if ( !HasPlayedFastForwardSound )
			{
				// Only play this sound once per game because it's annoying.
				HasPlayedFastForwardSound = true;
				//currentPlayer.PlaySound( "fast-forward" ).SetVolume( 0.05f );
			}

			IsFastForwarding = true;
		}

		// Now check if all balls are essentially still.
		foreach ( var ball in GameManager.Instance.Balls )
		{
			var physics = ball.Components.Get<Rigidbody>();

			if ( !physics.Velocity.IsNearlyZero( 0.2f ) )
				return;

			if ( ball.IsAnimating )
				return;
		}

		EndTurn();
	}

	public void StartGame()
	{
		Assert.True( Networking.IsHost );

		IncrementRoundCount();
    
		var players = GameManager.Instance.Players.ToList();
		PlayerOneId = players[0].GameObject.Id;
		PlayerTwoId = players[1].GameObject.Id;

		var startingPlayer = PlayerTwo;
		
		var cue = CreateCue();
		cue.Network.AssignOwnership( Connection.Find( startingPlayer.Network.OwnerId ) );

		startingPlayer.StartTurn();
		startingPlayer.StartPlacingWhiteBall();

		PlayerTurnEndTime = 30f;
		
		State = RoundState.Playing;
	}

	protected override void OnAwake()
	{
		Instance = this;
		PotHistory = new();
		
		base.OnAwake();
	}

	private void DoPlayerPotBall( PoolPlayer player, PoolBall ball, BallPotType type )
	{
		if ( PotHistory.Count != 0 && PotHistory[^1].Number == ball.Number )
			return;

		player.DidPotBall = true;

		PotHistory.Add( new()
		{
			Type = ball.Type,
			Number = ball.Number
		} );

		switch ( type )
		{
			case BallPotType.Normal:
				GameManager.Instance.AddToast( player.SteamId, $"{ player.SteamName } has potted a ball", ball.GetIconClass() );
				break;
			case BallPotType.Claim:
				GameManager.Instance.AddToast( player.SteamId, $"{ player.SteamName } has claimed { ball.Type }", ball.GetIconClass() );
				break;
			case BallPotType.Silent:
				break;
			default:
				throw new ArgumentOutOfRangeException( nameof( type ), type, null );
		}

		var owner = GetBallPlayer( ball );

		if ( owner.IsValid() )
			owner.Score++;
	}
	
	private PoolCue CreateCue()
	{
		var cueObject = GameManager.Instance.CuePrefab.Clone();
		var cue = cueObject.Components.Get<PoolCue>();
		cueObject.Network.SetOrphanedMode( NetworkOrphaned.Host );
		cueObject.BreakFromPrefab();
		cueObject.NetworkSpawn();
		return cue;
	}
}
