﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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

public class GameState : Component, INetworkSerializable
{
	public static GameState Instance { get; private set; }

	public List<PotHistoryItem> PotHistory { get; private set; }

	public PoolPlayer PlayerOne { get; private set; }
	public PoolPlayer PlayerTwo { get; private set; }
	public PoolPlayer CurrentPlayer { get; internal set; }

	public RealTimeUntil PlayerTurnEndTime { get; private set; }
	public int TimeLeftSeconds { get; private set; }
	public RoundState State { get; private set; }
	
	private bool DidClaimThisTurn { get; set; }
	private bool HasPlayedFastForwardSound { get; set; }
	private bool IsFastForwarding { get; set; }

	void INetworkSerializable.Write( ref ByteStream stream )
	{
		stream.Write( PlayerOne?.ConnectionId ?? Guid.Empty );
		stream.Write( PlayerTwo?.ConnectionId ?? Guid.Empty );
		stream.Write( CurrentPlayer?.ConnectionId ?? Guid.Empty );
		stream.Write( TimeLeftSeconds );
		stream.Write( State );
		stream.Write( PotHistory.Count );

		foreach ( var p in PotHistory )
		{
			stream.Write( p );
		}
	}

	void INetworkSerializable.Read( ByteStream stream )
	{
		var playerOneId = stream.Read<Guid>();
		var playerTwoId = stream.Read<Guid>();
		var currentPlayerId = stream.Read<Guid>();

		PlayerOne = GameManager.Instance.Players.FirstOrDefault( p => p.ConnectionId == playerOneId );
		PlayerTwo = GameManager.Instance.Players.FirstOrDefault( p => p.ConnectionId == playerTwoId );
		CurrentPlayer = GameManager.Instance.Players.FirstOrDefault( p => p.ConnectionId == currentPlayerId );
		State = stream.Read<RoundState>();
		TimeLeftSeconds = stream.Read<int>();

		var potHistoryCount = stream.Read<int>();
		PotHistory.Clear();

		for ( var i = 0; i < potHistoryCount; i++ )
		{
			PotHistory.Add( stream.Read<PotHistoryItem>() );
		}
	}

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

	public void OnBallEnterPocket( PoolBall ball, BallPocket pocket )
	{
		Assert.True( GameNetworkSystem.IsHost );

		ball.PlayPocketSound();

		if ( ball.LastStriker == null || !ball.LastStriker.IsValid() )
		{
			switch ( ball.Type )
			{
				case PoolBallType.White:
					_ = GameManager.Instance.RespawnBallAsync( ball, true );
					return;
				case PoolBallType.Black:
					_ = GameManager.Instance.RespawnBallAsync( ball, true );
					return;
			}

			var player = GetBallPlayer( ball );

			if ( player != null && player.IsValid() )
			{
				var currentPlayer = GameState.Instance.CurrentPlayer;

				if ( currentPlayer == player )
					player.HasSecondShot = true;

				DoPlayerPotBall( currentPlayer, ball, BallPotType.Silent );
			}

			_ = GameManager.Instance.RemoveBallAsync( ball, true );
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
		Assert.True( GameNetworkSystem.IsHost );

		if ( ball.Type != PoolBallType.White ) return;

		if ( ball.LastStriker.BallType == PoolBallType.White )
		{
			if ( other.Type == PoolBallType.Black )
			{
				// The player has somehow hit the black as their first strike.
				ball.LastStriker.Foul( FoulReason.HitOtherBall );
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

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !GameNetworkSystem.IsHost || State != RoundState.Playing )
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
		Assert.True( GameNetworkSystem.IsHost );

		var currentPlayer = CurrentPlayer;

		foreach ( var ball in GameManager.Instance.Balls )
		{
			var physics = ball.Components.Get<Rigidbody>();
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
		Assert.True( GameNetworkSystem.IsHost );
		
		PlayerOne = GameManager.Instance.Players.ElementAt( 0 );
		PlayerTwo = GameManager.Instance.Players.ElementAt( 1 );

		var startingPlayer = PlayerTwo;
		
		var cue = CreateCue();
		cue.Network.AssignOwnership( startingPlayer.Connection );

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
		var cueObject = SceneUtility.Instantiate( GameManager.Instance.CuePrefab );
		var cue = cueObject.Components.Get<PoolCue>();
		cueObject.BreakFromPrefab();
		cueObject.Network.Spawn();
		return cue;
	}
}
