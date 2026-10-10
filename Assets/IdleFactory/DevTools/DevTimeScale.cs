using System;
using IdleFactory.Unity.Bootstrap;

namespace IdleFactory.DevTools
{
    /// <summary>
    /// The development time-scale selection (F5-R11): x1, x10, x60, x600 and Max. Max is the S_max clamp of
    /// <see cref="FactoryRuntimeHost"/>, and the multiplier it really delivers is reported back by the host.
    /// </summary>
    public sealed class DevTimeScale : ITimeScaleSource
    {
        public const int MaxIndex = 4;

        private static readonly double[] Factors = { 1d, 10d, 60d, 600d, double.PositiveInfinity };
        private int selectedIndex;
        private double effectiveMultiplier = 1d;

        public event Action Changed;

        /// <summary>Index into x1, x10, x60, x600, Max.</summary>
        public int SelectedIndex => selectedIndex;

        public bool IsMax => selectedIndex == MaxIndex;

        public double Scale => Factors[selectedIndex];

        /// <summary>The multiplier the host last applied, after the clamp. 1 until a scaled advance has run.</summary>
        public double EffectiveMultiplier => effectiveMultiplier;

        /// <summary>The factor N shown in the marker: the preset, or the reported effective multiplier for Max.</summary>
        public long DisplayFactor => IsMax ? Math.Max(1L, (long)Math.Round(effectiveMultiplier)) : (long)Factors[selectedIndex];

        /// <summary>True when the marker must show: the selection is not x1.</summary>
        public bool IsAccelerated => selectedIndex != 0;

        public static string LabelOf(int index) => index == MaxIndex ? "Max" : "×" + (long)Factors[index];

        public void Select(int index)
        {
            if (index < 0 || index > MaxIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (index == selectedIndex)
            {
                return;
            }

            selectedIndex = index;
            if (index != MaxIndex)
            {
                effectiveMultiplier = Factors[index];
            }

            Changed?.Invoke();
        }

        public void ReportEffectiveMultiplier(double multiplier)
        {
            if (double.IsNaN(multiplier) || multiplier <= 0d || multiplier == effectiveMultiplier)
            {
                return;
            }

            var before = DisplayFactor;
            effectiveMultiplier = multiplier;
            if (IsMax && DisplayFactor != before)
            {
                Changed?.Invoke();
            }
        }
    }
}
