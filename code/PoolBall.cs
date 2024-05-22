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

	[Broadcast( NetPermission.HostOnly )]
	public void StartPlacing()
	{
		Physics.PhysicsBody.EnableSolidCollisions = false;
		Physics.PhysicsBody.MotionEnabled = false;
		Physics.Enabled = false;
	}

	[Broadcast( NetPermission.HostOnly )]
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
		Assert.True( Networking.IsHost );
		Assert.True( !IsAnimating );

		DisableCollisions();
		IsAnimating = true;

		while ( true )
		{
			await Task.Frame();

			RenderAlpha = RenderAlpha.LerpTo( 0f, Time.Delta * 5f );
			
			if ( LastPocket != null && LastPocket.IsValid() )
				Transform.Position = Transform.Position.LerpTo( LastPocket.Transform.Position, Time.Delta * 16f );

			if ( RenderAlpha.AlmostEqual( 0f ) )
				break;
		}
		
		EnableCollisions();
		IsAnimating = false;
	}

	[Broadcast( NetPermission.HostOnly )]
	private void DisableCollisions()
	{
		Physics.PhysicsBody.EnableSolidCollisions = false;
		Physics.PhysicsBody.MotionEnabled = false;
		Physics.PhysicsBody.Enabled = false;
	}

	[Broadcast( NetPermission.HostOnly )]
	private void EnableCollisions()
	{
		Physics.PhysicsBody.Enabled = true;
		Physics.PhysicsBody.EnableSolidCollisions = true;
		Physics.PhysicsBody.MotionEnabled = true;
	}

	[Broadcast( NetPermission.HostOnly )]
	public void StopPlacing()
	{
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
