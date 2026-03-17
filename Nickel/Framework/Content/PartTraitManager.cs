using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nickel;

internal sealed class PartTraitManager
{
	private readonly AfterDbInitManager<PartDamageModifierEntry> PartDamangeModifierManager;
	private readonly AfterDbInitManager<PartStunModifierEntry> PartStunModifierManager;
	private readonly EnumCasePool EnumCasePool;
	private readonly Dictionary<string, PartDamageModifierEntry> UniqueNameToPartDamageModifierEntry = [];
	private readonly Dictionary<PDamMod, PartDamageModifierEntry> PartDamageModifierToPartDamageModifierEntry = [];

	private readonly Dictionary<string, PartStunModifierEntry> UniqueNameToPartStunModifierEntry = [];
	private readonly Dictionary<PStunMod, PartStunModifierEntry> PartStunModifierToPartStunModifierEntry = [];
	private readonly IModManifest VanillaModManifest;
	private readonly Dictionary<string, PDamMod> VanillaPartDamageModifierTypes;
	private readonly Dictionary<string, PStunMod> VanillaPartStunModifierTypes;

	public PartTraitManager(EnumCasePool enumCasePool, Func<ModLoadPhaseState> currentModLoadPhaseProvider, IModManifest vanillaModManifest)
    {
        this.PartDamangeModifierManager = new(currentModLoadPhaseProvider, this.Inject);
		this.PartStunModifierManager = new(currentModLoadPhaseProvider, this.Inject);
		this.EnumCasePool = enumCasePool;
		this.VanillaModManifest = vanillaModManifest;

		this.VanillaPartDamageModifierTypes = Enum.GetValues<PDamMod>().ToDictionary(v => Enum.GetName(v)!, v => v);
		this.VanillaPartStunModifierTypes = Enum.GetValues<PStunMod>().ToDictionary(v => Enum.GetName(v)!, v => v);

		ShipPatches.OnModifyDamageForDamageModifier += this.PartDamageModifierModifyDamage;
		ShipPatches.OnPartsGetHit += this.PartStunModifierPartsGetHit;

		ShipPatches.OnRenderDamageModifierTooltip += this.PartDamageModifierRenderTooltip;
		ShipPatches.OnRenderDamageModifierIcon += this.PartDamageModifierRenderIcon;

		ShipPatches.OnRenderStunModifierIcon += this.PartStunModifierRenderIcon;
		ShipPatches.OnRenderStunModifierTooltip += this.PartStunModifierRenderTooltip;
    }

	private void PartDamageModifierModifyDamage(object? sender, ref ShipPatches.ModifyDamageForDamageModifierEventArgs e)
    {
		ref var dmodEntry = ref CollectionsMarshal.GetValueRefOrNullRef(this.PartDamageModifierToPartDamageModifierEntry, e.PartDamageModifier);

		if (Unsafe.IsNullRef(ref dmodEntry))
        {
            return;
        }

		if (dmodEntry.Configuration.ModifyIncomingDamage is not { } mod)
        {
			return;
        }

		e.IncomingDamage = mod(
			new PartDamageModifierConfiguration.ModifyIncomingDamageArgs()
            {
                State = e.State,
				Combat = e.Combat,
				Ship = e.Ship,
				Part = e.Part,
				PartDamageModifier = e.PartDamageModifier,
				IncomingDamage = e.IncomingDamage,
				Piercing = e.Piercing
            }
		);
    }

	private void PartDamageModifierRenderTooltip(object? sender, ref ShipPatches.RenderDamageModifierTooltipEventArgs e)
    {
		ref var damEntry = ref CollectionsMarshal.GetValueRefOrNullRef(this.PartDamageModifierToPartDamageModifierEntry, e.PartDamageModifier);

		if (Unsafe.IsNullRef(ref damEntry))
        {
            return;
        }

		if (damEntry.ModOwner == this.VanillaModManifest)
        {
            return;
        }

		e.G.tooltips.Add(e.TooltipPosition, new TTGlossary($"parttrait.dam{e.PartDamageModifier}"));
    }

	private void PartStunModifierPartsGetHit(object? sender, ref ShipPatches.PartsGetHitEventArgs e)
    {
		ref var stunEntry = ref CollectionsMarshal.GetValueRefOrNullRef(this.PartStunModifierToPartStunModifierEntry, e.PartStunModifier);

		if (Unsafe.IsNullRef(ref stunEntry))
        {
            return;
        }

		if (stunEntry.Configuration.PartsGetHit is not { } mod)
        {
			return;
        }

		mod(new PartStunModifierConfiguration.PartsGetHitArgs()
        {
            State = e.State,
			Combat = e.Combat,
			Ship = e.Ship,
			Part = e.Part,
			PartStunMod = e.PartStunModifier
        });
    }

