using System;
using System.Collections.Generic;
using System.Linq;

namespace Nanoray.PluginManager;

/// <summary>
/// The default <see cref="IPluginDependencyResolver{TPluginManifest,TVersion}"/> implementation.
/// </summary>
/// <typeparam name="TPluginManifest">The type of the plugin manifest.</typeparam>
/// <typeparam name="TVersion">The type representing a plugin version.</typeparam>
public sealed class PluginDependencyResolver<TPluginManifest, TVersion> : IPluginDependencyResolver<TPluginManifest, TVersion>
	where TPluginManifest : notnull
	where TVersion : struct, IEquatable<TVersion>, IComparable<TVersion>
{
	private readonly Func<TPluginManifest, RequiredManifestData> RequiredManifestDataProvider;

	/// <summary>
	/// Creates a new <see cref="PluginDependencyResolver{TPluginManifest,TVersion}"/>.
	/// </summary>
	/// <param name="requiredManifestDataProvider">A function which maps plugin manifests to the data required for this resolver.</param>
	public PluginDependencyResolver(Func<TPluginManifest, RequiredManifestData> requiredManifestDataProvider)
	{
		this.RequiredManifestDataProvider = requiredManifestDataProvider;
	}

	/// <inheritdoc/>
	public PluginDependencyResolveResult<TPluginManifest, TVersion> ResolveDependencies(IEnumerable<TPluginManifest> toResolve, IReadOnlySet<TPluginManifest>? resolved = null)
	{
		var toResolveLeft = toResolve.ToList();
		var allResolved = resolved?.ToList() ?? [];

		var manifestEntries = toResolveLeft.Concat(resolved ?? Enumerable.Empty<TPluginManifest>())
			.ToDictionary(m => m, m => new ManifestEntry { Manifest = m, Data = this.RequiredManifestDataProvider(m) });

		#region happy path
		List<IReadOnlySet<TPluginManifest>> loadSteps = [];
		Dictionary<TPluginManifest, PluginDependencyUnresolvableResult<TPluginManifest, TVersion>> unresolvable = [];

		DependencyMatchStatus GetDependencyMatchStatus(TPluginManifest manifest, PluginDependency<TVersion> dependency)
		{
			if (!manifestEntries.TryGetValue(manifest, out var entry))
				return DependencyMatchStatus.NotMatching;
			if (entry.Data.UniqueName != dependency.UniqueName)
				return DependencyMatchStatus.NotMatching;
			if (dependency.Version is null)
				return DependencyMatchStatus.OK;
			if (dependency.Version.Value.CompareTo(entry.Data.Version) <= 0)
				return DependencyMatchStatus.OK;
			return DependencyMatchStatus.Misversioned;
		}

		DependencyResolveStatus GetDependencyResolveStatus(IEnumerable<TPluginManifest> enumerable, PluginDependency<TVersion> dependency)
		{
			foreach (var manifestEntry in enumerable)
			{
				switch (GetDependencyMatchStatus(manifestEntry, dependency))
				{
					case DependencyMatchStatus.OK:
						return DependencyResolveStatus.OK;
					case DependencyMatchStatus.Misversioned:
						return DependencyResolveStatus.Misversioned;
					case DependencyMatchStatus.NotMatching:
					default:
						continue;
				}
			}
			return DependencyResolveStatus.NotResolved;
		}

		bool CanManifestBeResolved(TPluginManifest manifest, bool requireOptional)
		{
			if (!manifestEntries.TryGetValue(manifest, out var entry))
				return false;
			foreach (var dependency in entry.Data.Dependencies)
			{
				var dependencyResolveStatus = GetDependencyResolveStatus(allResolved, dependency);
				if (dependencyResolveStatus == DependencyResolveStatus.OK)
					continue;
				if (!requireOptional && !dependency.IsRequired && dependencyResolveStatus == DependencyResolveStatus.NotResolved)
					continue;
				return false;
			}
			return true;
		}

		while (toResolveLeft.Count > 0)
		{
			var oldCount = toResolveLeft.Count;
			Loop(true);
			Loop(false);
			if (toResolveLeft.Count == oldCount)
				break;

			void Loop(bool requireOptional)
			{
				while (toResolveLeft.Count > 0)
				{
					var loadStep = toResolveLeft.Where(m => CanManifestBeResolved(m, requireOptional)).ToHashSet();
					if (loadStep.Count == 0)
						break;

					loadSteps.Add(loadStep);
					allResolved.AddRange(loadStep);
					toResolveLeft.RemoveAll(loadStep.Contains);
				}
			}
		}

		if (toResolveLeft.Count == 0)
			return new PluginDependencyResolveResult<TPluginManifest, TVersion> { LoadSteps = loadSteps, Unresolvable = unresolvable };
		#endregion

		#region manifests unresolvable due to missing dependencies
		HashSet<PluginDependency<TVersion>> GetMissingDependencies(TPluginManifest manifest)
		{
			if (!manifestEntries.TryGetValue(manifest, out var entry))
				return [];
			return entry.Data.Dependencies
				.Where(d => GetDependencyResolveStatus(allResolved, d) != DependencyResolveStatus.OK && GetDependencyResolveStatus(toResolveLeft, d) != DependencyResolveStatus.OK)
				.ToHashSet();
		}

		while (toResolveLeft.Count > 0)
		{
			var missingDependencyManifests = toResolveLeft
				.Select(m => (Manifest: m, Dependencies: GetMissingDependencies(m)))
				.Where(e => e.Dependencies.Count > 0)
				.ToDictionary(e => e.Manifest, e => e.Dependencies);
			if (missingDependencyManifests.Count <= 0)
				break;

			foreach (var kvp in missingDependencyManifests)
			{
				var misverionedDependencies = kvp.Value
					.Where(d => GetDependencyResolveStatus(allResolved, d) == DependencyResolveStatus.Misversioned || GetDependencyResolveStatus(toResolveLeft, d) == DependencyResolveStatus.Misversioned)
					.ToHashSet();
				
				unresolvable[kvp.Key] = new PluginDependencyUnresolvableResult<TPluginManifest, TVersion>.MissingDependencies
				{
					Missing = kvp.Value.Where(d => !misverionedDependencies.Contains(d)).ToHashSet(),
					Misversioned = misverionedDependencies
				};
				toResolveLeft.Remove(kvp.Key);
			}
		}

		if (toResolveLeft.Count == 0)
			return new PluginDependencyResolveResult<TPluginManifest, TVersion> { LoadSteps = loadSteps, Unresolvable = unresolvable };
		#endregion

		#region manifests unresolvable due to dependency cycles
		PluginDependencyChain<TPluginManifest>? FindDependencyCycle(TPluginManifest firstManifest, TPluginManifest? currentManifest = default, List<TPluginManifest>? currentCycle = null)
		{
			currentManifest ??= firstManifest;
			currentCycle ??= [];

			if (Equals(currentManifest, firstManifest) && currentCycle.Count > 0)
				return new() { Values = currentCycle };
			if (!manifestEntries.TryGetValue(currentManifest, out var entry))
				return null;
			foreach (var dependency in entry.Data.Dependencies)
			{
				var matchingManifest = toResolveLeft.FirstOrDefault(m => GetDependencyMatchStatus(m, dependency) == DependencyMatchStatus.OK);
				if (matchingManifest is null)
					continue;
				var newCycle = currentCycle.Append(matchingManifest).ToList();
				var fullCycle = FindDependencyCycle(firstManifest, matchingManifest, newCycle);
				if (fullCycle is not null)
					return fullCycle;
			}
			return null;
		}

		while (toResolveLeft.Count > 0)
		{
			List<TPluginManifest> toRemove = [];
			foreach (var manifest in toResolveLeft)
			{
				if (toRemove.Contains(manifest))
					continue;
				if (FindDependencyCycle(manifest) is not { } cycle)
					continue;

				foreach (var cyclePart in cycle.Values)
					unresolvable[cyclePart] = new PluginDependencyUnresolvableResult<TPluginManifest, TVersion>.DependencyCycle { Cycle = cycle };
				toRemove.AddRange(cycle.Values);
			}

			if (toRemove.Count <= 0)
				break;
			_ = toResolveLeft.RemoveAll(toRemove.Contains);
		}
		#endregion

		foreach (var manifest in toResolveLeft)
			unresolvable[manifest] = new PluginDependencyUnresolvableResult<TPluginManifest, TVersion>.UnknownReason();
		return new PluginDependencyResolveResult<TPluginManifest, TVersion> { LoadSteps = loadSteps, Unresolvable = unresolvable };
	}

	/// <summary>
	/// Describes data required for resolving plugin load order via <see cref="PluginDependencyResolver{TPluginManifest,TVersion}"/>.
	/// </summary>
	public readonly struct RequiredManifestData
	{
		/// <summary>The unique name of the plugin.</summary>
		public string UniqueName { get; init; }
		
		/// <summary>The version of the plugin.</summary>
		public TVersion Version { get; init; }
		
		/// <summary>The plugin's dependencies.</summary>
		public IReadOnlySet<PluginDependency<TVersion>> Dependencies { get; init; }
	}

	private readonly struct ManifestEntry
	{
		public TPluginManifest Manifest { get; init; }
		public RequiredManifestData Data { get; init; }
	}

	private enum DependencyMatchStatus
	{
		NotMatching, Misversioned, OK
	}

	private enum DependencyResolveStatus
	{
		NotResolved, Misversioned, OK
	}
}
