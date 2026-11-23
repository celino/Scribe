using System;
using System.Linq;
using Xunit;

namespace Scribe.Tests
{
    // Test names start with the feature ID from tests/FEATURES.md; tools/validate.py checks that they exist.
    public class LedgerTests
    {
        const double Eps = 1e-9;

        static Ledger WithBalance(string kind, double balance)
        {
            var l = new Ledger();
            l.SetBalance(kind, balance);
            return l;
        }

        // ---- F01 delta comes from the balance difference ----

        [Fact, Trait("Feature", "F01")]
        public void F01_funds_gain_is_the_difference()
        {
            var l = WithBalance("funds", 716471.2);
            var e = l.RecordBalance("funds", 721471.2, 100, "ContractReward", "", "");
            Assert.Equal(5000, e.Delta, 6);
            Assert.Equal(721471.2, e.Balance, 6);
        }

        [Theory, Trait("Feature", "F01")]
        [InlineData(716471.2 - 2000, -2000, "RnDPartPurchase")]
        [InlineData(716471.2 + 750, 750, "VesselRecovery")]
        [InlineData(716471.2 - 300, -300, "CrewRecruited")]
        public void F01_funds_change_has_the_right_sign(double newBalance, double expected, string reason)
        {
            var l = WithBalance("funds", 716471.2);
            var e = l.RecordBalance("funds", newBalance, 1, reason, "", "");
            Assert.Equal(expected, e.Delta, 6);
            Assert.Equal(reason, e.Reason);
        }

        [Fact, Trait("Feature", "F01")]
        public void F01_reputation_and_science_by_difference()
        {
            var l = WithBalance("reputation", 399);
            Assert.Equal(6.38, l.RecordBalance("reputation", 405.38, 1, "ContractPenalty", "", "").Delta, 6);
            l.SetBalance("science", 168.1);
            Assert.Equal(-8, l.RecordBalance("science", 160.1, 2, "RnDTechResearch", "", "").Delta, 6);
        }

        [Fact, Trait("Feature", "F01")]
        public void F01_vessel_and_body_are_kept()
        {
            var l = WithBalance("funds", 0);
            var e = l.RecordBalance("funds", 10, 1, "VesselRecovery", "Aeris 3A", "Kerbin");
            Assert.Equal("Aeris 3A", e.Vessel);
            Assert.Equal("Kerbin", e.Body);
        }

        // ---- F02 first event and no-change events ----

        [Fact, Trait("Feature", "F02")]
        public void F02_first_event_without_a_balance_is_only_remembered()
        {
            var l = new Ledger();
            Assert.Null(l.RecordBalance("funds", 1000, 1, "x", "", ""));
            Assert.True(l.HasBalance("funds"));
            Assert.Equal(500, l.RecordBalance("funds", 1500, 2, "x", "", "").Delta, 6);
        }

        [Fact, Trait("Feature", "F02")]
        public void F02_same_balance_writes_nothing()
        {
            var l = WithBalance("funds", 1500);
            Assert.Null(l.RecordBalance("funds", 1500, 3, "x", "", ""));
            Assert.Empty(l.Entries);
        }

        [Fact, Trait("Feature", "F02")]
        public void F02_kinds_are_independent()
        {
            var l = WithBalance("funds", 1000);
            l.SetBalance("science", 10);
            Assert.Equal(2, l.RecordBalance("science", 12, 4, "x", "", "").Delta, 6);
            Assert.Single(l.Entries);
        }

        // ---- F03 cap and summaries ----

        [Fact, Trait("Feature", "F03")]
        public void F03_cap_is_respected_and_totals_survive()
        {
            var l = new Ledger { Cap = 20 };
            l.SetBalance("funds", 0);
            double balance = 0, sum = 0;
            for (int i = 1; i <= 100; i++)
            {
                balance += i; sum += i;
                l.RecordBalance("funds", balance, i * 2000.0, "r", "", "");
            }
            Assert.True(l.Entries.Count <= 20);
            Assert.Equal(sum, l.Entries.Sum(x => x.Delta), 6);
            Assert.Equal(100, l.Entries.Sum(x => x.Count));
            Assert.Equal("summary", l.Entries[0].Reason);
            Assert.Equal(balance, l.Entries.Last().Balance, 6);
        }

        [Fact, Trait("Feature", "F03")]
        public void F03_default_cap_is_5000()
        {
            Assert.Equal(5000, new Ledger().Cap);
        }