	private void PartDamageModifierRenderIcon(object? sender, ref ShipPatches.RenderDamageModifierIconEventArgs e)
    {
		ref var damEntry = ref CollectionsMarshal.GetValueRefOrNullRef(this.PartDamageModifierToPartDamageModifierEntry, e.PartDamageModifier);

		if (Unsafe.IsNullRef(ref damEntry))
        {
            return;
        }

		Draw.Sprite(damEntry.Configuration.Sprite, e.IconPosition.x + 1.0, e.IconPosition.y, color: e.IconOpacity);
    }

	private void PartStunModifierRenderTooltip(object? sender, ref ShipPatches.RenderStunModifierTooltipEventArgs e)
    {
		ref var damEntry = ref CollectionsMarshal.GetValueRefOrNullRef(this.PartStunModifierToPartStunModifierEntry, e.PartStunModifier);

		if (Unsafe.IsNullRef(ref damEntry))
        {
            return;
        }

		if (damEntry.ModOwner == this.VanillaModManifest)
        {
            return;
        }

		e.G.tooltips.Add(e.TooltipPosition, new TTGlossary($"parttrait.stu{e.PartStunModifier}"));
    }

	private void PartStunModifierRenderIcon(object? sender, ref ShipPatches.RenderStunModifierIconEventArgs e)
    {
		ref var stunEntry = ref CollectionsMarshal.GetValueRefOrNullRef(this.PartStunModifierToPartStunModifierEntry, e.PartStunModifier);

		if (Unsafe.IsNullRef(ref stunEntry))
        {
            return;
        }

		Draw.Sprite(stunEntry.Configuration.Sprite, e.IconPosition.x + 9.0, e.IconPosition.y, color: e.IconOpacity);
    }

	public IPartDamageModifierEntry RegisterPartDamageModifier(IModManifest owner, string name, PartDamageModifierConfiguration configuration)
    {
        var uniqueName = $"{owner.UniqueName}::{name}";
		if (this.UniqueNameToPartDamageModifierEntry.ContainsKey(name))
			throw new ArgumentException($"A part damage modifier with the unique name `{uniqueName}` is already registered", nameof(name));
		
		PartDamageModifierEntry entry = new(owner, name, this.EnumCasePool.ObtainEnumCase<PDamMod>(), configuration);
		this.UniqueNameToPartDamageModifierEntry[entry.UniqueName] = entry;
		this.PartDamageModifierToPartDamageModifierEntry[entry.PartDamageModifier] = entry;

		this.PartDamangeModifierManager.QueueOrInject(entry);
		return entry;
    }

	public IPartStunModifierEntry RegisterPartStunModifier(IModManifest owner, string name, PartStunModifierConfiguration configuration)
    {
        var uniqueName = $"{owner.UniqueName}::{name}";
		if (this.UniqueNameToPartStunModifierEntry.ContainsKey(name))
			throw new ArgumentException($"A part stun modifier with the unique name `{uniqueName}` is already registered", nameof(name));
		
		PartStunModifierEntry entry = new(owner, name, this.EnumCasePool.ObtainEnumCase<PStunMod>(), configuration);
		this.UniqueNameToPartStunModifierEntry[entry.UniqueName] = entry;
		this.PartStunModifierToPartStunModifierEntry[entry.PartStunModifier] = entry;

		this.PartStunModifierManager.QueueOrInject(entry);
		return entry;
    }

	public IPartDamageModifierEntry? LookupPartDamageModifierByUniqueName(string uniqueName)
    {
        if (this.UniqueNameToPartDamageModifierEntry.TryGetValue(uniqueName, out var entry))
        {
			return entry;
        }

		if (this.VanillaPartDamageModifierTypes.TryGetValue(uniqueName, out var pDamMod))
		{
			entry = new PartDamageModifierEntry(this.VanillaModManifest, uniqueName, pDamMod, new()
			{
				Name = _ => Loc.T($"parttrait.{pDamMod}.name"),
				Description = _ => Loc.T($"parttrait.{pDamMod}.desc")
			});

			this.UniqueNameToPartDamageModifierEntry[uniqueName] = entry;
			this.PartDamageModifierToPartDamageModifierEntry[entry.PartDamageModifier] = entry;
		}

		return entry;
    }

