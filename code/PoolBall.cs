using System.Linq;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Network;

namespace Facepunch.Pool;

public class PoolBall : Component, Component.ICollisionListener
{
	[Property] public PoolBallType Type { get; set; }
	[Property] public PoolBallNumber Number { get; set; }
	[Property] public Rigidbody Physics { get; set; }
	public PoolPlayer LastStriker { get; set; }
	public bool IsAnimating { get; private set; }
	
	private BallPocket LastPocket { get; set; }
	private float StartUpPosition { get; set; }
	[Sync] private float RenderAlpha { get; set; }

	public void OnEnterPocket( BallPocket pocket )
	{
		Assert.True( Networking.IsHost );
		LastPocket = pocket;
		GameState.Instance.OnBallEnterPocket( this, pocket );
	}

	public void StartPlacing()
	{
		Assert.True( Networking.IsHost );
		Physics.PhysicsBody.EnableSolidCollisions = false;
		Physics.PhysicsBody.MotionEnabled = false;
		Physics.Enabled = false;
		/* TODO: Disable collisions for pool ball so user doesn't activate penalties on collisions
		 *		 mutating physics.PhysicsBody.Enabled causes a host crash due to one of the following conditions from this exception on the host "System.ArgumentOutOfRangeException"
		 *		 1. Race condition: Network is attempting to sync the compontent but is unable to
		 *		 2. VooDoo magic: I hate networking code and yet I write more and more as.
		 */
	}

	public void Respawn( Vector3 position )
	{
		RenderAlpha = 1f;
		Transform.Scale = 1f;
		Transform.Position = position;

		var renderer = Components.Get<ModelRenderer>();
		renderer.Tint = renderer.Tint.WithAlpha( RenderAlpha );

		Physics.AngularVelocity = Vector3.Zero;
		Physics.Velocity = Vector3.Zero;
		Physics.ClearForces();
	}

	public string GetIconClass()
	{
		return Type switch
		{
			PoolBallType.Black => "black",
			PoolBallType.White => "white",
			_ => $"{Type.ToString().ToLower()}_{(int)Number}"
		};
	}

	public bool CanPlayerHit( PoolPlayer player )
	{
		if ( player.BallType == PoolBallType.White )
		{
			return Type != PoolBallType.Black;
		}

		if ( GameState.Instance.GetBallPlayer( this ) == player )
			return true;

		return Type == PoolBallType.Black && player.BallsLeft == 0;
	}

	public async Task AnimateIntoPocket()
	{
		return;
		Assert.True( Networking.IsHost );
		Assert.True( !IsAnimating );
		
		Physics.PhysicsBody.EnableSolidCollisions = false;
		Physics.PhysicsBody.MotionEnabled = false;
		Physics.PhysicsBody.Enabled = false;
		
		IsAnimating = true;

		while ( true )
		{
			// I don't know why but this causes a crash on the host instantly.
			await Task.Delay( 30 ); 

			RenderAlpha = RenderAlpha.LerpTo( 0f, Time.Delta * 5f );

			// So does attempting to mutate the position - ladd
			if ( LastPocket != null && LastPocket.IsValid() )
				Transform.Position = Transform.Position.LerpTo( LastPocket.Transform.Position, Time.Delta * 16f );

			if ( RenderAlpha.AlmostEqual( 0f ) )
				break;
		}
		

		Physics.PhysicsBody.Enabled = true;
		Physics.PhysicsBody.EnableSolidCollisions = true;
		Physics.PhysicsBody.MotionEnabled = true;
		IsAnimating = false;
	}

	public void StopPlacing()
	{
		Assert.True( Networking.IsHost );
		
		Physics.Enabled = true;
		Physics.PhysicsBody.EnableSolidCollisions = true;
		Physics.PhysicsBody.MotionEnabled = true;
		Physics.AngularVelocity = Vector3.Zero;
		Physics.Velocity = Vector3.Zero;
		Physics.ClearForces();
	}
	
	[Broadcast]
	public void TryMoveTo( Vector3 position )
	{
		if ( !Networking.IsHost ) return;

		// TODO: Prevent collisions with other balls. ;)

		Transform.Position = position.WithZ( Transform.Position.z );
	}

	protected override void OnStart()
	{
		Physics.AngularDamping = 0.6f;
		Physics.LinearDamping = 0.6f;

		StartUpPosition = Transform.Position.z;
		RenderAlpha = 1f;
		
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		var renderer = Components.Get<ModelRenderer>();

		if ( renderer is not null )
		{
			renderer.MaterialGroup = GetMaterialGroup();
			renderer.Tint = renderer.Tint.WithAlpha( RenderAlpha );
		}

		if ( Network.IsOwner )
		{
			// Constantly set our Z velocity to zero.
			//Physics.Velocity = Physics.Velocity.WithZ( 0f );

			// Constantly keep up at the correct Z position.
			//Transform.Position = Transform.Position.WithZ( StartUpPosition );
		}
		
		base.OnUpdate();
	}
	
	private string GetMaterialGroup()
	{
		return Type switch
		{
			PoolBallType.Black => "8",
			PoolBallType.Spots => ((int)Number).ToString(),
			PoolBallType.Stripes => ((int)Number + 8).ToString(),
			_ => "default"
		};
	}

	void ICollisionListener.OnCollisionStart( Collision info )
	{
		if ( !Networking.IsHost ) return;
		
		var otherObject = info.Other.GameObject;
		var otherBall = otherObject.Components.GetInDescendantsOrSelf<PoolBall>();
		if ( !otherBall.IsValid() ) return;

		LastStriker = GameState.Instance.CurrentPlayer;
		GameState.Instance.OnBallHitOtherBall( this, otherBall );

		PlayCollideSound( info.Contact.NormalSpeed );
	}

	void ICollisionListener.OnCollisionUpdate( Collision info )
	{
		
	}

	void ICollisionListener.OnCollisionStop( CollisionStop info )
	{
		
	}

	[Broadcast]
	public void PlayPocketSound()
	{
		Sound.Play( $"ball-pocket-{Game.Random.Int( 1, 2 )}" );
	}

	[Broadcast]
	private void PlayCollideSound( float speed )
	{
		var sound = Sound.Play( "ball-collide" );
		sound.Pitch = Game.Random.Float( 0.9f, 1f );
		sound.Volume = (1f / 100f) * speed;
	}
}
