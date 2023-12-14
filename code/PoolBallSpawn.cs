using Sandbox;

namespace Facepunch.Pool;

public class PoolBallSpawn : Component, Component.ExecuteInEditor
{
	[Property] public PoolBallType Type { get; set; }
	[Property] public PoolBallNumber Number { get; set; }

	protected override void DrawGizmos()
	{
		base.DrawGizmos();
	}

	protected override void OnUpdate()
	{
		var renderer = Components.Get<ModelRenderer>();
		if ( renderer is not null )
		{
			renderer.MaterialGroup = GetMaterialGroup();
		}
		
		base.OnUpdate();
	}

	private string GetMaterialGroup()
	{
		if ( Type == PoolBallType.Black )
			return "8";
		else if ( Type == PoolBallType.Spots )
			return ((int)Number).ToString();
		else if ( Type == PoolBallType.Stripes )
			return ((int)Number + 8).ToString();

		return "default";
	}
}