	public IPartStunModifierEntry? LookupPartStunModifierByUniqueName(string uniqueName)
    {
        if (this.UniqueNameToPartStunModifierEntry.TryGetValue(uniqueName, out var entry))
        {
			return entry;
        }

		if (this.VanillaPartStunModifierTypes.TryGetValue(uniqueName, out var pStunMod))
		{
			entry = new PartStunModifierEntry(this.VanillaModManifest, uniqueName, pStunMod, new()
			{
				Name = _ => Loc.T($"parttrait.{pStunMod}.name"),
				Description = _ => Loc.T($"parttrait.{pStunMod}.desc")
			});

			this.UniqueNameToPartStunModifierEntry[uniqueName] = entry;
			this.PartStunModifierToPartStunModifierEntry[entry.PartStunModifier] = entry;
		}

		return entry;
    }

	internal void InjectQueuedEntries()
	{
		this.PartDamangeModifierManager.InjectQueuedEntries();
		this.PartStunModifierManager.InjectQueuedEntries();
	}

	internal void InjectLocalizations(string locale, Dictionary<string, string> localizations)
	{
		foreach (var entry in this.UniqueNameToPartDamageModifierEntry.Values)
			this.InjectLocalization(locale, localizations, entry);

		foreach (var entry in this.UniqueNameToPartStunModifierEntry.Values)
			this.InjectLocalization(locale, localizations, entry);
	}

	private void Inject(PartDamageModifierEntry entry)
	{
		if (entry.Configuration.Sprite is { } icon)
        {
			DB.icons[$"dam{entry.PartDamageModifier}"] = icon;
        }
		this.InjectLocalization(DB.currentLocale.locale, DB.currentLocale.strings, entry);
	}

	private void InjectLocalization(string locale, Dictionary<string, string> localizations, PartDamageModifierEntry entry)
	{
		if (entry.ModOwner == this.VanillaModManifest)
			return;
		
		var key = entry.PartDamageModifier.ToString();
		if (entry.Configuration.Name.Localize(locale) is { } name)
			localizations[$"parttrait.dam{key}.name"] = name;
		if (entry.Configuration.Description.Localize(locale) is { } description)
			localizations[$"parttrait.dam{key}.desc"] = description;
	}

	private void Inject(PartStunModifierEntry entry)
	{
		if (entry.Configuration.Sprite is { } icon)
        {
			DB.icons[$"stu{entry.PartStunModifier}"] = icon;
        }
		this.InjectLocalization(DB.currentLocale.locale, DB.currentLocale.strings, entry);
	}

	private void InjectLocalization(string locale, Dictionary<string, string> localizations, PartStunModifierEntry entry)
	{
		if (entry.ModOwner == this.VanillaModManifest)
			return;
		
		var key = entry.PartStunModifier.ToString();
		if (entry.Configuration.Name.Localize(locale) is { } name)
			localizations[$"parttrait.stu{key}.name"] = name;
		if (entry.Configuration.Description.Localize(locale) is { } description)
			localizations[$"parttrait.stu{key}.desc"] = description;
	}

	private sealed class PartStunModifierEntry(IModManifest modOwner, string uniqueName, PStunMod pStunMod, PartStunModifierConfiguration configuration)
		: IPartStunModifierEntry
	{
		public PStunMod PartStunModifier { get; init; } = pStunMod;
		public PartStunModifierConfiguration Configuration { get; init; } = configuration;
		public IModManifest ModOwner { get; init; } = modOwner;
		public string UniqueName { get; init; } = uniqueName;

		public override string ToString() => this.UniqueName;
		public override int GetHashCode() => this.UniqueName.GetHashCode();
	}

	private sealed class PartDamageModifierEntry(IModManifest modOwner, string uniqueName, PDamMod pDamMod, PartDamageModifierConfiguration configuration)
		: IPartDamageModifierEntry
	{
		public PDamMod PartDamageModifier { get; init; } = pDamMod;
		public PartDamageModifierConfiguration Configuration { get; init; } = configuration;
		public IModManifest ModOwner { get; init; } = modOwner;
		public string UniqueName { get; init; } = uniqueName;

		public override string ToString() => this.UniqueName;
		public override int GetHashCode() => this.UniqueName.GetHashCode();
	}
}
