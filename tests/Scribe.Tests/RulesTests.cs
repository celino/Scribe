using System;
using System.Linq;
using Xunit;

namespace Scribe.Tests
{
    // Layer 3: the decisions of the KSP module, tested without the game. Each regression names the bug it came from.
    public class RulesTests
    {
        static readonly Guid A = Guid.NewGuid();
        static readonly Guid B = Guid.NewGuid();

        // ---- F16 destruction: only real losses, once per vessel ----

        [Fact, Trait("Feature", "F16")]
        public void F16_a_real_loss_is_recorded_once_even_when_both_events_fire()
        {
            // Vessel.Die() fires onVesselWillDestroy and, later, onVesselDestroy for the same vessel
            var f = new DestroyFilter();
            Assert.True(f.ShouldRecord(A, "Ship", 12, 0f));
            Assert.False(f.ShouldRecord(A, "Ship", 12, 5f));
        }

        [Theory, Trait("Feature", "F16")]
        [InlineData("Debris")]
        [InlineData("Flag")]
        [InlineData("SpaceObject")]
        public void F16_debris_flags_and_asteroids_are_noise(string type)
        {
            Assert.False(new DestroyFilter().ShouldRecord(A, type, 3, 0f));
        }

        [Fact, Trait("Feature", "F16")]
        public void F16_a_vessel_with_no_parts_is_not_a_loss()
        {
            Assert.False(new DestroyFilter().ShouldRecord(A, "Ship", 0, 0f));
        }

        [Fact, Trait("Feature", "F16")]
        public void F16_noise_does_not_use_up_the_vessel_id()
        {
            // a vessel seen with no parts first, then with parts, is still a loss
            var f = new DestroyFilter();
            Assert.False(f.ShouldRecord(A, "Ship", 0, 0f));
            Assert.True(f.ShouldRecord(A, "Ship", 4, 0.1f));
        }

        // ---- F09 docking is not a loss ----

        [Fact, Trait("Feature", "F09")]
        public void F09_a_vessel_merged_by_docking_is_ignored_inside_the_window()
        {
            var f = new DestroyFilter();
            f.NoteCoupled(A, 10f);
            f.NoteCoupled(B, 10f);
            Assert.False(f.ShouldRecord(A, "Ship", 5, 11f));
            Assert.False(f.ShouldRecord(B, "Ship", 5, 12.9f));
        }

        [Fact, Trait("Feature", "F09")]
        public void F09_after_the_window_a_loss_counts_again()
        {
            var f = new DestroyFilter();
            f.NoteCoupled(A, 10f);
            Assert.True(f.ShouldRecord(A, "Ship", 5, 10f + DestroyFilter.CoupleWindow));
        }

        // ---- F17 facility upgrade: one entry per upgrade ----

        [Fact, Trait("Feature", "F17")]
        public void F17_the_same_upgrade_fired_twice_in_a_second_is_one_entry()
        {
            // the Astronaut Complex test fired the event twice
            var d = new FacilityDebounce();
            Assert.True(d.Accept("AstronautComplex level 2", 100f));
            Assert.False(d.Accept("AstronautComplex level 2", 100.4f));
        }

        [Fact, Trait("Feature", "F17")]
        public void F17_another_facility_or_a_later_upgrade_is_accepted()
        {
            var d = new FacilityDebounce();
            Assert.True(d.Accept("VehicleAssemblyBuilding level 2", 100f));
            Assert.True(d.Accept("SpaceplaneHangar level 2", 100.2f));
            Assert.True(d.Accept("SpaceplaneHangar level 2", 102f));
        }

        [Fact, Trait("Feature", "F17")]
        public void F17_text_counts_levels_from_one()
        {
            Assert.Equal("SpaceCenter/Runway level 2", ScribeRules.FacilityText("SpaceCenter/Runway", 1));
        }

        // ---- F10 kerbal status ----

        [Theory, Trait("Feature", "F10")]
        [InlineData("Dead", "Dead")]
        [InlineData("Missing", "Missing")]
        [InlineData("Assigned", null)]
        [InlineData("Available", null)]
        public void F10_only_death_and_missing_are_recorded(string to, string expected)
        {
            Assert.Equal(expected, ScribeRules.KerbalReason(to));
        }

        // ---- F05 the node the module writes and reads ----

        [Fact, Trait("Feature", "F05")]
        public void F05_the_save_node_round_trips_ledger_balances_and_cap()
        {
            var l = new Ledger { Cap = 321 };
            l.SetBalance("funds", 1000.5);
            l.RecordBalance("funds", 1500.25, 42, "ContractReward", "Ship \"One\"", "Kerbin");
            l.RecordEvent("contract", "Completed", 43, "", "", "Survey // Mun\nline two");

            var node = new SaveNode();
            LedgerStore.Save(l, node);
            Ledger back = LedgerStore.Load(node);

            Assert.Equal(321, back.Cap);
            Assert.Equal(2, back.Entries.Count);
            Assert.Equal(499.75, back.Entries[0].Delta, 6);
            Assert.Equal(1500.25, back.Entries[0].Balance, 6);
            Assert.Equal("Ship \"One\"", back.Entries[0].Vessel);
            Assert.True(back.HasBalance("funds"));
            // "//" and newlines cannot live in a ConfigNode value: they are cleaned on the way out
            Assert.Equal("Survey / / Mun line two", back.Entries[1].Text);
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_a_save_without_balances_or_cap_loads_with_defaults()
        {
            Ledger l = LedgerStore.Load(new SaveNode());
            Assert.Equal(ScribeInfo.DefaultCap, l.Cap);
            Assert.Empty(l.Entries);
            Assert.False(l.HasBalance("funds"));
        }

        [Theory, Trait("Feature", "F05")]
        [InlineData("abc")]
        [InlineData("-5")]
        [InlineData("0")]
        public void F05_a_bad_cap_in_the_save_falls_back_to_the_default(string cap)
        {
            var node = new SaveNode();
            node.AddValue("cap", cap);
            Assert.Equal(ScribeInfo.DefaultCap, LedgerStore.Load(node).Cap);
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_clean_removes_newlines_and_comment_markers()
        {
            Assert.Equal("a  b c / /d", ScribeRules.Clean("a\r\nb\nc //d"));
        }

        // ---- F19 recovered debris, flags and space objects are not recorded ----

        [Theory, Trait("Feature", "F19")]
        [InlineData("Debris", false)]
        [InlineData("Flag", false)]
        [InlineData("SpaceObject", false)]
        [InlineData("Ship", true)]
        [InlineData("Probe", true)]
        [InlineData("Rover", true)]
        public void F19_recovered_debris_flags_and_space_objects_are_ignored(string type, bool recorded)
        {
            Assert.Equal(recorded, ScribeRules.IsRecordableVessel(type));
        }
    }
}
