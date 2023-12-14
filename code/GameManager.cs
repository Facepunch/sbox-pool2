using Sandbox;

namespace Facepunch.Pool;

public class GameManager : Component
{
	[Property] public GameObject BallPrefab { get; set; }
	
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
		}
		
		Scene.PhysicsWorld.SubSteps = 10;
		
		base.OnStart();
	}
}
