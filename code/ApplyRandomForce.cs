namespace Sandbox;

public class ApplyRandomForce : Component
{
	protected override void OnStart()
	{
		var comp = Components.Get<Rigidbody>();
		comp.ApplyForce( Vector3.Random * 1000f );
		
		base.OnStart();
	}
}
