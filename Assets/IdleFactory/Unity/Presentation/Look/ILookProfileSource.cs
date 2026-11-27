using System;

namespace IdleFactory.Unity.Presentation.Look
{
    /// <summary>
    /// The development seam that picks a look profile (contracts/look-profile.md rule 5, ARCH-6, ARCH-11). It mirrors <c>ITimeScaleSource</c>: a null
    /// default that production builds never register. Only the <c>IdleFactory.DevTools</c> assembly implements it and injects it at
    /// <c>BeforeSceneLoad</c>, before the bootstrap composes the world, so the one <see cref="LookProfileResolver"/> that the bootstrap owns can read it.
    /// A source decides nothing about the simulation; it names a profile by id and hands out the prototype profile assets it holds.
    /// </summary>
    public interface ILookProfileSource
    {
        /// <summary>False until the source knows every profile it can hand out. While false the resolver keeps the shipped default and logs nothing.</summary>
        bool IsReady { get; }

        /// <summary>The selected profile id, or null or empty for the shipped default. It may be a persisted value that no profile has.</summary>
        string SelectedProfileId { get; }

        /// <summary>The profile asset of an id, or false when the source holds none.</summary>
        bool TryGetProfile(string id, out LookProfile profile);

        /// <summary>Raised when the selection or the set of profiles changed; the resolver then re-resolves and applies the result on the next world rebuild.</summary>
        event Action Changed;
    }
}
