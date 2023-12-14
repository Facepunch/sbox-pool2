using System;

namespace Facepunch.Pool;

public enum FoulReason
{
	None = 0,
	PotWhiteBall = 1,
	BallLeftTable = 2,
	PotBlackTooEarly = 3,
	HitOtherBall = 4,
	PotOtherBall = 5,
	HitNothing = 6
}

internal static class FoulReasonExtension
{
	public static string ToMessage( this FoulReason reason, string playerName )
	{
		switch ( reason )
		{
			case FoulReason.PotWhiteBall:
				return $"{ playerName } potted the white ball";
			case FoulReason.BallLeftTable:
				return $"{ playerName } shot a ball off the table";
			case FoulReason.PotBlackTooEarly:
				return $"{ playerName } potted the black too early";
			case FoulReason.HitOtherBall:
				return $"{ playerName } hit the wrong ball";
			case FoulReason.PotOtherBall:
				return $"{ playerName } potted the wrong ball";
			case FoulReason.HitNothing:
				return $"{ playerName } didn't hit anything";
			case FoulReason.None:
				break;
			default:
				throw new ArgumentOutOfRangeException( nameof( reason ), reason, null );
		}

		return null;
	}
}
