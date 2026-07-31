using EntityARestaurantGame.UI;
using EntityARestaurantGame.UI.Screens;
using EntityARestaurantGame.World;

// The whole world — effects, items, locations, entities, dialog, controller —
// assembles in WorldBuilder (shared with tools/SmokeTest, so the two can't
// drift). Handcrafted content lives in data/entities + data/world; --gen
// swaps in a procedurally generated world from the data/worldgen pools
// (--gen <seed> pins it, --dump writes it out as ordinary content files).
bool generate = false;
int? seed = null;
for (int i = 0; i < args.Length; i++)
    if (args[i] == "--gen")
    {
        generate = true;
        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int parsed)) seed = parsed;
    }

var world = generate ? WorldBuilder.BuildGenerated(seed) : WorldBuilder.BuildDefault();

if (world.Map is { } map)
{
    Console.WriteLine(map.Summary());
    if (args.Contains("--dump"))
        Console.WriteLine($"[gen] dumped to {WorldGenerator.Dump(map, world.DataRoot)}");
}

// --3d opens the OpenGL view (--smoke auto-closes it, for automated checks);
// default remains the console UI, kept as the debugging fallback.
if (args.Contains("--3d"))
{
    var host = new EntityARestaurantGame.Render3D.Render3DHost(
        world.Controller, world.Locations,
        autoCloseSeconds: args.Contains("--smoke") ? 3 : 0);
    host.Run();
    return;
}

var renderer = new ConsoleRenderer();
var manager  = new ScreenManager(renderer);
manager.Push(new PawnSelectScreen(world.Controller, world.SpawnNobody));
manager.Run();
