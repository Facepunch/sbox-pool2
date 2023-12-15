using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Diagnostics;

namespace Facepunch.Pool;

public class GameManager : Component, Component.INetworkListener
{
	public static GameManager Instance { get; private set; }
	
	[Property] public GameObject BallPrefab { get; set; }
	[Property] public GameObject CuePrefab { get; set; }

	public IEnumerable<PoolPlayer> Players => Scene.GetAllComponents<PoolPlayer>();
	public IEnumerable<PoolBall> Balls => Scene.GetAllComponents<PoolBall>();

	public void OnActive( Connection connection )
	{
		Log.Info( $"{connection.DisplayName} has become active" );
		
		var playerObject = new GameObject();
		var player = playerObject.Components.Create<PoolPlayer>();
		
		player.Connection = connection;
		player.Network.AssignOwnership( connection );
		player.Network.Spawn( connection );

		playerObject.Parent = Scene;

		if ( Players.Count() != 2 ) return;

		Log.Info( "We have two players now, hurrah!" );
		StartGame();
	}

	protected override void OnAwake()
	{
		Instance = this;
		base.OnAwake();
	}

	protected override void OnStart()
	{
		var spawns = Scene.GetAllComponents<PoolBallSpawn>();

		foreach ( var spawn in spawns )
		{
			var ballObject = SceneUtility.Instantiate( BallPrefab );
			var ball = ballObject.Components.Get<PoolBall>();
			
			ball.Transform.World = spawn.Transform.World;
			ball.Number = spawn.Number;
			ball.Type = spawn.Type;

			ballObject.BreakFromPrefab();
			ballObject.Network.Spawn();
		}
		
		Scene.PhysicsWorld.SubSteps = 10;
		
		base.OnStart();
	}

	private PoolCue CreateCue()
	{
		var cueObject = SceneUtility.Instantiate( CuePrefab );
		var cue = cueObject.Components.Get<PoolCue>();
		cueObject.BreakFromPrefab();
		cueObject.Network.Spawn();
		return cue;
	}

	private void StartGame()
	{
		var startingPlayer = Players.FirstOrDefault();
		var cue = CreateCue();
		cue.Network.AssignOwnership( startingPlayer.Connection );
		Log.Info( $"Started game, gave cue to {startingPlayer.Connection.DisplayName}" );
	}
}
