using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ChalkPhysics.Lab3D;
using NUnit.Framework;
using UnityEngine;

namespace ChalkPhysics.Tests
{
    /// The 3D lab's rules, checked in edit mode in a second or two (Tools/Tests/run.ps1): the numbers the designer
    /// signed off in DESIGN.md 2б, so that a change that breaks them shows at once, not after a ten-minute bot run.
    public class LabRulesTests
    {
        GameState g;

        [SetUp]
        public void NewGame()
        {
            GameState.MemorySave = true;   // never the player's own save
            GameState.Use3D();
            g = new GameState();
            g.NewGame();
            Career.Fast = false;
            OrderSheet.forceQueue.Clear();
            OrderSheet.forceSlope = false;
            OrderSheet.NewDay();
        }

        /// Run in the open editor, the tests leave it as it was: saving to disk again, no forced orders.
        [TearDown]
        public void Leave()
        {
            GameState.MemorySave = false;
            Career.Fast = false;
            OrderSheet.forceQueue.Clear();
            OrderSheet.forceSlope = false;
        }

        // ------------------------------------------------------------------ accuracy and the instruments' error

        [Test]
        public void AccuracyIsAllWhenRightAndNothingAQuarterOff()
        {
            Assert.AreEqual(1, GameState.Accuracy(2, 2), 1e-9);
            Assert.AreEqual(0.6, GameState.Accuracy(2.2, 2), 1e-9);   // 10% off: 60%
            Assert.AreEqual(0, GameState.Accuracy(2.5, 2), 1e-9);     // a quarter off: nothing
            Assert.AreEqual(0, GameState.Accuracy(9, 2), 1e-9);
            Assert.AreEqual(1 - 0.1 / 0.45, GameState.Accuracy(1.1, 1, 0.45), 1e-9);   // the density, judged more kindly
        }

        [Test]
        public void InstrumentsAreOffByFivePercentAndTheUpgradesMakeThemTruer()
        {
            Assert.AreEqual(0.05, g.SpringError, 1e-9);
            Assert.AreEqual(0.05, g.VolumeError, 1e-9);
            Assert.AreEqual(0.05, g.SlipError, 1e-9);
            double was = g.SpringError;
            foreach (var id in new[] { "true1", "true2", "true3" })
            {
                g.Perks.Add(id);
                Assert.Less(g.SpringError, was, id + " must make the spring truer");
                was = g.SpringError;
            }
            Assert.AreEqual(0.003, g.SpringError, 1e-9);
        }

        [Test]
        public void ACarefulReadingOfTheCoarseScaleIsUsuallyThanked()
        {
            // a careful eye on 1 N divisions is within a tenth of a newton; the spring lies up to 5%
            var rng = new System.Random(1);
            int thanked = 0, n = 20000;
            for (int i = 0; i < n; i++)
            {
                double w = 0.6 + rng.NextDouble() * 6.2;
                double shown = w * (1 + (rng.NextDouble() * 2 - 1) * g.SpringError);
                double read = shown + (rng.NextDouble() * 2 - 1) * 0.1;
                if (GameState.Accuracy(read, w) >= 0.8) thanked++;
            }
            Assert.Greater(thanked / (double)n, 0.75, "«спасибо» from 80%: a careful reader must mostly earn it");
        }

        [Test]
        public void TheDynamometerBobsForSeveralSeconds()
        {
            var go = new GameObject("Dyna");
            var d = go.AddComponent<Dynamometer3D>();
            float x = 0, v = 0, target = 4 * d.perUnit, calm = 0, t = 0;
            const float dt = 1 / 60f;
            while (calm < 0.35f && t < 60)
            {
                MeasuringApparatus.Settle(ref x, ref v, target, d.hz, d.damping, dt);
                calm = MeasuringApparatus.AtRest(x, v, target) ? calm + dt : 0;
                t += dt;
            }
            Object.DestroyImmediate(go);
            Assert.That(t, Is.InRange(6f, 14f), "a soft spring: it bobs a while (DESIGN.md «Долгие приборы»)");
        }

        [Test]
        public void TheCanPoursALitreInAboutNineSecondsAndEnds()
        {
            float Time(float litres)
            {
                float cyl = 0, t = 0;
                const float dt = 1 / 60f;
                while (cyl < litres && t < 120)
                {
                    cyl = Mathf.MoveTowards(cyl, litres, Bath3D.PourRate(litres - cyl, 0.22f) * dt);
                    t += dt;
                }
                return t;
            }
            Assert.That(Time(1f), Is.InRange(7f, 11f));
            Assert.That(Time(2.8f), Is.InRange(13f, 19f));
            Assert.That(Time(0.4f), Is.InRange(4f, 8f));
            Assert.Less(Time(3.5f), 120f, "it ends: the last drops do not take forever");
        }

