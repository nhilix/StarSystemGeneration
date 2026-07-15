using System.IO;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyRefRoundTripTests
{
    [Fact]
    public void FacilityAndProjectBodyRefs_RoundTripByteIdentical()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));
        var facility = new Facility(0,
            (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1, a0.Seat, a0.Id,
            state.WorldYear) { Body = new BodyRef(0, 2) };
        state.Facilities.Add(facility);
        var project = new Project(0, ProjectKind.FacilityConstruction,
            a0.Id, a0.Id, 0, a0.Seat, 4.0, state.WorldYear)
        { Body = new BodyRef(0, 2), TypeId = facility.TypeId, TargetId = 0 };
        state.Projects.Add(project);

        var text1 = ArtifactSerializer.ToText(state);
        var reloaded = ArtifactSerializer.Load(new StringReader(text1));
        var text2 = ArtifactSerializer.ToText(reloaded);

        Assert.Equal(text1, text2);
        Assert.Equal(new BodyRef(0, 2), reloaded.Facilities[0].Body);
        Assert.Equal(new BodyRef(0, 2), reloaded.Projects[0].Body);
    }

    // Old artifacts predate the two trailing body fields. A FACILITY line
    // written by the pre-this-task (v2, 10-field) writer must still parse
    // via the length guard, defaulting Body to None — never throw.
    [Fact]
    public void OldFacilityLineWithoutBodyFields_ParsesWithBodyNone()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));
        state.Facilities.Add(new Facility(0,
            (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1, a0.Seat, a0.Id,
            state.WorldYear) { Body = new BodyRef(0, 2) });

        var text = ArtifactSerializer.ToText(state);
        // Strip the two trailing body fields off the FACILITY line to
        // simulate an old-format (pre-trailing-fields) record.
        var lines = text.Split('\n').Select(line =>
        {
            if (!line.StartsWith("FACILITY|")) return line;
            var parts = line.Split('|');
            return string.Join("|", parts.Take(parts.Length - 2));
        });
        var truncated = string.Join("\n", lines);

        var reloaded = ArtifactSerializer.Load(new StringReader(truncated));
        Assert.Equal(BodyRef.None, reloaded.Facilities[0].Body);
    }
}