        [Fact, Trait("Feature", "F03")]
        public void F03_newest_entries_stay_intact()
        {
            var l = new Ledger { Cap = 10 };
            for (int i = 0; i < 40; i++) l.RecordEvent("launch", "x", i * 100.0, "ship" + i, "Kerbin", "");
            Assert.Equal("ship39", l.Entries.Last().Vessel);
            Assert.Equal(1, l.Entries.Last().Count);
        }

        [Fact, Trait("Feature", "F03")]
        public void F03_property_any_sequence_keeps_count_and_sum()
        {
            // seeded, so a failure reproduces; stands in for a property-based test
            var rng = new Random(20261113);
            for (int round = 0; round < 200; round++)
            {
                var l = new Ledger { Cap = 1 + rng.Next(30) };
                l.SetBalance("funds", 0);
                double balance = 0, sum = 0, ut = 0;
                int n = rng.Next(1, 300), expected = 0;
                for (int i = 0; i < n; i++)
                {
                    ut += rng.NextDouble() * 50000;
                    if (rng.Next(3) == 0) { l.RecordEvent("launch", "x", ut, "v", "Kerbin", ""); expected++; }
                    else
                    {
                        double d = Math.Round((rng.NextDouble() - 0.4) * 1000, 2);
                        if (d == 0) continue;
                        balance += d; sum += d; expected++;
                        l.RecordBalance("funds", balance, ut, "r", "", "");
                    }
                }
                Assert.True(l.Entries.Count <= l.Cap, "round " + round + ": " + l.Entries.Count + " > " + l.Cap);
                Assert.Equal(expected, l.Entries.Sum(x => x.Count));
                // with a tiny cap everything ends in one "summary" entry, so sum all of them (events carry delta 0)
                Assert.Equal(sum, l.Entries.Sum(x => x.Delta), 4);
            }
        }

        // ---- F04 cap with far-apart days ----

        [Fact, Trait("Feature", "F04")]
        public void F04_cap_holds_with_one_summary_per_day_not_fitting()
        {
            var l = new Ledger { Cap = 5 };
            for (int i = 0; i < 50; i++) l.RecordEvent("launch", "x", i * 21600.0, "n" + i, "Kerbin", "");
            Assert.True(l.Entries.Count <= 5);
            Assert.Equal(50, l.Entries.Sum(x => x.Count));
        }

        [Fact, Trait("Feature", "F04")]
        public void F04_cap_of_one_still_terminates()
        {
            var l = new Ledger { Cap = 1 };
            for (int i = 0; i < 30; i++) l.RecordEvent("launch", "x", i * 1e7, "n", "Kerbin", "");
            Assert.True(l.Entries.Count <= 1);
            Assert.Equal(30, l.Entries.Sum(x => x.Count));
        }

        // ---- F05 save and load ----

