using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T528 (A-5, B-4): the model says why a missing-input machine lacks its input, not only which input it is. The card's cause line stays category-level
    /// ("Short on Iron Ore"); the reason (auto-sell draining the store, a full store, nothing upstream, a slow source) is a model value the card reads.
    /// </summary>
    public sealed class MissingInputCauseTests
    {
        private static readonly FactoryWorldLayout Layout = TestLayouts.Six();

        private static MachineBlockerInfo Blocker(string resource, bool autoSold = false) =>
            new MachineBlockerInfo(MachineBlocker.MissingInput, resource, 2, 0, autoSold, 0, 0, new CorrectiveAction[0]);

        private static RatesSnapshot Rates(long storeUsed, long storeCapacity, params (string Id, long Production, bool AutoSold)[] resources)
        {
            var list = new ResourceRates[resources.Length];
            for (var i = 0; i < list.Length; i++)
            {
                list[i] = new ResourceRates(resources[i].Id, 0, resources[i].AutoSold, resources[i].Production, 1_000_000);
            }

            return new RatesSnapshot(1, 1, storeUsed, storeCapacity, storeCapacity == 0 ? 0 : storeUsed * 1_000_000 / storeCapacity, list, new MachineDiagnosis[0]);
        }

        [Test]
        public void AnAutoSoldInputIsReportedAsAutoSoldWhateverElseIsTrue()
        {
            var rates = Rates(1000, 1000, ("iron.bar", 500_000, true));
            Assert.That(MachinePresentationModel.CauseOf(Blocker("iron.bar", true), rates), Is.EqualTo(MissingInputCause.AutoSold),
                "auto-sell explains an empty stock before a full store or a slow source does");
            Assert.That(MachinePresentationModel.CauseOf(Blocker("iron.bar", true), null), Is.EqualTo(MissingInputCause.AutoSold), "the blocker alone says it");
        }

        [Test]
        public void AnInputNoActiveMachineMakesHasNoSource()
        {
            var rates = Rates(10, 1000, ("iron.bar", 0, false));
            Assert.That(MachinePresentationModel.CauseOf(Blocker("iron.bar"), rates), Is.EqualTo(MissingInputCause.NoSource));
            Assert.That(MachinePresentationModel.CauseOf(Blocker("iron.unknown"), rates), Is.EqualTo(MissingInputCause.NoSource), "a resource the rates do not list has no source");
        }

        [Test]
        public void AFullStoreIsTheCauseWhenSomethingMakesTheInputButCannotDeliver()
        {
            var rates = Rates(1000, 1000, ("iron.bar", 500_000, false));
            Assert.That(MachinePresentationModel.CauseOf(Blocker("iron.bar"), rates), Is.EqualTo(MissingInputCause.StoreFull));
        }

        [Test]
        public void ASlowSourceIsTheCauseWhenTheStoreHasRoom()
        {
            var rates = Rates(10, 1000, ("iron.bar", 500_000, false));
            Assert.That(MachinePresentationModel.CauseOf(Blocker("iron.bar"), rates), Is.EqualTo(MissingInputCause.SourceTooSlow));
        }

        [Test]
        public void NoBlockerOrNoRatesGivesNoCause()
        {
            Assert.That(MachinePresentationModel.CauseOf(null, Rates(0, 1000)), Is.EqualTo(MissingInputCause.None));
            Assert.That(MachinePresentationModel.CauseOf(Blocker("iron.bar"), null), Is.EqualTo(MissingInputCause.None));
        }

        [Test]
        public void TheRefreshedPresentationCarriesTheCauseOfTheLimitingResourceAndOnlyForAMissingInputMachine()
        {
            var machines = new List<SynthMachine>
            {
                new SynthMachine { Slot = 0, Blocker = MachineBlocker.MissingInput, Phase = MachinePhase.Paused, Missing = new[] { "iron.bar" } },
                new SynthMachine { Slot = 1 },
            };
            var resources = PresentationModelSynth.Resources(0, 500_000, 0, 0);
            var built = PresentationModelSynth.Build(1, 1, machines, resources);
            var model = new MachinePresentationModel();
            model.Refresh(Layout, built.Factory, built.Views, built.Views.Length, built.Rates);
            Assert.That(model[0].LimitingResourceId, Is.EqualTo("iron.bar"));
            Assert.That(model[0].LimitingCause, Is.EqualTo(MissingInputCause.SourceTooSlow), "the store has room and something makes the input");
            Assert.That(model[1].LimitingCause, Is.EqualTo(MissingInputCause.None), "a running machine has no cause");

            // ApplyCompletions and the next refresh keep it: a rebuilt buffer is not a stripped one.
            var views = (MachineStateView[])built.Views.Clone();
            views[0] = new MachineStateView(0, 0, 1, true, MachinePhase.Paused, MachineBlocker.MissingInput, 1, 0, 1);
            Assert.That(model.ApplyCompletions(views, views.Length), Is.True);
            Assert.That(model[0].LimitingCause, Is.EqualTo(MissingInputCause.SourceTooSlow), "a completion delta does not drop the cause");
        }

        [Test]
        public void TheCardCopyNamesEveryCauseInAFewWordsAndNothingForNone()
        {
            foreach (MissingInputCause cause in Enum.GetValues(typeof(MissingInputCause)))
            {
                var text = IdleFactory.Unity.UI.MachineCardCopy.CauseDetail(cause);
                if (cause == MissingInputCause.None)
                {
                    Assert.That(text, Is.Null);
                    continue;
                }

                Assert.That(text, Is.Not.Null.And.Not.Empty, cause.ToString());
                Assert.That(text.Length, Is.LessThan(60), cause + " stays short");
                Assert.That(text.ToLowerInvariant(), Does.Not.Contain("plate").And.Not.Contain("bay"), "player text follows the World Bible words");
            }
        }
    }
}
