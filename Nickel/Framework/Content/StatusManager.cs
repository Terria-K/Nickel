using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Nickel;

internal sealed class StatusManager
{
	private readonly AfterDbInitManager<Entry> Manager;
	private readonly EnumCasePool EnumCasePool;
	private readonly IModManifest VanillaModManifest;
	private readonly Dictionary<Status, Entry> StatusToEntry = [];
	private readonly Dictionary<string, Entry> UniqueNameToEntry = [];
	private readonly Dictionary<string, Status> ReservedNameToStatus = [];
	private readonly Dictionary<Status, string> ReservedStatusToName = [];

	public StatusManager(Func<ModLoadPhaseState> currentModLoadPhaseProvider, EnumCasePool enumCasePool, IModManifest vanillaModManifest)
	{
		this.Manager = new(currentModLoadPhaseProvider, this.Inject);
		this.EnumCasePool = enumCasePool;
		this.VanillaModManifest = vanillaModManifest;
		
		ShipPatches.OnShouldStatusFlash += this.OnShouldStatusFlash;
		TTGlossaryPatches.OnTryGetIcon += this.OnTryGetIcon;
	}

	internal bool IsStateInvalid(State state)
	{
		var @checked = new HashSet<object>();
		return ContainsInvalidEntries(state);

		bool ContainsInvalidEntries(object? o)
		{
			if (o is null)
				return false;
			if (o is Status status && this.LookupByStatus(status) is null)
				return true;
			if (o.GetType().IsPrimitive)
				return false;
			if (!@checked.Add(o))
				return false;

			return o.GetType()
				.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Any(field => ContainsInvalidEntries(field.GetValue(o)));
		}
	}

	private void OnShouldStatusFlash(object? _, ref ShipPatches.ShouldStatusFlashEventArgs e)
	{
		if (this.LookupByStatus(e.Status) is not { } entry)
			return;
		if (entry.Configuration.ShouldFlash is not { } shouldFlash)
			return;
		e.ShouldFlash = shouldFlash(e.State, e.Combat, e.Ship, e.Status);
	}

	private void OnTryGetIcon(object? _, ref TTGlossaryPatches.TryGetIconEventArgs e)
	{
		var keySplit = e.Glossary.key.Split(".");
		if (keySplit.Length < 2)
			return;
		if (keySplit[0] != "status" || !int.TryParse(keySplit[1], out var statusId))
			return;
		if (!this.StatusToEntry.TryGetValue((Status)statusId, out var entry))
			return;
		e.Sprite = entry.Configuration.Definition.icon;
	}

