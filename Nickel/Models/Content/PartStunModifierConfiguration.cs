using System;

namespace Nickel;

/// <summary>
/// Describes all aspects of a part stun modifier (<see cref="PStunMod"/>).
/// </summary>
public readonly struct PartStunModifierConfiguration
{
	/// <summary>A localization provider for the name of the <see cref="PStunMod"/>.</summary>
	public SingleLocalizationProvider? Name { get; init; }

	/// <summary>A localization provider for the description of the <see cref="PStunMod"/>.</summary>
	public SingleLocalizationProvider? Description { get; init; }

	/// <summary>A sprite for the icon of the <see cref="PStunMod"/>.</summary>
	public Spr? Sprite { get; init; }

	/// <summary>A function controlling what will the parts do when it get hit</summary>
	public Action<PartsGetHitArgs>? PartsGetHit { get; init; }

	/// <seealso cref="PartsGetHit"/>
	public struct PartsGetHitArgs
    {
		/// <summary>The current state of the game.</summary>
        public required State State { get; init; }

		/// <summary>The current combat.</summary>
        public required Combat Combat { get; init; }

		/// <summary>The ship that has a part with this part stun modifier.</summary>
        public required Ship Ship { get; init; }

		/// <summary>The part that got hit.</summary>
        public required Part Part { get; init; }

		/// <summary>The part stun modifier.</summary>
		public required PStunMod PartStunMod { get; init; }
    }
}
