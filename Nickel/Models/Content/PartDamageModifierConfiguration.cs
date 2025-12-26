using System;

namespace Nickel;

/// <summary>
/// Describes all aspects of a part damage modifier (<see cref="PDamMod"/>).
/// </summary>
public readonly struct PartDamageModifierConfiguration
{
	/// <summary>A localization provider for the name of the <see cref="PDamMod"/>.</summary>
	public SingleLocalizationProvider? Name { get; init; }

	/// <summary>A localization provider for the description of the <see cref="PDamMod"/>.</summary>
	public SingleLocalizationProvider? Description { get; init; }

	/// <summary>A sprite for the icon of the <see cref="PDamMod"/>.</summary>
	public Spr? Sprite { get; init; }

	/// <summary>A function controlling what damage output would a ship takes</summary>
	public Func<ModifyIncomingDamageArgs, int>? ModifyIncomingDamage { get; init; }

	/// <seealso cref="ModifyIncomingDamage"/>
	public struct ModifyIncomingDamageArgs
    {
		/// <summary>The current state of the game.</summary>
        public required State State { get; init; }

		/// <summary>The current combat.</summary>
        public required Combat Combat { get; init; }

		/// <summary>The ship that has a part with this part damage modifier.</summary>
        public required Ship Ship { get; init; }

		/// <summary>The part that got hit.</summary>
        public required Part Part { get; init; }

		/// <summary>The part damage modifier.</summary>
		public required PDamMod PartDamageModifier { get; init; }

		/// <summary>The current incoming damage.</summary>
		public required int IncomingDamage { get; init; }

		/// <summary>If the attack has piercing</summary>
		public required bool Piercing { get; init; }
    }
}
