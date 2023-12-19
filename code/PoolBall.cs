using System.Linq;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Network;

namespace Facepunch.Pool;

public class PoolBall : Component, Component.ICollisionListener, INetworkSerializable
{
	[Property] public PoolBallType Type { get; set; }
	[Property] public PoolBallNumber Number { get; set; }
	public PoolPlayer LastStriker { get; set; }
	public bool IsAnimating { get; private set; }
	
	private BallPocket LastPocket { get; set; }
	private float StartUpPosition { get; set; }
	private float RenderAlpha { get; set; }

	public void OnEnterPocket( BallPocket pocket )
	{
		Assert.True( GameNetworkSystem.IsHost );
		
		LastPocket = pocket;
		GameState.Instance.OnBallEnterPocket( this, pocket );
	}

	public void StartPlacing()
	{
		Assert.True( GameNetworkSystem.IsHost );
		
		var physics = Components.Get<Rigidbody>();
		physics.PhysicsBody.EnableSolidCollisions = false;
		physics.PhysicsBody.MotionEnabled = false;
		physics.PhysicsBody.Enabled = false;
	}

	public void Respawn( Vector3 position )
	{
		RenderAlpha = 1f;
		Transform.Scale = 1f;
		Transform.Position = position;

		var renderer = Components.Get<ModelRenderer>();
		renderer.Tint = renderer.Tint.WithAlpha( RenderAlpha );

		var physics = Components.Get<Rigidbody>();
		physics.AngularVelocity = Vector3.Zero;
		physics.Velocity = Vector3.Zero;
		physics.ClearForces();
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
		Assert.True( GameNetworkSystem.IsHost );
		Assert.True( !IsAnimating );
		
		var physics = Components.Get<Rigidbody>();
		physics.PhysicsBody.EnableSolidCollisions = false;
		physics.PhysicsBody.MotionEnabled = false;
		physics.PhysicsBody.Enabled = false;
		
		IsAnimating = true;

		while ( true )
		{
			await Task.Delay( 30 );

			RenderAlpha = RenderAlpha.LerpTo( 0f, Time.Delta * 5f );
			
			if ( LastPocket != null && LastPocket.IsValid() )
				Transform.Position = Transform.Position.LerpTo( LastPocket.Transform.Position, Time.Delta * 16f );

			if ( RenderAlpha.AlmostEqual( 0f ) )
				break;
		}

		physics.PhysicsBody.EnableSolidCollisions = true;
		physics.PhysicsBody.MotionEnabled = true;
		physics.PhysicsBody.Enabled = true;
		IsAnimating = false;
	}

	public void StopPlacing()
	{
		Assert.True( GameNetworkSystem.IsHost );
		
		var physics = Components.Get<Rigidbody>();
		physics.PhysicsBody.EnableSolidCollisions = true;
		physics.PhysicsBody.MotionEnabled = true;
		physics.PhysicsBody.Enabled = true;
		physics.AngularVelocity = Vector3.Zero;
		physics.Velocity = Vector3.Zero;
		physics.ClearForces();
	}
	
	[Authority]
	public void TryMoveTo( Vector3 position )
	{
		/*
		var worldOBB = CollisionBounds + worldPos;

		foreach ( var ball in All.OfType<PoolBall>() )
		{
			if ( ball != this )
			{
				var ballOBB = ball.CollisionBounds + ball.Position;

				// We can't place on other balls.
				if ( ballOBB.Overlaps( worldOBB ) )
					return;
			}
		}
		*/

		//if ( within.ContainsXY( worldOBB ) )
		//{
		Transform.Position = position.WithZ( Transform.Position.z );
		
		var rigidbody = Components.Get<Rigidbody>();
		rigidbody.PhysicsBody.Position = Transform.Position;
		//}
	}

	protected override void OnStart()
	{
		var physics = Components.Get<Rigidbody>();
		physics.AngularDamping = 0.6f;
		physics.LinearDamping = 0.6f;

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
			var body = Components.Get<Rigidbody>();
			body.Velocity = body.Velocity.WithZ( 0f );
		
			// Constantly keep up at the correct Z position.
			Transform.Position = Transform.Position.WithZ( StartUpPosition );
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
	
	void INetworkSerializable.Write( ref ByteStream stream )
	{
		stream.Write( RenderAlpha );
	}

	void INetworkSerializable.Read( ByteStream stream )
	{
		RenderAlpha = stream.Read<float>();
	}

	void ICollisionListener.OnCollisionStart( Collision info )
	{
		if ( !GameNetworkSystem.IsHost ) return;
		
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