	internal void ModifyJsonContract(Type type, JsonContract contract)
	{
		if (type == typeof(Status) || type == typeof(Status?))
		{
			contract.Converter = new ModStringEnumConverter<Status>(
				modStringToEnumProvider: s =>
				{
					if (this.UniqueNameToEntry.TryGetValue(s, out var entry))
						return entry.Status;
					if (this.ReservedNameToStatus.TryGetValue(s, out var @enum))
						return @enum;

					@enum = this.EnumCasePool.ObtainEnumCase<Status>();
					this.ReservedNameToStatus[s] = @enum;
					this.ReservedStatusToName[@enum] = s;
					return @enum;
				},
				modEnumToStringProvider: v =>
				{
					if (this.StatusToEntry.TryGetValue(v, out var entry))
						return entry.UniqueName;
					if (this.ReservedStatusToName.TryGetValue(v, out var name))
						return name;

					name = v.ToString();
					this.ReservedNameToStatus[name] = v;
					this.ReservedStatusToName[v] = name;
					return name;
				}
			);
		}
		else if (type.IsConstructedGenericType && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>) || type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) && type.GetGenericArguments()[0] == typeof(Status))
		{
			contract.Converter = new CustomDictionaryConverter<Status>();
		}
	}

	internal void InjectQueuedEntries()
		=> this.Manager.InjectQueuedEntries();

	internal void InjectLocalizations(string locale, Dictionary<string, string> localizations)
	{
		foreach (var entry in this.UniqueNameToEntry.Values)
			this.InjectLocalization(locale, localizations, entry);
	}

	public IStatusEntry RegisterStatus(IModManifest owner, string name, StatusConfiguration configuration)
	{
		var uniqueName = $"{owner.UniqueName}::{name}";
		if (this.UniqueNameToEntry.ContainsKey(uniqueName))
			throw new ArgumentException($"A status with the unique name `{uniqueName}` is already registered", nameof(name));
		var status = this.ReservedNameToStatus.TryGetValue(uniqueName, out var reservedStatus) ? reservedStatus : this.EnumCasePool.ObtainEnumCase<Status>();
		this.ReservedNameToStatus.Remove(uniqueName);
		this.ReservedStatusToName.Remove(status);

		var entry = new Entry(owner, $"{owner.UniqueName}::{name}", status, configuration, this.Amend);
		this.StatusToEntry[entry.Status] = entry;
		this.UniqueNameToEntry[entry.UniqueName] = entry;
		this.Manager.QueueOrInject(entry);
		return entry;
	}

	public IStatusEntry? LookupByStatus(Status status)
	{
		if (this.StatusToEntry.TryGetValue(status, out var entry))
			return entry;
		if (!Enum.IsDefined(status))
			return null;

		var vanillaEntry = this.CreateVanillaEntry(status);
		this.StatusToEntry[status] = vanillaEntry;
		this.UniqueNameToEntry[vanillaEntry.UniqueName] = vanillaEntry;
		return vanillaEntry;
	}

	public IStatusEntry? LookupByUniqueName(string uniqueName)
	{
		if (this.UniqueNameToEntry.TryGetValue(uniqueName, out var entry))
			return entry;
		if (!Enum.TryParse<Status>(uniqueName, out var status))
			return null;
		
		var vanillaEntry = this.CreateVanillaEntry(status);
		this.StatusToEntry[status] = vanillaEntry;
		this.UniqueNameToEntry[vanillaEntry.UniqueName] = vanillaEntry;
		return vanillaEntry;
	}

	private Entry CreateVanillaEntry(Status status)
		=> new(
			modOwner: this.VanillaModManifest,
			uniqueName: Enum.GetName(status)!,
			status: status,
			configuration: new()
			{
				Definition = DB.statuses[status],
				Name = _ => Loc.T($"status.{status}.name"),
				Description = _ => Loc.T($"status.{status}.desc"),
			},
			amendDelegate: (_, _) => throw new InvalidOperationException("Vanilla entries cannot be amended")
		);

	private void Inject(Entry entry)
	{
		DB.statuses[entry.Status] = entry.Configuration.Definition;
		this.InjectLocalization(DB.currentLocale.locale, DB.currentLocale.strings, entry);
	}
	
	private void Amend(Entry entry, StatusConfiguration.Amends amends)
	{
		if (!this.UniqueNameToEntry.ContainsKey(entry.UniqueName))
			throw new ArgumentException($"A status with the unique name `{entry.UniqueName}` is not registered");

		if (amends.ShouldFlash is { } shouldFlash)
			entry.Configuration = entry.Configuration with { ShouldFlash = shouldFlash.Value };
	}

	private void InjectLocalization(string locale, Dictionary<string, string> localizations, Entry entry)
	{
		if (entry.ModOwner == this.VanillaModManifest)
			return;
		
		var key = entry.Status.Key();
		if (entry.Configuration.Name.Localize(locale) is { } name)
			localizations[$"status.{key}.name"] = name;
		if (entry.Configuration.Description.Localize(locale) is { } description)
			localizations[$"status.{key}.desc"] = description;
	}

	private sealed class Entry(
		IModManifest modOwner,
		string uniqueName,
		Status status,
		StatusConfiguration configuration,
		Action<Entry, StatusConfiguration.Amends> amendDelegate
	) : IStatusEntry
	{
		public IModManifest ModOwner { get; } = modOwner;
		public string UniqueName { get; } = uniqueName;
		public Status Status { get; } = status;
		public StatusConfiguration Configuration { get; internal set; } = configuration;

		public override string ToString()
			=> this.UniqueName;

		public override int GetHashCode()
			=> this.UniqueName.GetHashCode();
		
		public void Amend(StatusConfiguration.Amends amends)
			=> amendDelegate(this, amends);
	}
}