        [Test]
        public void TheBoardStopsWhereTheFrictionGivesWay()
        {
            Assert.AreEqual(45f, Incline3D.SlipAngle(1, 0, 70), 1e-3);
            Assert.AreEqual(Mathf.Atan(0.2f) * Mathf.Rad2Deg, Incline3D.SlipAngle(0.2, 0, 70), 1e-3);
            Assert.AreEqual(69f, Incline3D.SlipAngle(50, 0, 70), 1e-3, "never past the board's top");
            Assert.Greater(Incline3D.SlipAngle(0.5, 0.05f, 70), Incline3D.SlipAngle(0.5, 0, 70));
        }

        [Test]
        public void TheFishTakeTheirTime()
        {
            Assert.GreaterOrEqual(Rod3D.Sand, 15f);
            Assert.Greater(Rod3D.Sand, Rod3D.Hold + 3, "a bite can come and be missed within the sand");
        }

        // ------------------------------------------------------------------ the pizzeria: timetable, patience, tips

        [Test]
        public void CustomersComeOnATimetableWithARushAtNoon()
        {
            const int all = 7;
            const float day = 120;
            var t = Enumerable.Range(0, all).Select(n => CustomerLine.Timetable(n, all, day)).ToArray();
            Assert.AreEqual(0, t[0], 1e-4, "the first at once");
            for (int i = 1; i < all; i++) Assert.Greater(t[i], t[i - 1], "in order");
            Assert.LessOrEqual(t[all - 1], 0.8f * day, "the last by four o'clock or so");
            float first = t[1] - t[0], noon = t[all / 2 + 1] - t[all / 2];
            Assert.Less(noon, first * 0.75f, "closer together round noon");
        }

        [Test]
        public void PatienceGrowsWithTheQuestions()
        {
            Assert.AreEqual(80, OrderBook.PatienceFor(1), 1e-4);
            Assert.AreEqual(115, OrderBook.PatienceFor(2), 1e-4);
            Assert.Less(OrderBook.PatienceFor(1), 120, "one order is less than a whole day");
        }

        [Test]
        public void TheTipIsUpToHalfTheRewardForQuickAndRight()
        {
            Assert.AreEqual(15, OrderBook.Tip(30, 1, 1));
            Assert.AreEqual(0, OrderBook.Tip(30, 0, 1), "no patience left: no tip");
            Assert.AreEqual(0, OrderBook.Tip(30, 1, 0), "quick but wrong: no tip");
            Assert.AreEqual(4, OrderBook.Tip(30, 0.5, 0.5));
        }

        // ------------------------------------------------------------------ the odd measurements

        [Test]
        public void OddAnswersAreScoredBoxByBox()
        {
            int sweetSour = 0b00101;   // the taste mask: sweet (1) and sour (3)
            Assert.AreEqual(1, Oddity.Score("taste", "13", sweetSour), 1e-9);
            Assert.AreEqual(2 / 3.0, Oddity.Score("taste", "1", sweetSour), 1e-6, "four boxes of five right");
            Assert.AreEqual(1 / 3.0, Oddity.Score("taste", "12", sweetSour), 1e-6, "three of five");
            Assert.AreEqual(0.5, Oddity.Score("note", "2", 1 << 2), 1e-9, "a neighbouring note: half");
            Assert.AreEqual(0, Oddity.Score("glow", "2", 1 << 0), 1e-9);
        }

        [Test]
        public void AnOrderPaysForTheWorkItTakes()
        {
            Assert.AreEqual(1f, Oddity.Work(new List<string> { "dyna" }));
            Assert.AreEqual(0.5f, Oddity.Work(new List<string> { "glow" }), "a flick of the switch is half a weighing");
            Assert.Less(Oddity.Work(new List<string> { "mirror" }), 1f);
            Assert.Greater(Oddity.Work(new List<string> { "fish" }), 1f, "the pond takes a wait");
        }

        // ------------------------------------------------------------------ the order sheets

        static OrderSheet Sheet() => new GameObject("Sheet").AddComponent<OrderSheet>();

        [Test]
        public void NoStoryTwiceInADay()
        {
            var s = Sheet();
            var seen = new HashSet<string>();
            for (int i = 0; i < 8; i++)
            {
                s.Deal(i + 1, false, false);
                var story = Regex.Replace(s.story, @"\d+", "#");   // the numbers in a story change, the story does not
                Assert.IsTrue(seen.Add(story), "told twice in a day: " + story);
            }
            Object.DestroyImmediate(s.gameObject);
        }

        [Test]
        public void TheStoryAsksForWhatTheTableAsks()
        {
            var s = Sheet();
            var odd = Oddity.All.ToList();
            for (int i = 0; i < 300; i++)
            {
                if (i % 20 == 0) OrderSheet.NewDay();
                s.Deal(i + 1, true, true, true, odd);
                Assert.IsNotEmpty(s.tests);
                Assert.IsFalse(string.IsNullOrEmpty(s.story));
                CollectionAssert.Contains(Persona.All, s.who, "every story has its customer");
                if (s.tests.Exists(Oddity.Is)) Assert.AreEqual(1, s.tests.Count, "an odd order asks one question");
                Assert.Greater(s.reward, 0);
            }
            Object.DestroyImmediate(s.gameObject);
        }

