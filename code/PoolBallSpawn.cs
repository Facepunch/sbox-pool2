using Sandbox;

namespace Facepunch.Pool;

public class PoolBallSpawn : Component
{
	[Property] public PoolBallType Type { get; set; }
	[Property] public PoolBallNumber Number { get; set; }

	protected override void DrawGizmos()
	{
		var model = Model.Load( "models/pool/pool_ball.vmdl" );
		Gizmo.Hitbox.Model( model );

		if ( Gizmo.IsSelected )
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
		else if ( Gizmo.IsHovered )
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
		
		if ( Gizmo.IsHovered || Gizmo.IsSelected )
			Gizmo.Draw.LineSphere( Vector3.Zero, 1.15f, 10 );
		
		Gizmo.Draw.Color = Color.White;
		
		var so = Gizmo.Draw.Model( model, global::Transform.Zero );
		so.SetMaterialGroup( GetMaterialGroup() );
		so.ColorTint = Color.White.WithAlpha( 0.5f );
		
		base.DrawGizmos();
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
