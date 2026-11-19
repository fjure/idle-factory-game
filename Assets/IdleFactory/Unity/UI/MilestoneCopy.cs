using System;
using System.Collections.Generic;
using System.Globalization;
using IdleFactory.Unity.Presentation;
using UnityEngine;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The milestone strings (T540, FR-035, FR-047): the exact copy of <c>specs/006-world-first-factory-experience/reviews/world-copy.md</c> section 3 (B-01 to B-04),
    /// owned by the World Designer, with the template fields <c>{n}</c> (the factory level) and <c>{k}</c> (the locations that opened). The banner and the offline
    /// summary read the same strings (R14). A key that is missing from the table never throws: the text falls back to <see cref="FallbackText"/> and a warning
    /// names the key once, so a copy gap is visible in the log and never in a crash.
    /// </summary>
    public sealed class MilestoneCopy
    {
        /// <summary>Factory level up with unlocks (world-copy B-01).</summary>
        public const string LevelWithUnlocksKey = "B-01";

        /// <summary>Factory level up with exactly one new location (B-01s, provisional until the World Designer confirms it in T547; UI/UX U1).</summary>
        public const string LevelWithOneUnlockKey = "B-01s";

        /// <summary>Factory level up without unlocks (B-02).</summary>
        public const string LevelOnlyKey = "B-02";

        /// <summary>Prestige completion (B-03).</summary>
        public const string PrestigeKey = "B-03";

        /// <summary>Stage change, shown with B-01 or B-02 (B-04).</summary>
        public const string DeckExtendedKey = "B-04";

        /// <summary>What the banner says when its copy key is missing from the table.</summary>
        public const string FallbackText = "Milestone reached.";

        private static readonly Dictionary<string, string> Shipped = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { LevelWithUnlocksKey, "Level {n}. {k} new locations open." },
            { LevelWithOneUnlockKey, "Level {n}. 1 new location opens." },
            { LevelOnlyKey, "Level {n} reached." },
            { PrestigeKey, "New run begins." },
            { DeckExtendedKey, "Deck extended." },
        };

        private readonly IReadOnlyDictionary<string, string> table;
        private readonly Action<string> warn;
        private readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The shipped copy, warnings to the Unity log.</summary>
        public MilestoneCopy()
            : this(null, null)
        {
        }

        /// <summary>A copy table (null: the shipped one) and a warning sink (null: the Unity log). Tests pass a short table to prove the fallback.</summary>
        public MilestoneCopy(IReadOnlyDictionary<string, string> copyTable, Action<string> warning)
        {
            table = copyTable ?? Shipped;
            warn = warning ?? (message => Debug.LogWarning(message));
        }

        /// <summary>The keys the shipped table defines (for the coverage test).</summary>
        public static IReadOnlyCollection<string> ShippedKeys => Shipped.Keys;

        /// <summary>The main line of a milestone: B-01, B-02 or B-03.</summary>
        public string Primary(PendingMilestone milestone)
        {
            switch (milestone.Kind)
            {
                case MilestoneKind.PrestigeCompleted:
                    return Resolve(PrestigeKey, milestone);
                default:
                    return Resolve(milestone.NewlyUnlockedCount == 1 ? LevelWithOneUnlockKey : milestone.NewlyUnlockedCount > 1 ? LevelWithUnlocksKey : LevelOnlyKey, milestone);
            }
        }

        /// <summary>The second line (B-04 "Deck extended.") when the level-up raised the stage, otherwise an empty string.</summary>
        public string Secondary(PendingMilestone milestone) =>
            milestone.Kind == MilestoneKind.FactoryLevelUp && milestone.StageRaised ? Resolve(DeckExtendedKey, milestone) : string.Empty;

        /// <summary>The whole text for a screen reader and for the summary: the lines joined by a space.</summary>
        public string Full(PendingMilestone milestone)
        {
            var secondary = Secondary(milestone);
            return secondary.Length == 0 ? Primary(milestone) : Primary(milestone) + " " + secondary;
        }

        private string Resolve(string key, PendingMilestone milestone)
        {
            if (!table.TryGetValue(key, out var template) || string.IsNullOrEmpty(template))
            {
                if (warned.Add(key))
                {
                    warn("Milestone copy key " + key + " is missing from the copy table; showing the fallback text.");
                }

                return FallbackText;
            }

            return template
                .Replace("{n}", milestone.Level.ToString(CultureInfo.InvariantCulture))
                .Replace("{k}", milestone.NewlyUnlockedCount.ToString(CultureInfo.InvariantCulture));
        }
    }
}
