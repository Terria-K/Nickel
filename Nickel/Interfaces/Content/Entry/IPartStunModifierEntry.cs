namespace Nickel;

/// <summary>
/// Describes a ship part stun modifier (<see cref="PStunMod"/>).
/// </summary>
public interface IPartStunModifierEntry : IModOwned
{
	/// <summary>The part stun modifier (<see cref="PStunMod"/>) described by this entry.</summary>
	PStunMod PartStunModifier { get; }
	
	/// <summary>The configuration used to register the part stun modifier (<see cref="PStunMod"/>).</summary>
	PartStunModifierConfiguration Configuration { get; }
}
