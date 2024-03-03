using System;
using System.Linq;
using Sandbox;
using Sandbox.Services;

namespace Facepunch.Pool;

public enum EloOutcome
{
	Loss = 0,
	Win = 1
}

public class EloScore
{
	public static void Update( EloScore playerOne, EloScore playerTwo, EloOutcome outcome )
	{
		const int eloK = 32;
		var delta = (int)(eloK * ((double)outcome - GetExpectationToWin( playerOne, playerTwo )));
		
		playerOne.Delta = delta;
		playerOne.Rating += delta;
		playerOne.Rating = Math.Max( playerOne.Rating, 1000 );

		playerTwo.Delta = -delta;
		playerTwo.Rating -= delta;
		playerOne.Rating = Math.Max( playerOne.Rating, 1000 );
	}
	
	public static double GetExpectationToWin( EloScore playerOne, EloScore playerTwo )
	{
		return 1f / (1f + MathF.Pow( 10f, (playerTwo.Rating - playerOne.Rating) / 400f ));
	}
	
	public int Rating { get; set; }
	public int Delta { get; set; }

	public PlayerRank GetRank()
	{
		return Elo.GetRank( Rating );
	}

	public PlayerRank GetNextRank()
	{
		return Elo.GetNextRank( Rating );
	}

	public int GetLevel()
	{
		return Elo.GetLevel( Rating );
	}

	public async void Initialize( PoolPlayer player )
	{
		var leaderboard = Leaderboards.Get( "elo" );
		leaderboard.TargetSteamId = (long)player.Network.OwnerConnection.SteamId;
		leaderboard.MaxEntries = 1;
		await leaderboard.Refresh();

		if ( leaderboard.TotalEntries == 0 )
		{
			Rating = 1000;
			return;
		}
		
		var entry = leaderboard.Entries.FirstOrDefault();

		if ( entry.SteamId == (long)player.Network.OwnerConnection.SteamId )
		{
			Rating = Math.Max( (int)entry.Value, 1000 );
		}
	}
}
