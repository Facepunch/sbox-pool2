namespace Facepunch.Pool;

public static class BBoxExtension
{
	public static bool ContainsXY( this BBox a, BBox b )
	{
		return (
			b.Mins.x >= a.Mins.x && b.Maxs.x < a.Maxs.x &&
			b.Mins.y >= a.Mins.y && b.Maxs.y < a.Maxs.y
		); ;
	}

	public static BBox ToWorldSpace( this BBox bbox, Transform transform )
	{
		return new BBox
		{
			Mins = transform.PointToWorld( bbox.Mins ),
			Maxs = transform.PointToWorld( bbox.Maxs )
		};
	}
}
