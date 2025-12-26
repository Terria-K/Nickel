using HarmonyLib;
using Microsoft.Extensions.Logging;
using Nanoray.Shrike;
using Nanoray.Shrike.Harmony;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;

namespace Nickel;

internal static class ShipPatches
{
	internal static RefEventHandler<ShouldStatusFlashEventArgs>? OnShouldStatusFlash;
	internal static RefEventHandler<RenderDamageModifierTooltipEventArgs>? OnRenderDamageModifierTooltip;
	internal static RefEventHandler<RenderDamageModifierIconEventArgs>? OnRenderDamageModifierIcon;
	internal static RefEventHandler<RenderStunModifierTooltipEventArgs>? OnRenderStunModifierTooltip;
	internal static RefEventHandler<RenderStunModifierIconEventArgs>? OnRenderStunModifierIcon;
	internal static RefEventHandler<ModifyDamageForDamageModifierEventArgs>? OnModifyDamageForDamageModifier;
	internal static RefEventHandler<PartsGetHitEventArgs>? OnPartsGetHit;

	internal static void Apply(Harmony harmony)
	{
		harmony.Patch(
				original: AccessTools.DeclaredMethod(typeof(Ship), nameof(Ship.RenderStatusRow))
					?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(Ship)}.{nameof(Ship.RenderStatusRow)}`"),
				transpiler: new HarmonyMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(RenderStatusRow_Transpiler))
			);

		harmony.Patch(
			original: AccessTools.DeclaredMethod(typeof(Ship), nameof(Ship.RenderPartUI))
				?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(Ship)}.{nameof(Ship.RenderPartUI)}`"),
			transpiler: new HarmonyMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(RenderPartUI_Transpiler))
		);

		harmony.Patch(
			original: AccessTools.DeclaredMethod(typeof(Ship), nameof(Ship.ModifyDamageDueToParts))
				?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(Ship)}.{nameof(Ship.ModifyDamageDueToParts)}`"),
			transpiler: new HarmonyMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(ModifyIncomingDamageDueToParts_Transpiler))
		);

		harmony.Patch(
			original: AccessTools.DeclaredMethod(typeof(Ship), nameof(Ship.NormalDamage))
				?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(Ship)}.{nameof(Ship.NormalDamage)}`"),
			prefix: new HarmonyMethod(NormalDamage_Prefix)
		);
	}

	[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
	private static IEnumerable<CodeInstruction> RenderStatusRow_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod)
	{
		try
		{
			return new SequenceBlockMatcher<CodeInstruction>(instructions)
				.Find([
					ILMatches.Ldloc<Status>(originalMethod).CreateLdlocInstruction(out var ldlocStatus),
					ILMatches.LdcI4((int)Enum.Parse<Status>(nameof(Status.autododgeLeft))),
					ILMatches.Beq,
					ILMatches.Ldloc<Status>(originalMethod),
					ILMatches.LdcI4((int)Enum.Parse<Status>(nameof(Status.autododgeRight))),
					ILMatches.Beq.GetBranchTarget(out var branchTarget)
				])
				.PointerMatcher(branchTarget)
				.Find(ILMatches.Stloc<bool>(originalMethod).CreateLdlocaInstruction(out var ldlocaShouldFlash))
				.Find(ILMatches.Br.GetBranchTarget(out branchTarget))
				.PointerMatcher(branchTarget)
				.ExtractLabels(out var labels)
				.Insert(SequenceMatcherPastBoundsDirection.Before, SequenceMatcherInsertionResultingBounds.IncludingInsertion, [
					new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
					new CodeInstruction(OpCodes.Ldarg_1),
					ldlocStatus,
					ldlocaShouldFlash,
					new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(RenderStatusRow_Transpiler_ModifyShouldFlash)))
				])
				.AllElements();
		}
		catch (Exception ex)
		{
			Nickel.Instance.ModManager.Logger.LogCritical("Could not patch method {DeclaringType}::{Method} - {ModLoaderName} probably won't work.\nReason: {Exception}", originalMethod.DeclaringType, originalMethod, NickelConstants.Name, ex);
			return instructions;
		}
	}

	[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    private static IEnumerable<CodeInstruction> RenderPartUI_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod)
    {
        try
        {
            return new SequenceBlockMatcher<CodeInstruction>(instructions)
                .Find([
                    ILMatches.Ldloc<PDamMod>(originalMethod).CreateLdlocInstruction(out var ldlocDamMod),
                    ILMatches.LdcI4((int)Enum.Parse<PDamMod>(nameof(PDamMod.brittle))),
                    ILMatches.BneUn.GetBranchTarget(out var damttBranchTarget)
                ])
                .Find([
                    ILMatches.Ldarg(1),
                    ILMatches.Ldfld("tooltips"),
                    ILMatches.Ldloc<Vec>(originalMethod).CreateLdlocInstruction(out var ldlocttPos)
                ])

                .PointerMatcher(damttBranchTarget)
                .ExtractLabels(out var labels)
                .Insert(SequenceMatcherPastBoundsDirection.Before, SequenceMatcherInsertionResultingBounds.IncludingInsertion, [
                    new CodeInstruction(OpCodes.Ldarg_1).WithLabels(labels),
                    ldlocDamMod,
                    ldlocttPos,
                    new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(MethodBase.GetCurrentMethod()!.DeclaringType, nameof(RenderPartUI_Transpiler_RenderDamageModifierTooltip))),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_3),
                    ldlocttPos,
                    new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(MethodBase.GetCurrentMethod()!.DeclaringType, nameof(RenderPartUI_Transpiler_RenderStunModifierTooltip))),
                ])

                .Find(
                    ILMatches.Call("Sprite"),
                    ILMatches.Ldloca<Color>(originalMethod).CreateLdlocaInstruction(out var ldlocaiconOpacity),
                    ILMatches.LdcR8(1)
                )
                .Find([
                    ILMatches.Call("Sprite"),
                    ILMatches.Br.GetBranchTarget(out var damsprBranchTarget),
                    ILMatches.AnyLdcI4,
                    ILMatches.Newobj(typeof(Spr?).GetConstructor([typeof(Spr)])!),
                    ILMatches.Ldloc<Vec>(originalMethod).CreateLdlocInstruction(out var ldlocIconV),
                ])

                .PointerMatcher(damsprBranchTarget)
                .ExtractLabels(out var sprLabels)
                .Insert(SequenceMatcherPastBoundsDirection.Before, SequenceMatcherInsertionResultingBounds.IncludingInsertion, [
					new CodeInstruction(OpCodes.Ldarg_3).WithLabels(sprLabels),
					ldlocDamMod,
                    ldlocaiconOpacity,
                    ldlocIconV,
                    new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(MethodBase.GetCurrentMethod()!.DeclaringType, nameof(RenderPartUI_Transpiler_RenderPartTraitIcon)))
                ])
                .AllElements();
        }
        catch (Exception ex)
        {
			Nickel.Instance.ModManager.Logger.LogCritical("Could not patch method {DeclaringType}::{Method} - {ModLoaderName} probably won't work.\nReason: {Exception}", originalMethod.DeclaringType, originalMethod, NickelConstants.Name, ex);
            return instructions;
        }
    }

	[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    private static IEnumerable<CodeInstruction> ModifyIncomingDamageDueToParts_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod)
    {
        try
        {
            return new SequenceBlockMatcher<CodeInstruction>(instructions)
                .Find([
					new ElementMatch<CodeInstruction>("switch", (x) => x.opcode == OpCodes.Switch),
					ILMatches.Br.GetBranchTarget(out var switchBranchTarget)
                ])
                .Find([
					ILMatches.Stloc<int>(originalMethod).CreateLdlocaInstruction(out var ldlocanum),
                    ILMatches.Br.GetBranchTarget(out var caseBranchTarget),
                ])
				.PointerMatcher(switchBranchTarget)
				.ExtractLabels(out var labels)
				.Insert(SequenceMatcherPastBoundsDirection.Before, SequenceMatcherInsertionResultingBounds.IncludingInsertion, [
					new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
					new CodeInstruction(OpCodes.Ldarg_1),
					new CodeInstruction(OpCodes.Ldarg_2),
					new CodeInstruction(OpCodes.Ldarg, 3),
					new CodeInstruction(OpCodes.Ldarg, 4),
					new CodeInstruction(OpCodes.Ldarg, 5),
					ldlocanum,
                    new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(MethodBase.GetCurrentMethod()!.DeclaringType, nameof(ModifyIncomingDamageDueToParts_Transpiler_ModifyDamage))),
					new CodeInstruction(OpCodes.Br, caseBranchTarget.Value)
				])
				.AllElements();
        }
		catch (Exception ex)
        {
			Nickel.Instance.ModManager.Logger.LogCritical("Could not patch method {DeclaringType}::{Method} - {ModLoaderName} probably won't work.\nReason: {Exception}", originalMethod.DeclaringType, originalMethod, NickelConstants.Name, ex);
            return instructions;
        }
    }

	private static void NormalDamage_Prefix(State s, Combat c, Ship __instance, int? maybeWorldGridX)
    {
        if (maybeWorldGridX is null)
        {
            return;
        }

        var part = __instance.GetPartAtWorldX(maybeWorldGridX.Value);

		if (part is null)
        {
            return;
        }

		var args = new PartsGetHitEventArgs()
        {
            State = s,
			Combat = c,
			Ship = __instance,
			Part = part,
			PartStunModifier = part.stunModifier
        };

		OnPartsGetHit?.Invoke(null, ref args);
    }

	private static void ModifyIncomingDamageDueToParts_Transpiler_ModifyDamage(Ship ship, State s, Combat c, int incomingDamage, Part part, bool piercing, ref int num)
    {
        var args = new ModifyDamageForDamageModifierEventArgs()
        {
            State = s,
			Combat = c,
			Ship = ship,
			Part = part,
			PartDamageModifier = part.GetDamageModifier(),
			IncomingDamage = incomingDamage,
			Piercing = piercing
        };

		OnModifyDamageForDamageModifier?.Invoke(null, ref args);
		num = args.IncomingDamage;
    }

	private static void RenderPartUI_Transpiler_RenderDamageModifierTooltip(G g, PDamMod pdamMod, Vec ttPos)
    {
        var args = new RenderDamageModifierTooltipEventArgs()
        {
			G = g,
			PartDamageModifier = pdamMod,
			TooltipPosition = ttPos
        };

		OnRenderDamageModifierTooltip?.Invoke(null, ref args);
    }

	private static void RenderPartUI_Transpiler_RenderStunModifierTooltip(G g, Part part, Vec ttPos)
    {
        var args = new RenderStunModifierTooltipEventArgs()
        {
			G = g,
			PartStunModifier = part.stunModifier,
			TooltipPosition = ttPos
        };

		OnRenderStunModifierTooltip?.Invoke(null, ref args);
    }

	private static void RenderPartUI_Transpiler_RenderPartTraitIcon(Part part, PDamMod pdamMod, in Color iconOpacity, Vec iconV)
    {
        var damargs = new RenderDamageModifierIconEventArgs()
        {
			PartDamageModifier = pdamMod,
			IconPosition = iconV,
			IconOpacity = iconOpacity
        };

		OnRenderDamageModifierIcon?.Invoke(null, ref damargs);

        var stuargs = new RenderStunModifierIconEventArgs()
        {
			PartStunModifier = part.stunModifier,
			IconPosition = iconV,
			IconOpacity = iconOpacity
        };

		OnRenderStunModifierIcon?.Invoke(null, ref stuargs);
    }

	private static void RenderStatusRow_Transpiler_ModifyShouldFlash(Ship ship, G g, Status status, ref bool shouldFlash)
	{
		if (g.state.route is not Combat combat)
			return;
		
		var args = new ShouldStatusFlashEventArgs
		{
			State = g.state,
			Combat = combat,
			Ship = ship,
			Status = status,
			ShouldFlash = shouldFlash
		};
		OnShouldStatusFlash?.Invoke(null, ref args);
		shouldFlash = args.ShouldFlash;
	}

	internal struct ShouldStatusFlashEventArgs
	{
		public required State State { get; init; }
		public required Combat Combat { get; init; }
		public required Ship Ship { get; init; }
		public required Status Status { get; init; }
		public required bool ShouldFlash;
	}

	internal readonly struct RenderDamageModifierTooltipEventArgs
    {
		public required G G { get; init; }
		public required PDamMod PartDamageModifier { get; init; }
		public required Vec TooltipPosition { get; init; }
    }

	internal readonly struct RenderDamageModifierIconEventArgs
    {
		public required PDamMod PartDamageModifier { get; init; }
		public required Vec IconPosition { get; init; }
		public required Color IconOpacity { get; init; }
    }

	internal readonly struct RenderStunModifierTooltipEventArgs
    {
		public required G G { get; init; }
		public required PStunMod PartStunModifier { get; init; }
		public required Vec TooltipPosition { get; init; }
    }

	internal readonly struct RenderStunModifierIconEventArgs
    {
		public required PStunMod PartStunModifier { get; init; }
		public required Vec IconPosition { get; init; }
		public required Color IconOpacity { get; init; }
    }

	internal struct ModifyDamageForDamageModifierEventArgs
    {
		public required State State { get; init; }
		public required Combat Combat { get; init; }
		public required Ship Ship { get; init; }
		public required Part Part { get; init; }
		public required PDamMod PartDamageModifier { get; init; }
		public required bool Piercing { get; init; }
		public required int IncomingDamage;
    }

	internal readonly struct PartsGetHitEventArgs
    {
        public required State State { get; init; }
		public required Combat Combat { get; init; }
		public required Ship Ship { get; init; }
		public required Part Part { get; init; }
		public required PStunMod PartStunModifier { get; init; }
    }
}
