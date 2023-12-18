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
	public PoolPlayer LastStriker { get; private set; }
	public bool IsAnimating { get; private set; }
	public BallPocket LastPocket { get; private set; }
	
	private float StartUpPosition { get; set; }

	public void OnEnterPocket( BallPocket pocket )
	{
		if ( IsAnimating ) return;

		LastPocket = pocket;
		GameState.Instance.OnBallEnterPocket( this, pocket );
	}
	
	public void ResetLastStriker()
	{
		LastStriker = null;
	}

	public void StartPlacing()
	{
		Assert.True( GameNetworkSystem.IsHost );
		var physics = Components.Get<Rigidbody>();
		physics.PhysicsBody.EnableSolidCollisions = false;
		physics.PhysicsBody.Enabled = false;
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

		var renderer = Components.Get<ModelRenderer>();
		var physics = Components.Get<Rigidbody>();
		physics.PhysicsBody.EnableSolidCollisions = false;
		physics.PhysicsBody.MotionEnabled = false;
		physics.PhysicsBody.Enabled = false;
		
		IsAnimating = true;

		while ( true )
		{
			await Task.Delay( 30 );

			renderer.Tint = renderer.Tint.WithAlpha( renderer.Tint.a.LerpTo( 0f, Time.Delta * 5f ) );
			
			if ( LastPocket != null && LastPocket.IsValid() )
				Transform.Position = Transform.Position.LerpTo( LastPocket.Transform.Position, Time.Delta * 16f );

			if ( renderer.Tint.a.AlmostEqual( 0f ) )
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
		physics.PhysicsBody.Enabled = true;
		physics.AngularVelocity = Vector3.Zero;
		physics.Velocity = Vector3.Zero;
		physics.ClearForces();
	}
	
	[Authority]
	public void TryMoveTo( Vector3 worldPos )
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
			Transform.Position = worldPos.WithZ( Transform.Position.z );
		//}
	}

	protected override void OnStart()
	{
		var physics = Components.Get<Rigidbody>();
		physics.AngularDamping = 0.6f;
		physics.LinearDamping = 0.6f;

		StartUpPosition = Transform.Position.z;
		
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		var renderer = Components.Get<ModelRenderer>();

		if ( renderer is not null )
			renderer.MaterialGroup = GetMaterialGroup();

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

	public void OnCollisionStart( Collision info )
	{
		if ( !GameNetworkSystem.IsHost ) return;
		
		var otherObject = info.Other.GameObject;
		var otherBall = otherObject.Components.GetInDescendantsOrSelf<PoolBall>();
		if ( !otherBall.IsValid() ) return;

		LastStriker = GameState.Instance.CurrentPlayer;
		GameState.Instance.OnBallHitOtherBall( this, otherBall );

		PlayCollideSound( info.Contact.NormalSpeed );
	}

	public void OnCollisionUpdate( Collision info )
	{
		
	}

	public void OnCollisionStop( CollisionStop info )
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
