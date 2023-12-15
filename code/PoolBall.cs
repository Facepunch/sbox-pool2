using Sandbox;

namespace Facepunch.Pool;

public class PoolBall : Component, Component.ICollisionListener
{
	[Property] public PoolBallType Type { get; set; }
	[Property] public PoolBallNumber Number { get; set; }
	
	private float StartUpPosition { get; set; }

	public void OnEnterPocket( BallPocket pocket )
	{
		
	}

	protected override void OnStart()
	{
		var body = Components.Get<Rigidbody>();
		body.AngularDamping = 0.6f;
		body.LinearDamping = 0.6f;

		StartUpPosition = Transform.Position.z;
		
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		var renderer = Components.Get<ModelRenderer>();

		if ( renderer is not null )
			renderer.MaterialGroup = GetMaterialGroup();

		// Constantly set our Z velocity to zero.
		var body = Components.Get<Rigidbody>();
		body.Velocity = body.Velocity.WithZ( 0f );
		
		// Constantly keep up at the correct Z position.
		Transform.Position = Transform.Position.WithZ( StartUpPosition );
		
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
		var otherObject = info.Other.GameObject;
		var otherBall = otherObject.Components.GetInDescendantsOrSelf<PoolBall>();

		if ( otherBall.IsValid() )
		{
			
		}
	}

	public void OnCollisionUpdate( Collision info )
	{
		
	}

	public void OnCollisionStop( CollisionStop info )
	{
		
	}
}