        [Test]
        public void TheLightPaysLessThanAWeighing()
        {
            var s = Sheet();
            OrderSheet.forceQueue.Add("glow");
            s.Deal(1, false, false, false, new List<string> { "glow" });
            CollectionAssert.AreEqual(new[] { "glow" }, s.tests);
            Assert.LessOrEqual(s.reward, 18, "20–35 for a weighing, half of it for the switch");
            Object.DestroyImmediate(s.gameObject);
        }

        // ------------------------------------------------------------------ the goals between the days

        [Test]
        public void TheGrantGoalOfAllOrdersNeedsThemBeforeTheEvening()
        {
            g.Lessons = 2;   // a day whose goal is «all the orders»
            Career.Morning(g, 5);
            StringAssert.Contains("до конца рабочего дня", Career.GoalText(g));
            g.Career.todayDelivered = 5;
            Assert.IsNull(Career.CheckGoal(g, false), "all sent, but after six: not met");
            Assert.IsNotNull(Career.CheckGoal(g, true));
            Assert.AreEqual(1, g.Career.grantSteps);
        }

        [Test]
        public void ARegularTrustsLessWhenLeftWaiting()
        {
            g.Career.delivered = 20;   // the regulars are open
            for (int i = 0; i < 3; i++) Career.Delivered(g, "baker", 0.9, 20, false);
            int stars = Career.Stars(g, "baker");
            Assert.Greater(stars, 0);
            Career.Lost(g, "baker");
            Assert.Less(Career.Trust(g, "baker"), 3 + 1);
            Assert.LessOrEqual(Career.Stars(g, "baker"), stars);
        }

        // ------------------------------------------------------------------ the certificates

        [Test]
        public void NoCertificateCallsAMeasurementUnusual()
        {
            foreach (var d in Awards.All)
            {
                StringAssert.DoesNotContain("необычн", d.title.ToLowerInvariant());
                StringAssert.DoesNotContain("необычн", d.why.ToLowerInvariant());
            }
            Assert.AreEqual(Awards.All.Length, Awards.All.Select(d => d.id).Distinct().Count(), "ids are unique");
        }

        [Test]
        public void TheFirstOrderIsSignedByItsCustomer()
        {
            var got = Awards.Delivered(g, "duck", new[] { "dyna" }, new[] { 0.9 }, 0.9, false, "");
            var first = got.Find(a => a.id == "first");
            Assert.IsNotNull(first);
            Assert.AreEqual(Persona.Name("duck"), first.signer);
        }

        [Test]
        public void FiveDepartmentsInADayEarnTheJackOfAllTrades()
        {
            Awards.Morning(g, 0, 4);
            foreach (var t in new[] { "dyna", "bath", "dens", "angle" }) Awards.Delivered(g, "baker", new[] { t }, new[] { 1.0 }, 1, false, "");
            Assert.IsFalse(Awards.Has(g, "odd"), "four departments are not five");
            Awards.Delivered(g, "cook", new[] { "taste" }, new[] { 1.0 }, 1, false, "");
            Assert.IsTrue(Awards.Has(g, "odd"));
        }

        // ------------------------------------------------------------------ the save

        [Test]
        public void ACertificatesPlaceSurvivesTheSave()
        {
            g.Career.awards.Add(new AwardData { id = "first", x = 1.5f, y = 1.7f, z = 3.49f, yaw = 180, wall = "Partition", placed = true });
            g.Save();
            var again = new GameState();
            var a = again.Career.awards.Find(x => x.id == "first");
            Assert.IsNotNull(a);
            Assert.AreEqual(1.5f, a.x, 1e-5);
            Assert.AreEqual("Partition", a.wall);
            g = again;
        }

        // ------------------------------------------------------------------ the map

        /// The instruments' upgrades and the departments the map sells for coins (DESIGN.md 2б).
        static readonly string[] CoinPerks =
        {
            "scale1", "scale2", "scale3", "true1", "true2", "true3", "vscale1", "vscale2", "vtrue1", "vtrue2", "astep1", "atrue1", "atrue2",
            "ord1", "ord2", "ord3", "x_taste", "x_glow", "x_fish", "x_cat", "x_tickle", "x_mirror", "x_note",
        };

        [Test]
        public void EveryUpgradeOfTheLabCostsCoins()
        {
            foreach (var p in CoinPerks.Select(Defs.Pk))
            {
                Assert.IsNotNull(p);
                Assert.AreEqual(Cur.Coin, p.cost, p.id);
                Assert.Greater(p.price, 0, p.id);
            }
        }
    }
}
