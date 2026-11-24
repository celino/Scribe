using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Scribe.Tests
{
    // The save format: a version-1 save made by 0.1.0 must always load, and a save from a newer Scribe must not lose data here.
    public class SchemaTests
    {
        // Reads the ConfigNode text of the fixture into a SaveNode (the shape the plugin builds from the game's ConfigNode).
        static SaveNode Parse(string text)
        {
            var root = new SaveNode();
            var stack = new Stack<SaveNode>();
            stack.Push(root);
            string pending = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
                if (line == "{") { stack.Push(stack.Peek().AddNode(pending)); pending = null; }
                else if (line == "}") stack.Pop();
                else if (line.Contains(" = ", StringComparison.Ordinal))
                {
                    int i = line.IndexOf(" = ", StringComparison.Ordinal);
                    stack.Peek().AddValue(line.Substring(0, i), line.Substring(i + 3));
                }
                else pending = line;
            }
            return root.GetNode("SCRIBE");
        }

        static string Fixture()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "tests", "fixtures", "ledger-v1.sfs.txt"))) dir = Path.GetDirectoryName(dir);
            return File.ReadAllText(Path.Combine(dir, "tests", "fixtures", "ledger-v1.sfs.txt"));
        }

        [Fact, Trait("Feature", "F21")]
        public void F21_a_version_1_save_without_a_schema_line_loads_completely()
        {
            Ledger l = LedgerStore.Load(Parse(Fixture()));
            Assert.Equal(1, l.Schema);
            Assert.Equal(5000, l.Cap);
            Assert.Equal(4, l.Entries.Count);
            Assert.Equal("VesselRollout", l.Entries[0].Reason);
            Assert.Equal(-1200.0, l.Entries[0].Delta, 6);
            Assert.Equal(98800.0, l.Entries[0].Balance, 6);
            Assert.Equal("Kerbal X", l.Entries[0].Vessel);
            Assert.Equal("Kerbin", l.Entries[0].Body);
            Assert.Equal(3, l.Entries[0].Count);
            Assert.Equal("launch", l.Entries[1].Type);
            Assert.Equal(101.5, l.Entries[1].Ut, 6);
            Assert.Equal("First Flight", l.Entries[2].Text);
            Assert.Equal(40, l.Entries[3].Count);
            Assert.Equal("folded 40 entries", l.Entries[3].Text);
            Assert.Equal(new[] { "funds", "science", "reputation" }, l.Balances.Select(b => b.Key).ToArray());
            Assert.Equal(59999.5, l.Balances.First().Value, 6);
        }

        [Fact, Trait("Feature", "F21")]
        public void F21_saving_a_version_1_save_writes_the_schema_and_loses_nothing()
        {
            Ledger l = LedgerStore.Load(Parse(Fixture()));
            var saved = new SaveNode();
            LedgerStore.Save(l, saved);
            Assert.Equal("1", saved.GetValue("schema"));
            Ledger again = LedgerStore.Load(saved);
            Assert.Equal(l.Entries.Count, again.Entries.Count);
            for (int i = 0; i < l.Entries.Count; i++)
                Assert.Equal(Ledger.ToText(new[] { l.Entries[i] }), Ledger.ToText(new[] { again.Entries[i] }));
        }

        [Fact, Trait("Feature", "F21")]
        public void F21_a_save_from_a_newer_scribe_keeps_what_this_version_does_not_know()
        {
            var node = Parse(Fixture());
            node.Values.RemoveAll(v => v.Key == "cap");
            node.AddValue("schema", "7");
            node.AddValue("future", "x");
            node.AddNode("FUTURE").AddValue("a", "b");
            node.Nodes.First(n => n.Key == "ENTRY").Value.AddValue("mood", "happy");

            Ledger l = LedgerStore.Load(node);
            Assert.Equal(7, l.Schema);
            var saved = new SaveNode();
            LedgerStore.Save(l, saved);
            Assert.Equal("7", saved.GetValue("schema"));           // never lowered
            Assert.Equal("x", saved.GetValue("future"));
            Assert.Equal("b", saved.GetNode("FUTURE").GetValue("a"));
            var firstEntry = saved.Nodes.First(n => n.Key == "ENTRY").Value;
            Assert.Equal("happy", firstEntry.GetValue("mood"));
        }

        [Theory, Trait("Feature", "F21")]
        [InlineData("0")]
        [InlineData("-3")]
        [InlineData("many")]
        [InlineData("")]
        public void F21_a_broken_schema_value_reads_as_version_1(string value)
        {
            var node = Parse(Fixture());
            node.AddValue("schema", value);
            Assert.Equal(1, LedgerStore.Load(node).Schema);
        }

        // ---- F22 contract GUID and agent, kerbal role ----

        [Fact, Trait("Feature", "F22")]
        public void F22_contract_guid_agent_and_role_survive_a_save_and_a_load()
        {
            var l = new Ledger();
            l.Add(new Entry { Ut = 5, Type = "contract", Reason = "Accepted", Text = "Explore the Mun", Contract = "0b1c2d3e-0000-0000-0000-000000000001", Agent = "Kerbin World-Firsts Record-Keeping Society" });
            l.Add(new Entry { Ut = 6, Type = "kerbal", Reason = "Dead", Vessel = "Kerbal X", Body = "Mun", Text = "Jebediah Kerman", Role = "Pilot" });
            var saved = new SaveNode();
            LedgerStore.Save(l, saved);
            Ledger back = LedgerStore.Load(saved);
            Assert.Equal("0b1c2d3e-0000-0000-0000-000000000001", back.Entries[0].Contract);
            Assert.Equal("Kerbin World-Firsts Record-Keeping Society", back.Entries[0].Agent);
            Assert.Equal("Pilot", back.Entries[1].Role);
            Assert.Equal("Explore the Mun", back.Entries[0].Text);
        }

        [Fact, Trait("Feature", "F22")]
        public void F22_entries_without_the_new_fields_do_not_write_them()
        {
            var keys = Ledger.ToPairs(new Entry { Type = "contract", Reason = "Accepted", Text = "T" }).Select(p => p.Key).ToList();
            Assert.DoesNotContain("contract", keys);
            Assert.DoesNotContain("agent", keys);
            Assert.DoesNotContain("role", keys);
        }

        // ---- F23 a balance change carries the vessel only when it belongs to it ----

        [Theory, Trait("Feature", "F23")]
        [InlineData("VesselRollout", true)]
        [InlineData("VesselRecovery", true)]
        [InlineData("VesselLoss", true)]
        [InlineData("Vessels", true)]
        [InlineData("ScienceTransmission", true)]
        [InlineData("Progression", true)]
        [InlineData("Mission", true)]
        [InlineData("ContractReward", false)]
        [InlineData("ContractAdvance", false)]
        [InlineData("ContractPenalty", false)]
        [InlineData("ContractDecline", false)]
        [InlineData("Contracts", false)]
        [InlineData("Strategies", false)]
        [InlineData("RnDPartPurchase", false)]
        [InlineData("Structures", false)]
        [InlineData("CrewRecruited", false)]
        [InlineData("Cheating", false)]
        [InlineData("None", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void F23_which_reasons_belong_to_the_active_vessel(string reason, bool expected)
        {
            Assert.Equal(expected, ScribeRules.RelatesToVessel(reason));
        }
    }
}
