namespace Nickel;

/// <summary>
/// Describes a ship part damage modifier (<see cref="PDamMod"/>).
/// </summary>
public interface IPartDamageModifierEntry : IModOwned
{
	/// <summary>The part damage modifier (<see cref="PDamMod"/>) described by this entry.</summary>
	PDamMod PartDamageModifier { get; }
	
	/// <summary>The configuration used to register the part damage modifier (<see cref="PDamMod"/>).</summary>
	PartDamageModifierConfiguration Configuration { get; }
}
