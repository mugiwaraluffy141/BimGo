using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using BimGo.Format;
using BimGo.Clashes;
using BimGo.Scene;

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }
#endif

class Program
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Main(string[] args)
    {
        foreach (var value in new[] { "80000000-0000-0000-0000-000000000000", "ffffffff-ffff-ffff-ffff-ffffffffffff", "f1234567-89ab-cdef-0123-456789abcdef" })
        {
            var guid = Guid.Parse(value);
            Check(IfcGuid.TryDecode(IfcGuid.Encode(guid), out var decoded) && decoded == guid, "high-bit IFC GUID " + value);
        }
        if (args[0] == "write")
        {
            int count = CategoryCatalog.All.Count;
            var scene = new SceneData {
                Vertices = new[] { new SceneVertex(Vector3.Zero, Vector3.UnitZ, 0xff000000), new SceneVertex(Vector3.UnitX, Vector3.UnitZ, 0xff000000), new SceneVertex(Vector3.UnitY, Vector3.UnitZ, 0xff000000) },
                Indices = new uint[] { 0, 1, 2 },
                Elements = new[] { new ElementRecord { ElementId = 4294967297L, UniqueId = "element-uid", Name = "Door", CategoryName = "Doors", FamilyType = "Door", LevelName = "L1", Link = 1, OpaqueCount = 3, Bounds = new Aabb(Vector3.Zero, Vector3.One) } },
                Levels = new[] { new LevelInfo("L1", 0) },
                Rooms = new[] { new RoomInfo { Number = "101", Name = "Office", UniqueId = "room-uid", ElementId = 9001, LevelName = "L1", BottomZ = 0, TopZ = 3, Min = Vector2.Zero, Max = Vector2.One, Loops = new[] { new[] { Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY } }, Parameters = new[] { new RoomParameter("Department", "Engineering", double.NaN) } } },
                Links = new[] { new LinkInfo { Index = 1, Name = "Link", Title = "Structure", ModelKey = "source-model", InstanceUniqueId = "link-instance", InstanceId = 555, OriginX = 123.25 } },
                CategoryLoaded = new bool[count], CategoryElementCounts = new int[count], Bounds = new Aabb(Vector3.Zero, Vector3.One), ModelTitle = "Smoke", Settings = new LaunchSettings(),
                Provenance = new ModelProvenance { ModelTitle = "Smoke", ModelKey = "host-model", RevitVersion = "2023" }
            };
            var clashes = new ClashDocument { Tests = new List<ClashTest> { new ClashTest { Id = "test-1", Name = "Smoke test" } }, Clashes = new List<ClashRecord> { new ClashRecord { Id = "clash-1", TestId = "test-1", A = new ClashElementRef { UniqueId = "element-uid", Link = "link-instance" }, B = new ClashElementRef { UniqueId = "host-uid" }, X = 1.25, Status = ClashStatus.REVIEWED } } };
            var doc = new BimGoDocument { Scene = scene, Comments = new CommentDocument { Model = "Smoke", Comments = new List<CommentRecord> { new CommentRecord { Id = "comment-1", Text = "Review", ElementId = 4294967297L, ElementUniqueId = "element-uid", X = 1.25, Y = -2.5, Z = 3.75 } } }, Clashes = ClashFiles.Serialize(clashes) };
            Check(BimGoWriter.Write(args[1], doc, new WriterInfo("smoke", "1"), FileKinds.EXPORT, out var error), error);
        }
        else
        {
            var doc = BimGoReader.Read(args[1], new LaunchSettings(), out var error);
            Check(doc != null, error);
            Check(doc.Scene.Rooms.Length == 1 && doc.Scene.Rooms[0].UniqueId == "room-uid" && doc.Scene.Rooms[0].Parameters[0].Value == "Engineering", "room");
            Check(doc.Scene.Elements[0].UniqueId == "element-uid" && doc.Scene.Elements[0].ElementId == 4294967297L && doc.Scene.LinkOf(doc.Scene.Elements[0]).InstanceUniqueId == "link-instance" && doc.Scene.Links[0].ModelKey == "source-model" && doc.Scene.Links[0].OriginX == 123.25, "link identity");
            Check(doc.Comments.Comments.Count == 1 && doc.Comments.Comments[0].Id == "comment-1" && doc.Comments.Comments[0].ElementUniqueId == "element-uid" && doc.Comments.Comments[0].Z == 3.75, "comment");
            var clashes = ClashFiles.Parse(doc.Clashes, out error);
            Check(clashes != null && clashes.Tests.Count == 1 && clashes.Clashes.Count == 1 && clashes.Clashes[0].Id == "clash-1" && clashes.Clashes[0].A.Link == "link-instance" && clashes.Clashes[0].Status == ClashStatus.REVIEWED && clashes.Clashes[0].X == 1.25, "clash: " + error);
        }
        Console.WriteLine(args[0] + " passed; high-bit IFC GUID passed");
    }
}
