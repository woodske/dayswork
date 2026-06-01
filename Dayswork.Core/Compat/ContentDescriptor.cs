namespace Dayswork.Core.Compat;

/// <summary>The kind of world content described for a classification override.</summary>
public enum WorldContentKind
{
    ResourceClump,
    Tree,
    Object,
    Animal,
}

/// <summary>
/// Pure, content-agnostic descriptor of a world object, used by expansion profiles to decide
/// classification overrides without referencing live Stardew types. <see cref="Identifier"/> carries
/// the kind-specific key (e.g., resource-clump sheet index, tree type, object/animal type id).
/// Minimal in U-SVE-01; extended as needed when consumed in U-SVE-04.
/// </summary>
public readonly record struct ContentDescriptor(WorldContentKind Kind, string Identifier);