        [Fact, Trait("Feature", "F05")]
        public void F05_numbers_and_strings_survive()
        {
            var l = WithBalance("funds", 100.5);
            l.RecordBalance("funds", 90.25, 12345.678, "VesselRecovery", "Aeris 3A", "Kerbin");
            var back = Ledger.FromText(Ledger.ToText(l.Entries));
            Assert.Single(back);
            Assert.Equal(-10.25, back[0].Delta, 9);
            Assert.Equal(12345.678, back[0].Ut, 9);
            Assert.Equal(90.25, back[0].Balance, 9);
            Assert.Equal("Aeris 3A", back[0].Vessel);
            Assert.Equal("VesselRecovery", back[0].Reason);
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_quotes_newlines_backslashes_survive()
        {
            var l = new Ledger();
            l.RecordEvent("destroyed", "crash", 20000, "Jeb's \"Ship\"", "Mun", "line1\nline2 \\ end");
            var back = Ledger.FromText(Ledger.ToText(l.Entries));
            Assert.Equal("Jeb's \"Ship\"", back[0].Vessel);
            Assert.Equal("line1\nline2 \\ end", back[0].Text);
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_double_slash_in_text_survives()
        {
            // KSP's ConfigNode treats // as a comment start, so the module must be able to carry it through
            var l = new Ledger();
            l.RecordEvent("contract", "Completed", 1, "", "", "Visit http://example.com // now");
            var back = Ledger.FromText(Ledger.ToText(l.Entries));
            Assert.Equal("Visit http://example.com // now", back[0].Text);
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_double_precision_round_trips_exactly()
        {
            var e = new Entry { Ut = 0.1 + 0.2, Delta = 1.0 / 3.0, Balance = 716471.2 };
            var back = Ledger.FromPairs(Ledger.ToPairs(e));
            Assert.Equal(e.Ut, back.Ut);
            Assert.Equal(e.Delta, back.Delta);
            Assert.Equal(e.Balance, back.Balance);
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_unknown_keys_and_garbage_numbers_do_not_throw()
        {
            var back = Ledger.FromPairs(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("ut", "not a number"),
                new System.Collections.Generic.KeyValuePair<string, string>("future-key", "x"),
                new System.Collections.Generic.KeyValuePair<string, string>("count", "??"),
            });
            Assert.Equal(0, back.Ut);
            Assert.Equal(1, back.Count);
        }

        // ---- F06 summaries survive save and load ----

        [Fact, Trait("Feature", "F06")]
        public void F06_summary_count_and_delta_survive()
        {
            var s = new Entry { Type = "funds", Reason = "summary", Count = 7, Delta = -3.5, Balance = 9 };
            var back = Ledger.FromPairs(Ledger.ToPairs(s));
            Assert.Equal(7, back.Count);
            Assert.Equal(-3.5, back.Delta, 9);
        }

        [Fact, Trait("Feature", "F06")]
        public void F06_pruned_ledger_round_trips()
        {
            var l = new Ledger { Cap = 8 };
            l.SetBalance("funds", 0);
            double b = 0;
            for (int i = 1; i <= 60; i++) { b += i; l.RecordBalance("funds", b, i * 9000.0, "r", "", ""); }
            var back = Ledger.FromText(Ledger.ToText(l.Entries));
            Assert.Equal(l.Entries.Count, back.Count);
            Assert.Equal(60, back.Sum(x => x.Count));
            Assert.Equal(l.Entries.Sum(x => x.Delta), back.Sum(x => x.Delta), 9);
        }

        // ---- F18 part-by-part charges in one instant are one entry ----

        [Fact, Trait("Feature", "F18")]
        public void F18_part_by_part_charges_in_one_instant_are_one_entry()
        {
            var l = WithBalance("funds", 1000);
            double b = 1000;
            for (int i = 0; i < 40; i++) { b -= 10; l.RecordBalance("funds", b, 50.0, "VesselRollout", "Probe", "Kerbin"); }
            var e = Assert.Single(l.Entries);
            Assert.Equal(-400, e.Delta, 6);
            Assert.Equal(600, e.Balance, 6);
            Assert.Equal(40, e.Count);
        }

        [Fact, Trait("Feature", "F18")]
        public void F18_credit_and_debits_of_the_same_reason_stay_apart()
        {
            // the recovery with one credit and many debits must stay readable, not net out
            var l = WithBalance("funds", 1000);
            double b = 1000;
            b += 15349; l.RecordBalance("funds", b, 9.0, "VesselRecovery", "Coyote", "");
            for (int i = 0; i < 31; i++) { b -= 500; l.RecordBalance("funds", b, 9.0, "VesselRecovery", "Coyote", ""); }
            Assert.Equal(2, l.Entries.Count);
            Assert.Equal(15349, l.Entries[0].Delta, 6);
            Assert.Equal(-15500, l.Entries[1].Delta, 6);
            Assert.Equal(31, l.Entries[1].Count);
        }

        [Fact, Trait("Feature", "F18")]
        public void F18_other_instant_reason_or_vessel_is_a_new_entry()
        {
            var l = WithBalance("funds", 1000);
            l.RecordBalance("funds", 990, 10.0, "VesselRollout", "A", "");
            l.RecordBalance("funds", 980, 12.5, "VesselRollout", "A", "");   // later instant
            l.RecordBalance("funds", 970, 12.5, "VesselRecovery", "A", "");  // other reason
            l.RecordBalance("funds", 960, 12.5, "VesselRecovery", "B", "");  // other vessel
            Assert.Equal(4, l.Entries.Count);
        }

        [Fact, Trait("Feature", "F18")]
        public void F18_events_are_never_grouped_and_a_group_survives_save_and_load()
        {
            var l = WithBalance("funds", 1000);
            l.RecordEvent("launch", "Launch", 5.0, "A", "Kerbin", "");
            l.RecordEvent("launch", "Launch", 5.0, "A", "Kerbin", "");
            Assert.Equal(2, l.Entries.Count);
            l.RecordBalance("funds", 990, 6.0, "VesselRollout", "", "");
            l.RecordBalance("funds", 980, 6.0, "VesselRollout", "", "");
            var back = Ledger.FromText(Ledger.ToText(l.Entries));
            Assert.Equal(3, back.Count);
            Assert.Equal(2, back[2].Count);
            Assert.Equal(-20, back[2].Delta, 6);
        }
    }
}
