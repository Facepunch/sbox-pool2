using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Network;

namespace Facepunch.Pool;

public class GameManager : Component, Component.INetworkListener
{
	public static GameManager Instance { get; private set; }

	public PoolBall BlackBall => Balls.FirstOrDefault( b => b.Type == PoolBallType.Black );
	public PoolBall WhiteBall => Balls.FirstOrDefault( b => b.Type == PoolBallType.White );
	public IEnumerable<PoolPlayer> Players => Scene.GetAllComponents<PoolPlayer>();
	public IEnumerable<PoolBall> Balls => Scene.GetAllComponents<PoolBall>();
	
	[Property] public GameObject BallPrefab { get; set; }
	[Property] public GameObject CuePrefab { get; set; }
	
	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;

		if ( !GameNetworkSystem.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			await Task.DelayRealtimeSeconds( 0.1f );
			GameNetworkSystem.CreateLobby();
		}
	}

	void INetworkListener.OnActive( Connection connection )
	{
		Log.Info( $"{connection.DisplayName} has become active" );
		
		var playerObject = new GameObject();
		var player = playerObject.Components.Create<PoolPlayer>();

		player.SteamName = connection.DisplayName ?? "local";
		player.SteamId = connection.SteamId;
		playerObject.NetworkSpawn( connection );
		if ( Players.Count() != 2 ) return;
		
		GameState.Instance.StartGame();
	}

	
	[Broadcast]
	public void AddToast( ulong steamId, string text, string iconClass = "" )
	{
		var player = Players.FirstOrDefault( p => p.SteamId == steamId );
		if ( !player.IsValid() ) return;

		ToastList.Current.AddItem( player, text, iconClass );
	}
	
	public async Task RespawnBallAsync( PoolBall ball, bool shouldAnimate = false )
	{
		Assert.True( Networking.IsHost );

		if ( shouldAnimate )
		{
			ball.StartAnimating();

			while ( ball.IsAnimating )
			{
				await Task.Delay( 30 );
			}
		}

		var spawns = Scene.GetAllComponents<PoolBallSpawn>();

		foreach ( PoolBallSpawn spawn in spawns )
		{
			if ( spawn.Type != ball.Type || spawn.Number != ball.Number )
				continue;

			ball.Respawn( spawn.Transform.Position );
			return;
		}
	}

	public async Task RemoveBallAsync( PoolBall ball, bool shouldAnimate = false )
	{
		Assert.True( Networking.IsHost );

		if ( shouldAnimate )
		{
			ball.StartAnimating();

			while ( ball.IsAnimating )
			{
				await Task.Delay( 30 );
			}
		}

		ball.GameObject.Destroy();
	}

	protected override void OnAwake()
	{
		Instance = this;
		base.OnAwake();
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			var spawns = Scene.GetAllComponents<PoolBallSpawn>();

			foreach ( var spawn in spawns )
			{
				var ballObject = BallPrefab.Clone();
				var ball = ballObject.Components.Get<PoolBall>();
			
				ball.Transform.World = spawn.Transform.World;
				ball.Number = spawn.Number;
				ball.Type = spawn.Type;

				ballObject.Network.SetOrphanedMode( NetworkOrphaned.Host );
				ballObject.BreakFromPrefab();
				ballObject.NetworkSpawn();
			}
			
			var stateObject = new GameObject();
			stateObject.Name = "State";
			stateObject.Components.Create<GameState>();
			stateObject.Network.SetOrphanedMode( NetworkOrphaned.Host );
			stateObject.NetworkSpawn();
		}
		
		Scene.PhysicsWorld.SubSteps = 10;
		
		base.OnStart();
	}
}
