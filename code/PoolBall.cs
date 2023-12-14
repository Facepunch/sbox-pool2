using Sandbox;

namespace Facepunch.Pool;

public class PoolBall : Component
{
	[Property] public PoolBallType Type { get; set; }
	[Property] public PoolBallNumber Number { get; set; }

	protected override void OnStart()
	{
		var body = Components.Get<Rigidbody>();
		body.AngularDamping = 0.4f;
		body.LinearDamping = 0.6f;
		
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		var renderer = Components.Get<ModelRenderer>();

		if ( renderer is not null )
			renderer.MaterialGroup = GetMaterialGroup();
		
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
}
