using System.Linq;
using NUnit.Framework;
using StarGen.Core.Atlas;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.AtlasView.Tests
{
    /// <summary>Slice AC — the domain interior surfaced in the atlas: the
    /// worked/outpost mark derivation (AC1.3/AC1.4) and the outpost selection
    /// resolution (AC1.4). Loads the seed-42 golden (its port #7 founds the
    /// outpost "Belmi") and asserts over the pure seams the layers and the
    /// pointer both consume, so nothing here depends on rendering or input.</summary>
    public class DomainInteriorTests
    {
        [Test]
        public void Marks_LiveOutpostSurfaces_EmptyDomainYieldsNone()
        {
            var go = new GameObject("DomainInteriorMarks");
            try
            {
                var host = go.AddComponent<SimHost>();
                Assert.IsTrue(host.LoadArtifact(), host.LoadError);
                var model = host.Model;
                var eye = host.Eye;

                var set = DomainInteriorMarks.Build(model, eye);

                // the golden's single live outpost — Belmi, founded by port #7.
                var belmi = set.Outposts.FirstOrDefault(m => m.Name == "Belmi");
                Assert.AreEqual("Belmi", belmi.Name,
                    "expected the seed-42 outpost 'Belmi' among the marks");
                Assert.AreEqual(7, belmi.ParentPortId);
                Assert.AreEqual(host.State.Outposts[belmi.OutpostId].Hex,
                    belmi.Hex);

                // per-port: port #7's domain has the outpost; a domain with no
                // outposts and no off-port workings yields no marks at all.
                var portSeven = DomainInteriorMarks.ForPort(model, eye, 7);
                Assert.IsNotEmpty(portSeven.Outposts,
                    "port #7's domain should carry the outpost mark");

                int emptyPortId = -1;
                foreach (var p in host.State.Ports)
                {
                    var card = DomainInteriorQuery.Card(model, eye, p.Id);
                    if (card != null && card.Outposts.Count == 0
                        && card.SatelliteHexes.Count == 0)
                    { emptyPortId = p.Id; break; }
                }
                Assert.GreaterOrEqual(emptyPortId, 0,
                    "the golden should have at least one bare-domain port");
                var empty = DomainInteriorMarks.ForPort(model, eye, emptyPortId);
                Assert.IsEmpty(empty.Worked);
                Assert.IsEmpty(empty.Outposts);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Selection_OutpostHexResolvesOutpost_PortHexStillPort()
        {
            var go = new GameObject("DomainInteriorSelection");
            try
            {
                var host = go.AddComponent<SimHost>();
                Assert.IsTrue(host.LoadArtifact(), host.LoadError);
                var model = host.Model;
                var state = host.State;
                var eye = host.Eye;

                // the outpost's hex resolves to the outpost (a real port hex
                // still outranks it — tested next).
                var outpost = state.Outposts.First(o => o.Name == "Belmi");
                var atOutpost = SelectionModel.Resolve(model, state, eye,
                    outpost.Hex, HexQuery.At(model, eye, outpost.Hex));
                Assert.AreEqual(SelectionKind.Outpost, atOutpost.Kind);
                Assert.AreEqual(outpost.Id, atOutpost.Id);

                // a genuine port hex resolves to the port, never the outpost.
                var portHex = state.Ports[0].Hex;
                var atPort = SelectionModel.Resolve(model, state, eye, portHex,
                    HexQuery.At(model, eye, portHex));
                Assert.AreEqual(SelectionKind.Port, atPort.Kind);
                Assert.AreEqual(0, atPort.Id);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
