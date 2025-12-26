using System.Collections.Generic;

namespace Nickel;

/// <summary>
/// A mod-specific ship registry.
/// Allows looking up and registering ships and ship parts.
/// </summary>
public interface IModShips
{
	/// <summary>
	/// A dictionary containing all entries registered by the owner of this helper.
	/// </summary>
	IReadOnlyDictionary<string, IShipEntry> RegisteredShips { get; }
	
	/// <summary>
	/// A dictionary containing all entries registered by the owner of this helper.
	/// </summary>
	IReadOnlyDictionary<string, IPartTypeEntry> RegisteredPartTypes { get; }

	/// <summary>
	/// A dictionary containing all entries registered by the owner of this helper.
	/// </summary>
	IReadOnlyDictionary<string, IPartEntry> RegisteredParts { get; }

	/// <summary>
	/// A dictionary containing all entries registered by the owner of this helper.
	/// </summary>
	IReadOnlyDictionary<string, IPartDamageModifierEntry> RegisteredPartDamageModifiers { get; }

	/// <summary>
	/// A dictionary containing all entries registered by the owner of this helper.
	/// </summary>
	IReadOnlyDictionary<string, IPartStunModifierEntry> RegisteredPartStunModifiers { get; }
	
	/// <summary>
	/// Lookup a <see cref="StarterShip"/> entry by its full <see cref="IModOwned.UniqueName"/>.
	/// </summary>
	/// <param name="uniqueName">The unique name to retrieve an entry for.</param>
	/// <returns>An entry, or <c>null</c> if the unique name does not match any known ships.</returns>
	IShipEntry? LookupByUniqueName(string uniqueName);

	/// <summary>
	/// Lookup a <see cref="PDamMod"/> entry by its full <see cref="IModOwned.UniqueName"/>.
	/// </summary>
	/// <param name="uniqueName">The unique name to retrieve an entry for.</param>
	/// <returns>An entry, or <c>null</c> if the unique name does not match any known part damage modifiers.</returns>
	IPartDamageModifierEntry? LookupPartDamageModifierByUniqueName(string uniqueName);
	
	/// <summary>
	/// Lookup a <see cref="PStunMod"/> entry by its full <see cref="IModOwned.UniqueName"/>.
	/// </summary>
	/// <param name="uniqueName">The unique name to retrieve an entry for.</param>
	/// <returns>An entry, or <c>null</c> if the unique name does not match any known part stun modifiers.</returns>
	IPartStunModifierEntry? LookupPartStunModifierByUniqueName(string uniqueName);

	/// <summary>
	/// Register a new <see cref="StarterShip"/>.
	/// </summary>
	/// <param name="name">The local (mod-level) name for the <see cref="StarterShip"/>. This has to be unique across all ships in the mod.</param>
	/// <param name="configuration">A configuration describing all aspects of the <see cref="StarterShip"/>.</param>
	/// <returns>An entry for the new <see cref="StarterShip"/>.</returns>
	IShipEntry RegisterShip(string name, ShipConfiguration configuration);
	
	/// <summary>
	/// Register a new ship part type (<see cref="PType"/>).
	/// </summary>
	/// <param name="name">The local (mod-level) name for the part type (<see cref="PType"/>). This has to be unique across all ship part types in the mod.</param>
	/// <param name="configuration">A configuration describing all aspects of the part type (<see cref="PType"/>).</param>
	/// <returns>An entry for the new part type (<see cref="PType"/>).</returns>
	IPartTypeEntry RegisterPartType(string name, PartTypeConfiguration configuration);
	
	/// <summary>
	/// Register a new ship part with a specific skin.
	/// </summary>
	/// <param name="name">The local (mod-level) name for the part. This has to be unique across all ship parts in the mod.</param>
	/// <param name="configuration">A configuration describing all aspects of the part.</param>
	/// <returns>An entry for the new part.</returns>
	IPartEntry RegisterPart(string name, PartConfiguration configuration);

	/// <summary>
	/// Register a new ship part damage modifier (<see cref="PDamMod"/>).
	/// </summary>
	/// <param name="name">The local (mod-level) name for the part damage modifier (<see cref="PDamMod"/> ). This has to be unique across all ship part damage modifiers in the mod.</param>
	/// <param name="configuration">A configuration describing all aspects of the part damage modifier.</param>
	/// <returns>An entry for the new part damage modifier.</returns>
	IPartDamageModifierEntry RegisterPartDamageModifier(string name, PartDamageModifierConfiguration configuration);

	/// <summary>
	/// Register a new ship part stun modifier (<see cref="PStunMod"/>).
	/// </summary>
	/// <param name="name">The local (mod-level) name for the part stun modifier (<see cref="PStunMod"/> ). This has to be unique across all ship part stun modifiers in the mod.</param>
	/// <param name="configuration">A configuration describing all aspects of the part stun modifier.</param>
	/// <returns>An entry for the new part stun modifier.</returns>
	IPartStunModifierEntry RegisterPartStunModifier(string name, PartStunModifierConfiguration configuration);
}
