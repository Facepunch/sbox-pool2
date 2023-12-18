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

	void INetworkListener.OnActive( Connection connection )
	{
		Log.Info( $"{connection.DisplayName} has become active" );
		
		var playerObject = new GameObject();
		var player = playerObject.Components.Create<PoolPlayer>();

		player.SteamName = connection.DisplayName ?? "local";
		player.SteamId = connection.SteamId;
		player.ConnectionId = connection.Id;
		player.Connection = connection;
		player.Network.Spawn();

		playerObject.Parent = Scene;

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
		Assert.True( GameNetworkSystem.IsHost );
		
		if ( shouldAnimate )
			await ball.AnimateIntoPocket();

		var spawners = Scene.GetAllComponents<PoolBallSpawn>();

		foreach ( var spawner in spawners )
		{
			if ( spawner.Type != ball.Type || spawner.Number != ball.Number )
			{
				continue;
			}

			ball.Transform.Scale = 1f;
			ball.Transform.Position = spawner.Transform.Position;

			var renderer = ball.Components.Get<ModelRenderer>();
			renderer.Tint = renderer.Tint.WithAlpha( 1f );

			var physics = ball.Components.Get<Rigidbody>();
			physics.AngularVelocity = Vector3.Zero;
			physics.Velocity = Vector3.Zero;
			physics.ClearForces();

			return;
		}
	}

	public async Task RemoveBallAsync( PoolBall ball, bool shouldAnimate = false )
	{
		Assert.True( GameNetworkSystem.IsHost );
		
		if ( shouldAnimate )
			await ball.AnimateIntoPocket();

		ball.GameObject.Destroy();
	}

	protected override void OnAwake()
	{
		Instance = this;
		
		base.OnAwake();
	}

	protected override void OnStart()
	{
		if ( GameNetworkSystem.IsHost )
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
			
			var stateObject = new GameObject();
			var state = stateObject.Components.Create<GameState>();
			stateObject.Parent = Scene;
		
			state.Network.Spawn();
		}
		
		Scene.PhysicsWorld.SubSteps = 10;
		
		base.OnStart();
	}
}
