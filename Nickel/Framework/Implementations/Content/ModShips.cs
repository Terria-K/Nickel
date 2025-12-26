using System;
using System.Collections.Generic;

namespace Nickel;

internal sealed class ModShips(
	IModManifest modManifest,
	Func<ShipManager> shipManagerProvider,
	Func<PartManager> partManagerProvider,
	Func<PartTraitManager> partTraitManagerProvider
) : IModShips
{
	public IReadOnlyDictionary<string, IShipEntry> RegisteredShips
		=> this.RegisteredShipStorage;
	
	public IReadOnlyDictionary<string, IPartTypeEntry> RegisteredPartTypes
		=> this.RegisteredPartTypeStorage;
	
	public IReadOnlyDictionary<string, IPartEntry> RegisteredParts
		=> this.RegisteredPartStorage;

	public IReadOnlyDictionary<string, IPartDamageModifierEntry> RegisteredPartDamageModifiers => 
		this.RegisteredPartDamageModifierStorage;

	public IReadOnlyDictionary<string, IPartStunModifierEntry> RegisteredPartStunModifiers => 
		this.RegisteredPartStunModifierStorage;

	private readonly Dictionary<string, IShipEntry> RegisteredShipStorage = [];
	private readonly Dictionary<string, IPartTypeEntry> RegisteredPartTypeStorage = [];
	private readonly Dictionary<string, IPartEntry> RegisteredPartStorage = [];
	private readonly Dictionary<string, IPartDamageModifierEntry> RegisteredPartDamageModifierStorage = [];
	private readonly Dictionary<string, IPartStunModifierEntry> RegisteredPartStunModifierStorage = [];
	
	public IShipEntry? LookupByUniqueName(string uniqueName)
		=> shipManagerProvider().LookupByUniqueName(uniqueName);

	public IPartTypeEntry? LookupPartTypeByUniqueName(string uniqueName)
		=> partManagerProvider().LookupPartTypeByUniqueName(uniqueName);

	public IPartDamageModifierEntry? LookupPartDamageModifierByUniqueName(string uniqueName)
		=> partTraitManagerProvider().LookupPartDamageModifierByUniqueName(uniqueName);

	public IPartStunModifierEntry? LookupPartStunModifierByUniqueName(string uniqueName)
		=> partTraitManagerProvider().LookupPartStunModifierByUniqueName(uniqueName);

	public IShipEntry RegisterShip(string name, ShipConfiguration configuration)
	{
		var entry = shipManagerProvider().RegisterShip(modManifest, name, configuration);
		this.RegisteredShipStorage[name] = entry;
		return entry;
	}

	public IPartTypeEntry RegisterPartType(string name, PartTypeConfiguration configuration)
	{
		var entry = partManagerProvider().RegisterPartType(modManifest, name, configuration);
		this.RegisteredPartTypeStorage[name] = entry;
		return entry;
	}

	public IPartEntry RegisterPart(string name, PartConfiguration configuration)
	{
		var entry = partManagerProvider().RegisterPart(modManifest, name, configuration);
		this.RegisteredPartStorage[name] = entry;
		return entry;
	}
    
	public IPartDamageModifierEntry RegisterPartDamageModifier(string name, PartDamageModifierConfiguration configuration)
    {
        var entry = partTraitManagerProvider().RegisterPartDamageModifier(modManifest, name, configuration);
		this.RegisteredPartDamageModifierStorage[name] = entry;
		return entry;
    }

	public IPartStunModifierEntry RegisterPartStunModifier(string name, PartStunModifierConfiguration configuration)
    {
        var entry = partTraitManagerProvider().RegisterPartStunModifier(modManifest, name, configuration);
		this.RegisteredPartStunModifierStorage[name] = entry;
		return entry;
    }
}
